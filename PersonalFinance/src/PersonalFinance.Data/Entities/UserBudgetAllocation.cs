namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's budget allocation for a given month.
/// Stores the percentage allocation for each spending category.
/// </summary>
public class UserBudgetAllocation : AuditableEntity
{
    /// <summary>Monthly income amount in dollars.</summary>
    public decimal MonthlyIncome { get; set; }

    /// <summary>Percentage allocated to Housing (rent/mortgage).</summary>
    public decimal HousingPercent { get; set; }

    /// <summary>Percentage allocated to Food (groceries + dining).</summary>
    public decimal FoodPercent { get; set; }

    /// <summary>Percentage allocated to Transportation.</summary>
    public decimal TransportationPercent { get; set; }

    /// <summary>Percentage allocated to Utilities.</summary>
    public decimal UtilitiesPercent { get; set; }

    /// <summary>Percentage allocated to Insurance.</summary>
    public decimal InsurancePercent { get; set; }

    /// <summary>Percentage allocated to Healthcare.</summary>
    public decimal HealthcarePercent { get; set; }

    /// <summary>Percentage allocated to Savings.</summary>
    public decimal SavingsPercent { get; set; }

    /// <summary>Percentage allocated to Debt Repayment.</summary>
    public decimal DebtRepaymentPercent { get; set; }

    /// <summary>Percentage allocated to Entertainment.</summary>
    public decimal EntertainmentPercent { get; set; }

    /// <summary>Percentage allocated to Miscellaneous.</summary>
    public decimal MiscellaneousPercent { get; set; }

    /// <summary>The month this budget applies to (e.g., "2026-01").</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>Timestamp when this budget was last saved.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
