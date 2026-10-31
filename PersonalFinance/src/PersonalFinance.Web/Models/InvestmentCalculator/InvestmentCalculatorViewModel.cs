using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Web.Models.InvestmentCalculator;

/// <summary>
/// The Investment Calculator page: the Reporting Currency its amounts are in, and the money sliders' starting values,
/// ranges and steps in that currency (<see cref="InvestmentCalculatorLimits"/> scaled by <see cref="MoneyBounds"/>).
/// The page script does every calculation.
/// </summary>
public sealed class InvestmentCalculatorViewModel
{
    /// <summary>The Reporting Currency code, one of <see cref="SupportedCurrencies"/>.</summary>
    public required string CurrencyCode { get; init; }

    public decimal Initial => Scaled(InvestmentCalculatorLimits.DefaultInitial);

    public decimal Monthly => Scaled(InvestmentCalculatorLimits.DefaultMonthly);

    public decimal InitialSliderMax => Scaled(InvestmentCalculatorLimits.InitialSliderMax);

    public decimal InitialStep => Scaled(InvestmentCalculatorLimits.InitialStep);

    public decimal InitialMax => Scaled(InvestmentCalculatorLimits.InitialMax);

    public decimal MonthlySliderMax => Scaled(InvestmentCalculatorLimits.MonthlySliderMax);

    public decimal MonthlyStep => Scaled(InvestmentCalculatorLimits.MonthlyStep);

    public decimal MonthlyMax => Scaled(InvestmentCalculatorLimits.MonthlyMax);

    /// <summary>What the "more a month" what-if lever adds to the monthly contribution.</summary>
    public decimal LeverMonthly => Scaled(InvestmentCalculatorLimits.LeverMonthly);

    public decimal Goal => Scaled(InvestmentCalculatorLimits.DefaultGoal);

    public decimal GoalMax => Scaled(InvestmentCalculatorLimits.GoalMax);

    /// <summary>The active Savings Goals the goal tile can start from (#593); empty hides the picker.</summary>
    public IReadOnlyList<SavingsGoalOption> SavingsGoals { get; init; } = [];

    /// <summary>
    /// True when the Savings Goals could not be loaded: the tile says so in place of the picker, and the calculator
    /// keeps working.
    /// </summary>
    public bool SavingsGoalsUnavailable { get; init; }

    /// <summary>
    /// How many active Savings Goals the picker leaves out because there is no exchange rate to show their target in
    /// the Reporting Currency yet; above zero, the tile says so.
    /// </summary>
    public int SavingsGoalsWithoutRate { get; init; }

    /// <summary>The Account's saved Investment Scenarios, last edited first (#592); the page script lists them.</summary>
    public IReadOnlyList<InvestmentScenarioDto> Scenarios { get; init; } = [];

    /// <summary>
    /// True when the scenarios could not be loaded: the Saved panel shows the unavailable notice and Save is disabled;
    /// the calculator keeps working.
    /// </summary>
    public bool ScenariosUnavailable { get; init; }

    /// <summary>The most scenarios an Account keeps.</summary>
    public int MaxScenarios => InvestmentCalculatorLimits.MaxScenariosPerUser;

    /// <summary>The model for <paramref name="currencyCode"/>; an unsupported code reads as US dollars.</summary>
    public static InvestmentCalculatorViewModel For(string? currencyCode) =>
        new() { CurrencyCode = SupportedCurrencies.CodeOrDefault(currencyCode) };

    private decimal Scaled(long dollars) => MoneyBounds.Max(dollars, CurrencyCode);
}
