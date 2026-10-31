using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Learning.ContentFiles;
using PersonalFinance.ApiService.Features.Learning.Domain;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// <see cref="IJourneyService"/> over EF Core and <see cref="ILearningContent"/>. The Lessons and their order come
/// from the content folder; the database holds only which ones the Account finished, when and in which zone, and what
/// it said about each Review card. Status is worked out on every read from the injected <see cref="TimeProvider"/>,
/// so a late return finds the next Lesson simply open.
/// </summary>
internal sealed class JourneyService : IJourneyService
{
    /// <summary>How many cards a Review shows.</summary>
    private const int CardsPerReview = 3;

    /// <summary>A card retires after this many "Knew it" in a row.</summary>
    private const int RetireAfterKnewIt = 2;

    private readonly ILearningContent _content;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public JourneyService(AppDbContext db, ILearningContent content, TimeProvider timeProvider)
    {
        _db = db;
        _content = content;
        _timeProvider = timeProvider;
    }

    public async Task<JourneyStateDto> GetStateAsync(string userId, string? language,
        CancellationToken cancellationToken = default)
    {
        List<LessonCompletion> completions = await CompletionsQuery(userId).ToListAsync(cancellationToken);
        bool? show = await ShowOnDashboardQuery(userId).FirstOrDefaultAsync(cancellationToken);
        return BuildState(completions, show ?? true, language);
    }

    public async Task<JourneyStateDto?> FinishLessonAsync(string userId, string lessonSlug,
        FinishLessonRequest request, string? language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_content.FindLesson(lessonSlug, LearningContentFiles.English) is null)
        {
            return null;
        }

        List<LessonCompletion> completions = await CompletionsQuery(userId).ToListAsync(cancellationToken);
        bool show = await ShowOnDashboardQuery(userId).FirstOrDefaultAsync(cancellationToken) ?? true;
        JourneyStateDto before = BuildState(completions, show, language);
        LessonStatus status = before.Lessons.Single(l => l.Slug == lessonSlug).Status;
        if (status == LessonStatus.Finished)
        {
            return before;
        }

        if (status == LessonStatus.Locked)
        {
            throw new ApiErrorException(ApiErrorKeys.Learning.LessonLocked,
                "This Lesson is not open yet. Finish the one before it first, then come back after midnight.",
                statusCode: StatusCodes.Status409Conflict);
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        var completion = new LessonCompletion
        {
            UserId = userId,
            LessonSlug = lessonSlug,
            FinishedAt = now,
            TimeZoneId = JourneyClock.ResolveZoneId(request.TimeZone),
            CreatedAt = now
        };
        _db.LessonCompletions.Add(completion);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A second finish of the same Lesson raced this one: the unique index kept the first, which is the one
            // that dates the next Lesson, so the Journey is simply re-read.
            _db.ChangeTracker.Clear();
            return await GetStateAsync(userId, language, cancellationToken);
        }

        completions.Add(completion);
        return BuildState(completions, show, language);
    }

    public async Task<ReviewDto> GetReviewAsync(string userId, string? language,
        CancellationToken cancellationToken = default)
    {
        string code = LanguageOrEnglish(language);
        IReadOnlyList<Lesson> lessons = _content.GetLessons(code);
        HashSet<string> finished = (await CompletionsQuery(userId).Select(c => c.LessonSlug).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        if (finished.Count == 0 || lessons.All(l => finished.Contains(l.Slug)))
        {
            return new ReviewDto();
        }

        Dictionary<string, ReviewCardState> states = (await CardStatesQuery(userId).ToListAsync(cancellationToken))
            .ToDictionary(s => s.TermSlug, StringComparer.Ordinal);
        List<ReviewCardDto> cards = lessons
            .Where(l => finished.Contains(l.Slug))
            .SelectMany((lesson, lessonIndex) => lesson.Terms.Select((slug, termIndex) =>
                (lesson.Slug, Term: slug, LessonIndex: lessonIndex, TermIndex: termIndex)))
            .Select(card => (card, state: states.GetValueOrDefault(card.Term)))
            .Where(c => c.state is null || c.state.KnewItStreak < RetireAfterKnewIt)
            .OrderBy(c => c.state is { KnewIt: false } ? 0 : 1)
            .ThenBy(c => c.state?.LastShownAt ?? DateTime.MinValue)
            .ThenBy(c => c.card.LessonIndex)
            .ThenBy(c => c.card.TermIndex)
            .Select(c => (c.card.Slug, entry: _content.FindTerm(c.card.Term, code)))
            .Where(c => c.entry is not null)
            .Take(CardsPerReview)
            .Select(c => new ReviewCardDto
            {
                Slug = c.entry!.Slug,
                Term = c.entry.Term,
                EnglishTerm = _content.FindTerm(c.entry.Slug, LearningContentFiles.English)?.Term ?? c.entry.Term,
                OneLine = c.entry.OneLine,
                LessonSlug = c.Slug
            })
            .ToList();
        return new ReviewDto { Cards = cards };
    }

    public async Task<bool> MarkReviewCardAsync(string userId, string termSlug, MarkReviewCardRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<string> finished = await CompletionsQuery(userId).Select(c => c.LessonSlug).ToListAsync(cancellationToken);
        bool taught = _content.GetLessons(LearningContentFiles.English)
            .Any(l => finished.Contains(l.Slug, StringComparer.Ordinal) && l.Terms.Contains(termSlug, StringComparer.Ordinal));
        if (!taught)
        {
            return false;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        ReviewCardState? state = await _db.ReviewCardStates
            .Where(s => s.UserId == userId && s.TermSlug == termSlug)
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new ReviewCardState { UserId = userId, TermSlug = termSlug, CreatedAt = now };
            _db.ReviewCardStates.Add(state);
        }
        else
        {
            state.UpdatedAt = now;
        }

        state.KnewIt = request.KnewIt;
        state.KnewItStreak = request.KnewIt ? state.KnewItStreak + 1 : 0;
        state.LastShownAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetShowOnDashboardAsync(string userId, bool show, CancellationToken cancellationToken = default)
    {
        UserPreference? preference = await _db.UserPreferences
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (preference is null)
        {
            // No Language chosen yet: the row says so with an empty code, as the Web's preference service does.
            _db.UserPreferences.Add(new UserPreference { UserId = userId, ShowJourneyOnDashboard = show, CreatedAt = now });
        }
        else if (preference.ShowJourneyOnDashboard != show)
        {
            preference.ShowJourneyOnDashboard = show;
            preference.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The Account's Lesson completions, oldest first. Internal so tests can check it translates to Azure SQL.</summary>
    internal IQueryable<LessonCompletion> CompletionsQuery(string userId) =>
        _db.LessonCompletions.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.FinishedAt).ThenBy(c => c.Id);

    /// <summary>The Account's Review card states. Internal so tests can check it translates to Azure SQL.</summary>
    internal IQueryable<ReviewCardState> CardStatesQuery(string userId) =>
        _db.ReviewCardStates.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Id);

    /// <summary>The Account's Dashboard preference, or none. Internal so tests can check it translates to Azure SQL.</summary>
    internal IQueryable<bool?> ShowOnDashboardQuery(string userId) =>
        _db.UserPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .Select(p => (bool?)p.ShowJourneyOnDashboard);

    /// <summary>
    /// Walks the Lessons by day. A finished Lesson dates the next one's opening at the local midnight after its
    /// finish; the first unfinished Lesson is open once that moment has passed, and every Lesson after it is locked
    /// without a date, since nothing dates it yet.
    /// </summary>
    private JourneyStateDto BuildState(IReadOnlyList<LessonCompletion> completions, bool showOnDashboard,
        string? language)
    {
        IReadOnlyList<Lesson> lessons = _content.GetLessons(LanguageOrEnglish(language));
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        Dictionary<string, LessonCompletion> finished = completions
            .GroupBy(c => c.LessonSlug, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.FinishedAt).ThenBy(c => c.Id).First(), StringComparer.Ordinal);

        var state = new JourneyStateDto { ShowOnDashboard = showOnDashboard };
        DateTime? nextOpensAt = null;
        bool previousFinished = true;
        foreach (Lesson lesson in lessons)
        {
            var progress = new LessonProgressDto
            {
                Slug = lesson.Slug,
                Day = lesson.Day,
                Title = lesson.Title,
                Keepsake = new KeepsakeDto { Symbol = lesson.Keepsake.Symbol, Name = lesson.Keepsake.Name }
            };
            if (finished.TryGetValue(lesson.Slug, out LessonCompletion? completion))
            {
                progress.Status = LessonStatus.Finished;
                progress.FinishedAtUtc = DateTime.SpecifyKind(completion.FinishedAt, DateTimeKind.Utc);
                nextOpensAt = JourneyClock.NextLocalMidnightUtc(completion.FinishedAt, JourneyClock.Zone(completion.TimeZoneId));
                previousFinished = true;
            }
            else if (previousFinished && (nextOpensAt is null || nextOpensAt <= now))
            {
                progress.Status = LessonStatus.Open;
                previousFinished = false;
            }
            else
            {
                progress.Status = LessonStatus.Locked;
                progress.OpensAtUtc = previousFinished ? nextOpensAt : null;
                previousFinished = false;
            }

            state.Lessons.Add(progress);
        }

        state.Started = state.Lessons.Any(l => l.Status == LessonStatus.Finished);
        state.Finished = state.Lessons.Count > 0 && state.Lessons.All(l => l.Status == LessonStatus.Finished);
        return state;
    }

    private static string LanguageOrEnglish(string? language) =>
        string.IsNullOrWhiteSpace(language) ? LearningContentFiles.English : language;
}
