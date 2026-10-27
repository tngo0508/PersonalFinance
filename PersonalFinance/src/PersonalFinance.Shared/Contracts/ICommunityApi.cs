using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Guild Hall (the Community Feature): the Pseudonym, polls, comments, Strategies and likes.
/// Authors are identified by the authenticated Account, never by a name the Web app passes.
/// </summary>
public interface ICommunityApi
{
    /// <summary>
    /// Gets the full Guild Hall data (the viewer's Pseudonym, polls, the board's Strategies, comments), with the board
    /// limited to <paramref name="category"/> when one is given.
    /// </summary>
    [Get("/api/community")]
    Task<CommunityResponseDto> GetCommunityAsync([Query] string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the viewer's Pseudonym with a new one; every past post shows it.
    /// </summary>
    [Post("/api/community/pseudonym/reroll")]
    Task<CommunityPseudonymDto> RerollPseudonymAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Votes on a poll option.
    /// </summary>
    [Post("/api/community/vote")]
    Task<CommunityResponseDto> VoteAsync([Body] CommunityVoteRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a comment to a poll or an Approved Strategy.
    /// </summary>
    [Post("/api/community/comment")]
    Task<CommunityCommentDto> AddCommentAsync([Body] CommunityCommentRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a like on a comment.
    /// </summary>
    [Post("/api/community/comment/{commentId}/like")]
    Task<CommentLikeResult> ToggleCommentLikeAsync(int commentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comments for a specific poll.
    /// </summary>
    [Get("/api/community/poll-comments")]
    Task<List<CommunityCommentDto>> GetPollCommentsAsync([Query] string pollCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins a Strategy, Pending until an Admin reviews it.
    /// </summary>
    [Post("/api/community/strategies")]
    Task<CommunityStrategyDto> SubmitStrategyAsync([Body] CommunityStrategyRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the Strategies in <paramref name="status"/> for the Admin queue, oldest first. Requires the Admin Policy.
    /// </summary>
    [Get("/api/community/strategies/review")]
    Task<List<CommunityStrategyReviewItemDto>> ListStrategiesForReviewAsync(
        [Query] CommunityStrategyStatus status = CommunityStrategyStatus.Pending,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves or rejects a Strategy. Requires the Admin Policy; an unknown Strategy is 404.
    /// </summary>
    [Put("/api/community/strategies/{strategyId}/status")]
    Task ReviewStrategyAsync(int strategyId, [Body] CommunityStrategyReviewRequestDto request,
        CancellationToken cancellationToken = default);
}
