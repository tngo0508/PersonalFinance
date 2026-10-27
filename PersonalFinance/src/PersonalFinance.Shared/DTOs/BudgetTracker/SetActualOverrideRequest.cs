namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Request to set or revert a manual Actual override for a category in a specific month.
/// Pass null or omit to revert back to the sum of transactions.
/// </summary>
public class SetActualOverrideRequest
{
    /// <summary>Manual override amount, or null to revert to sum of transactions.</summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// The ISO 4217 currency of the amount, one of <c>SupportedCurrencies</c>; null keeps the stored line's currency
    /// (US dollars for a new one).
    /// </summary>
    public string? CurrencyCode { get; set; }
}
