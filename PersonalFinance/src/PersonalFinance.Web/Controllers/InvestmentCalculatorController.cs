using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Web.Models.InvestmentCalculator;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Investment Calculator: a projection of how an investment plan grows, in the Account's
/// Reporting Currency. The page script (investment-math.js) does every calculation, so the page keeps working when
/// the ApiService is down; this controller picks the currency and reads the Savings Goals the goal tile can start
/// from (never writing to them).
/// </summary>
[Authorize]
public class InvestmentCalculatorController : Controller
{
    private readonly IUserPreferenceService _preferences;
    private readonly ISavingsGoalsApi _savingsGoalsApi;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InvestmentCalculatorController> _logger;

    public InvestmentCalculatorController(IUserPreferenceService preferences, ISavingsGoalsApi savingsGoalsApi,
        TimeProvider timeProvider, ILogger<InvestmentCalculatorController> logger)
    {
        _preferences = preferences;
        _savingsGoalsApi = savingsGoalsApi;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Investment Calculator with its money sliders scaled to the Reporting Currency, and the active
    /// Savings Goals its goal tile can start from. A failed goal load only removes the picker.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string currency = SupportedCurrencies.CodeOrDefault(userId is null
            ? SupportedCurrencies.DefaultCode
            : await _preferences.GetReportingCurrencyAsync(userId, cancellationToken));

        IReadOnlyList<SavingsGoalOption> goals = [];
        bool unavailable = false;
        try
        {
            List<SavingsGoalDto> saved = await _savingsGoalsApi.GetSavingsGoalsAsync(cancellationToken);
            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            goals = SavingsGoalOption.From(saved, currency, today);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load savings goals for the Investment Calculator; hiding the goal picker.");
            unavailable = true;
        }

        return View(new InvestmentCalculatorViewModel
        {
            CurrencyCode = currency,
            SavingsGoals = goals,
            SavingsGoalsUnavailable = unavailable
        });
    }
}
