using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Service implementation of the Guild Hall (the Community Feature): Pseudonyms, polls, comments, stories and likes.
/// An author is shown by its Pseudonym, resolved from the Account at read time, so a re-roll shows on every past post
/// and no DTO carries a display name or email (ADR 0018). A post whose author has no Pseudonym any more (a Former
/// member, ADR 0005) is returned with a null author.
/// </summary>
internal sealed class CommunityService : ICommunityService
{
    /// <summary>
    /// Saves of a Pseudonym retried when a unique index rejects a row another request wrote at the same time: the
    /// same Account's first visit twice, or two Accounts drawing the same name.
    /// </summary>
    private const int PseudonymSaveAttempts = 3;

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CommunityService> _logger;

    public CommunityService(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<CommunityService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CommunityResponseDto> GetCommunityAsync(string userId, CancellationToken cancellationToken = default)
    {
        CommunityPseudonym pseudonym = await GetOrCreatePseudonymAsync(userId, cancellationToken);
        List<CommunityPoll> polls = await ActivePollsAsync(cancellationToken);

        return new CommunityResponseDto
        {
            Pseudonym = pseudonym.Name,
            ActivePolls = polls.Select(p => MapPollToDto(p, userId)).ToList(),
            Stories = await VisibleStoriesQuery(userId).Take(6).ToListAsync(cancellationToken),
            RecentComments = await VisibleCommentsQuery(userId).Take(10).ToListAsync(cancellationToken),
        };
    }

    public async Task<CommunityPseudonymDto> RerollPseudonymAsync(string userId, CancellationToken cancellationToken = default)
    {
        CommunityPseudonym pseudonym = await GetOrCreatePseudonymAsync(userId, cancellationToken);

        for (int attempt = 1; ; attempt++)
        {
            pseudonym.Name = await PickUnusedNameAsync(pseudonym.Name, cancellationToken);
            pseudonym.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (attempt < PseudonymSaveAttempts)
            {
                // Another Account drew the same name first; draw again.
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Pseudonym re-rolled: UserId={UserId}", userId);
        }

        return new CommunityPseudonymDto { Name = pseudonym.Name };
    }

    public async Task<CommunityResponseDto> VoteAsync(string userId, CommunityVoteRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.PollCode))
        {
            throw new ApiErrorException(ApiErrorKeys.Community.PollCodeRequired, "A poll code is required.");
        }

        if (request.OptionId <= 0)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.OptionIdRequired, "Option ID is required.");
        }

        // Look up the poll by its code
        CommunityPoll? poll = await _dbContext.CommunityPolls
            .FirstOrDefaultAsync(p => p.PollCode == request.PollCode, cancellationToken);

        if (poll == null || !poll.IsActive)
        {
            _logger.LogWarning("Vote attempt on invalid or inactive poll: PollCode={PollCode}", request.PollCode);
            throw new ApiErrorException(ApiErrorKeys.Community.PollNotFound, "The poll was not found or is inactive.");
        }

        // Check if the option belongs to the poll
        CommunityPollOption? option = await _dbContext.CommunityPollOptions
            .FirstOrDefaultAsync(o => o.Id == request.OptionId && o.PollId == poll.Id, cancellationToken);

        if (option == null)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.InvalidOption, "Invalid option.");
        }

        // Check if user already voted
        CommunityPollVote? existingVote = await _dbContext.CommunityPollVotes
            .FirstOrDefaultAsync(v => v.UserId == userId && v.PollId == poll.Id, cancellationToken);

        if (existingVote != null)
        {
            if (existingVote.OptionId != request.OptionId)
            {
                existingVote.OptionId = request.OptionId;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("User changed vote: UserId={UserId}, PollId={PollId}, OptionId={OptionId}", userId, poll.Id, request.OptionId);
                }
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
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("New vote recorded: UserId={UserId}, PollId={PollId}, OptionId={OptionId}", userId, poll.Id, request.OptionId);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return the updated polls only; the page re-reads the rest when it reloads.
        List<CommunityPoll> updatedPolls = await ActivePollsAsync(cancellationToken);
        return new CommunityResponseDto
        {
            ActivePolls = updatedPolls.Select(p => MapPollToDto(p, userId)).ToList(),
        };
    }

    public async Task<CommunityCommentDto> AddCommentAsync(string userId, CommunityCommentRequestDto request, CancellationToken cancellationToken = default)
    {
        string trimmedText = request.Text.Trim();
        if (string.IsNullOrEmpty(trimmedText))
        {
            throw new ApiErrorException(ApiErrorKeys.Community.CommentEmpty, "Comment text cannot be empty.", "Text");
        }

        if (trimmedText.Length > 2000)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.CommentTooLong, "Comment text is too long (max 2000 characters).", "Text",
                ErrorArgs.Number("max", 2000));
        }

        int? pollId = null;
        if (!string.IsNullOrEmpty(request.PollCode))
        {
            CommunityPoll? poll = await _dbContext.CommunityPolls
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PollCode == request.PollCode, cancellationToken);
            if (poll == null)
            {
                throw new ApiErrorException(ApiErrorKeys.Community.InvalidPoll, "No poll matches this code.");
            }
            pollId = poll.Id;
        }

        int? storyId = null;
        if (request.StoryId.HasValue && request.StoryId.Value > 0)
        {
            CommunityStory? story = await _dbContext.CommunityStories
                .FindAsync([request.StoryId.Value], cancellationToken);
            if (story == null || !story.IsVisible)
            {
                throw new ApiErrorException(ApiErrorKeys.Community.InvalidStory, "Invalid story.");
            }
            storyId = story.Id;
        }

        CommunityPseudonym author = await GetOrCreatePseudonymAsync(userId, cancellationToken);
        var comment = new CommunityComment
        {
            UserId = userId,
            Text = trimmedText,
            PollId = pollId,
            StoryId = storyId,
            IsVisible = true,
        };

        await _dbContext.CommunityComments.AddAsync(comment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("New comment added: CommentId={CommentId}, UserId={UserId}", comment.Id, userId);
        }

        return new CommunityCommentDto
        {
            Id = comment.Id,
            AuthorPseudonym = author.Name,
            IsOwn = true,
            Text = comment.Text,
            CreatedAtUtc = comment.CreatedAt,
            PollId = comment.PollId,
            StoryId = comment.StoryId,
        };
    }

    public async Task<CommentLikeResult> ToggleCommentLikeAsync(string userId, int commentId, CancellationToken cancellationToken = default)
    {
        CommunityComment? comment = await _dbContext.CommunityComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.IsVisible, cancellationToken);

        if (comment == null)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.CommentNotFound, "Comment not found.", statusCode: StatusCodes.Status404NotFound);
        }

        CommunityCommentLike? existingLike = await _dbContext.CommunityCommentLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CommentId == commentId, cancellationToken);

        if (existingLike != null)
        {
            existingLike.SoftDelete();
        }
        else
        {
            var like = new CommunityCommentLike
            {
                UserId = userId,
                CommentId = commentId,
            };
            await _dbContext.CommunityCommentLikes.AddAsync(like, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CommentLikeResult
        {
            Success = true,
            NewLikeCount = await _dbContext.CommunityCommentLikes.CountAsync(l => l.CommentId == commentId, cancellationToken),
            IsLiked = existingLike == null,
        };
    }

    public async Task<List<CommunityCommentDto>> GetPollCommentsAsync(string userId, string pollCode, CancellationToken cancellationToken = default)
    {
        CommunityPoll? poll = await _dbContext.CommunityPolls
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PollCode == pollCode, cancellationToken);

        if (poll == null)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.PollNotFound, "The poll was not found or is inactive.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return await VisibleCommentsQuery(userId)
            .Where(c => c.PollId == poll.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<CommunityStoryDto> SubmitStoryAsync(string userId, CommunityStoryRequestDto request, CancellationToken cancellationToken = default)
    {
        string trimmedTitle = request.Title.Trim();
        string trimmedSummary = request.Summary.Trim();
        string trimmedBody = request.Body.Trim();
        string trimmedCategory = request.Category.Trim();

        if (string.IsNullOrEmpty(trimmedTitle) || trimmedTitle.Length > 500)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.StoryTitle, "A title is required and must be under 500 characters.", "Title",
                ErrorArgs.Number("max", 500));
        }

        if (string.IsNullOrEmpty(trimmedSummary) || trimmedSummary.Length > 2000)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.StorySummary, "Summary is required and must be under 2000 characters.",
                "Summary", ErrorArgs.Number("max", 2000));
        }

        if (string.IsNullOrEmpty(trimmedBody) || trimmedBody.Length < 100)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.StoryBodyMin, "Story body must have at least 100 characters.", "Body",
                ErrorArgs.Number("min", 100));
        }

        if (trimmedBody.Length > 10000)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.StoryBodyMax, "Story body must be shorter than 10000 characters.", "Body",
                ErrorArgs.Number("max", 10000));
        }

        if (string.IsNullOrEmpty(trimmedCategory) || trimmedCategory.Length > 50)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.StoryCategory, "Category is required and must be under 50 characters.",
                "Category", ErrorArgs.Number("max", 50));
        }

        int wordCount = trimmedBody.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        int readTimeMinutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));

        CommunityPseudonym author = await GetOrCreatePseudonymAsync(userId, cancellationToken);
        var story = new CommunityStory
        {
            UserId = userId,
            Title = trimmedTitle,
            Summary = trimmedSummary,
            Body = trimmedBody,
            Category = trimmedCategory,
            IsVisible = true,
            IsFeatured = false,
            PublishedAtUtc = DateTime.UtcNow,
            ReadTimeMinutes = readTimeMinutes,
        };

        await _dbContext.CommunityStories.AddAsync(story, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("New story submitted: StoryId={StoryId}, UserId={UserId}", story.Id, userId);
        }

        return new CommunityStoryDto
        {
            Id = story.Id,
            AuthorPseudonym = author.Name,
            IsOwn = true,
            Title = story.Title,
            Summary = story.Summary,
            Body = story.Body,
            Category = story.Category,
            IsFeatured = story.IsFeatured,
            PublishedAtUtc = story.PublishedAtUtc,
            ReadTimeMinutes = story.ReadTimeMinutes,
        };
    }

    public async Task<StoryLikeResult> ToggleStoryLikeAsync(string userId, int storyId, CancellationToken cancellationToken = default)
    {
        CommunityStory? story = await _dbContext.CommunityStories
            .FirstOrDefaultAsync(s => s.Id == storyId && s.IsVisible, cancellationToken);

        if (story == null)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.StoryNotFound, "Story not found.", statusCode: StatusCodes.Status404NotFound);
        }

        CommunityStoryLike? existingLike = await _dbContext.CommunityStoryLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.StoryId == storyId, cancellationToken);

        if (existingLike != null)
        {
            existingLike.SoftDelete();
        }
        else
        {
            var like = new CommunityStoryLike
            {
                UserId = userId,
                StoryId = storyId,
            };
            await _dbContext.CommunityStoryLikes.AddAsync(like, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StoryLikeResult
        {
            Success = true,
            NewLikeCount = await _dbContext.CommunityStoryLikes.CountAsync(l => l.StoryId == storyId, cancellationToken),
            IsLiked = existingLike == null,
        };
    }

    public async Task<CommunityStoryDto?> GetStoryByIdAsync(string userId, int storyId, CancellationToken cancellationToken = default)
    {
        return await VisibleStoriesQuery(userId)
            .FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
    }

    // =========================================================================
    // Queries (internal so tests can check they translate to Azure SQL)
    // =========================================================================

    /// <summary>
    /// The visible stories, featured first then newest, each with its author's Pseudonym: null once the author is a
    /// Former member, since the Purge deletes the Pseudonym and leaves the story under the placeholder id.
    /// </summary>
    internal IQueryable<CommunityStoryDto> VisibleStoriesQuery(string userId) =>
        from story in _dbContext.CommunityStories.AsNoTracking()
        where story.IsVisible
        join author in _dbContext.CommunityPseudonyms.AsNoTracking() on story.UserId equals author.UserId into authors
        from author in authors.DefaultIfEmpty()
        orderby story.IsFeatured descending, story.PublishedAtUtc descending, story.Id descending
        select new CommunityStoryDto
        {
            Id = story.Id,
            AuthorPseudonym = author == null ? null : author.Name,
            IsOwn = story.UserId == userId,
            Title = story.Title,
            Summary = story.Summary,
            Body = story.Body,
            Category = story.Category,
            Likes = story.LikesList.Count,
            CommentCount = story.Comments.Count(c => c.IsVisible),
            IsFeatured = story.IsFeatured,
            PublishedAtUtc = story.PublishedAtUtc,
            ReadTimeMinutes = story.ReadTimeMinutes,
            IsLikedByUser = story.LikesList.Any(l => l.UserId == userId),
        };

    /// <summary>The visible comments, newest first, each with its author's Pseudonym (null for a Former member).</summary>
    internal IQueryable<CommunityCommentDto> VisibleCommentsQuery(string userId) =>
        from comment in _dbContext.CommunityComments.AsNoTracking()
        where comment.IsVisible
        join author in _dbContext.CommunityPseudonyms.AsNoTracking() on comment.UserId equals author.UserId into authors
        from author in authors.DefaultIfEmpty()
        orderby comment.CreatedAt descending, comment.Id descending
        select new CommunityCommentDto
        {
            Id = comment.Id,
            AuthorPseudonym = author == null ? null : author.Name,
            IsOwn = comment.UserId == userId,
            Text = comment.Text,
            CreatedAtUtc = comment.CreatedAt,
            Likes = comment.LikesList.Count,
            IsLikedByUser = comment.LikesList.Any(l => l.UserId == userId),
            PollId = comment.PollId,
            StoryId = comment.StoryId,
        };

    // =========================================================================
    // Private Helpers
    // =========================================================================

    /// <summary>
    /// The Account's Pseudonym, created on first use. A unique index on the Account id and one on the name guard
    /// against two requests creating it at once; the loser re-reads the winner's row or draws another name.
    /// </summary>
    private async Task<CommunityPseudonym> GetOrCreatePseudonymAsync(string userId, CancellationToken cancellationToken)
    {
        CommunityPseudonym? existing = await _dbContext.CommunityPseudonyms
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        for (int attempt = 1; ; attempt++)
        {
            var pseudonym = new CommunityPseudonym
            {
                UserId = userId,
                Name = await PickUnusedNameAsync(null, cancellationToken),
            };
            _dbContext.CommunityPseudonyms.Add(pseudonym);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Pseudonym created: UserId={UserId}", userId);
                }

                return pseudonym;
            }
            catch (DbUpdateException) when (attempt < PseudonymSaveAttempts)
            {
                _dbContext.Entry(pseudonym).State = EntityState.Detached;
                existing = await _dbContext.CommunityPseudonyms
                    .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
                if (existing is not null)
                {
                    return existing;
                }
            }
        }
    }

    /// <summary>
    /// A name no Account holds. Soft-deleted rows count too: an Account in its Grace Period keeps its name, so it
    /// gets it back when it cancels. The lists make a few thousand names and this app has far fewer Accounts, so the
    /// taken names are read in one query.
    /// </summary>
    private async Task<string> PickUnusedNameAsync(string? current, CancellationToken cancellationToken)
    {
        List<string> issued = await _dbContext.CommunityPseudonyms
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);
        var taken = new HashSet<string>(issued, StringComparer.OrdinalIgnoreCase);
        if (current is not null)
        {
            taken.Add(current);
        }

        return PseudonymGenerator.Pick(taken, Random.Shared);
    }

    private Task<List<CommunityPoll>> ActivePollsAsync(CancellationToken cancellationToken) =>
        _dbContext.CommunityPolls
            .AsNoTracking()
            .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
            .Include(p => p.Votes)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    private static CommunityPollDto MapPollToDto(CommunityPoll p, string userId)
    {
        int totalVotes = p.Votes.Count;
        CommunityPollVote? userVote = userId != null ? p.Votes.FirstOrDefault(v => v.UserId == userId) : null;

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
}
