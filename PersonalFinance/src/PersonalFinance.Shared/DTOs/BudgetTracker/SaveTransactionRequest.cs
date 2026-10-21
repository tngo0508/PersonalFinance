namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Request to create or update a Budget Tracker transaction.
/// </summary>
public class SaveTransactionRequest
{
    /// <summary>Transaction calendar date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Transaction amount (> 0, up to 2 decimal places).</summary>
    public decimal Amount { get; set; }

    /// <summary>Description or note (max 200 characters).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Identifier of the category this transaction belongs to.</summary>
    public int CategoryId { get; set; }
}
