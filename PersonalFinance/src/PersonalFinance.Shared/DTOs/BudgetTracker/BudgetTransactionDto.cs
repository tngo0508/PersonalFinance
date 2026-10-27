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

    /// <summary>Transaction amount, in <see cref="CurrencyCode"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>The ISO 4217 currency of <see cref="Amount"/> (ADR 0016).</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="Amount"/> in the Account's Reporting Currency at the rate of <see cref="Date"/>; null when there is no
    /// rate at all. Equal to <see cref="Amount"/> when the transaction is in the Reporting Currency.
    /// </summary>
    public decimal? ReportingAmount { get; set; }

    /// <summary>Description or note for the transaction.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Identifier of the category.</summary>
    public int CategoryId { get; set; }

    /// <summary>Name of the category.</summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Kind of the category (Expense or Income).</summary>
    public BudgetCategoryKind Kind { get; set; }
}
