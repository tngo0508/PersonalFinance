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
    /// Votes on a poll option.
    /// </summary>
    Task<CommunityVoteResultDto> VoteAsync(string userId, CommunityVoteRequestDto request, CancellationToken cancellationToken = default);

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

/// <summary>
/// Result of a vote operation.
/// </summary>
public sealed class CommunityVoteResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public CommunityResponseDto? Response { get; set; }
}
