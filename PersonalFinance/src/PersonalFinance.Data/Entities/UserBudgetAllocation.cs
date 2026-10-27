using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's budget allocation for a given month.
/// Stores the percentage and the exact dollar amount allocated to each spending category.
/// </summary>
public class UserBudgetAllocation : AuditableEntity
{
    /// <summary>
    /// ISO 4217 code, one of <see cref="SupportedCurrencies"/> (ADR 0016). The income and every category amount of this
    /// allocation are in this currency. Rows saved before amounts carried a currency are <c>USD</c>.
    /// </summary>
    public string CurrencyCode { get; set; } = SupportedCurrencies.DefaultCode;

    /// <summary>Monthly income amount in dollars.</summary>
    public decimal MonthlyIncome { get; set; }

    /// <summary>Dollar amount allocated to Housing (rent/mortgage).</summary>
    public decimal HousingAmount { get; set; }

    /// <summary>Dollar amount allocated to Food (groceries + dining).</summary>
    public decimal FoodAmount { get; set; }

    /// <summary>Dollar amount allocated to Transportation.</summary>
    public decimal TransportationAmount { get; set; }

    /// <summary>Dollar amount allocated to Utilities.</summary>
    public decimal UtilitiesAmount { get; set; }

    /// <summary>Dollar amount allocated to Insurance.</summary>
    public decimal InsuranceAmount { get; set; }

    /// <summary>Dollar amount allocated to Healthcare.</summary>
    public decimal HealthcareAmount { get; set; }

    /// <summary>Dollar amount allocated to Savings.</summary>
    public decimal SavingsAmount { get; set; }

    /// <summary>Dollar amount allocated to Debt Repayment.</summary>
    public decimal DebtRepaymentAmount { get; set; }

    /// <summary>Dollar amount allocated to Entertainment.</summary>
    public decimal EntertainmentAmount { get; set; }

    /// <summary>Dollar amount allocated to Miscellaneous.</summary>
    public decimal MiscellaneousAmount { get; set; }

    /// <summary>The month this budget applies to (e.g., "2026-01").</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>Timestamp when this budget was last saved.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
