using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PersonalFinance.ApiService.Services;

/// <summary>
///     Authentication scheme name for the X-User-Id header-based internal authentication.
/// </summary>
public static class XUserIdAuthenticationDefaults
{
    public const string Scheme = "XUserId";
}

/// <summary>
///     Authentication handler that treats the <c>X-User-Id</c> header as a valid authentication credential.
///     This allows internal service-to-service calls from the Web proxy to satisfy <c>[Authorize]</c> requirements
///     without requiring a separate JWT or cookie-based authentication layer.
/// </summary>
public class XUserIdAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ILogger<XUserIdAuthenticationHandler> _logger;

    public XUserIdAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ILogger<XUserIdAuthenticationHandler> logger)
        : base(options, loggerFactory, encoder)
    {
        _logger = logger;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // If the X-User-Id header is not present, return no result so the next handler can try
        if (!Request.Headers.TryGetValue("X-User-Id", out var userIdValues) ||
            string.IsNullOrEmpty(userIdValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = userIdValues.First() ?? string.Empty;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        _logger.LogDebug("Authenticated user {UserId} via X-User-Id header.", userId);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}