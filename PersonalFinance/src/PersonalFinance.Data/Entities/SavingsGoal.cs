using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's savings goal with progress tracking.
/// </summary>
public class SavingsGoal : AuditableEntity
{
    /// <summary>
    /// ISO 4217 code, one of <see cref="SupportedCurrencies"/> (ADR 0016). The target, the saved amount and every
    /// Contribution of this goal are in this currency. Rows saved before amounts carried a currency are <c>USD</c>.
    /// </summary>
    public string CurrencyCode { get; set; } = SupportedCurrencies.DefaultCode;

    /// <summary>Display name of the goal (e.g., "Emergency Fund", "Vacation").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Target amount in dollars.</summary>
    public decimal TargetAmount { get; set; }

    /// <summary>
    /// The saved amount: the total of the goal's Contributions, cached here and rewritten in the same save as every
    /// Contribution change. Dashboard, Search, the AI Assistant and Data Export read it.
    /// </summary>
    public decimal CurrentAmount { get; set; }

    /// <summary>Optional description of the goal.</summary>
    public string? Description { get; set; }

    /// <summary>Optional target date to reach the goal.</summary>
    public DateTime? TargetDate { get; set; }

    /// <summary>Reached: derived, true while the saved amount meets or passes the target. The user never sets it.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// The highest milestone (0, 50, 75 or 100 percent) a Notification has been sent for. It never falls, so a
    /// withdrawal and re-deposit don't repeat a milestone Notification.
    /// </summary>
    public int HighestMilestoneNotified { get; set; }
}
