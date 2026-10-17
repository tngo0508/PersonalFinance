namespace PersonalFinance.ApiService.Notification.Services;

/// <summary>
/// DTO for notification data transferred between layers.
/// </summary>
public class NotificationDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
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