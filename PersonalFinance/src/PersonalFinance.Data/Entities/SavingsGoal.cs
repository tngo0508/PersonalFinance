namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's savings goal with progress tracking.
/// </summary>
public class SavingsGoal : AuditableEntity
{
    /// <summary>Display name of the goal (e.g., "Emergency Fund", "Vacation").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Target amount in dollars.</summary>
    public decimal TargetAmount { get; set; }

    /// <summary>Current amount saved in dollars.</summary>
    public decimal CurrentAmount { get; set; }

    /// <summary>Optional description of the goal.</summary>
    public string? Description { get; set; }

    /// <summary>Optional target date to reach the goal.</summary>
    public DateTime? TargetDate { get; set; }

    /// <summary>Whether this goal is marked as completed.</summary>
    public bool IsCompleted { get; set; }
}
