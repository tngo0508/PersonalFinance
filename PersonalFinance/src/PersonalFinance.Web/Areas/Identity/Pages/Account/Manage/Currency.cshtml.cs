using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Account settings › Currency: the Account's Reporting Currency (ADR 0016, localization spec §2.5), the one currency
/// its totals are shown in. Independent of the Language: a Vietnamese reader may report in US dollars. Changing it
/// never rewrites a stored amount; the pages convert at the latest rate from then on.
/// </summary>
public class CurrencyModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserPreferenceService _preferences;
    private readonly IStringLocalizer _localizer;

    public CurrencyModel(UserManager<IdentityUser> userManager, IUserPreferenceService preferences,
        IStringLocalizer localizer)
    {
        _userManager = userManager;
        _preferences = preferences;
        _localizer = localizer;
    }

    /// <summary>The ISO 4217 code chosen in the form.</summary>
    [BindProperty]
    public string? ReportingCurrency { get; set; }

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

        ReportingCurrency = await _preferences.GetReportingCurrencyAsync(userId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        string? userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return NotFound();
        }

        SupportedCurrency? chosen = SupportedCurrencies.Find(ReportingCurrency);
        if (chosen is null)
        {
            ModelState.AddModelError(nameof(ReportingCurrency), _localizer["Account.Currency.Invalid"]);
            return Page();
        }

        await _preferences.SetReportingCurrencyAsync(userId, chosen.Code, cancellationToken);
        Saved = true;
        return RedirectToPage();
    }
}
