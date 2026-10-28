namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The ranges a Money Priorities answer must fall in (spec #315, the Priority Walk column table), shared by the
/// ApiService validator and the Web form. The amounts are written in whole US dollars;
/// <see cref="Helpers.MoneyBounds"/> scales them to the Walk's currency (#444).
/// </summary>
public static class MoneyPrioritiesLimits
{
    /// <summary>The largest extra monthly amount, in US dollars. It must also be over 0.</summary>
    public const long MonthlyAmountMax = 1_000_000;

    /// <summary>The longest Purpose, in characters.</summary>
    public const int PurposeMaxLength = 300;

    /// <summary>The largest debt APR, in percent. 0 means no debt.</summary>
    public const decimal DebtAprMax = 100m;

    /// <summary>The largest debt balance, in US dollars. It must also be over 0.</summary>
    public const long DebtBalanceMax = 100_000_000;

    /// <summary>The most months of expenses an emergency fund can be said to cover.</summary>
    public const decimal EmergencyFundMonthsMax = 120m;

    /// <summary>The largest monthly expenses, in US dollars. They must also be over 0.</summary>
    public const long MonthlyExpensesMax = 1_000_000;

    /// <summary>The largest employer match, in percent of pay.</summary>
    public const decimal EmployerMatchPercentMax = 100m;

    /// <summary>The shortest horizon, in whole years.</summary>
    public const int HorizonYearsMin = 1;

    /// <summary>The longest horizon, in whole years.</summary>
    public const int HorizonYearsMax = 50;

    /// <summary>How many of an Account's newest Priority Walks Earlier results lists.</summary>
    public const int RecentWalkCount = 10;
}
