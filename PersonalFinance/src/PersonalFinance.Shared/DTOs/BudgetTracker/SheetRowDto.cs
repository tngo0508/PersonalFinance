namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// A single category line in a section of the monthly budget sheet. <see cref="Planned"/>, <see cref="Actual"/> and
/// <see cref="Diff"/> are in the sheet's Reporting Currency (ADR 0016), so the section totals sum them; a line stored
/// in another currency also carries its original amounts and currency.
/// </summary>
public class SheetRowDto
{
    /// <summary>Identifier of the budget category.</summary>
    public int CategoryId { get; set; }

    /// <summary>Display name of the category.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Planned amount for this category in this month, in the Reporting Currency.</summary>
    public decimal Planned { get; set; }

    /// <summary>Effective actual amount (override when present, otherwise transaction sum), in the Reporting Currency.</summary>
    public decimal Actual { get; set; }

    /// <summary>
    /// The ISO 4217 currency the line's planned amount and override are stored in: the Reporting Currency when the
    /// line has none yet.
    /// </summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The stored planned amount in <see cref="CurrencyCode"/>; null when that is the Reporting Currency.</summary>
    public decimal? OriginalPlanned { get; set; }

    /// <summary>
    /// The stored override, or the transaction sum when every transaction shares <see cref="CurrencyCode"/>, in that
    /// currency; null when that is the Reporting Currency or the transactions are mixed.
    /// </summary>
    public decimal? OriginalActual { get; set; }

    /// <summary>
    /// Calculated difference.
    /// For Expense: Planned - Actual.
    /// For Income: Actual - Planned.
    /// </summary>
    public decimal Diff { get; set; }

    /// <summary>True if Actual is set via a manual override rather than transaction sum.</summary>
    public bool ActualIsOverride { get; set; }

    /// <summary>True if the category has been archived/soft-deleted.</summary>
    public bool IsArchived { get; set; }
}
