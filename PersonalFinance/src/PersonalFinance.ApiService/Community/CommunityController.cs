using Microsoft.AspNetCore.Mvc;
using MediatR;
using PersonalFinance.ApiService.Community;
using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.ApiService.Community;

/// <summary>
/// RESTful API controller for community features: polls, comments, stories, and likes.
/// All endpoints are scoped to the current user identified via the X-User-Id header forwarded by the Web proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CommunityController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommunityController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the current user's ID from the X-User-Id header forwarded by the Web proxy.
    /// </summary>
    private string GetUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userId))
        {
            var value = userId.First();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return "anonymous";
    }

    /// <summary>
    /// Gets the full community data (polls, stats, stories, comments) for a user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommunityResponseDto>> GetCommunity(
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var response = await _mediator.Send(new GetCommunityQuery { UserId = userId }, cancellationToken);
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
        var userId = GetUserId();
        var result = await _mediator.Send(new VoteOnPollCommand { UserId = userId, Request = request }, cancellationToken);
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
        var userId = GetUserId();
        try
        {
            var comment = await _mediator.Send(new AddCommentCommand { UserId = userId, Request = request, AuthorName = authorName ?? string.Empty }, cancellationToken);
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
        var userId = GetUserId();
        try
        {
            var result = await _mediator.Send(new ToggleCommentLikeCommand { UserId = userId, CommentId = commentId }, cancellationToken);
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
            var comments = await _mediator.Send(new GetPollCommentsQuery { PollCode = pollCode }, cancellationToken);
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
        var userId = GetUserId();
        try
        {
            var story = await _mediator.Send(new SubmitStoryCommand { UserId = userId, Request = request, AuthorName = authorName ?? string.Empty }, cancellationToken);
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
        var userId = GetUserId();
        try
        {
            var result = await _mediator.Send(new ToggleStoryLikeCommand { UserId = userId, StoryId = storyId }, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
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
        var userId = GetUserId();
        var story = await _mediator.Send(new GetStoryByIdQuery { UserId = userId, StoryId = storyId }, cancellationToken);
        if (story == null)
            return NotFound();
        return Ok(story);
    }
}
