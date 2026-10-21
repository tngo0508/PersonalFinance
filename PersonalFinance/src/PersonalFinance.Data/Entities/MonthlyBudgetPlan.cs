namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's Budget Tracker plan for one calendar month.
/// </summary>
public class MonthlyBudgetPlan : AuditableEntity
{
    /// <summary>Calendar year of the plan.</summary>
    public int Year { get; set; }

    /// <summary>Calendar month of the plan (1-12).</summary>
    public int Month { get; set; }

    /// <summary>Planned and override lines, one per category for this month.</summary>
    public ICollection<MonthlyBudgetLine> Lines { get; set; } = new List<MonthlyBudgetLine>();
}
