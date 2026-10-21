namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// The app's financial ratios, defined once (see <c>GLOSSARY.md</c>). Percentages are rounded to one decimal place.
/// </summary>
public static class FinanceMetrics
{
    /// <summary>
    /// Savings Rate: planned annual savings as a percentage of annual income, from the Net Worth parameters.
    /// <c>null</c> when there is no income to compare with.
    /// </summary>
    public static decimal? SavingsRatePercent(decimal annualSavings, decimal annualIncome)
    {
        return annualIncome > 0m ? Math.Round(annualSavings / annualIncome * 100m, 1) : null;
    }

    /// <summary>
    /// Actual Savings Rate: the share of actual income left after actual expenses, for budget months
    /// (Budget Tracker or synced Google Sheet). 0 when there is no income.
    /// </summary>
    public static decimal ActualSavingsRatePercent(decimal actualIncome, decimal actualExpenses)
    {
        return actualIncome > 0m
            ? Math.Round((actualIncome - actualExpenses) / actualIncome * 100m, 1)
            : 0m;
    }
}
