namespace PersonalFinance.Data.Entities;

/// <summary>
/// Entity representing a user notification for budget alerts, savings milestones, and sync status.
/// Inherits from <see cref="AuditableEntity"/> for soft-delete support and audit fields.
/// </summary>
public class Notification : AuditableEntity
{
    /// <summary>
    /// Gets or sets the type of notification (BudgetAlert, SavingsMilestone, SyncStatus).
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Gets or sets the title of the notification.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed message of the notification.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this notification has been read.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this notification was marked as read.
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Gets or sets the source feature that generated this notification
    /// (e.g., "Budget", "SavingsGoals", "GoogleDrive").
    /// </summary>
    public string? SourceFeature { get; set; }
}
