using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Service for the Guild Hall (the Community Feature): the Pseudonym, polls, comments, Strategies and likes. Every
/// author is the Account that posted, shown by its Pseudonym; no method takes or returns a display name (ADR 0018).
/// </summary>
public interface ICommunityService
{
    /// <summary>
    /// Gets the full Guild Hall data (the viewer's Pseudonym, polls, the board's Strategies, comments) for an Account,
    /// creating its Pseudonym on the first visit. The board holds Approved Strategies and the Account's own in any
    /// status, only in <paramref name="category"/> when one is given; another Account's Pending or Rejected Strategy
    /// is never in it.
    /// </summary>
    Task<CommunityResponseDto> GetCommunityAsync(string userId, string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the Account's Pseudonym with a new one. Its past posts show the new one, since they reference the
    /// Account, not the name.
    /// </summary>
    Task<CommunityPseudonymDto> RerollPseudonymAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Votes on a poll option and returns the updated active polls.
    /// </summary>
    /// <exception cref="PersonalFinance.ApiService.Infrastructure.ApiErrorException">
    /// The request names no poll or option, the poll is unknown or inactive, or the option is not the poll's.
    /// </exception>
    Task<CommunityResponseDto> VoteAsync(string userId, CommunityVoteRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a comment to a poll or an Approved Strategy under the Account's Pseudonym.
    /// </summary>
    Task<CommunityCommentDto> AddCommentAsync(string userId, CommunityCommentRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a like on a comment.
    /// </summary>
    Task<CommentLikeResult> ToggleCommentLikeAsync(string userId, int commentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comments for a specific poll, as seen by the Account.
    /// </summary>
    Task<List<CommunityCommentDto>> GetPollCommentsAsync(string userId, string pollCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins a Strategy under the Account's Pseudonym, Pending until an Admin reviews it. The request is validated by
    /// the controller first; its texts are stored trimmed.
    /// </summary>
    Task<CommunityStrategyDto> SubmitStrategyAsync(string userId, CommunityStrategyRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The Admin review queue: the Strategies in <paramref name="status"/>, oldest first, each flagged when it names
    /// a money amount.
    /// </summary>
    Task<List<CommunityStrategyReviewItemDto>> ListStrategiesForReviewAsync(CommunityStrategyStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an Admin's decision on a Strategy (Approved or Rejected; the controller validates it). False when no
    /// such Strategy exists.
    /// </summary>
    Task<bool> ReviewStrategyAsync(int strategyId, CommunityStrategyStatus status,
        CancellationToken cancellationToken = default);
}
