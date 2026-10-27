using PersonalFinance.Shared.DTOs.Community;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Guild Hall (the Community Feature): the Pseudonym, polls, comments, stories and likes.
/// Authors are identified by the authenticated Account, never by a name the Web app passes.
/// </summary>
public interface ICommunityApi
{
    /// <summary>
    /// Gets the full Guild Hall data (the viewer's Pseudonym, polls, stories, comments).
    /// </summary>
    [Get("/api/community")]
    Task<CommunityResponseDto> GetCommunityAsync(CancellationToken cancellationToken = default);

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
    /// Adds a comment to a poll or story.
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
    /// Submits a new community story.
    /// </summary>
    [Post("/api/community/story")]
    Task<CommunityStoryDto> SubmitStoryAsync([Body] CommunityStoryRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a like on a story.
    /// </summary>
    [Post("/api/community/story/{storyId}/like")]
    Task<StoryLikeResult> ToggleStoryLikeAsync(int storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single story by ID.
    /// </summary>
    [Get("/api/community/story/{storyId}")]
    Task<CommunityStoryDto?> GetStoryByIdAsync(int storyId, CancellationToken cancellationToken = default);
}
