namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's savings goal with progress tracking.
/// </summary>
public class SavingsGoal
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the ASP.NET Identity user who owns this goal.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

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

    /// <summary>Timestamp when this goal was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when this goal was last updated.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
