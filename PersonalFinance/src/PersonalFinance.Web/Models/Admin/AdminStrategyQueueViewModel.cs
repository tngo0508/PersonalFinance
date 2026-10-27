using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Admin;

/// <summary>
/// View model for the Admin Strategy review queue.
/// </summary>
public class AdminStrategyQueueViewModel
{
    /// <summary>
    /// The Strategies in <see cref="Status"/>, oldest first. Empty means there are none (a failed load shows the
    /// Unavailable State).
    /// </summary>
    public List<CommunityStrategyReviewItemDto> Items { get; set; } = new();

    /// <summary>
    /// The status the queue is filtered to.
    /// </summary>
    public CommunityStrategyStatus Status { get; set; } = CommunityStrategyStatus.Pending;
}
