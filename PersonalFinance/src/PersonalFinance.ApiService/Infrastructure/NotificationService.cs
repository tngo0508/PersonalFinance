using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Service for managing user notifications including CRUD operations and read status tracking.
/// </summary>
public class NotificationService : INotificationService
{
    /// <summary>The column size of <see cref="Notification.Arguments"/>.</summary>
    private const int MaxArgumentsLength = 2000;

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
            TitleKey = request.TitleKey,
            MessageKey = request.MessageKey,
            Arguments = SerializeArguments(request.Arguments),
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
        List<Notification> notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        return notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            Type = n.Type.ToString(),
            Title = n.Title,
            Message = n.Message,
            TitleKey = n.TitleKey,
            MessageKey = n.MessageKey,
            Arguments = DeserializeArguments(n.Arguments),
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            SourceFeature = n.SourceFeature,
            CreatedAt = n.CreatedAt
        }).ToList();
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

    /// <summary>The arguments as the JSON object stored in <see cref="Notification.Arguments"/>, or null when none.</summary>
    private static string? SerializeArguments(Dictionary<string, string>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return null;
        }

        string json = JsonSerializer.Serialize(arguments);
        if (json.Length > MaxArgumentsLength)
        {
            throw new ArgumentException($"Notification arguments exceed {MaxArgumentsLength} characters.", nameof(arguments));
        }

        return json;
    }

    /// <summary>Reads stored arguments back; malformed JSON reads as none, so the English snapshot is shown.</summary>
    private static Dictionary<string, string>? DeserializeArguments(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
