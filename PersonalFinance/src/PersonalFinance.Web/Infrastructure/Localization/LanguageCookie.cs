using Microsoft.AspNetCore.Localization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Writes the standard <c>.AspNetCore.Culture</c> cookie, so the built-in <see cref="CookieRequestCultureProvider"/>
/// picks the language on every later request (spec §2.3). The formatting culture is written too (<c>vi-VN</c>), though
/// <see cref="LanguageCultureProvider"/> derives it from the language either way.
/// </summary>
internal static class LanguageCookie
{
    /// <summary>How long the choice is remembered.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    /// <summary>Sets the cookie for <paramref name="language"/> on the response.</summary>
    public static void Write(HttpResponse response, AppLanguage language, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(language);

        response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language.FormattingCulture, language.Code)),
            new CookieOptions
            {
                Expires = now.Add(Lifetime),
                // A preference the visitor set themself, needed for the page to be in their language: strictly necessary.
                IsEssential = true,
                HttpOnly = true,
                Secure = response.HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
    }
}
