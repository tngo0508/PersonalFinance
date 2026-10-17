namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a dated Budget Tracker expense or income entry.
/// The month is derived from <see cref="Date"/>; the transaction has no separate month reference.
/// </summary>
public class BudgetTransaction : AuditableEntity
{
    /// <summary>Foreign key to the budget category. Its kind decides whether this is an expense or income.</summary>
    public int CategoryId { get; set; }

    /// <summary>Calendar date of the transaction.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Positive amount of the transaction.</summary>
    public decimal Amount { get; set; }

    /// <summary>Free-text description of the transaction.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Navigation property to the budget category.</summary>
    public BudgetCategory? Category { get; set; }
}