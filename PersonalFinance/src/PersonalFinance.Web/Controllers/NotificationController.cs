using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// JSON endpoints behind the notification bell in the app shell (<c>js/core/notifications.js</c>), forwarded to
/// ApiService through <see cref="INotificationApi"/>. Notifications are rendered in the reader's language here, at
/// read time, from the key and arguments they were stored with. Writes require the antiforgery token.
/// </summary>
[Authorize]
[JsonApiErrors]
[Route("api/[controller]")]
[Produces("application/json")]
public class NotificationController : Controller
{
    private readonly INotificationApi _notificationApi;
    private readonly IStringLocalizer _localizer;

    public NotificationController(INotificationApi notificationApi, IStringLocalizer localizer)
    {
        _notificationApi = notificationApi;
        _localizer = localizer;
    }

    /// <summary>
    /// Gets the signed-in Account's latest notifications.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        List<NotificationDto> notifications = await _notificationApi.GetNotificationsAsync(cancellationToken);
        foreach (NotificationDto notification in notifications)
        {
            NotificationText.Localize(_localizer, notification);
        }

        return Ok(notifications);
    }

    /// <summary>
    /// Gets the number of unread notifications for the badge.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken = default)
    {
        return Ok(await _notificationApi.GetUnreadCountAsync(cancellationToken));
    }

    /// <summary>
    /// Marks one notification as read.
    /// </summary>
    [HttpPost("{id:int}/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken = default)
    {
        return Ok(await _notificationApi.MarkAsReadAsync(id, cancellationToken));
    }

    /// <summary>
    /// Marks every notification as read.
    /// </summary>
    [HttpPost("mark-all-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        return Ok(await _notificationApi.MarkAllAsReadAsync(cancellationToken));
    }
}
