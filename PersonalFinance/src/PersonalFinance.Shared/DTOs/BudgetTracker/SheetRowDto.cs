namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// A single category line in a section of the monthly budget sheet.
/// </summary>
public class SheetRowDto
{
    /// <summary>Identifier of the budget category.</summary>
    public int CategoryId { get; set; }

    /// <summary>Display name of the category.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Planned amount for this category in this month.</summary>
    public decimal Planned { get; set; }

    /// <summary>Effective actual amount (override when present, otherwise transaction sum).</summary>
    public decimal Actual { get; set; }

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