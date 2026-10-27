using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Service for the Guild Hall (the Community Feature): the Pseudonym, polls, comments, stories and likes. Every
/// author is the Account that posted, shown by its Pseudonym; no method takes or returns a display name (ADR 0018).
/// </summary>
public interface ICommunityService
{
    /// <summary>
    /// Gets the full Guild Hall data (the viewer's Pseudonym, polls, stories, comments) for an Account, creating its
    /// Pseudonym on the first visit.
    /// </summary>
    Task<CommunityResponseDto> GetCommunityAsync(string userId, CancellationToken cancellationToken = default);

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
    /// Adds a comment to a poll or story under the Account's Pseudonym.
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
    /// Submits a new community story under the Account's Pseudonym.
    /// </summary>
    Task<CommunityStoryDto> SubmitStoryAsync(string userId, CommunityStoryRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a like on a story.
    /// </summary>
    Task<StoryLikeResult> ToggleStoryLikeAsync(string userId, int storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single story by ID.
    /// </summary>
    Task<CommunityStoryDto?> GetStoryByIdAsync(string userId, int storyId, CancellationToken cancellationToken = default);
}
