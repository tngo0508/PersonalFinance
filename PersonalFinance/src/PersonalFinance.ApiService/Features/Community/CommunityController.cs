using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// RESTful API controller for community features: polls, comments, stories, and likes.
/// All endpoints are scoped to the current user identified by the authenticated Account (see XUserIdAuthenticationHandler).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CommunityController : ControllerBase
{
    private readonly ICommunityService _communityService;

    public CommunityController(ICommunityService communityService)
    {
        _communityService = communityService;
    }

    /// <summary>
    /// Gets the full community data (polls, stats, stories, comments) for a user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommunityResponseDto>> GetCommunity(
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        CommunityResponseDto response = await _communityService.GetCommunityAsync(userId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Votes on a poll option.
    /// </summary>
    [HttpPost("vote")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommunityResponseDto>> Vote(
        [FromBody] CommunityVoteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        CommunityVoteResultDto result = await _communityService.VoteAsync(userId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result.Response);
    }

    /// <summary>
    /// Adds a comment to a poll or story.
    /// </summary>
    [HttpPost("comment")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommunityCommentDto>> AddComment(
        [FromBody] CommunityCommentRequestDto request,
        [FromQuery] string authorName,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        try
        {
            CommunityCommentDto comment =
                await _communityService.AddCommentAsync(userId, request, authorName ?? string.Empty, cancellationToken);
            return CreatedAtAction(nameof(GetCommunity), comment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Toggles a like on a comment.
    /// </summary>
    [HttpPost("comment/{commentId}/like")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentLikeResult>> ToggleCommentLike(
        int commentId,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        try
        {
            CommentLikeResult result = await _communityService.ToggleCommentLikeAsync(userId, commentId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Gets comments for a specific poll.
    /// </summary>
    [HttpGet("poll-comments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CommunityCommentDto>>> GetPollComments(
        [FromQuery] string pollCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<CommunityCommentDto> comments = await _communityService.GetPollCommentsAsync(pollCode, cancellationToken);
            return Ok(comments);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Submits a new community story.
    /// </summary>
    [HttpPost("story")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommunityStoryDto>> SubmitStory(
        [FromBody] CommunityStoryRequestDto request,
        [FromQuery] string authorName,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        try
        {
            CommunityStoryDto story = await _communityService.SubmitStoryAsync(userId, request, authorName ?? string.Empty,
                cancellationToken);
            return CreatedAtAction(nameof(GetCommunity), story);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Toggles a like on a story.
    /// </summary>
    [HttpPost("story/{storyId}/like")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<StoryLikeResult>> ToggleStoryLike(
        int storyId,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        try
        {
            StoryLikeResult result = await _communityService.ToggleStoryLikeAsync(userId, storyId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Gets all community stories for the current user.
    /// </summary>
    [HttpGet("Stories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CommunityStoryDto>>> GetStories(
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        CommunityResponseDto response = await _communityService.GetCommunityAsync(userId, cancellationToken);
        return Ok(response.Stories ?? new List<CommunityStoryDto>());
    }

    /// <summary>
    /// Gets a single story by ID.
    /// </summary>
    [HttpGet("story/{storyId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommunityStoryDto>> GetStoryById(
        int storyId,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        CommunityStoryDto? story = await _communityService.GetStoryByIdAsync(userId, storyId, cancellationToken);
        if (story == null)
            return NotFound();
        return Ok(story);
    }
}
