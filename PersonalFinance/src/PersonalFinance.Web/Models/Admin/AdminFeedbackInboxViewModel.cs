using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Admin;

/// <summary>
/// View model for the Admin Feedback inbox page.
/// </summary>
public class AdminFeedbackInboxViewModel
{
    /// <summary>
    /// The Feedback matching <see cref="Status"/>, newest first. Empty means there is no such Feedback (a failed
    /// load shows the Unavailable State).
    /// </summary>
    public List<FeedbackAdminItemDto> Items { get; set; } = new();

    /// <summary>
    /// The Feedback Status the inbox is filtered to; null shows New and Reviewed Feedback (Archived hidden).
    /// </summary>
    public FeedbackStatus? Status { get; set; }
}
