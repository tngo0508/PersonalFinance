namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// DTO for notification data transferred between layers.
/// </summary>
public class NotificationDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>
    /// Localization key of the title (<c>Notifications.SavingsMilestone.Title</c>). When set, the Web app renders the
    /// title from it in the reader's language; <see cref="Title"/> is the English snapshot (localization spec §2.3).
    /// </summary>
    public string? TitleKey { get; set; }

    /// <summary>Localization key of the message, rendered like <see cref="TitleKey"/>.</summary>
    public string? MessageKey { get; set; }

    /// <summary>The named arguments of <see cref="TitleKey"/> and <see cref="MessageKey"/>.</summary>
    public Dictionary<string, string>? Arguments { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? SourceFeature { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request DTO for creating a notification.
/// </summary>
public class CreateNotificationRequestDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>
    /// Localization key of the title (<c>Notifications.SavingsMilestone.Title</c>). When set, the Web app renders the
    /// title from it in the reader's language; <see cref="Title"/> is the English snapshot (localization spec §2.3).
    /// </summary>
    public string? TitleKey { get; set; }

    /// <summary>Localization key of the message, rendered like <see cref="TitleKey"/>.</summary>
    public string? MessageKey { get; set; }

    /// <summary>The named arguments of <see cref="TitleKey"/> and <see cref="MessageKey"/>.</summary>
    public Dictionary<string, string>? Arguments { get; set; }
    public string? SourceFeature { get; set; }
}

/// <summary>
/// Service for managing user notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates a new notification for a user.
    /// </summary>
    Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all notifications for a user, ordered by creation date descending.
    /// </summary>
    Task<List<NotificationDto>> GetNotificationsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of unread notifications for a user.
    /// </summary>
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    Task<bool> MarkAsReadAsync(string userId, int notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all notifications for a user as read.
    /// </summary>
    Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
}
