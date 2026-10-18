namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// Shared helper methods for budget-related calculations.
/// </summary>
public static class BudgetHelper
{
    /// <summary>
    /// Calculates the percentage of monthly income represented by a dollar amount.
    /// Returns 0 when income is 0 to avoid division by zero.
    /// </summary>
    /// <param name="monthlyIncome">The total monthly income.</param>
    /// <param name="amount">The dollar amount to convert to a percentage.</param>
    /// <returns>The percentage value, or 0 when income is 0.</returns>
    public static decimal ToPercent(decimal monthlyIncome, decimal amount) =>
        monthlyIncome > 0 ? amount * 100m / monthlyIncome : 0m;
}
