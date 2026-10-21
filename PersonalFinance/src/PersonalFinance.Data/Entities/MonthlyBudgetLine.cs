namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing one category's planned amount and optional manual Actual override for a month.
/// </summary>
public class MonthlyBudgetLine : AuditableEntity
{
    /// <summary>Foreign key to the monthly plan this line belongs to.</summary>
    public int PlanId { get; set; }

    /// <summary>Foreign key to the budget category.</summary>
    public int CategoryId { get; set; }

    /// <summary>Planned amount for the category in this month.</summary>
    public decimal PlannedAmount { get; set; }

    /// <summary>Manual Actual that replaces the sum of transactions when set; null means use transactions.</summary>
    public decimal? ActualOverride { get; set; }

    /// <summary>Navigation property to the monthly plan.</summary>
    public MonthlyBudgetPlan? Plan { get; set; }

    /// <summary>Navigation property to the budget category.</summary>
    public BudgetCategory? Category { get; set; }
}
