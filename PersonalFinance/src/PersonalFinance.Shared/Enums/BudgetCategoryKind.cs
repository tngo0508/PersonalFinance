namespace PersonalFinance.Shared.Enums;

/// <summary>
/// Enum representing whether a Budget Tracker category groups expenses or income.
/// </summary>
public enum BudgetCategoryKind
{
    /// <summary>
    /// Spending category. Transactions in this category are expenses.
    /// </summary>
    Expense = 1,

    /// <summary>
    /// Earnings category. Transactions in this category are income.
    /// </summary>
    Income = 2
}
