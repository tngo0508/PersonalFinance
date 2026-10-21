using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// Cookie authentication redirect handling that answers fetch/XHR and <c>/api</c> calls with a status code
/// (401 or 403) instead of a 302 to the login page, which JavaScript callers cannot follow meaningfully.
/// </summary>
public static class ApiAwareCookieRedirects
{
    /// <summary>
    /// Replaces the login and access-denied redirects on <paramref name="events"/>.
    /// </summary>
    public static void Apply(CookieAuthenticationEvents events)
    {
        events.OnRedirectToLogin = context => RedirectOrStatus(context, StatusCodes.Status401Unauthorized);
        events.OnRedirectToAccessDenied = context => RedirectOrStatus(context, StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// True for requests made by scripts rather than page navigation.
    /// </summary>
    public static bool IsApiRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api") ||
               request.Headers.Accept.Any(h => h != null && h.Contains("application/json")) ||
               request.Headers.XRequestedWith == "XMLHttpRequest";
    }

    private static Task RedirectOrStatus(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = statusCode;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }

        return Task.CompletedTask;
    }
}
