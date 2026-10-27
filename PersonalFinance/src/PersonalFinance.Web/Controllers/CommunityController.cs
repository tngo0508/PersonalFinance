using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Models.Community;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Guild Hall page (the Community Feature): the viewer's Pseudonym, polls, the board of
/// Strategies and comments. All data operations are delegated to the ApiService via the ICommunityApi Refit client, which knows the
/// Account from the forwarded user id; no name ever leaves this app.
/// </summary>
[Authorize]
[JsonApiErrors]
[IgnoreAntiforgeryToken]
public sealed class CommunityController : Controller
{
    private readonly ICommunityApi _communityApi;
    private readonly ILogger<CommunityController> _logger;
    private readonly IStringLocalizer _localizer;

    public CommunityController(
        ICommunityApi communityApi,
        ILogger<CommunityController> logger,
        IStringLocalizer localizer)
    {
        _communityApi = communityApi;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Displays the Guild Hall with the viewer's Pseudonym, interactive polls, the board of Strategies (only
    /// <paramref name="category"/>'s when it is one of the board's categories) and recent comments.
    /// When the ApiService is unavailable, shows the Unavailable State instead of an empty page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? category = null, CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        string? filter = CommunityText.StrategyCategories.Contains(category, StringComparer.Ordinal) ? category : null;
        try
        {
            CommunityResponseDto response = await _communityApi.GetCommunityAsync(filter, cancellationToken);
            CommunityViewModel model = MapToViewModel(response);
            model.Category = filter;
            return View(model);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error loading community data for user {UserId}", userId);
            return this.UnavailableView(_localizer["Community.Title"], _localizer["Community.Message.Unavailable"], "Community");
        }
    }

    /// <summary>
    /// Replaces the viewer's Pseudonym with a new one via AJAX.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> RerollPseudonym(CancellationToken cancellationToken = default)
    {
        CommunityPseudonymDto pseudonym = await _communityApi.RerollPseudonymAsync(cancellationToken);

        return Json(new
        {
            success = true,
            pseudonym = pseudonym.Name,
        });
    }

    /// <summary>
    /// Handles a poll vote submission via AJAX.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> Vote([FromBody] VoteRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.PollCode))
        {
            return BadRequest(new { success = false, error = _localizer["Community.Message.PollRequired"].Value });
        }

        if (request.OptionId <= 0)
        {
            return BadRequest(new { success = false, error = _localizer["Community.Message.OptionRequired"].Value });
        }

        var voteRequest = new CommunityVoteRequestDto
        {
            PollCode = request.PollCode,
            OptionId = request.OptionId
        };

        CommunityResponseDto response = await _communityApi.VoteAsync(voteRequest, cancellationToken);
        CommunityPollDto? updatedPoll = response.ActivePolls.FirstOrDefault(p => p.Id == request.PollCode);

        return Json(new
        {
            success = true,
            poll = updatedPoll != null ? MapPollToViewModel(updatedPoll) : null,
        });
    }

    /// <summary>
    /// Handles a new comment submission via AJAX.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> AddComment([FromBody] CommentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { success = false, error = _localizer["Community.Message.CommentRequired"].Value });
        }

        if (request.Text!.Length > 2000)
        {
            return BadRequest(new { success = false, error = _localizer.Named("Community.Message.CommentTooLong", ("max", 2000)) });
        }

        var commentRequest = new CommunityCommentRequestDto
        {
            Text = request.Text!,
            PollCode = request.PollCode,
            StrategyId = request.StrategyId
        };

        CommunityCommentDto comment = await _communityApi.AddCommentAsync(commentRequest, cancellationToken);

        return Json(new
        {
            success = true,
            comment = new
            {
                id = comment.Id,
                author = CommunityText.Author(_localizer, comment.AuthorPseudonym),
                isOwn = comment.IsOwn,
                text = comment.Text,
                createdAt = comment.CreatedAtUtc,
                likes = comment.Likes,
                pollId = comment.PollId,
                strategyId = comment.StrategyId,
            }
        });
    }

    /// <summary>
    /// Handles toggling a like on a comment via AJAX.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> ToggleLike([FromBody] LikeRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CommentId <= 0)
        {
            return BadRequest(new { success = false, error = _localizer["Community.Message.CommentIdRequired"].Value });
        }

        CommentLikeResult result = await _communityApi.ToggleCommentLikeAsync(request.CommentId, cancellationToken);

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
        List<CommunityCommentDto> comments = await _communityApi.GetPollCommentsAsync(pollCode, cancellationToken);

        return Json(new
        {
            success = true,
            comments = comments.Select(c => new
            {
                id = c.Id,
                author = CommunityText.Author(_localizer, c.AuthorPseudonym),
                isOwn = c.IsOwn,
                text = c.Text,
                createdAt = c.CreatedAtUtc,
                likes = c.Likes,
            }),
        });
    }

    /// <summary>
    /// Pins a Strategy via AJAX. It is sent Pending: only the viewer sees it until an Admin approves it. The answer
    /// carries what the page needs to pin the viewer's waiting note at once.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> SubmitStrategy([FromBody] StrategyRequest request, CancellationToken cancellationToken = default)
    {
        var strategyRequest = new CommunityStrategyRequestDto
        {
            Category = request.Category ?? string.Empty,
            Title = request.Title ?? string.Empty,
            Tried = request.Tried ?? string.Empty,
            Happened = request.Happened ?? string.Empty,
            Keeping = request.Keeping ?? string.Empty,
        };

        CommunityStrategyDto strategy = await _communityApi.SubmitStrategyAsync(strategyRequest, cancellationToken);

        return Json(new
        {
            success = true,
            strategy = new
            {
                id = strategy.Id,
                category = CommunityText.Category(_localizer, strategy.Category),
                title = strategy.Title,
                tried = strategy.Tried,
                happened = strategy.Happened,
                keeping = strategy.Keeping,
                author = CommunityText.Author(_localizer, strategy.AuthorPseudonym),
            },
        });
    }

    // =========================================================================
    // Mapping helpers: API DTOs → View Models
    // =========================================================================

    private static CommunityViewModel MapToViewModel(CommunityResponseDto response)
    {
        return new CommunityViewModel
        {
            Pseudonym = response.Pseudonym,
            ActivePolls = response.ActivePolls.Select(MapPollToViewModel).ToList(),
            Strategies = response.Strategies.Select(s => new CommunityStrategy
            {
                Id = s.Id,
                Category = s.Category,
                Title = s.Title,
                Tried = s.Tried,
                Happened = s.Happened,
                Keeping = s.Keeping,
                AuthorPseudonym = s.AuthorPseudonym,
                IsOwn = s.IsOwn,
                Status = s.Status,
                CreatedAt = s.CreatedAtUtc,
            }).ToList(),
            RecentComments = response.RecentComments.Select(c => new CommunityComment
            {
                Id = c.Id,
                AuthorPseudonym = c.AuthorPseudonym,
                IsOwn = c.IsOwn,
                Text = c.Text,
                CreatedAt = c.CreatedAtUtc,
                Likes = c.Likes,
                IsLikedByUser = c.IsLikedByUser,
                PollId = c.PollId,
                StrategyId = c.StrategyId,
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
        public string? Text { get; set; }
        public string? PollCode { get; set; }
        public int? StrategyId { get; set; }
    }

    /// <summary>
    /// Request model for toggling comment likes.
    /// </summary>
    public sealed class LikeRequest
    {
        public int CommentId { get; set; }
    }

    /// <summary>
    /// Request model for pinning a Strategy.
    /// </summary>
    public sealed class StrategyRequest
    {
        public string? Category { get; set; }
        public string? Title { get; set; }
        public string? Tried { get; set; }
        public string? Happened { get; set; }
        public string? Keeping { get; set; }
    }
}
