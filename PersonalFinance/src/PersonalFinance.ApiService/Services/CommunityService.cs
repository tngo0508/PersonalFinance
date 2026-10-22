using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service implementation for managing community features: polls, comments, stories, and likes.
/// </summary>
public class CommunityService : ICommunityService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CommunityService> _logger;

    public CommunityService(
        AppDbContext dbContext,
        ILogger<CommunityService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CommunityResponseDto> GetCommunityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var response = new CommunityResponseDto();

        // Fetch active polls with options and vote counts
        var polls = await _dbContext.CommunityPolls
            .AsNoTracking()
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .Include(p => p.Votes)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        response.ActivePolls = polls.Select(p => MapPollToDto(p, userId)).ToList();
        response.QuickStats = await GetQuickStatsAsync(cancellationToken);
        response.Stories = await GetStoriesAsync(userId, cancellationToken);
        response.RecentComments = await GetRecentCommentsAsync(10, cancellationToken);

        return response;
    }

    public async Task<CommunityVoteResultDto> VoteAsync(string userId, CommunityVoteRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.PollCode))
        {
            return new CommunityVoteResultDto { Success = false, ErrorMessage = "Poll code is required." };
        }

        if (request.OptionId <= 0)
        {
            return new CommunityVoteResultDto { Success = false, ErrorMessage = "Option ID is required." };
        }

        // Look up the poll by its code
        var poll = await _dbContext.CommunityPolls
            .FirstOrDefaultAsync(p => p.PollCode == request.PollCode, cancellationToken);

        if (poll == null || !poll.IsActive)
        {
            _logger.LogWarning("Vote attempt on invalid or inactive poll: PollCode={PollCode}", request.PollCode);
            return new CommunityVoteResultDto { Success = false, ErrorMessage = "Poll not found or inactive." };
        }

        // Check if the option belongs to the poll
        var option = await _dbContext.CommunityPollOptions
            .FirstOrDefaultAsync(o => o.Id == request.OptionId && o.PollId == poll.Id, cancellationToken);

        if (option == null)
        {
            return new CommunityVoteResultDto { Success = false, ErrorMessage = "Invalid option." };
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
            };
            await _dbContext.CommunityPollVotes.AddAsync(vote, cancellationToken);
            _logger.LogInformation("New vote recorded: UserId={UserId}, PollId={PollId}, OptionId={OptionId}", userId, poll.Id, request.OptionId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return updated community data
        var response = new CommunityResponseDto();
        var updatedPolls = await _dbContext.CommunityPolls
            .AsNoTracking()
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .Include(p => p.Votes)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        response.ActivePolls = updatedPolls.Select(p => MapPollToDto(p, userId)).ToList();

        return new CommunityVoteResultDto { Success = true, Response = response };
    }

    public async Task<CommunityCommentDto> AddCommentAsync(string userId, CommunityCommentRequestDto request, string authorName, CancellationToken cancellationToken = default)
    {
        var trimmedText = request.Text.Trim();
        if (string.IsNullOrEmpty(trimmedText))
        {
            throw new ArgumentException("Comment text cannot be empty.");
        }

        if (trimmedText.Length > 2000)
        {
            throw new ArgumentException("Comment text is too long (max 2000 characters).");
        }

        int? pollId = null;
        if (!string.IsNullOrEmpty(request.PollCode))
        {
            var poll = await _dbContext.CommunityPolls
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PollCode == request.PollCode, cancellationToken);
            if (poll == null)
            {
                throw new ArgumentException("Invalid poll.");
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
                throw new ArgumentException("Invalid story.");
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
        };

        await _dbContext.CommunityComments.AddAsync(comment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New comment added: CommentId={CommentId}, UserId={UserId}", comment.Id, userId);

        return new CommunityCommentDto
        {
            Id = comment.Id,
            AuthorName = comment.AuthorName,
            Text = comment.Text,
            Topic = comment.Topic,
            CreatedAtUtc = comment.CreatedAt,
            Likes = comment.Likes,
            PollId = comment.PollId,
            StoryId = storyId,
        };
    }

    public async Task<CommentLikeResult> ToggleCommentLikeAsync(string userId, int commentId, CancellationToken cancellationToken = default)
    {
        var comment = await _dbContext.CommunityComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.IsVisible, cancellationToken);

        if (comment == null)
        {
            throw new KeyNotFoundException("Comment not found.");
        }

        var existingLike = await _dbContext.CommunityCommentLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CommentId == commentId, cancellationToken);

        if (existingLike != null)
        {
            existingLike.SoftDelete();
            comment.Likes--;
        }
        else
        {
            var like = new CommunityCommentLike
            {
                UserId = userId,
                CommentId = commentId,
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

    public async Task<List<CommunityCommentDto>> GetPollCommentsAsync(string pollCode, CancellationToken cancellationToken = default)
    {
        var poll = await _dbContext.CommunityPolls
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PollCode == pollCode, cancellationToken);

        if (poll == null)
        {
            throw new KeyNotFoundException("Poll not found.");
        }

        var comments = await _dbContext.CommunityComments
            .AsNoTracking()
            .Where(c => c.IsVisible && c.PollId == poll.Id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommunityCommentDto
            {
                AuthorName = c.AuthorName,
                Text = c.Text,
                Topic = c.Topic,
                CreatedAtUtc = c.CreatedAt,
                Likes = c.Likes,
                PollId = c.PollId,
                StoryId = null,
            })
            .ToListAsync(cancellationToken);

        return comments;
    }

    public async Task<CommunityStoryDto> SubmitStoryAsync(string userId, CommunityStoryRequestDto request, string authorName, CancellationToken cancellationToken = default)
    {
        var trimmedTitle = request.Title.Trim();
        var trimmedSummary = request.Summary.Trim();
        var trimmedBody = request.Body.Trim();
        var trimmedCategory = request.Category.Trim();

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

        var wordCount = trimmedBody.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
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
        var story = await _dbContext.CommunityStories
            .FirstOrDefaultAsync(s => s.Id == storyId && s.IsVisible, cancellationToken);

        if (story == null)
        {
            throw new KeyNotFoundException("Story not found.");
        }

        var existingLike = await _dbContext.CommunityStoryLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.StoryId == storyId, cancellationToken);

        if (existingLike != null)
        {
            existingLike.SoftDelete();
            story.Likes--;
        }
        else
        {
            var like = new CommunityStoryLike
            {
                UserId = userId,
                StoryId = storyId,
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

    public async Task<CommunityStoryDto?> GetStoryByIdAsync(string userId, int storyId, CancellationToken cancellationToken = default)
    {
        var story = await _dbContext.CommunityStories
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == storyId && s.IsVisible, cancellationToken);

        if (story == null)
            return null;

        bool isLikedByUser = false;
        if (userId != null)
        {
            isLikedByUser = await _dbContext.CommunityStoryLikes
                .AsNoTracking()
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

    // =========================================================================
    // Private Helpers
    // =========================================================================

    private static CommunityPollDto MapPollToDto(CommunityPoll p, string userId)
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
    }

    private async Task<List<CommunityStatDto>> GetQuickStatsAsync(CancellationToken cancellationToken)
    {
        var totalPolls = await _dbContext.CommunityPolls.AsNoTracking().CountAsync(cancellationToken);
        var totalVotes = await _dbContext.CommunityPollVotes.AsNoTracking().CountAsync(cancellationToken);
        var totalComments = await _dbContext.CommunityComments
            .AsNoTracking()
            .Where(c => c.IsVisible)
            .CountAsync(cancellationToken);
        var totalStories = await _dbContext.CommunityStories
            .AsNoTracking()
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
            .AsNoTracking()
            .Where(s => s.IsVisible)
            .OrderByDescending(s => s.IsFeatured)
            .ThenByDescending(s => s.PublishedAtUtc)
            .Take(6)
            .ToListAsync(cancellationToken);

        // Fetch all like counts in a single query to avoid N+1
        var storyIds = stories.Select(s => s.Id).ToList();
        var userLikes = new HashSet<int>();
        if (userId != null && storyIds.Count > 0)
        {
            var likeStoryIds = await _dbContext.CommunityStoryLikes
                .AsNoTracking()
                .Where(l => l.UserId == userId && storyIds.Contains(l.StoryId))
                .Select(l => l.StoryId)
                .ToListAsync(cancellationToken);
            userLikes = new HashSet<int>(likeStoryIds);
        }

        return stories.Select(s => new CommunityStoryDto
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
            IsLikedByUser = userLikes.Contains(s.Id),
        }).ToList();
    }

    private async Task<List<CommunityCommentDto>> GetRecentCommentsAsync(int count, CancellationToken cancellationToken)
    {
        return await _dbContext.CommunityComments
            .AsNoTracking()
            .Where(c => c.IsVisible)
            .OrderByDescending(c => c.CreatedAt)
            .Take(count)
            .Select(c => new CommunityCommentDto
            {
                AuthorName = c.AuthorName,
                Text = c.Text,
                Topic = c.Topic,
                CreatedAtUtc = c.CreatedAt,
                Likes = c.Likes,
                PollId = c.PollId,
                StoryId = null,
            })
            .ToListAsync(cancellationToken);
    }
}
