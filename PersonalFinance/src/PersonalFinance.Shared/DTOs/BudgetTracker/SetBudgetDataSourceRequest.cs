using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Request to explicitly set the user's budget data source preference.
/// </summary>
public class SetBudgetDataSourceRequest
{
    /// <summary>The data source to select (GoogleSheet or BudgetTracker).</summary>
    public BudgetDataSource Source { get; set; }
}
