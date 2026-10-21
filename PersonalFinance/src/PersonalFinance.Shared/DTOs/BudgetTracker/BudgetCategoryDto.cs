using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Data transfer object for a Budget Tracker category.
/// </summary>
public class BudgetCategoryDto
{
    /// <summary>Category identifier.</summary>
    public int Id { get; set; }

    /// <summary>Category name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the category is for Expense or Income.</summary>
    public BudgetCategoryKind Kind { get; set; }

    /// <summary>Display sort position within its kind.</summary>
    public int SortOrder { get; set; }

    /// <summary>Whether the category is archived (soft-deleted).</summary>
    public bool IsArchived { get; set; }
}
