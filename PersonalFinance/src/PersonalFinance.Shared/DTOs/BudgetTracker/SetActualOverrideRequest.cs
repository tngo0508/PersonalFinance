namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Request to set or revert a manual Actual override for a category in a specific month.
/// Pass null or omit to revert back to the sum of transactions.
/// </summary>
public class SetActualOverrideRequest
{
    /// <summary>Manual override amount, or null to revert to sum of transactions.</summary>
    public decimal? Amount { get; set; }
}