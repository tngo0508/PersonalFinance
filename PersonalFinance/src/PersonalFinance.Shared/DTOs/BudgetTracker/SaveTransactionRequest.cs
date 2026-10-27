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

    /// <summary>
    /// The ISO 4217 currency of the amount, one of <c>SupportedCurrencies</c>; null keeps the stored transaction's currency
    /// (US dollars for a new one).
    /// </summary>
    public string? CurrencyCode { get; set; }
}
