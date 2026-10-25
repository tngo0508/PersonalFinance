using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Account settings › Language: the source of truth for the Account's Language (localization spec §2.3). Saving stores
/// it and rewrites the culture cookie, so the page reloads in the new language. The account-menu switcher stores the
/// same preference.
/// </summary>
public class LanguageModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserPreferenceService _preferences;
    private readonly IStringLocalizer _localizer;
    private readonly TimeProvider _timeProvider;

    public LanguageModel(UserManager<IdentityUser> userManager, IUserPreferenceService preferences,
        IStringLocalizer localizer, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _preferences = preferences;
        _localizer = localizer;
        _timeProvider = timeProvider;
    }

    /// <summary>The language code chosen in the form.</summary>
    [BindProperty]
    public string? Language { get; set; }

    /// <summary>True on the page shown right after a save, for the confirmation message.</summary>
    [TempData]
    public bool Saved { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        string? userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return NotFound();
        }

        // An Account that never chose sees the language it is using now.
        Language = await _preferences.GetLanguageAsync(userId, cancellationToken) ?? AppLanguages.Current().Code;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        string? userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return NotFound();
        }

        AppLanguage? chosen = AppLanguages.Find(Language);
        if (chosen is null)
        {
            ModelState.AddModelError(nameof(Language), _localizer["Account.Language.Invalid"]);
            return Page();
        }

        await _preferences.SetLanguageAsync(userId, chosen.Code, cancellationToken);
        LanguageCookie.Write(Response, chosen, _timeProvider.GetUtcNow());
        Saved = true;
        return RedirectToPage();
    }
}
