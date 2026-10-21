namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Request to set the planned amount for a category in a specific month.
/// </summary>
public class SetPlannedRequest
{
    /// <summary>Planned amount (>= 0, up to 2 decimal places).</summary>
    public decimal Amount { get; set; }
}
