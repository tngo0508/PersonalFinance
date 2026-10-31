namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The Investment Calculator's money inputs (#587): the starting plan, the sliders' ranges and steps, and the largest
/// amount a field takes. Written in whole US dollars; <see cref="Helpers.MoneyBounds"/> scales each to the Reporting
/// Currency, so the sliders cover sensible amounts in dong too. Every amount is also at least 0. The rate and year
/// bounds below are the same as the page's math module's (investment-math.js); the Investment Scenario validators
/// (#592) check a saved plan against all of them.
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

    /// <summary>What the "more a month" what-if lever adds to the monthly contribution, in US dollars.</summary>
    public const long LeverMonthly = 100;

    /// <summary>The largest starting amount the field takes, in US dollars.</summary>
    public const long InitialMax = 100_000_000;

    /// <summary>The largest monthly contribution the field takes, in US dollars.</summary>
    public const long MonthlyMax = 1_000_000;

    /// <summary>The goal amount the Reach a goal tile opens with, in US dollars.</summary>
    public const long DefaultGoal = 500_000;

    /// <summary>The largest goal amount the field takes, in US dollars.</summary>
    public const long GoalMax = 1_000_000_000;

    /// <summary>The highest assumed yearly return, in percent.</summary>
    public const decimal AssumedReturnMax = 15m;

    /// <summary>The fewest years a plan runs.</summary>
    public const int YearsMin = 1;

    /// <summary>The most years a plan runs.</summary>
    public const int YearsMax = 50;

    /// <summary>The highest yearly raise of the monthly contribution, in percent.</summary>
    public const decimal ContributionIncreaseMax = 10m;

    /// <summary>The highest yearly inflation, in percent.</summary>
    public const decimal InflationMax = 10m;

    /// <summary>The highest yearly fund fee, in percent.</summary>
    public const decimal FeeMax = 3m;

    /// <summary>The longest name of an Investment Scenario.</summary>
    public const int ScenarioNameMaxLength = 100;

    /// <summary>The longest note of an Investment Scenario.</summary>
    public const int ScenarioNoteMaxLength = 500;

    /// <summary>The most Investment Scenarios an Account keeps (deleted ones do not count).</summary>
    public const int MaxScenariosPerUser = 20;
}
