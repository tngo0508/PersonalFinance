using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Models.InvestmentCalculator;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Investment Calculator: a projection of how an investment plan grows, in the Account's
/// Reporting Currency. The page script (investment-math.js) does every calculation, so the page keeps working when
/// the ApiService is down; this controller only picks the currency.
/// </summary>
[Authorize]
public class InvestmentCalculatorController : Controller
{
    private readonly IUserPreferenceService _preferences;

    public InvestmentCalculatorController(IUserPreferenceService preferences)
    {
        _preferences = preferences;
    }

    /// <summary>
    /// Displays the Investment Calculator with its money sliders scaled to the Reporting Currency.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string currency = userId is null
            ? SupportedCurrencies.DefaultCode
            : await _preferences.GetReportingCurrencyAsync(userId, cancellationToken);
        return View(InvestmentCalculatorViewModel.For(currency));
    }
}
