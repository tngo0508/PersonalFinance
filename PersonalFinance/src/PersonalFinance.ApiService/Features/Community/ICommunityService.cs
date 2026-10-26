using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Service for managing community features: polls, comments, stories, and likes.
/// </summary>
public interface ICommunityService
{
    /// <summary>
    /// Gets the full community data (polls, stats, stories, comments) for a user.
    /// </summary>
    Task<CommunityResponseDto> GetCommunityAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Votes on a poll option and returns the updated active polls.
    /// </summary>
    /// <exception cref="PersonalFinance.ApiService.Infrastructure.ApiErrorException">
    /// The request names no poll or option, the poll is unknown or inactive, or the option is not the poll's.
    /// </exception>
    Task<CommunityResponseDto> VoteAsync(string userId, CommunityVoteRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a comment to a poll or story.
    /// </summary>
    Task<CommunityCommentDto> AddCommentAsync(string userId, CommunityCommentRequestDto request, string authorName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a like on a comment.
    /// </summary>
    Task<CommentLikeResult> ToggleCommentLikeAsync(string userId, int commentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comments for a specific poll.
    /// </summary>
    Task<List<CommunityCommentDto>> GetPollCommentsAsync(string pollCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a new community story.
    /// </summary>
    Task<CommunityStoryDto> SubmitStoryAsync(string userId, CommunityStoryRequestDto request, string authorName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a like on a story.
    /// </summary>
    Task<StoryLikeResult> ToggleStoryLikeAsync(string userId, int storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single story by ID.
    /// </summary>
    Task<CommunityStoryDto?> GetStoryByIdAsync(string userId, int storyId, CancellationToken cancellationToken = default);
}
