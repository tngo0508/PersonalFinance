using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for notification operations.
/// </summary>
public interface INotificationApi
{
    /// <summary>
    /// Gets all notifications for the current user.
    /// </summary>
    [Get("/api/Notification")]
    Task<List<NotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of unread notifications for the current user.
    /// </summary>
    [Get("/api/Notification/unread-count")]
    Task<UnreadCountResponseDto> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new notification for the current user.
    /// </summary>
    [Post("/api/Notification")]
    Task<CreateNotificationResponseDto> CreateNotificationAsync([Body] CreateNotificationRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    [Post("/api/Notification/{id}/read")]
    Task<MarkReadResponseDto> MarkAsReadAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    [Post("/api/Notification/mark-all-read")]
    Task<MarkAllReadResponseDto> MarkAllAsReadAsync(CancellationToken cancellationToken = default);
}

// =========================================================================
// DTOs
// =========================================================================

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

public class CreateNotificationRequestDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SourceFeature { get; set; }
}

public class UnreadCountResponseDto
{
    public int Count { get; set; }
}

public class CreateNotificationResponseDto
{
    public int Id { get; set; }
}

public class MarkReadResponseDto
{
    public bool Success { get; set; }
}

public class MarkAllReadResponseDto
{
    public bool Success { get; set; }
}