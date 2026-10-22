using PersonalFinance.Shared.DTOs.Feedback;

namespace PersonalFinance.Web.Models;

/// <summary>
/// View model for the Admin Feedback inbox page.
/// </summary>
public class AdminFeedbackInboxViewModel
{
    /// <summary>
    /// All Feedback, newest first. Empty means there is no Feedback (a failed load shows the Unavailable State).
    /// </summary>
    public List<FeedbackAdminItemDto> Items { get; set; } = new();
}
