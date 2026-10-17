using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's Budget Tracker category (expense or income).
/// Deleting a category soft-deletes it so historical months keep their names and totals.
/// </summary>
public class BudgetCategory : AuditableEntity
{
    /// <summary>Display name of the category, unique per user and kind (case-insensitive).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the category groups expenses or income.</summary>
    public BudgetCategoryKind Kind { get; set; }

    /// <summary>Position of the category in its table.</summary>
    public int SortOrder { get; set; }
}