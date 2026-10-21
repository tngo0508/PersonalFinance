using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Data transfer object describing the user's active budget data source preference.
/// </summary>
public class BudgetDataSourceDto
{
    /// <summary>The active budget data source (GoogleSheet or BudgetTracker).</summary>
    public BudgetDataSource Source { get; set; }

    /// <summary>True if this is the system default (not yet explicitly chosen by the user).</summary>
    public bool IsDefault { get; set; }
}
