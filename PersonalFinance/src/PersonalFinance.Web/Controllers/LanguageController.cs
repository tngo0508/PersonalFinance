using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// The language switcher's target (spec §2.3): a form POST with antiforgery, never a GET, that writes the culture cookie
/// and returns to the page it came from. Open to anonymous visitors, so the Landing, sign-in and legal pages can switch.
/// </summary>
[AllowAnonymous]
public class LanguageController : Controller
{
    private readonly TimeProvider _timeProvider;

    public LanguageController(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Switches the UI language to <paramref name="language"/> and redirects to <paramref name="returnUrl"/> when it is
    /// local, otherwise to the home page. An unsupported language changes nothing.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set([FromForm] string? language, [FromForm] string? returnUrl)
    {
        AppLanguage? chosen = AppLanguages.Find(language);
        if (chosen is null)
        {
            return BadRequest();
        }

        LanguageCookie.Write(Response, chosen, _timeProvider.GetUtcNow());
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
    }
}
