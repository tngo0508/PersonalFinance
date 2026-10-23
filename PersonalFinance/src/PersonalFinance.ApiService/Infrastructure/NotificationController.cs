using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// API controller for notification endpoints.
/// All endpoints are scoped to the current user identified by the authenticated Account (see XUserIdAuthenticationHandler).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// Gets all notifications for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        List<NotificationDto> notifications = await _notificationService.GetNotificationsAsync(userId, cancellationToken);
        return Ok(notifications);
    }

    /// <summary>
    /// Gets the count of unread notifications for the current user.
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        int count = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new { count });
    }

    /// <summary>
    /// Creates a new notification for the current user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        int id = await _notificationService.CreateNotificationAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetNotifications), new { id });
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    [HttpPost("{id}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        bool success = await _notificationService.MarkAsReadAsync(userId, id, cancellationToken);

        if (!success)
        {
            return NotFound();
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    [HttpPost("mark-all-read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(new { success = true });
    }
}
