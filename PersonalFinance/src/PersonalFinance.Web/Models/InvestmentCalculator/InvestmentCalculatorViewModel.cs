using PersonalFinance.Shared.Constants;
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

    /// <summary>The model for <paramref name="currencyCode"/>; an unsupported code reads as US dollars.</summary>
    public static InvestmentCalculatorViewModel For(string? currencyCode) =>
        new() { CurrencyCode = SupportedCurrencies.CodeOrDefault(currencyCode) };

    private decimal Scaled(long dollars) => MoneyBounds.Max(dollars, CurrencyCode);
}
