namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The Investment Calculator's money inputs (#587): the starting plan, the sliders' ranges and steps, and the largest
/// amount a field takes. Written in whole US dollars; <see cref="Helpers.MoneyBounds"/> scales each to the Reporting
/// Currency, so the sliders cover sensible amounts in dong too. Every amount is also at least 0. The rates and years
/// keep their own ranges in the page's math module (investment-math.js).
/// </summary>
public static class InvestmentCalculatorLimits
{
    /// <summary>The starting amount of a new plan, in US dollars.</summary>
    public const long DefaultInitial = 10_000;

    /// <summary>The monthly contribution of a new plan, in US dollars.</summary>
    public const long DefaultMonthly = 500;

    /// <summary>The right end of the starting amount slider, in US dollars.</summary>
    public const long InitialSliderMax = 500_000;

    /// <summary>The starting amount slider's step, in US dollars.</summary>
    public const long InitialStep = 1_000;

    /// <summary>The right end of the monthly contribution slider, in US dollars.</summary>
    public const long MonthlySliderMax = 10_000;

    /// <summary>The monthly contribution slider's step, in US dollars.</summary>
    public const long MonthlyStep = 50;

    /// <summary>The largest starting amount the field takes, in US dollars.</summary>
    public const long InitialMax = 100_000_000;

    /// <summary>The largest monthly contribution the field takes, in US dollars.</summary>
    public const long MonthlyMax = 1_000_000;

    /// <summary>The goal amount the Reach a goal tile opens with, in US dollars.</summary>
    public const long DefaultGoal = 500_000;

    /// <summary>The largest goal amount the field takes, in US dollars.</summary>
    public const long GoalMax = 1_000_000_000;
}
