using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.Web.Models.Admin;

/// <summary>
/// View model for the Admin Comment moderation list.
/// </summary>
public class AdminCommentListViewModel
{
    /// <summary>
    /// The newest Comments, hidden ones included. Empty means there are none (a failed load shows the Unavailable
    /// State).
    /// </summary>
    public List<CommunityCommentReviewItemDto> Items { get; set; } = new();
}
