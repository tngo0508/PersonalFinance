using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace PersonalFinance.ApiService.Infrastructure;

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

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // If the X-User-Id header is not present, return no result so the next handler can try
        if (!Request.Headers.TryGetValue("X-User-Id", out var userIdValues) ||
            string.IsNullOrEmpty(userIdValues))
            return AuthenticateResult.NoResult();

        var userId = userIdValues.First() ?? string.Empty;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        // Roles are always read from the database on every request; a client-supplied roles header is never trusted.
        var userManager = Context.RequestServices.GetService<UserManager<IdentityUser>>();
        if (userManager is not null)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                var roles = await userManager.GetRolesAsync(user);
                claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        _logger.LogDebug("Authenticated user {UserId} via X-User-Id header.", userId);
        return AuthenticateResult.Success(ticket);
    }
}