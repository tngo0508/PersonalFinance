using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Web.Models;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Community page featuring polls, statistics, and case studies.
/// All data operations are delegated to <see cref="ICommunityService"/>.
/// </summary>
[Authorize]
public sealed class CommunityController : Controller
{
    private readonly ICommunityService _communityService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CommunityController> _logger;

    public CommunityController(
        ICommunityService communityService,
        AppDbContext dbContext,
        ILogger<CommunityController> logger)
    {
        _communityService = communityService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Community page with interactive polls, quick stats, and relatable case studies.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var model = await _communityService.GetCommunityViewModelAsync(userId, cancellationToken);

        return View(model);
    }

    /// <summary>
    /// Handles a poll vote submission via AJAX.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> Vote([FromBody] VoteRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { success = false, error = "You must be logged in to vote." });
        }

        if (string.IsNullOrEmpty(request.PollCode))
        {
            return BadRequest(new { success = false, error = "Poll ID is required." });
        }

        if (request.OptionId <= 0)
        {
            return BadRequest(new { success = false, error = "Option ID is required." });
        }

        // Look up the poll by its code
        var poll = await _dbContext.CommunityPolls
            .FirstOrDefaultAsync(p => p.PollCode == request.PollCode, cancellationToken);

        if (poll == null)
        {
            return NotFound(new { success = false, error = "Poll not found." });
        }

        var success = await _communityService.VoteAsync(userId, poll.Id, request.OptionId, cancellationToken);

        if (!success)
        {
            return BadRequest(new { success = false, error = "Failed to record vote." });
        }

        // Return updated poll data for the client
        var updatedModel = await _communityService.GetCommunityViewModelAsync(userId, cancellationToken);
        var updatedPoll = updatedModel.ActivePolls.FirstOrDefault(p => p.Id == request.PollCode);

        return Json(new
        {
            success = true,
            message = "Thanks for voting!",
            poll = updatedPoll,
        });
    }

    /// <summary>
    /// Handles a new comment submission via AJAX.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> AddComment([FromBody] CommentRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { success = false, error = "You must be logged in to comment." });
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { success = false, error = "Comment text is required." });
        }

        if (request.Text!.Length > 2000)
        {
            return BadRequest(new { success = false, error = "Comment is too long (max 2000 characters)." });
        }

        var authorName = User.Identity?.Name ?? "Anonymous";

        int? pollId = null;
        int? storyId = null;

        if (!string.IsNullOrEmpty(request.PollCode))
        {
            var poll = await _dbContext.CommunityPolls
                .FirstOrDefaultAsync(p => p.PollCode == request.PollCode, cancellationToken);
            pollId = poll?.Id;
        }

        if (request.StoryId.HasValue && request.StoryId.Value > 0)
        {
            storyId = request.StoryId.Value;
        }

        var topic = request.Topic ?? (pollId.HasValue ? "Poll" : "Story");

        try
        {
            var comment = await _communityService.AddCommentAsync(
                userId, authorName, topic, request.Text!, pollId, storyId, cancellationToken);

            return Json(new
            {
                success = true,
                comment = new
                {
                    id = comment.Id,
                    author = comment.AuthorName,
                    text = comment.Text,
                    topic = comment.Topic,
                    createdAt = comment.CreatedAtUtc,
                    likes = comment.Likes,
                    pollId = comment.PollId,
                    storyId = comment.StoryId,
                }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Handles toggling a like on a comment via AJAX.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> ToggleLike([FromBody] LikeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { success = false, error = "You must be logged in to like comments." });
        }

        if (request.CommentId <= 0)
        {
            return BadRequest(new { success = false, error = "Comment ID is required." });
        }

        var result = await _communityService.ToggleLikeAsync(userId, request.CommentId, cancellationToken);

        return Json(new
        {
            success = result.Success,
            likes = result.NewLikeCount,
            isLiked = result.IsLiked,
        });
    }

    /// <summary>
    /// Gets comments for a specific poll via AJAX.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> GetPollComments([FromQuery] string pollCode, CancellationToken cancellationToken = default)
    {
        var poll = await _dbContext.CommunityPolls
            .FirstOrDefaultAsync(p => p.PollCode == pollCode, cancellationToken);

        if (poll == null)
        {
            return NotFound(new { success = false, error = "Poll not found." });
        }

        var comments = await _communityService.GetPollCommentsAsync(poll.Id, cancellationToken);

        return Json(new
        {
            success = true,
            comments = comments.Select(c => new
            {
                id = c.Id,
                author = c.AuthorName,
                text = c.Text,
                topic = c.Topic,
                createdAt = c.CreatedAtUtc,
                likes = c.Likes,
            }),
        });
    }

    /// <summary>
    /// Submits a new community story via AJAX.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> SubmitStory([FromBody] StoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { success = false, error = "You must be logged in to submit a story." });
        }

        var authorName = User.Identity?.Name ?? "Anonymous";

        try
        {
            var story = await _communityService.SubmitStoryAsync(
                userId, authorName, request.Title!, request.Summary!, request.Body!, request.Category!, cancellationToken);

            return Json(new
            {
                success = true,
                message = "Your story has been submitted successfully!",
                story = new
                {
                    id = story.Id,
                    title = story.Title,
                    category = story.Category,
                },
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Toggles a like on a story via AJAX.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> ToggleStoryLike([FromBody] StoryLikeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new { success = false, error = "You must be logged in to like stories." });
        }

        if (request.StoryId <= 0)
        {
            return BadRequest(new { success = false, error = "Story ID is required." });
        }

        var result = await _communityService.ToggleStoryLikeAsync(userId, request.StoryId, cancellationToken);

        return Json(new
        {
            success = result.Success,
            likes = result.NewLikeCount,
            isLiked = result.IsLiked,
        });
    }

    private string? GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return userIdClaim;
    }

    /// <summary>
    /// Request model for poll voting.
    /// </summary>
    public sealed class VoteRequest
    {
        public string? PollCode { get; set; }
        public int OptionId { get; set; }
    }

    /// <summary>
    /// Request model for adding comments.
    /// </summary>
    public sealed class CommentRequest
    {
        public string? Topic { get; set; }
        public string? Text { get; set; }
        public string? PollCode { get; set; }
        public int? StoryId { get; set; }
    }

    /// <summary>
    /// Request model for toggling comment likes.
    /// </summary>
    public sealed class LikeRequest
    {
        public int CommentId { get; set; }
    }

    /// <summary>
    /// Request model for submitting a story.
    /// </summary>
    public sealed class StoryRequest
    {
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string? Body { get; set; }
        public string? Category { get; set; }
    }

    /// <summary>
    /// Request model for toggling story likes.
    /// </summary>
    public sealed class StoryLikeRequest
    {
        public int StoryId { get; set; }
    }
}
