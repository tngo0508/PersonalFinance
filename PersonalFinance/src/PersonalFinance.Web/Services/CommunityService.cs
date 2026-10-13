using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Services;

/// <summary>
/// Service interface for Community page business logic.
/// </summary>
public interface ICommunityService
{
    /// <summary>
    /// Gets the full community view model for the current user.
    /// </summary>
    Task<CommunityViewModel> GetCommunityViewModelAsync(string? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a vote for a poll option. Returns true if the vote was recorded.
    /// </summary>
    Task<bool> VoteAsync(string userId, int pollId, int optionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new community comment.
    /// </summary>
    Task<CommunityCommentDto> AddCommentAsync(string userId, string authorName, string topic, string text,
        int? pollId, int? storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a like on a comment for the current user.
    /// </summary>
    Task<CommentLikeResult> ToggleLikeAsync(string userId, int commentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent comments for display.
    /// </summary>
    Task<List<CommunityCommentDto>> GetRecentCommentsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comments for a specific poll.
    /// </summary>
    Task<List<CommunityCommentDto>> GetPollCommentsAsync(int pollId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comments for a specific story.
    /// </summary>
    Task<List<CommunityCommentDto>> GetStoryCommentsAsync(int storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a new community story.
    /// </summary>
    Task<CommunityStoryDto> SubmitStoryAsync(string userId, string authorName, string title, string summary,
        string body, string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a like on a story for the current user.
    /// </summary>
    Task<StoryLikeResult> ToggleStoryLikeAsync(string userId, int storyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single story by ID.
    /// </summary>
    Task<CommunityStoryDto?> GetStoryByIdAsync(int storyId, string? userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for returning comment data to the view/API.
/// </summary>
public sealed class CommunityCommentDto
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int Likes { get; set; }
    public bool IsLikedByUser { get; set; }
    public int? PollId { get; set; }
    public int? StoryId { get; set; }
}

/// <summary>
/// Result of a comment like toggle operation.
/// </summary>
public sealed class CommentLikeResult
{
    public bool Success { get; set; }
    public int NewLikeCount { get; set; }
    public bool IsLiked { get; set; }
}

/// <summary>
/// DTO for returning story data to the view/API.
/// </summary>
public sealed class CommunityStoryDto
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Likes { get; set; }
    public int CommentCount { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public int ReadTimeMinutes { get; set; }
    public bool IsLikedByUser { get; set; }
}

/// <summary>
/// Result of a story like toggle operation.
/// </summary>
public sealed class StoryLikeResult
{
    public bool Success { get; set; }
    public int NewLikeCount { get; set; }
    public bool IsLiked { get; set; }
}

/// <summary>
/// Service implementation for Community page business logic.
/// </summary>
public sealed class CommunityService : ICommunityService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CommunityService> _logger;

    public CommunityService(AppDbContext dbContext, ILogger<CommunityService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CommunityViewModel> GetCommunityViewModelAsync(string? userId, CancellationToken cancellationToken = default)
    {
        var model = new CommunityViewModel();

        // Fetch active polls with options and vote counts
        var polls = await _dbContext.CommunityPolls
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .Include(p => p.Votes)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        model.ActivePolls = polls.Select(p =>
        {
            var totalVotes = p.Votes.Count;
            var userVote = userId != null ? p.Votes.FirstOrDefault(v => v.UserId == userId) : null;

            return new Models.CommunityPoll
            {
                Id = p.PollCode,
                Question = p.Question,
                Description = p.Description ?? string.Empty,
                Category = p.Category,
                TotalVotes = totalVotes,
                IsActive = p.IsActive,
                HasVoted = userVote != null,
                VotedOptionId = userVote?.OptionId,
                Options = p.Options.OrderBy(o => o.Order).Select(o => new Models.PollOption
                {
                    Id = o.Id,
                    Text = o.Text,
                    Votes = o.Votes.Count,
                    Percentage = totalVotes > 0 ? Math.Round((double)o.Votes.Count / totalVotes * 100, 1) : 0,
                }).ToList(),
            };
        }).ToList();

        // Quick stats — aggregated from real community data
        model.QuickStats = await GetQuickStatsAsync(cancellationToken);

        // Stories — fetched from database
        model.Stories = await GetStoriesForViewModelAsync(userId, cancellationToken);

        // Recent comments — map DTOs to view model comments
        var recentCommentsDto = await GetRecentCommentsAsync(10, cancellationToken);
        model.RecentComments = MapToViewModelComments(recentCommentsDto);

        return model;
    }

    public async Task<bool> VoteAsync(string userId, int pollId, int optionId, CancellationToken cancellationToken = default)
    {
        // Check if the poll exists and is active
        var poll = await _dbContext.CommunityPolls.FindAsync([pollId], cancellationToken);
        if (poll == null || !poll.IsActive)
        {
            _logger.LogWarning("Vote attempt on invalid or inactive poll: PollId={PollId}", pollId);
            return false;
        }

        // Check if the option belongs to the poll
        var option = await _dbContext.CommunityPollOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.PollId == pollId, cancellationToken);

        if (option == null)
        {
            _logger.LogWarning("Vote attempt on invalid option: OptionId={OptionId}, PollId={PollId}", optionId, pollId);
            return false;
        }

        // Check if user already voted on this poll
        var existingVote = await _dbContext.CommunityPollVotes
            .FirstOrDefaultAsync(v => v.UserId == userId && v.PollId == pollId, cancellationToken);

        if (existingVote != null)
        {
            // Change vote if the option is different
            if (existingVote.OptionId == optionId)
            {
                // Same option — no-op
                return true;
            }

            existingVote.OptionId = optionId;
            _logger.LogInformation("User changed vote: UserId={UserId}, PollId={PollId}, OptionId={OptionId}", userId, pollId, optionId);
        }
        else
        {
            // New vote
            var vote = new CommunityPollVote
            {
                UserId = userId,
                PollId = pollId,
                OptionId = optionId,
                CreatedAtUtc = DateTime.UtcNow,
            };

            await _dbContext.CommunityPollVotes.AddAsync(vote, cancellationToken);
            _logger.LogInformation("New vote recorded: UserId={UserId}, PollId={PollId}, OptionId={OptionId}", userId, pollId, optionId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CommunityCommentDto> AddCommentAsync(
        string userId,
        string authorName,
        string topic,
        string text,
        int? pollId,
        int? storyId,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        var trimmedText = text.Trim();
        if (string.IsNullOrEmpty(trimmedText))
        {
            throw new ArgumentException("Comment text cannot be empty.");
        }

        if (trimmedText.Length > 2000)
        {
            throw new ArgumentException("Comment text is too long (max 2000 characters).");
        }

        // Validate poll if provided
        if (pollId.HasValue)
        {
            var poll = await _dbContext.CommunityPolls.FindAsync([pollId.Value], cancellationToken);
            if (poll == null)
            {
                throw new ArgumentException("Invalid poll ID.");
            }
        }

        // Validate story if provided
        if (storyId.HasValue)
        {
            var story = await _dbContext.CommunityStories.FindAsync([storyId.Value], cancellationToken);
            if (story == null || !story.IsVisible)
            {
                throw new ArgumentException("Invalid story ID.");
            }
        }

        // Get story title for backward compatibility with CaseStudyTitle field
        string? storyTitle = null;
        if (storyId.HasValue)
        {
            var story = await _dbContext.CommunityStories.FindAsync([storyId.Value], cancellationToken);
            if (story != null)
            {
                storyTitle = story.Title;
                story.CommentCount++;
            }
        }

        var comment = new PersonalFinance.Data.Entities.CommunityComment
        {
            UserId = userId,
            AuthorName = authorName.Trim(),
            Text = trimmedText,
            Topic = topic.Trim(),
            PollId = pollId,
            CaseStudyTitle = storyTitle,
            Likes = 0,
            IsVisible = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New comment added: CommentId={CommentId}, UserId={UserId}, Topic={Topic}", comment.Id, userId, topic);

        return new CommunityCommentDto
        {
            Id = comment.Id,
            AuthorName = comment.AuthorName,
            Text = comment.Text,
            Topic = comment.Topic,
            CreatedAtUtc = comment.CreatedAtUtc,
            Likes = comment.Likes,
            PollId = comment.PollId,
            StoryId = storyId,
        };
    }

    public async Task<CommentLikeResult> ToggleLikeAsync(string userId, int commentId, CancellationToken cancellationToken = default)
    {
        var comment = await _dbContext.CommunityComments.FindAsync([commentId], cancellationToken);
        if (comment == null || !comment.IsVisible)
        {
            return new CommentLikeResult { Success = false };
        }

        var existingLike = await _dbContext.CommunityCommentLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CommentId == commentId, cancellationToken);

        if (existingLike != null)
        {
            // Remove like
            _dbContext.CommunityCommentLikes.Remove(existingLike);
            comment.Likes--;
        }
        else
        {
            // Add like
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

        return new CommentLikeResult
        {
            Success = true,
            NewLikeCount = Math.Max(0, comment.Likes),
            IsLiked = existingLike == null,
        };
    }

    public async Task<List<CommunityCommentDto>> GetRecentCommentsAsync(int count, CancellationToken cancellationToken = default)
    {
        var comments = await _dbContext.CommunityComments
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

        return comments;
    }

    public async Task<List<CommunityCommentDto>> GetPollCommentsAsync(int pollId, CancellationToken cancellationToken = default)
    {
        var comments = await _dbContext.CommunityComments
            .Where(c => c.IsVisible && c.PollId == pollId)
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

        return comments;
    }

    public async Task<List<CommunityCommentDto>> GetStoryCommentsAsync(int storyId, CancellationToken cancellationToken = default)
    {
        var comments = await _dbContext.CommunityComments
            .Where(c => c.IsVisible && c.CaseStudyTitle != null)
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

        return comments;
    }

    public async Task<CommunityStoryDto> SubmitStoryAsync(
        string userId,
        string authorName,
        string title,
        string summary,
        string body,
        string category,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        var trimmedTitle = title.Trim();
        var trimmedSummary = summary.Trim();
        var trimmedBody = body.Trim();
        var trimmedCategory = category.Trim();

        if (string.IsNullOrEmpty(trimmedTitle) || trimmedTitle.Length > 500)
        {
            throw new ArgumentException("Title is required and must be under 500 characters.");
        }

        if (string.IsNullOrEmpty(trimmedSummary) || trimmedSummary.Length > 2000)
        {
            throw new ArgumentException("Summary is required and must be under 2000 characters.");
        }

        if (string.IsNullOrEmpty(trimmedBody) || trimmedBody.Length < 100)
        {
            throw new ArgumentException("Story body must be at least 100 characters.");
        }

        if (trimmedBody.Length > 10000)
        {
            throw new ArgumentException("Story body must be under 10000 characters.");
        }

        if (string.IsNullOrEmpty(trimmedCategory) || trimmedCategory.Length > 50)
        {
            throw new ArgumentException("Category is required and must be under 50 characters.");
        }

        // Estimate reading time (average 200 words per minute)
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

        _logger.LogInformation("New story submitted: StoryId={StoryId}, UserId={UserId}, Title={Title}", story.Id, userId, trimmedTitle);

        return new CommunityStoryDto
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
        };
    }

    public async Task<StoryLikeResult> ToggleStoryLikeAsync(string userId, int storyId, CancellationToken cancellationToken = default)
    {
        var story = await _dbContext.CommunityStories.FindAsync([storyId], cancellationToken);
        if (story == null || !story.IsVisible)
        {
            return new StoryLikeResult { Success = false };
        }

        var existingLike = await _dbContext.CommunityStoryLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.StoryId == storyId, cancellationToken);

        if (existingLike != null)
        {
            // Remove like
            _dbContext.CommunityStoryLikes.Remove(existingLike);
            story.Likes--;
        }
        else
        {
            // Add like
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

        return new StoryLikeResult
        {
            Success = true,
            NewLikeCount = Math.Max(0, story.Likes),
            IsLiked = existingLike == null,
        };
    }

    public async Task<CommunityStoryDto?> GetStoryByIdAsync(int storyId, string? userId, CancellationToken cancellationToken = default)
    {
        var story = await _dbContext.CommunityStories
            .FirstOrDefaultAsync(s => s.Id == storyId && s.IsVisible, cancellationToken);

        if (story == null)
            return null;

        bool isLikedByUser = false;
        if (userId != null)
        {
            isLikedByUser = await _dbContext.CommunityStoryLikes
                .AnyAsync(l => l.UserId == userId && l.StoryId == storyId, cancellationToken);
        }

        return new CommunityStoryDto
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
        };
    }

    /// <summary>
    /// Computes quick stats from real community data.
    /// </summary>
    private async Task<List<CommunityStat>> GetQuickStatsAsync(CancellationToken cancellationToken)
    {
        var totalPolls = await _dbContext.CommunityPolls.CountAsync(cancellationToken);
        var totalVotes = await _dbContext.CommunityPollVotes.CountAsync(cancellationToken);
        var totalComments = await _dbContext.CommunityComments
            .Where(c => c.IsVisible)
            .CountAsync(cancellationToken);
        var totalStories = await _dbContext.CommunityStories
            .Where(s => s.IsVisible)
            .CountAsync(cancellationToken);

        return new List<CommunityStat>
        {
            new()
            {
                Title = "Active Polls",
                Value = totalPolls.ToString(),
                Description = "Community polls currently open for voting.",
                Icon = "📊",
                ThemeColor = "blue",
            },
            new()
            {
                Title = "Total Votes Cast",
                Value = totalVotes.ToString(),
                Description = "Votes cast across all community polls.",
                Icon = "🗳️",
                ThemeColor = "green",
            },
            new()
            {
                Title = "Community Stories",
                Value = totalStories.ToString(),
                Description = "Real-life financial stories shared by the community.",
                Icon = "📖",
                ThemeColor = "purple",
            },
            new()
            {
                Title = "Comments & Discussions",
                Value = totalComments.ToString(),
                Description = "Comments shared across polls and stories.",
                Icon = "💬",
                ThemeColor = "orange",
            },
        };
    }

    /// <summary>
    /// Fetches stories from the database for the view model.
    /// </summary>
    private async Task<List<Models.CommunityStory>> GetStoriesForViewModelAsync(string? userId, CancellationToken cancellationToken)
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
                // This will be resolved in a separate query to avoid N+1
                isLikedByUser = _dbContext.CommunityStoryLikes
                    .Any(l => l.UserId == userId && l.StoryId == s.Id);
            }

            return new Models.CommunityStory
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
                IsLikedByUser = isLikedByUser,
            };
        }).ToList();
    }

    private static List<Models.CommunityComment> MapToViewModelComments(List<CommunityCommentDto> dtos)
    {
        return dtos.Select(d => new Models.CommunityComment
        {
            Id = d.Id,
            Author = d.AuthorName,
            Text = d.Text,
            CreatedAt = d.CreatedAtUtc,
            Topic = d.Topic,
            Likes = d.Likes,
            IsLikedByUser = d.IsLikedByUser,
            PollId = d.PollId,
            StoryId = d.StoryId,
        }).ToList();
    }
}
