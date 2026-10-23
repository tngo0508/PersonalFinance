using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
///     Authentication scheme name for the X-User-Id header-based internal authentication.
/// </summary>
public static class XUserIdAuthenticationDefaults
{
    public const string Scheme = "XUserId";

    /// <summary>Header carrying the shared secret that proves the caller is the Web app.</summary>
    public const string InternalKeyHeader = "X-Internal-Key";

    /// <summary>Configuration key holding the shared secret (<c>Internal:ApiKey</c>).</summary>
    public const string InternalKeyConfig = "Internal:ApiKey";
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
        if (!Request.Headers.TryGetValue("X-User-Id", out StringValues userIdValues) ||
            string.IsNullOrEmpty(userIdValues))
            return AuthenticateResult.NoResult();

        string? configuredKey = Context.RequestServices.GetService<IConfiguration>()?[XUserIdAuthenticationDefaults.InternalKeyConfig];
        bool keyConfigured = !string.IsNullOrEmpty(configuredKey);
        if (keyConfigured)
        {
            // A configured shared secret is mandatory: callers without it are not authenticated at all.
            string supplied = Request.Headers[XUserIdAuthenticationDefaults.InternalKeyHeader].FirstOrDefault() ?? string.Empty;
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(configuredKey!)))
            {
                _logger.LogWarning("Rejected X-User-Id request with a missing or invalid internal key.");
                return AuthenticateResult.NoResult();
            }
        }

        string userId = userIdValues.First() ?? string.Empty;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        // Roles are always read from the database on every request; a client-supplied roles header is never trusted.
        // They are only attached when the caller proved itself with the shared secret (or in Development),
        // because X-User-Id alone is spoofable when the API is reachable from outside.
        bool isDevelopment = Context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true;
        UserManager<IdentityUser>? userManager = Context.RequestServices.GetService<UserManager<IdentityUser>>();
        if (userManager is not null && (keyConfigured || isDevelopment))
        {
            IdentityUser? user = await userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                IList<string> roles = await userManager.GetRolesAsync(user);
                claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Authenticated user {UserId} via X-User-Id header.", userId);
        }
        return AuthenticateResult.Success(ticket);
    }
}
