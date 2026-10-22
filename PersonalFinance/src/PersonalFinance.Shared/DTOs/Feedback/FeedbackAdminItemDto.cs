using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.Feedback;

/// <summary>
/// One piece of Feedback as an Admin sees it in the Feedback inbox.
/// </summary>
public class FeedbackAdminItemDto
{
    public int Id { get; set; }

    public FeedbackCategory Category { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Email of the Account that sent the Feedback; empty when the Account no longer exists.
    /// </summary>
    public string AccountEmail { get; set; } = string.Empty;

    /// <summary>
    /// The app-relative path of the page the Feedback was sent from, if known.
    /// </summary>
    public string? PageUrl { get; set; }

    public FeedbackStatus Status { get; set; }

    /// <summary>
    /// When the Feedback was sent (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
