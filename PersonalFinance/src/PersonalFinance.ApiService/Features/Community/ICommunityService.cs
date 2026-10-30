using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Service for the Guild Hall (the Community Feature): the Pseudonym, polls, Comments, Strategies and Reactions. Every
/// author is the Account that posted, shown by its Pseudonym; no method takes or returns a display name (ADR 0018).
/// </summary>
public interface ICommunityService
{
    /// <summary>
    /// Gets the full Guild Hall data (the viewer's Pseudonym, polls, the board's Strategies, each Approved Strategy
    /// and poll with its shown Comments) for an Account, creating its Pseudonym on the first visit. The board holds
    /// Approved Strategies and the Account's own in any status, only in <paramref name="category"/> when one is given;
    /// another Account's Pending or Rejected Strategy is never in it.
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
    /// Adds a Comment to a poll or an Approved Strategy under the Account's Pseudonym. It is shown at once; an Admin
    /// can hide it afterwards.
    /// </summary>
    Task<CommunityCommentDto> AddCommentAsync(string userId, CommunityCommentRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the Account's Reaction of the request's kind to an Approved Strategy or a shown Comment, or takes it back
    /// when the Account already holds it. The request is validated by the controller first (one target, a known kind).
    /// The result carries the kind's new count only for a Strategy.
    /// </summary>
    /// <exception cref="PersonalFinance.ApiService.Infrastructure.ApiErrorException">
    /// 404: no Approved Strategy, or no shown Comment, matches the id.
    /// </exception>
    Task<CommunityReactionResultDto> ToggleReactionAsync(string userId, CommunityReactionRequestDto request,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// The Admin moderation list: the newest Comments, hidden ones included, each flagged when it names a money amount.
    /// </summary>
    Task<List<CommunityCommentReviewItemDto>> ListCommentsForReviewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Hides a Comment from everyone, or shows it again. False when no such Comment exists.
    /// </summary>
    Task<bool> SetCommentHiddenAsync(int commentId, bool isHidden, CancellationToken cancellationToken = default);
}
