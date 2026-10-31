using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Web.Models.InvestmentCalculator;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Investment Calculator: a projection of how an investment plan grows, in the Account's
/// Reporting Currency. The page script (investment-math.js) does every calculation, so the page keeps working when
/// the ApiService is down; this controller picks the currency, reads the Savings Goals the goal tile can start from
/// (never writing to them) and the Account's saved Investment Scenarios for the Saved panel (#592). Saving is done
/// by <see cref="InvestmentScenariosController"/>.
/// </summary>
[Authorize]
public class InvestmentCalculatorController : Controller
{
    private readonly IUserPreferenceService _preferences;
    private readonly ISavingsGoalsApi _savingsGoalsApi;
    private readonly IInvestmentScenariosApi _scenariosApi;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InvestmentCalculatorController> _logger;

    public InvestmentCalculatorController(IUserPreferenceService preferences, ISavingsGoalsApi savingsGoalsApi,
        IInvestmentScenariosApi scenariosApi, TimeProvider timeProvider, ILogger<InvestmentCalculatorController> logger)
    {
        _preferences = preferences;
        _savingsGoalsApi = savingsGoalsApi;
        _scenariosApi = scenariosApi;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Investment Calculator with its money sliders scaled to the Reporting Currency, and the active
    /// Savings Goals its goal tile can start from, and the saved Investment Scenarios. A failed load never blocks the
    /// calculator: failed goals only remove the picker, and failed scenarios show the Saved panel's unavailable notice
    /// with Save disabled.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string currency = SupportedCurrencies.CodeOrDefault(userId is null
            ? SupportedCurrencies.DefaultCode
            : await _preferences.GetReportingCurrencyAsync(userId, cancellationToken));

        // The two reads are independent: run them together, each failing on its own.
        Task<GoalsLoad> goalsTask = LoadSavingsGoalsAsync(currency, cancellationToken);
        Task<ScenariosLoad> scenariosTask = LoadScenariosAsync(cancellationToken);
        await Task.WhenAll(goalsTask, scenariosTask);
        GoalsLoad goals = await goalsTask;
        ScenariosLoad scenarios = await scenariosTask;

        return View(new InvestmentCalculatorViewModel
        {
            CurrencyCode = currency,
            SavingsGoals = goals.Options,
            SavingsGoalsUnavailable = goals.Unavailable,
            SavingsGoalsWithoutRate = goals.WithoutRate,
            Scenarios = scenarios.Scenarios,
            ScenariosUnavailable = scenarios.Unavailable
        });
    }

    private async Task<GoalsLoad> LoadSavingsGoalsAsync(string currency, CancellationToken cancellationToken)
    {
        try
        {
            List<SavingsGoalDto> saved = await _savingsGoalsApi.GetSavingsGoalsAsync(cancellationToken);
            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            return new GoalsLoad(SavingsGoalOption.From(saved, currency, today), false,
                SavingsGoalOption.CountWithoutRate(saved, currency));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load savings goals for the Investment Calculator; hiding the goal picker.");
            return new GoalsLoad([], true, 0);
        }
    }

    private async Task<ScenariosLoad> LoadScenariosAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ScenariosLoad(await _scenariosApi.GetScenariosAsync(cancellationToken), false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load Investment Scenarios; the Saved panel shows the unavailable notice.");
            return new ScenariosLoad([], true);
        }
    }

    private sealed record GoalsLoad(IReadOnlyList<SavingsGoalOption> Options, bool Unavailable, int WithoutRate);

    private sealed record ScenariosLoad(IReadOnlyList<InvestmentScenarioDto> Scenarios, bool Unavailable);
}
