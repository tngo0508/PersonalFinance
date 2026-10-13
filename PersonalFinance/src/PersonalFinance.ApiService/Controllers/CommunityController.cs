using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// RESTful API controller for community features: polls, comments, stories, and likes.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CommunityController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CommunityController> _logger;

    public CommunityController(
        AppDbContext dbContext,
        ILogger<CommunityController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Gets the full community data (polls, stats, stories, comments) for a user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommunityResponseDto>> GetCommunity(
        [FromQuery] string? userId,
        CancellationToken cancellationToken = default)
    {
        var response = new CommunityResponseDto();

        // Fetch active polls with options and vote counts
        var polls = await _dbContext.CommunityPolls
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .Include(p => p.Votes)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        response.ActivePolls = polls.Select(p =>
        {
            var totalVotes = p.Votes.Count;
            var userVote = userId != null ? p.Votes.FirstOrDefault(v => v.UserId == userId) : null;

            return new CommunityPollDto
            {
                Id = p.PollCode,
                Question = p.Question,
                Description = p.Description ?? string.Empty,
                Category = p.Category,
                TotalVotes = totalVotes,
                IsActive = p.IsActive,
                HasVoted = userVote != null,
                VotedOptionId = userVote?.OptionId,
                Options = p.Options.OrderBy(o => o.Order).Select(o => new CommunityPollOptionDto
                {
                    Id = o.Id,
                    Text = o.Text,
                    Votes = o.Votes.Count,
                    Percentage = totalVotes > 0 ? Math.Round((double)o.Votes.Count / totalVotes * 100, 1) : 0,
                }).ToList(),
            };
        }).ToList();

        // Quick stats
        response.QuickStats = await GetQuickStatsAsync(cancellationToken);

        // Stories
        response.Stories = await GetStoriesAsync(userId, cancellationToken);

        // Recent comments
        response.RecentComments = await GetRecentCommentsAsync(10, cancellationToken);

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
        [FromQuery] string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.PollCode))
        {
            return BadRequest("Poll code is required.");
        }

        if (request.OptionId <= 0)
        {
            return BadRequest("Option ID is required.");
        }

        // Look up the poll by its code
        var poll = await _dbContext.CommunityPolls
            .FirstOrDefaultAsync(p => p.PollCode == request.PollCode, cancellationToken);

        if (poll == null || !poll.IsActive)
        {
            _logger.LogWarning("Vote attempt on invalid or inactive poll: PollCode={PollCode}", request.PollCode);
            return BadRequest("Poll not found or inactive.");
        }

        // Check if the option belongs to the poll
        var option = await _dbContext.CommunityPollOptions
            .FirstOrDefaultAsync(o => o.Id == request.OptionId && o.PollId == poll.Id, cancellationToken);

        if (option == null)
        {
            return BadRequest("Invalid option.");
        }

        // Check if user already voted
        var existingVote = await _dbContext.CommunityPollVotes
            .FirstOrDefaultAsync(v => v.UserId == userId && v.PollId == poll.Id, cancellationToken);

        if (existingVote != null)
        {
            if (existingVote.OptionId == request.OptionId)
            {
                // Same option — no-op
            }
            else
            {
                existingVote.OptionId = request.OptionId;
                _logger.LogInformation("User changed vote: UserId={UserId}, PollId={PollId}, OptionId={OptionId}", userId, poll.Id, request.OptionId);
            }
        }
        else
        {
            var vote = new CommunityPollVote
            {
                UserId = userId,
                PollId = poll.Id,
                OptionId = request.OptionId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _dbContext.CommunityPollVotes.AddAsync(vote, cancellationToken);
            _logger.LogInformation("New vote recorded: UserId={UserId}, PollId={PollId}, OptionId={OptionId}", userId, poll.Id, request.OptionId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return updated community data
        var response = new CommunityResponseDto();
        var updatedPolls = await _dbContext.CommunityPolls
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .Include(p => p.Votes)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        response.ActivePolls = updatedPolls.Select(p =>
        {
            var totalVotes = p.Votes.Count;
            var userVote = userId != null ? p.Votes.FirstOrDefault(v => v.UserId == userId) : null;
            return new CommunityPollDto
            {
                Id = p.PollCode,
                Question = p.Question,
                Description = p.Description ?? string.Empty,
                Category = p.Category,
                TotalVotes = totalVotes,
                IsActive = p.IsActive,
                HasVoted = userVote != null,
                VotedOptionId = userVote?.OptionId,
                Options = p.Options.OrderBy(o => o.Order).Select(o => new CommunityPollOptionDto
                {
                    Id = o.Id,
                    Text = o.Text,
                    Votes = o.Votes.Count,
                    Percentage = totalVotes > 0 ? Math.Round((double)o.Votes.Count / totalVotes * 100, 1) : 0,
                }).ToList(),
            };
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Adds a comment to a poll or story.
    /// </summary>
    [HttpPost("comment")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommunityCommentDto>> AddComment(
        [FromBody] CommunityCommentRequestDto request,
        [FromQuery] string userId,
        [FromQuery] string authorName,
        CancellationToken cancellationToken = default)
    {
        var trimmedText = request.Text.Trim();
        if (string.IsNullOrEmpty(trimmedText))
        {
            return BadRequest("Comment text cannot be empty.");
        }

        if (trimmedText.Length > 2000)
        {
            return BadRequest("Comment text is too long (max 2000 characters).");
        }

        int? pollId = null;
        if (!string.IsNullOrEmpty(request.PollCode))
        {
            var poll = await _dbContext.CommunityPolls
                .FirstOrDefaultAsync(p => p.PollCode == request.PollCode, cancellationToken);
            if (poll == null)
            {
                return BadRequest("Invalid poll.");
            }
            pollId = poll.Id;
        }

        int? storyId = null;
        string? storyTitle = null;
        if (request.StoryId.HasValue && request.StoryId.Value > 0)
        {
            var story = await _dbContext.CommunityStories
                .FindAsync([request.StoryId.Value], cancellationToken);
            if (story == null || !story.IsVisible)
            {
                return BadRequest("Invalid story.");
            }
            storyId = story.Id;
            storyTitle = story.Title;
            story.CommentCount++;
        }

        var comment = new PersonalFinance.Data.Entities.CommunityComment
        {
            UserId = userId,
            AuthorName = authorName.Trim(),
            Text = trimmedText,
            Topic = request.Topic.Trim(),
            PollId = pollId,
            CaseStudyTitle = storyTitle,
            Likes = 0,
            IsVisible = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _dbContext.CommunityComments.AddAsync(comment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New comment added: CommentId={CommentId}, UserId={UserId}", comment.Id, userId);

        return CreatedAtAction(nameof(GetCommunity), new CommunityCommentDto
        {
            Id = comment.Id,
            AuthorName = comment.AuthorName,
            Text = comment.Text,
            Topic = comment.Topic,
            CreatedAtUtc = comment.CreatedAtUtc,
            Likes = comment.Likes,
            PollId = comment.PollId,
            StoryId = storyId,
        });
    }

    /// <summary>
    /// Toggles a like on a comment.
    /// </summary>
    [HttpPost("comment/{commentId}/like")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentLikeResult>> ToggleCommentLike(
        int commentId,
        [FromQuery] string userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _dbContext.CommunityComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.IsVisible, cancellationToken);

        if (comment == null)
        {
            return NotFound();
        }

        var existingLike = await _dbContext.CommunityCommentLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CommentId == commentId, cancellationToken);

        if (existingLike != null)
        {
            _dbContext.CommunityCommentLikes.Remove(existingLike);
            comment.Likes--;
        }
        else
        {
            var like = new CommunityCommentLike
            {
                UserId = userId,
                CommentId = commentId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _dbContext.CommunityCommentLikes.AddAsync(like, cancellationToken);
            comment.Likes++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CommentLikeResult
        {
            Success = true,
            NewLikeCount = Math.Max(0, comment.Likes),
            IsLiked = existingLike == null,
        });
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
        var poll = await _dbContext.CommunityPolls
            .FirstOrDefaultAsync(p => p.PollCode == pollCode, cancellationToken);

        if (poll == null)
        {
            return NotFound();
        }

        var comments = await _dbContext.CommunityComments
            .Where(c => c.IsVisible && c.PollId == poll.Id)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new CommunityCommentDto
            {
                Id = c.Id,
                AuthorName = c.AuthorName,
                Text = c.Text,
                Topic = c.Topic,
                CreatedAtUtc = c.CreatedAtUtc,
                Likes = c.Likes,
                PollId = c.PollId,
                StoryId = null,
            })
            .ToListAsync(cancellationToken);

        return Ok(comments);
    }

    /// <summary>
    /// Submits a new community story.
    /// </summary>
    [HttpPost("story")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommunityStoryDto>> SubmitStory(
        [FromBody] CommunityStoryRequestDto request,
        [FromQuery] string userId,
        [FromQuery] string authorName,
        CancellationToken cancellationToken = default)
    {
        var trimmedTitle = request.Title.Trim();
        var trimmedSummary = request.Summary.Trim();
        var trimmedBody = request.Body.Trim();
        var trimmedCategory = request.Category.Trim();

        if (string.IsNullOrEmpty(trimmedTitle) || trimmedTitle.Length > 500)
        {
            return BadRequest("Title is required and must be under 500 characters.");
        }

        if (string.IsNullOrEmpty(trimmedSummary) || trimmedSummary.Length > 2000)
        {
            return BadRequest("Summary is required and must be under 2000 characters.");
        }

        if (string.IsNullOrEmpty(trimmedBody) || trimmedBody.Length < 100)
        {
            return BadRequest("Story body must be at least 100 characters.");
        }

        if (trimmedBody.Length > 10000)
        {
            return BadRequest("Story body must be under 10000 characters.");
        }

        if (string.IsNullOrEmpty(trimmedCategory) || trimmedCategory.Length > 50)
        {
            return BadRequest("Category is required and must be under 50 characters.");
        }

        var wordCount = trimmedBody.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var readTimeMinutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));

        var story = new PersonalFinance.Data.Entities.CommunityStory
        {
            UserId = userId,
            AuthorName = authorName.Trim(),
            Title = trimmedTitle,
            Summary = trimmedSummary,
            Body = trimmedBody,
            Category = trimmedCategory,
            Likes = 0,
            CommentCount = 0,
            IsVisible = true,
            IsFeatured = false,
            PublishedAtUtc = DateTime.UtcNow,
            ReadTimeMinutes = readTimeMinutes,
        };

        await _dbContext.CommunityStories.AddAsync(story, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New story submitted: StoryId={StoryId}, UserId={UserId}", story.Id, userId);

        return CreatedAtAction(nameof(GetCommunity), new CommunityStoryDto
        {
            Id = story.Id,
            AuthorName = story.AuthorName,
            Title = story.Title,
            Summary = story.Summary,
            Body = story.Body,
            Category = story.Category,
            Likes = story.Likes,
            CommentCount = story.CommentCount,
            IsFeatured = story.IsFeatured,
            PublishedAtUtc = story.PublishedAtUtc,
            ReadTimeMinutes = story.ReadTimeMinutes,
        });
    }

    /// <summary>
    /// Toggles a like on a story.
    /// </summary>
    [HttpPost("story/{storyId}/like")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<StoryLikeResult>> ToggleStoryLike(
        int storyId,
        [FromQuery] string userId,
        CancellationToken cancellationToken = default)
    {
        var story = await _dbContext.CommunityStories
            .FirstOrDefaultAsync(s => s.Id == storyId && s.IsVisible, cancellationToken);

        if (story == null)
        {
            return NotFound();
        }

        var existingLike = await _dbContext.CommunityStoryLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.StoryId == storyId, cancellationToken);

        if (existingLike != null)
        {
            _dbContext.CommunityStoryLikes.Remove(existingLike);
            story.Likes--;
        }
        else
        {
            var like = new CommunityStoryLike
            {
                UserId = userId,
                StoryId = storyId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _dbContext.CommunityStoryLikes.AddAsync(like, cancellationToken);
            story.Likes++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new StoryLikeResult
        {
            Success = true,
            NewLikeCount = Math.Max(0, story.Likes),
            IsLiked = existingLike == null,
        });
    }

    /// <summary>
    /// Gets a single story by ID.
    /// </summary>
    [HttpGet("story/{storyId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommunityStoryDto>> GetStoryById(
        int storyId,
        [FromQuery] string? userId,
        CancellationToken cancellationToken = default)
    {
        var story = await _dbContext.CommunityStories
            .FirstOrDefaultAsync(s => s.Id == storyId && s.IsVisible, cancellationToken);

        if (story == null)
            return NotFound();

        bool isLikedByUser = false;
        if (userId != null)
        {
            isLikedByUser = await _dbContext.CommunityStoryLikes
                .AnyAsync(l => l.UserId == userId && l.StoryId == storyId, cancellationToken);
        }

        return Ok(new CommunityStoryDto
        {
            Id = story.Id,
            AuthorName = story.AuthorName,
            Title = story.Title,
            Summary = story.Summary,
            Body = story.Body,
            Category = story.Category,
            Likes = story.Likes,
            CommentCount = story.CommentCount,
            IsFeatured = story.IsFeatured,
            PublishedAtUtc = story.PublishedAtUtc,
            ReadTimeMinutes = story.ReadTimeMinutes,
            IsLikedByUser = isLikedByUser,
        });
    }

    private async Task<List<CommunityStatDto>> GetQuickStatsAsync(CancellationToken cancellationToken)
    {
        var totalPolls = await _dbContext.CommunityPolls.CountAsync(cancellationToken);
        var totalVotes = await _dbContext.CommunityPollVotes.CountAsync(cancellationToken);
        var totalComments = await _dbContext.CommunityComments
            .Where(c => c.IsVisible)
            .CountAsync(cancellationToken);
        var totalStories = await _dbContext.CommunityStories
            .Where(s => s.IsVisible)
            .CountAsync(cancellationToken);

        return new List<CommunityStatDto>
        {
            new() { Title = "Active Polls", Value = totalPolls.ToString(), Description = "Community polls currently open for voting.", Icon = "📊", ThemeColor = "blue" },
            new() { Title = "Total Votes Cast", Value = totalVotes.ToString(), Description = "Votes cast across all community polls.", Icon = "🗳️", ThemeColor = "green" },
            new() { Title = "Community Stories", Value = totalStories.ToString(), Description = "Real-life financial stories shared by the community.", Icon = "📖", ThemeColor = "purple" },
            new() { Title = "Comments & Discussions", Value = totalComments.ToString(), Description = "Comments shared across polls and stories.", Icon = "💬", ThemeColor = "orange" },
        };
    }

    private async Task<List<CommunityStoryDto>> GetStoriesAsync(string? userId, CancellationToken cancellationToken)
    {
        var stories = await _dbContext.CommunityStories
            .Where(s => s.IsVisible)
            .OrderByDescending(s => s.IsFeatured)
            .ThenByDescending(s => s.PublishedAtUtc)
            .Take(6)
            .ToListAsync(cancellationToken);

        return stories.Select(s =>
        {
            bool isLikedByUser = false;
            if (userId != null)
            {
                isLikedByUser = _dbContext.CommunityStoryLikes
                    .Any(l => l.UserId == userId && l.StoryId == s.Id);
            }

            return new CommunityStoryDto
            {
                Id = s.Id,
                AuthorName = s.AuthorName,
                Title = s.Title,
                Summary = s.Summary,
                Body = s.Body,
                Category = s.Category,
                Likes = s.Likes,
                CommentCount = s.CommentCount,
                IsFeatured = s.IsFeatured,
                PublishedAtUtc = s.PublishedAtUtc,
                ReadTimeMinutes = s.ReadTimeMinutes,
                IsLikedByUser = isLikedByUser,
            };
        }).ToList();
    }

    private async Task<List<CommunityCommentDto>> GetRecentCommentsAsync(int count, CancellationToken cancellationToken)
    {
        return await _dbContext.CommunityComments
            .Where(c => c.IsVisible)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(count)
            .Select(c => new CommunityCommentDto
            {
                Id = c.Id,
                AuthorName = c.AuthorName,
                Text = c.Text,
                Topic = c.Topic,
                CreatedAtUtc = c.CreatedAtUtc,
                Likes = c.Likes,
                PollId = c.PollId,
                StoryId = null,
            })
            .ToListAsync(cancellationToken);
    }
}
