namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Request to set the planned amount for a category in a specific month.
/// </summary>
public class SetPlannedRequest
{
    /// <summary>Planned amount (>= 0, up to 2 decimal places).</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The ISO 4217 currency of the amount, one of <c>SupportedCurrencies</c>; null keeps the stored line's currency
    /// (US dollars for a new one).
    /// </summary>
    public string? CurrencyCode { get; set; }
}
