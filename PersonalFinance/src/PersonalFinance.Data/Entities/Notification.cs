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
    /// Gets or sets the localization key of the title (e.g. <c>Notifications.SavingsMilestone.Title</c>). When set, the
    /// Web app renders the title from this key in the reader's language and <see cref="Title"/> is the English snapshot
    /// kept for the Data Export and for notifications written before keys existed (localization spec §2.3).
    /// </summary>
    public string? TitleKey { get; set; }

    /// <summary>
    /// Gets or sets the localization key of the message, rendered like <see cref="TitleKey"/>.
    /// </summary>
    public string? MessageKey { get; set; }

    /// <summary>
    /// Gets or sets the named arguments of <see cref="TitleKey"/> and <see cref="MessageKey"/>, as a JSON object of
    /// strings (<c>{"goalName":"Emergency fund","percent":"50"}</c>).
    /// </summary>
    public string? Arguments { get; set; }

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
