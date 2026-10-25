using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// How much of the user's data one Data Source holds: what a Data Source Removal would delete, or has deleted.
/// </summary>
public class BudgetDataSourceSummaryDto
{
    /// <summary>The Data Source these counts belong to.</summary>
    public BudgetDataSource Source { get; set; }

    /// <summary>Calendar months with data: synced months for the Google Sheet; months with a plan or a transaction for the Budget Tracker.</summary>
    public int MonthCount { get; set; }

    /// <summary>Transactions: synced transaction records, or Budget Tracker transactions.</summary>
    public int TransactionCount { get; set; }

    /// <summary>True when the source holds any month or transaction.</summary>
    public bool HasData => MonthCount > 0 || TransactionCount > 0;
}
