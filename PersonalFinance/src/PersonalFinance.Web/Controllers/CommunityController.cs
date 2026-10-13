using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Community page featuring polls, statistics, and stories.
/// All data operations are delegated to the ApiService via the ICommunityApi Refit client.
/// </summary>
[Authorize]
[IgnoreAntiforgeryToken]
public sealed class CommunityController : Controller
{
    private readonly ICommunityApi _communityApi;
    private readonly ILogger<CommunityController> _logger;

    public CommunityController(
        ICommunityApi communityApi,
        ILogger<CommunityController> logger)
    {
        _communityApi = communityApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Community page with interactive polls, quick stats, and relatable case studies.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        try
        {
            var response = await _communityApi.GetCommunityAsync(cancellationToken);
            var model = MapToViewModel(response);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading community data for user {UserId}", userId);
            var model = new CommunityViewModel();
            model.ErrorMessage = "Unable to load community data at this time.";
            return View(model);
        }
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

        try
        {
            var voteRequest = new CommunityVoteRequestDto
            {
                PollCode = request.PollCode,
                OptionId = request.OptionId
            };

            var response = await _communityApi.VoteAsync(voteRequest, cancellationToken);
            var updatedPoll = response.ActivePolls.FirstOrDefault(p => p.Id == request.PollCode);

            return Json(new
            {
                success = true,
                message = "Thanks for voting!",
                poll = updatedPoll != null ? MapPollToViewModel(updatedPoll) : null,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voting on poll {PollCode} for user {UserId}", request.PollCode, userId);
            return StatusCode(502, new { success = false, error = "Failed to record vote." });
        }
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
        var topic = request.Topic ?? "General";

        var commentRequest = new CommunityCommentRequestDto
        {
            Topic = topic,
            Text = request.Text!,
            PollCode = request.PollCode,
            StoryId = request.StoryId
        };

        try
        {
            var comment = await _communityApi.AddCommentAsync(commentRequest, authorName, cancellationToken);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment for user {UserId}", userId);
            return StatusCode(502, new { success = false, error = "Failed to add comment." });
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

        try
        {
            var result = await _communityApi.ToggleCommentLikeAsync(request.CommentId, cancellationToken);

            return Json(new
            {
                success = result.Success,
                likes = result.NewLikeCount,
                isLiked = result.IsLiked,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling like on comment {CommentId} for user {UserId}", request.CommentId, userId);
            return StatusCode(502, new { success = false, error = "Failed to toggle like." });
        }
    }

    /// <summary>
    /// Gets comments for a specific poll via AJAX.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> GetPollComments([FromQuery] string pollCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var comments = await _communityApi.GetPollCommentsAsync(pollCode, cancellationToken);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting poll comments for poll {PollCode}", pollCode);
            return StatusCode(502, new { success = false, error = "Failed to load comments." });
        }
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

        var storyRequest = new CommunityStoryRequestDto
        {
            Title = request.Title!,
            Summary = request.Summary!,
            Body = request.Body!,
            Category = request.Category!
        };

        try
        {
            var story = await _communityApi.SubmitStoryAsync(storyRequest, authorName, cancellationToken);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting story for user {UserId}", userId);
            return StatusCode(502, new { success = false, error = "Failed to submit story." });
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

        try
        {
            var result = await _communityApi.ToggleStoryLikeAsync(request.StoryId, cancellationToken);

            return Json(new
            {
                success = result.Success,
                likes = result.NewLikeCount,
                isLiked = result.IsLiked,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling story like for story {StoryId} by user {UserId}", request.StoryId, userId);
            return StatusCode(502, new { success = false, error = "Failed to toggle like." });
        }
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    // =========================================================================
    // Mapping helpers: API DTOs → View Models
    // =========================================================================

    private static CommunityViewModel MapToViewModel(CommunityResponseDto response)
    {
        return new CommunityViewModel
        {
            ActivePolls = response.ActivePolls.Select(MapPollToViewModel).ToList(),
            QuickStats = response.QuickStats.Select(s => new CommunityStat
            {
                Title = s.Title,
                Value = s.Value,
                Description = s.Description,
                Icon = s.Icon,
                ThemeColor = s.ThemeColor,
            }).ToList(),
            Stories = response.Stories.Select(s => new CommunityStory
            {
                Id = s.Id,
                Title = s.Title,
                Summary = s.Summary,
                Body = s.Body,
                Author = s.AuthorName,
                PublishedDate = s.PublishedAtUtc,
                CommentCount = s.CommentCount,
                Category = s.Category,
                ReadTimeMinutes = s.ReadTimeMinutes,
                Likes = s.Likes,
                IsFeatured = s.IsFeatured,
                IsLikedByUser = s.IsLikedByUser,
            }).ToList(),
            RecentComments = response.RecentComments.Select(c => new CommunityComment
            {
                Id = c.Id,
                Author = c.AuthorName,
                Text = c.Text,
                CreatedAt = c.CreatedAtUtc,
                Topic = c.Topic,
                Likes = c.Likes,
                IsLikedByUser = c.IsLikedByUser,
                PollId = c.PollId,
                StoryId = c.StoryId,
            }).ToList(),
        };
    }

    private static CommunityPoll MapPollToViewModel(CommunityPollDto dto)
    {
        return new CommunityPoll
        {
            Id = dto.Id,
            Question = dto.Question,
            Description = dto.Description,
            Category = dto.Category,
            TotalVotes = dto.TotalVotes,
            IsActive = dto.IsActive,
            HasVoted = dto.HasVoted,
            VotedOptionId = dto.VotedOptionId,
            Options = dto.Options.Select(o => new PollOption
            {
                Id = o.Id,
                Text = o.Text,
                Votes = o.Votes,
                Percentage = o.Percentage,
            }).ToList(),
        };
    }

    // =========================================================================
    // Request models (local to this controller)
    // =========================================================================

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
