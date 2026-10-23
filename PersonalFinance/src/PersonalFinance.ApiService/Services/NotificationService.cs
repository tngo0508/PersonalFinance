using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service for managing user notifications including CRUD operations and read status tracking.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly AppDbContext _dbContext;

    public NotificationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        NotificationType notificationType = request.Type switch
        {
            "BudgetAlert" => NotificationType.BudgetAlert,
            "SavingsMilestone" => NotificationType.SavingsMilestone,
            "SyncStatus" => NotificationType.SyncStatus,
            _ => NotificationType.BudgetAlert
        };

        var notification = new Data.Entities.Notification
        {
            UserId = userId,
            Type = notificationType,
            Title = request.Title,
            Message = request.Message,
            SourceFeature = request.SourceFeature,
            IsRead = false
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return notification.Id;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        List<NotificationDto> notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type.ToString(),
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                SourceFeature = n.SourceFeature,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return notifications;
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(string userId, int notificationId,
        CancellationToken cancellationToken = default)
    {
        Notification? notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification is null)
        {
            return false;
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        List<Notification> notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (Notification? notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
