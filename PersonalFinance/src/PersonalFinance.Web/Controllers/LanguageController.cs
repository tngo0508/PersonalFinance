using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// The language switcher's target (spec §2.3): a form POST with antiforgery, never a GET, that writes the culture cookie
/// and returns to the page it came from. Open to anonymous visitors, so the Landing, sign-in and legal pages can switch;
/// for a signed-in Account the choice is also stored, so it follows the Account to its next sign-in.
/// </summary>
[AllowAnonymous]
public class LanguageController : Controller
{
    private readonly IUserPreferenceService _preferences;
    private readonly TimeProvider _timeProvider;

    public LanguageController(IUserPreferenceService preferences, TimeProvider timeProvider)
    {
        _preferences = preferences;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Switches the UI language to <paramref name="language"/> and redirects to <paramref name="returnUrl"/> when it is
    /// local, otherwise to the home page. An unsupported language changes nothing.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set([FromForm] string? language, [FromForm] string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        AppLanguage? chosen = AppLanguages.Find(language);
        if (chosen is null)
        {
            return BadRequest();
        }

        // Only ever the signed-in Account's own row: the id comes from its authentication cookie, never the form.
        string? userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        if (!string.IsNullOrEmpty(userId))
        {
            await _preferences.SetLanguageAsync(userId, chosen.Code, cancellationToken);
        }

        LanguageCookie.Write(Response, chosen, _timeProvider.GetUtcNow());
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
    }
}
