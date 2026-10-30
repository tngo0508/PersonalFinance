using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Service implementation of the Guild Hall (the Community Feature): Pseudonyms, Polls, Comments, Strategies,
/// Reactions, and Challenges with their private Check-ins.
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

    public async Task<CommunityResponseDto> GetCommunityAsync(string userId, string? category = null,
        CancellationToken cancellationToken = default)
    {
        CommunityPseudonym pseudonym = await GetOrCreatePseudonymAsync(userId, cancellationToken);
        List<OpenPollRow> polls = await OpenPollsQuery(userId).ToListAsync(cancellationToken);
        List<CommunityStrategyDto> strategies = await BoardStrategiesQuery(userId, category)
            .Take(CommunityStrategyLimits.BoardSize)
            .ToListAsync(cancellationToken);

        List<int> strategyIds = strategies
            .Where(s => s.Status == CommunityStrategyStatus.Approved)
            .Select(s => s.Id)
            .ToList();
        List<int> pollIds = polls.Select(p => p.Id).ToList();
        List<CommunityCommentDto> comments = await ThreadCommentsQuery(userId, strategyIds, pollIds)
            .ToListAsync(cancellationToken);
        ILookup<int, CommunityCommentDto> byStrategy = comments
            .Where(c => c.StrategyId.HasValue)
            .ToLookup(c => c.StrategyId!.Value);
        ILookup<int, CommunityCommentDto> byPoll = comments
            .Where(c => c.PollId.HasValue)
            .ToLookup(c => c.PollId!.Value);

        foreach (CommunityStrategyDto strategy in strategies)
        {
            strategy.Comments = byStrategy[strategy.Id].ToList();
        }

        return new CommunityResponseDto
        {
            Pseudonym = pseudonym.Name,
            ActivePolls = polls.Select(p =>
            {
                CommunityPollDto dto = MapPollToDto(p);
                dto.Comments = byPoll[p.Id].ToList();
                return dto;
            }).ToList(),
            Strategies = strategies,
            Challenges = await ChallengeViewsAsync(userId, await ListedChallengesQuery().ToListAsync(cancellationToken),
                cancellationToken),
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
        List<OpenPollRow> updatedPolls = await OpenPollsQuery(userId).ToListAsync(cancellationToken);
        return new CommunityResponseDto
        {
            ActivePolls = updatedPolls.Select(MapPollToDto).ToList(),
        };
    }

    public async Task<CommunityCommentDto> AddCommentAsync(string userId, CommunityCommentRequestDto request, CancellationToken cancellationToken = default)
    {
        string trimmedText = request.Text.Trim();
        if (string.IsNullOrEmpty(trimmedText))
        {
            throw new ApiErrorException(ApiErrorKeys.Community.CommentEmpty, "Comment text cannot be empty.", "Text");
        }

        if (trimmedText.Length > CommunityCommentLimits.TextMaxLength)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.CommentTooLong, "Comment text is too long (max 2000 characters).", "Text",
                ErrorArgs.Number("max", CommunityCommentLimits.TextMaxLength));
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

        int? strategyId = null;
        if (request.StrategyId.HasValue && request.StrategyId.Value > 0)
        {
            bool approved = await _dbContext.CommunityStrategies.AnyAsync(
                s => s.Id == request.StrategyId.Value && s.Status == CommunityStrategyStatus.Approved,
                cancellationToken);
            if (!approved)
            {
                throw new ApiErrorException(ApiErrorKeys.Community.InvalidStrategy, "No approved Strategy matches this id.");
            }
            strategyId = request.StrategyId.Value;
        }

        CommunityPseudonym author = await GetOrCreatePseudonymAsync(userId, cancellationToken);
        var comment = new CommunityComment
        {
            UserId = userId,
            Text = trimmedText,
            PollId = pollId,
            StrategyId = strategyId,
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
            StrategyId = comment.StrategyId,
        };
    }

    public async Task<CommunityReactionResultDto> ToggleReactionAsync(string userId, CommunityReactionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        int? strategyId = request.StrategyId is > 0 ? request.StrategyId : null;
        int? commentId = strategyId is null ? request.CommentId : null;
        if (strategyId is not null)
        {
            bool approved = await _dbContext.CommunityStrategies.AnyAsync(
                s => s.Id == strategyId && s.Status == CommunityStrategyStatus.Approved, cancellationToken);
            if (!approved)
            {
                throw new ApiErrorException(ApiErrorKeys.Community.InvalidStrategy, "No approved Strategy matches this id.",
                    statusCode: StatusCodes.Status404NotFound);
            }
        }
        else
        {
            bool shown = await _dbContext.CommunityComments.AnyAsync(
                c => c.Id == commentId && c.IsVisible
                     && (c.StrategyId == null || c.Strategy!.Status == CommunityStrategyStatus.Approved),
                cancellationToken);
            if (!shown)
            {
                throw new ApiErrorException(ApiErrorKeys.Community.CommentNotFound, "Comment not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
        }

        CommunityReaction? existing = await _dbContext.CommunityReactions.FirstOrDefaultAsync(
            r => r.UserId == userId && r.Kind == request.Kind && r.StrategyId == strategyId && r.CommentId == commentId,
            cancellationToken);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (existing is not null)
        {
            existing.IsDeleted = true;
            existing.DeletedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var reaction = new CommunityReaction
            {
                UserId = userId,
                Kind = request.Kind,
                StrategyId = strategyId,
                CommentId = commentId,
                CreatedAt = now,
            };
            _dbContext.CommunityReactions.Add(reaction);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The same Account's other request (a double click) gave this Reaction first: it is held either way.
                _dbContext.Entry(reaction).State = EntityState.Detached;
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Reaction toggled: UserId={UserId}, Kind={Kind}, StrategyId={StrategyId}, CommentId={CommentId}",
                userId, request.Kind, strategyId, commentId);
        }

        return new CommunityReactionResultDto
        {
            Kind = request.Kind,
            ByViewer = existing is null,
            Count = strategyId is null
                ? null
                : await _dbContext.CommunityReactions.CountAsync(
                    r => r.StrategyId == strategyId && r.Kind == request.Kind, cancellationToken),
        };
    }

    public async Task<CommunityStrategyDto> SubmitStrategyAsync(string userId, CommunityStrategyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        CommunityPseudonym author = await GetOrCreatePseudonymAsync(userId, cancellationToken);
        var strategy = new CommunityStrategy
        {
            UserId = userId,
            Category = request.Category.Trim(),
            Title = request.Title.Trim(),
            Tried = request.Tried.Trim(),
            Happened = request.Happened.Trim(),
            Keeping = request.Keeping.Trim(),
            Status = CommunityStrategyStatus.Pending,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _dbContext.CommunityStrategies.Add(strategy);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Strategy submitted for review: StrategyId={StrategyId}, UserId={UserId}", strategy.Id, userId);
        }

        return new CommunityStrategyDto
        {
            Id = strategy.Id,
            AuthorPseudonym = author.Name,
            IsOwn = true,
            Category = strategy.Category,
            Title = strategy.Title,
            Tried = strategy.Tried,
            Happened = strategy.Happened,
            Keeping = strategy.Keeping,
            Status = strategy.Status,
            CreatedAtUtc = strategy.CreatedAt,
        };
    }

    public async Task<List<CommunityStrategyReviewItemDto>> ListStrategiesForReviewAsync(CommunityStrategyStatus status,
        CancellationToken cancellationToken = default)
    {
        List<CommunityStrategyReviewItemDto> items = await ReviewQueueQuery(status).ToListAsync(cancellationToken);
        foreach (CommunityStrategyReviewItemDto item in items)
        {
            item.HasAmount = MoneyAmountRule.ContainsAmount(item.Title, item.Tried, item.Happened, item.Keeping);
        }

        return items;
    }

    public async Task<bool> ReviewStrategyAsync(int strategyId, CommunityStrategyStatus status,
        CancellationToken cancellationToken = default)
    {
        CommunityStrategy? strategy = await _dbContext.CommunityStrategies
            .FirstOrDefaultAsync(s => s.Id == strategyId, cancellationToken);
        if (strategy is null)
        {
            return false;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        strategy.Status = status;
        strategy.ReviewedAt = now;
        strategy.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Strategy reviewed: StrategyId={StrategyId}, Status={Status}", strategyId, status);
        }

        return true;
    }

    public async Task<List<CommunityCommentReviewItemDto>> ListCommentsForReviewAsync(
        CancellationToken cancellationToken = default)
    {
        List<CommunityCommentReviewItemDto> items = await CommentReviewQuery()
            .Take(CommunityCommentLimits.ReviewListSize)
            .ToListAsync(cancellationToken);
        foreach (CommunityCommentReviewItemDto item in items)
        {
            item.HasAmount = MoneyAmountRule.ContainsAmount(item.Text);
        }

        return items;
    }

    public async Task<bool> SetCommentHiddenAsync(int commentId, bool isHidden, CancellationToken cancellationToken = default)
    {
        CommunityComment? comment = await _dbContext.CommunityComments
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return false;
        }

        comment.IsVisible = !isHidden;
        comment.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Comment moderated: CommentId={CommentId}, Hidden={Hidden}", commentId, isHidden);
        }

        return true;
    }

    public Task<List<CommunityPollAdminItemDto>> ListPollsAsync(CancellationToken cancellationToken = default) =>
        PollAdminQuery().ToListAsync(cancellationToken);

    public async Task<CommunityPollAdminItemDto> CreatePollAsync(CommunityPollRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string? description = request.Description?.Trim();
        var poll = new CommunityPoll
        {
            PollCode = Guid.NewGuid().ToString("N"),
            Question = request.Question.Trim(),
            Description = string.IsNullOrEmpty(description) ? null : description,
            Category = request.Category.Trim(),
            IsActive = false,
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
        };
        List<string> options = request.Options
            .Select(o => o.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (int i = 0; i < options.Count; i++)
        {
            poll.Options.Add(new CommunityPollOption { Text = options[i], Order = i + 1 });
        }

        _dbContext.CommunityPolls.Add(poll);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Poll written: PollId={PollId}", poll.Id);
        }

        return new CommunityPollAdminItemDto
        {
            Id = poll.Id,
            Question = poll.Question,
            Description = poll.Description,
            Category = poll.Category,
            IsOpen = poll.IsActive,
            CreatedAtUtc = poll.CreatedAtUtc,
            Options = options,
        };
    }

    public async Task<bool> SetPollOpenAsync(int pollId, bool isOpen, CancellationToken cancellationToken = default)
    {
        CommunityPoll? poll = await _dbContext.CommunityPolls.FirstOrDefaultAsync(p => p.Id == pollId, cancellationToken);
        if (poll is null)
        {
            return false;
        }

        poll.IsActive = isOpen;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Poll status changed: PollId={PollId}, IsOpen={IsOpen}", pollId, isOpen);
        }

        return true;
    }

    public async Task<CommunityChallengeDto> JoinChallengeAsync(string userId, int challengeId, string? timeZone,
        CancellationToken cancellationToken = default)
    {
        string zoneId = BrowserTimeZone.Resolve(timeZone);
        CommunityChallenge challenge = await NotEndedChallengeAsync(challengeId, zoneId, cancellationToken);
        CommunityChallengeMember? member = await _dbContext.CommunityChallengeMembers.FirstOrDefaultAsync(
            m => m.UserId == userId && m.ChallengeId == challengeId, cancellationToken);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (member is null)
        {
            var joined = new CommunityChallengeMember
            {
                UserId = userId,
                ChallengeId = challengeId,
                TimeZoneId = zoneId,
                CreatedAt = now,
            };
            _dbContext.CommunityChallengeMembers.Add(joined);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The same Account's other request (a double click) joined first: it is a member either way.
                _dbContext.Entry(joined).State = EntityState.Detached;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Challenge joined: ChallengeId={ChallengeId}, UserId={UserId}", challengeId, userId);
            }
        }
        else
        {
            await RememberZoneAsync(member, zoneId, now, cancellationToken);
        }

        return (await ChallengeViewsAsync(userId, [challenge], cancellationToken)).Single();
    }

    public async Task<CommunityChallengeDto> CheckInAsync(string userId, int challengeId, string? timeZone,
        CancellationToken cancellationToken = default)
    {
        string zoneId = BrowserTimeZone.Resolve(timeZone);
        CommunityChallenge challenge = await NotEndedChallengeAsync(challengeId, zoneId, cancellationToken);
        CommunityChallengeMember? member = await _dbContext.CommunityChallengeMembers.FirstOrDefaultAsync(
            m => m.UserId == userId && m.ChallengeId == challengeId, cancellationToken);
        if (member is null)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.ChallengeNotJoined, "Join the Challenge to check in.",
                statusCode: StatusCodes.Status409Conflict);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        await RememberZoneAsync(member, zoneId, utcNow.UtcDateTime, cancellationToken);
        DateOnly? period = ChallengeCalendar.CurrentPeriod(challenge.Period, challenge.StartDate, challenge.EndDate,
            BrowserTimeZone.Today(utcNow, BrowserTimeZone.Zone(zoneId)));
        if (period is null)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.ChallengeNotStarted, "The Challenge has not started yet.",
                statusCode: StatusCodes.Status409Conflict);
        }

        bool already = await _dbContext.CommunityCheckIns.AnyAsync(
            c => c.UserId == userId && c.ChallengeId == challengeId && c.PeriodStart == period.Value, cancellationToken);
        if (!already)
        {
            var checkIn = new CommunityCheckIn
            {
                UserId = userId,
                ChallengeId = challengeId,
                PeriodStart = period.Value,
                CreatedAt = utcNow.UtcDateTime,
            };
            _dbContext.CommunityCheckIns.Add(checkIn);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The same Account's other request (a double click) checked in first: the period is kept either way.
                _dbContext.Entry(checkIn).State = EntityState.Detached;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Checked in: ChallengeId={ChallengeId}, UserId={UserId}, PeriodStart={PeriodStart}",
                    challengeId, userId, period.Value);
            }
        }

        return (await ChallengeViewsAsync(userId, [challenge], cancellationToken)).Single();
    }

    public Task<List<CommunityChallengeAdminItemDto>> ListChallengesAsync(CancellationToken cancellationToken = default) =>
        ChallengeAdminQuery().ToListAsync(cancellationToken);

    public async Task<CommunityChallengeAdminItemDto> CreateChallengeAsync(CommunityChallengeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var challenge = new CommunityChallenge
        {
            Title = request.Title.Trim(),
            Habit = request.Habit.Trim(),
            Period = request.Period,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
        };
        _dbContext.CommunityChallenges.Add(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Challenge written: ChallengeId={ChallengeId}", challenge.Id);
        }

        return new CommunityChallengeAdminItemDto
        {
            Id = challenge.Id,
            Title = challenge.Title,
            Habit = challenge.Habit,
            Period = challenge.Period,
            StartDate = challenge.StartDate,
            EndDate = challenge.EndDate,
            CreatedAtUtc = challenge.CreatedAtUtc,
        };
    }

    // =========================================================================
    // Queries (internal so tests can check they translate to Azure SQL)
    // =========================================================================

    /// <summary>
    /// The Challenges that may still be running somewhere: those ending no earlier than yesterday in UTC, since local
    /// dates run up to a day behind it. Each viewer's own time zone then decides which have ended.
    /// </summary>
    internal IQueryable<CommunityChallenge> ListedChallengesQuery()
    {
        DateOnly yesterday = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime).AddDays(-1);
        return _dbContext.CommunityChallenges.AsNoTracking()
            .Where(c => c.EndDate >= yesterday)
            .OrderBy(c => c.StartDate)
            .ThenBy(c => c.Id);
    }

    /// <summary>
    /// How many Accounts checked in to each of <paramref name="challengeIds"/> for each period starting on or after
    /// <paramref name="from"/>. Counts only: no Account id leaves this query.
    /// </summary>
    internal IQueryable<CheckInCountRow> CheckInCountsQuery(IReadOnlyCollection<int> challengeIds, DateOnly from) =>
        _dbContext.CommunityCheckIns.AsNoTracking()
            .Where(c => challengeIds.Contains(c.ChallengeId) && c.PeriodStart >= from)
            .GroupBy(c => new { c.ChallengeId, c.PeriodStart })
            .Select(g => new CheckInCountRow
            {
                ChallengeId = g.Key.ChallengeId,
                PeriodStart = g.Key.PeriodStart,
                Count = g.Count(),
            });

    /// <summary>How many Accounts joined each of <paramref name="challengeIds"/>. Counts only, like the Check-ins'.</summary>
    internal IQueryable<CheckInCountRow> MemberCountsQuery(IReadOnlyCollection<int> challengeIds) =>
        _dbContext.CommunityChallengeMembers.AsNoTracking()
            .Where(m => challengeIds.Contains(m.ChallengeId))
            .GroupBy(m => m.ChallengeId)
            .Select(g => new CheckInCountRow { ChallengeId = g.Key, Count = g.Count() });

    /// <summary>
    /// Every Challenge for the Admin list, newest start first, with how many Accounts joined it.
    /// </summary>
    internal IQueryable<CommunityChallengeAdminItemDto> ChallengeAdminQuery() =>
        _dbContext.CommunityChallenges.AsNoTracking()
            .OrderByDescending(c => c.StartDate)
            .ThenByDescending(c => c.Id)
            .Select(c => new CommunityChallengeAdminItemDto
            {
                Id = c.Id,
                Title = c.Title,
                Habit = c.Habit,
                Period = c.Period,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                CreatedAtUtc = c.CreatedAtUtc,
                MemberCount = _dbContext.CommunityChallengeMembers.Count(m => m.ChallengeId == c.Id),
            });

    /// <summary>
    /// The open Polls, newest first, each with its options in order, every option's number of votes and the option
    /// <paramref name="userId"/> voted for. The counts never leave the service: <see cref="MapPollToDto"/> turns them
    /// into shares, and only for a viewer who voted.
    /// </summary>
    internal IQueryable<OpenPollRow> OpenPollsQuery(string userId) =>
        _dbContext.CommunityPolls.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Select(p => new OpenPollRow
            {
                Id = p.Id,
                PollCode = p.PollCode,
                Question = p.Question,
                Description = p.Description,
                Category = p.Category,
                VotedOptionId = _dbContext.CommunityPollVotes
                    .Where(v => v.PollId == p.Id && v.UserId == userId)
                    .Select(v => v.OptionId)
                    .FirstOrDefault(),
                Options = p.Options
                    .OrderBy(o => o.Order)
                    .ThenBy(o => o.Id)
                    .Select(o => new OpenPollOptionRow { Id = o.Id, Text = o.Text, Votes = o.Votes.Count })
                    .ToList(),
            });

    /// <summary>
    /// Every Poll for the Admin list, open and closed, newest first, with its options in order and its number of votes.
    /// </summary>
    internal IQueryable<CommunityPollAdminItemDto> PollAdminQuery() =>
        _dbContext.CommunityPolls.AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Select(p => new CommunityPollAdminItemDto
            {
                Id = p.Id,
                Question = p.Question,
                Description = p.Description,
                Category = p.Category,
                IsOpen = p.IsActive,
                CreatedAtUtc = p.CreatedAtUtc,
                Options = p.Options.OrderBy(o => o.Order).ThenBy(o => o.Id).Select(o => o.Text).ToList(),
                VoteCount = p.Votes.Count,
            });

    /// <summary>
    /// The board as <paramref name="userId"/> sees it: Approved Strategies and the viewer's own in any status, only in
    /// <paramref name="category"/> when one is given. The viewer's Pending ones come first, then their Rejected ones,
    /// then the Approved ones, each newest first. Each carries its author's Pseudonym, null once the author is a
    /// Former member, and its Reaction counts; its Comments are filled in afterwards.
    /// </summary>
    internal IQueryable<CommunityStrategyDto> BoardStrategiesQuery(string userId, string? category)
    {
        IQueryable<CommunityStrategy> strategies = _dbContext.CommunityStrategies.AsNoTracking()
            .Where(s => s.Status == CommunityStrategyStatus.Approved || s.UserId == userId);
        if (!string.IsNullOrEmpty(category))
        {
            strategies = strategies.Where(s => s.Category == category);
        }

        return from strategy in strategies
               join author in _dbContext.CommunityPseudonyms.AsNoTracking() on strategy.UserId equals author.UserId into authors
               from author in authors.DefaultIfEmpty()
               orderby strategy.Status == CommunityStrategyStatus.Approved,
                   strategy.Status == CommunityStrategyStatus.Rejected,
                   strategy.CreatedAt descending,
                   strategy.Id descending
               select new CommunityStrategyDto
               {
                   Id = strategy.Id,
                   AuthorPseudonym = author == null ? null : author.Name,
                   IsOwn = strategy.UserId == userId,
                   Category = strategy.Category,
                   Title = strategy.Title,
                   Tried = strategy.Tried,
                   Happened = strategy.Happened,
                   Keeping = strategy.Keeping,
                   Status = strategy.Status,
                   CreatedAtUtc = strategy.CreatedAt,
                   HelpfulCount = _dbContext.CommunityReactions.Count(
                       r => r.StrategyId == strategy.Id && r.Kind == CommunityReactionKind.Helpful),
                   CheeringCount = _dbContext.CommunityReactions.Count(
                       r => r.StrategyId == strategy.Id && r.Kind == CommunityReactionKind.CheeringYouOn),
                   HelpfulByViewer = _dbContext.CommunityReactions.Any(
                       r => r.StrategyId == strategy.Id && r.UserId == userId && r.Kind == CommunityReactionKind.Helpful),
                   CheeringByViewer = _dbContext.CommunityReactions.Any(
                       r => r.StrategyId == strategy.Id && r.UserId == userId && r.Kind == CommunityReactionKind.CheeringYouOn),
               };
    }

    /// <summary>
    /// The Strategies in <paramref name="status"/> for the Admin queue, oldest first, without the amount flag (it is
    /// set in memory: the rule is a regular expression).
    /// </summary>
    internal IQueryable<CommunityStrategyReviewItemDto> ReviewQueueQuery(CommunityStrategyStatus status) =>
        from strategy in _dbContext.CommunityStrategies.AsNoTracking()
        where strategy.Status == status
        join author in _dbContext.CommunityPseudonyms.AsNoTracking() on strategy.UserId equals author.UserId into authors
        from author in authors.DefaultIfEmpty()
        orderby strategy.CreatedAt, strategy.Id
        select new CommunityStrategyReviewItemDto
        {
            Id = strategy.Id,
            AuthorPseudonym = author == null ? null : author.Name,
            Category = strategy.Category,
            Title = strategy.Title,
            Tried = strategy.Tried,
            Happened = strategy.Happened,
            Keeping = strategy.Keeping,
            Status = strategy.Status,
            CreatedAtUtc = strategy.CreatedAt,
            ReviewedAtUtc = strategy.ReviewedAt,
        };

    /// <summary>
    /// The shown Comments on the Strategies in <paramref name="strategyIds"/> and the polls in
    /// <paramref name="pollIds"/>, oldest first so each thread reads in order, each with its author's Pseudonym (null
    /// for a Former member) and whether the viewer reacted. No count of Reactions: a Comment shows none.
    /// </summary>
    internal IQueryable<CommunityCommentDto> ThreadCommentsQuery(string userId, IReadOnlyCollection<int> strategyIds,
        IReadOnlyCollection<int> pollIds) =>
        from comment in _dbContext.CommunityComments.AsNoTracking()
        where comment.IsVisible
              && ((comment.StrategyId != null && strategyIds.Contains(comment.StrategyId.Value))
                  || (comment.PollId != null && pollIds.Contains(comment.PollId.Value)))
        join author in _dbContext.CommunityPseudonyms.AsNoTracking() on comment.UserId equals author.UserId into authors
        from author in authors.DefaultIfEmpty()
        orderby comment.CreatedAt, comment.Id
        select new CommunityCommentDto
        {
            Id = comment.Id,
            AuthorPseudonym = author == null ? null : author.Name,
            IsOwn = comment.UserId == userId,
            Text = comment.Text,
            CreatedAtUtc = comment.CreatedAt,
            HelpfulByViewer = _dbContext.CommunityReactions.Any(
                r => r.CommentId == comment.Id && r.UserId == userId && r.Kind == CommunityReactionKind.Helpful),
            CheeringByViewer = _dbContext.CommunityReactions.Any(
                r => r.CommentId == comment.Id && r.UserId == userId && r.Kind == CommunityReactionKind.CheeringYouOn),
            PollId = comment.PollId,
            StrategyId = comment.StrategyId,
        };

    /// <summary>
    /// Every Comment for the Admin moderation list, hidden ones included, newest first, with what it replies to and
    /// without the amount flag (set in memory, like the Strategy queue's).
    /// </summary>
    internal IQueryable<CommunityCommentReviewItemDto> CommentReviewQuery() =>
        from comment in _dbContext.CommunityComments.AsNoTracking()
        join author in _dbContext.CommunityPseudonyms.AsNoTracking() on comment.UserId equals author.UserId into authors
        from author in authors.DefaultIfEmpty()
        orderby comment.CreatedAt descending, comment.Id descending
        select new CommunityCommentReviewItemDto
        {
            Id = comment.Id,
            AuthorPseudonym = author == null ? null : author.Name,
            Text = comment.Text,
            CreatedAtUtc = comment.CreatedAt,
            IsHidden = !comment.IsVisible,
            StrategyTitle = comment.Strategy == null ? null : comment.Strategy.Title,
            PollQuestion = comment.Poll == null ? null : comment.Poll.Question,
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

    /// <summary>
    /// The Challenge <paramref name="challengeId"/>, unless it does not exist or has ended in <paramref name="zoneId"/>,
    /// which throws a 404 <c>ChallengeNotFound</c>.
    /// </summary>
    private async Task<CommunityChallenge> NotEndedChallengeAsync(int challengeId, string zoneId,
        CancellationToken cancellationToken)
    {
        CommunityChallenge? challenge = await _dbContext.CommunityChallenges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == challengeId, cancellationToken);
        DateOnly today = BrowserTimeZone.Today(_timeProvider.GetUtcNow(), BrowserTimeZone.Zone(zoneId));
        if (challenge is null || today > challenge.EndDate)
        {
            throw new ApiErrorException(ApiErrorKeys.Community.ChallengeNotFound, "No running Challenge matches this id.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return challenge;
    }

    /// <summary>Stores the time zone the browser last sent with a membership, when it changed.</summary>
    private async Task RememberZoneAsync(CommunityChallengeMember member, string zoneId, DateTime now,
        CancellationToken cancellationToken)
    {
        if (member.TimeZoneId == zoneId)
        {
            return;
        }

        member.TimeZoneId = zoneId;
        member.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The <paramref name="challenges"/> as <paramref name="userId"/> sees them, leaving out any that has ended in the
    /// viewer's time zone (the membership's, or UTC for a Challenge it has not joined): running ones first, then the
    /// ones about to start. Only the viewer's own Check-ins are read, for its streak; everyone else's only as counts.
    /// </summary>
    private async Task<List<CommunityChallengeDto>> ChallengeViewsAsync(string userId,
        List<CommunityChallenge> challenges, CancellationToken cancellationToken)
    {
        if (challenges.Count == 0)
        {
            return [];
        }

        List<int> ids = challenges.Select(c => c.Id).ToList();
        Dictionary<int, string> zones = await _dbContext.CommunityChallengeMembers.AsNoTracking()
            .Where(m => m.UserId == userId && ids.Contains(m.ChallengeId))
            .ToDictionaryAsync(m => m.ChallengeId, m => m.TimeZoneId, cancellationToken);
        ILookup<int, DateOnly> own = (await _dbContext.CommunityCheckIns.AsNoTracking()
                .Where(c => c.UserId == userId && ids.Contains(c.ChallengeId))
                .Select(c => new { c.ChallengeId, c.PeriodStart })
                .ToListAsync(cancellationToken))
            .ToLookup(c => c.ChallengeId, c => c.PeriodStart);

        Dictionary<int, int> members = await MemberCountsQuery(ids)
            .ToDictionaryAsync(r => r.ChallengeId, r => r.Count, cancellationToken);

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        var shown = challenges
            .Select(challenge =>
            {
                bool isMember = zones.TryGetValue(challenge.Id, out string? zoneId);
                DateOnly today = BrowserTimeZone.Today(utcNow, BrowserTimeZone.Zone(zoneId ?? BrowserTimeZone.Utc));
                return new
                {
                    Challenge = challenge,
                    IsMember = isMember,
                    Ended = today > challenge.EndDate,
                    Current = ChallengeCalendar.CurrentPeriod(challenge.Period, challenge.StartDate, challenge.EndDate,
                        today),
                };
            })
            .Where(c => !c.Ended)
            .ToList();

        List<DateOnly> currents = shown.Where(c => c.Current.HasValue).Select(c => c.Current!.Value).ToList();
        Dictionary<(int, DateOnly), int> counts = currents.Count == 0
            ? []
            : (await CheckInCountsQuery(ids, currents.Min()).ToListAsync(cancellationToken))
            .ToDictionary(r => (r.ChallengeId, r.PeriodStart), r => r.Count);

        return shown
            .OrderBy(c => c.Current is null)
            .ThenBy(c => c.Challenge.StartDate)
            .ThenBy(c => c.Challenge.Id)
            .Select(c =>
            {
                var kept = new HashSet<DateOnly>(own[c.Challenge.Id]);
                return new CommunityChallengeDto
                {
                    Id = c.Challenge.Id,
                    Title = c.Challenge.Title,
                    Habit = c.Challenge.Habit,
                    Period = c.Challenge.Period,
                    StartDate = c.Challenge.StartDate,
                    EndDate = c.Challenge.EndDate,
                    CurrentPeriodStart = c.Current,
                    IsMember = c.IsMember,
                    CheckedInThisPeriod = c.Current.HasValue && kept.Contains(c.Current.Value),
                    Streak = c.Current.HasValue
                        ? ChallengeCalendar.Streak(c.Challenge.Period, c.Challenge.StartDate, c.Current.Value, kept)
                        : 0,
                    CheckedInCount = c.Current.HasValue
                        ? counts.GetValueOrDefault((c.Challenge.Id, c.Current.Value))
                        : 0,
                    MemberCount = members.GetValueOrDefault(c.Challenge.Id),
                };
            })
            .ToList();
    }

    /// <summary>
    /// An open Poll as the viewer sees it. Each option's share is a whole percentage, the shares adding up to 100 by
    /// the largest remainder (ties go to the earlier option); before the viewer votes, no option has one.
    /// </summary>
    private static CommunityPollDto MapPollToDto(OpenPollRow p)
    {
        bool hasVoted = p.VotedOptionId is not null;
        int total = p.Options.Sum(o => o.Votes);
        int[] shares = p.Options.Select(o => total == 0 ? 0 : o.Votes * 100 / total).ToArray();
        if (total > 0)
        {
            IEnumerable<int> byRemainder = Enumerable.Range(0, shares.Length)
                .OrderByDescending(i => p.Options[i].Votes * 100 % total)
                .ThenBy(i => i)
                .Take(100 - shares.Sum());
            foreach (int i in byRemainder)
            {
                shares[i]++;
            }
        }

        return new CommunityPollDto
        {
            Id = p.PollCode,
            Question = p.Question,
            Description = p.Description ?? string.Empty,
            Category = p.Category,
            IsActive = true,
            HasVoted = hasVoted,
            VotedOptionId = p.VotedOptionId,
            Options = p.Options.Select((o, i) => new CommunityPollOptionDto
            {
                Id = o.Id,
                Text = o.Text,
                Percentage = hasVoted ? shares[i] : null,
            }).ToList(),
        };
    }

    /// <summary>An open Poll read by <see cref="OpenPollsQuery"/>, before its counts become shares.</summary>
    internal sealed class OpenPollRow
    {
        public int Id { get; init; }
        public string PollCode { get; init; } = string.Empty;
        public string Question { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Category { get; init; } = string.Empty;
        public int? VotedOptionId { get; init; }
        public List<OpenPollOptionRow> Options { get; init; } = new();
    }

    /// <summary>
    /// How many Accounts checked in to a Challenge for one period (<see cref="CheckInCountsQuery"/>), or joined it
    /// (<see cref="MemberCountsQuery"/>, with no period).
    /// </summary>
    internal sealed class CheckInCountRow
    {
        public int ChallengeId { get; init; }
        public DateOnly PeriodStart { get; init; }
        public int Count { get; init; }
    }

    /// <summary>An option of an <see cref="OpenPollRow"/> with its number of votes.</summary>
    internal sealed class OpenPollOptionRow
    {
        public int Id { get; init; }
        public string Text { get; init; } = string.Empty;
        public int Votes { get; init; }
    }
}
