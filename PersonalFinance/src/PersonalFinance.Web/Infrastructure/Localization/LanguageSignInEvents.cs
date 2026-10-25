using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Writes the culture cookie from the Account's stored Language whenever it signs in (localization spec §2.3), so the
/// Language follows the Account to a new browser or device. One hook on the application cookie covers every way in:
/// password, Google, two-factor, recovery code, registration and the refresh after a password change. An Account
/// with no stored Language keeps whatever the request already had.
/// </summary>
internal static partial class LanguageSignInEvents
{
    /// <summary>Adds the hook to <paramref name="events"/>, keeping any <c>OnSignedIn</c> already there.</summary>
    public static void Apply(CookieAuthenticationEvents events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Func<CookieSignedInContext, Task> previous = events.OnSignedIn;
        events.OnSignedIn = async context =>
        {
            await previous(context);
            await WriteStoredLanguageAsync(context.HttpContext, context.Principal);
        };
    }

    /// <summary>
    /// Writes the cookie for <paramref name="principal"/>'s stored Language. A failure to read it is logged and
    /// ignored: signing in must not depend on the preference.
    /// </summary>
    public static async Task WriteStoredLanguageAsync(HttpContext httpContext, ClaimsPrincipal? principal)
    {
        string? userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        IServiceProvider services = httpContext.RequestServices;
        try
        {
            string? stored = await services.GetRequiredService<IUserPreferenceService>()
                .GetLanguageAsync(userId, httpContext.RequestAborted);
            AppLanguage? language = AppLanguages.Find(stored);
            if (language is not null)
            {
                LanguageCookie.Write(httpContext.Response, language, services.GetRequiredService<TimeProvider>().GetUtcNow());
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReadFailed(services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(LanguageSignInEvents)), ex);
        }
    }

    [LoggerMessage(EventId = 3403, EventName = "LanguagePreferenceReadFailed", Level = LogLevel.Warning,
        Message = "Could not read the stored language at sign-in; the request's language is kept.")]
    private static partial void LogReadFailed(ILogger logger, Exception exception);
}
