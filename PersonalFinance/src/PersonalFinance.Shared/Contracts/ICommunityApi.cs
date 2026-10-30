using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Guild Hall (the Community Feature): the Pseudonym, polls, Comments, Strategies and Reactions.
/// Authors are identified by the authenticated Account, never by a name the Web app passes.
/// </summary>
public interface ICommunityApi
{
    /// <summary>
    /// Gets the full Guild Hall data (the viewer's Pseudonym, polls, the board's Strategies, each with its Comments),
    /// with the board limited to <paramref name="category"/> when one is given.
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
    /// Adds a Comment to a poll or an Approved Strategy. It is shown at once.
    /// </summary>
    [Post("/api/community/comment")]
    Task<CommunityCommentDto> AddCommentAsync([Body] CommunityCommentRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives or takes back a Reaction on an Approved Strategy or a shown Comment.
    /// </summary>
    [Post("/api/community/reactions")]
    Task<CommunityReactionResultDto> ToggleReactionAsync([Body] CommunityReactionRequestDto request,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Lists the newest Comments for the Admin moderation list, hidden ones included. Requires the Admin Policy.
    /// </summary>
    [Get("/api/community/comments/review")]
    Task<List<CommunityCommentReviewItemDto>> ListCommentsForReviewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Hides a Comment or shows it again. Requires the Admin Policy; an unknown Comment is 404.
    /// </summary>
    [Put("/api/community/comments/{commentId}/visibility")]
    Task SetCommentVisibilityAsync(int commentId, [Body] CommunityCommentVisibilityRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every Poll for the Admin, open and closed, newest first. Requires the Admin Policy.
    /// </summary>
    [Get("/api/community/polls")]
    Task<List<CommunityPollAdminItemDto>> ListPollsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a new Poll, stored closed. Requires the Admin Policy; an invalid Poll is 400.
    /// </summary>
    [Post("/api/community/polls")]
    Task<CommunityPollAdminItemDto> CreatePollAsync([Body] CommunityPollRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a Poll for voting or closes it. Requires the Admin Policy; an unknown Poll is 404.
    /// </summary>
    [Put("/api/community/polls/{pollId}/status")]
    Task SetPollStatusAsync(int pollId, [Body] CommunityPollStatusRequestDto request,
        CancellationToken cancellationToken = default);
}
