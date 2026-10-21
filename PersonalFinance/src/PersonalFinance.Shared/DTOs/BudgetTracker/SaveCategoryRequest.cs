using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Request to create or update a budget category.
/// </summary>
public class SaveCategoryRequest
{
    /// <summary>Category name (1-100 characters).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether this is an Expense or Income category.</summary>
    public BudgetCategoryKind Kind { get; set; }

    /// <summary>Sort position within this category kind.</summary>
    public int SortOrder { get; set; }
}
