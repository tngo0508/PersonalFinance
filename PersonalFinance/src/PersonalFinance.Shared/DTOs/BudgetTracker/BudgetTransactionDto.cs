using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Data transfer object for a Budget Tracker transaction.
/// </summary>
public class BudgetTransactionDto
{
    /// <summary>Transaction identifier.</summary>
    public int Id { get; set; }

    /// <summary>Calendar date of the transaction.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Transaction amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Description or note for the transaction.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Identifier of the category.</summary>
    public int CategoryId { get; set; }

    /// <summary>Name of the category.</summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Kind of the category (Expense or Income).</summary>
    public BudgetCategoryKind Kind { get; set; }
}
