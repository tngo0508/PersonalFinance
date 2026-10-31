using System.Net;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Learning;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// What a learner sees of the Journey: which Lesson is open and when the next one opens, with the fixture content's
/// two Lessons (<c>build-a-cushion</c> on Day 1 teaching emergency-fund and compound-interest, <c>borrow-wisely</c>
/// on Day 2 teaching apr and credit-score) and a frozen clock.
/// </summary>
public class JourneyServiceTests
{
    private const string UserId = "learner";
    private const string Saigon = "Asia/Ho_Chi_Minh";

    // 22:30 UTC on 1 November 2026 is 05:30 on 2 November in Ho Chi Minh City; the next midnight there is 17:00 UTC.
    private static readonly DateTimeOffset Finish = new(2026, 11, 1, 22, 30, 0, TimeSpan.Zero);
    private static readonly DateTime NextSaigonMidnight = new(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"Journey_{Guid.NewGuid():N}").Options);

    private static JourneyService Service(AppDbContext db, FixedTimeProvider clock) =>
        new(db, LearningContent.Load(LearningContentFolders.Fixture), clock);

    [Fact]
    public async Task GetState_NewAccount_HasDayOneOpen_AndTheRestLockedWithoutADate()
    {
        await using AppDbContext db = CreateContext();

        JourneyStateDto state = await Service(db, new FixedTimeProvider(Finish)).GetStateAsync(UserId, "en");

        Assert.False(state.Started);
        Assert.False(state.Finished);
        Assert.True(state.ShowOnDashboard);
        Assert.Equal([("build-a-cushion", 1, LessonStatus.Open), ("borrow-wisely", 2, LessonStatus.Locked)],
            state.Lessons.Select(l => (l.Slug, l.Day, l.Status)));
        Assert.All(state.Lessons, l => Assert.Null(l.OpensAtUtc));
        Assert.Equal(("🫙", "Coin jar"), (state.Lessons[0].Keepsake.Symbol, state.Lessons[0].Keepsake.Name));
    }

    [Fact]
    public async Task Finish_OpenLesson_DatesTheNextLessonAtTheNextLocalMidnight()
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);

        JourneyStateDto? state = await Service(db, clock)
            .FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");

        Assert.NotNull(state);
        Assert.True(state.Started);
        Assert.False(state.Finished);
        LessonProgressDto first = state.Lessons[0];
        Assert.Equal((LessonStatus.Finished, Finish.UtcDateTime), (first.Status, first.FinishedAtUtc));
        LessonProgressDto second = state.Lessons[1];
        Assert.Equal((LessonStatus.Locked, NextSaigonMidnight), (second.Status, second.OpensAtUtc));
        LessonCompletion row = await db.LessonCompletions.SingleAsync();
        Assert.Equal(("build-a-cushion", Finish.UtcDateTime, Saigon), (row.LessonSlug, row.FinishedAt, row.TimeZoneId));
    }

    [Fact]
    public async Task GetState_OnceTheLocalMidnightPasses_OpensTheNextLesson_AndNothingElse()
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);
        JourneyService service = Service(db, clock);
        await service.FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");

        clock.Now = new DateTimeOffset(NextSaigonMidnight).AddMinutes(-1);
        Assert.Equal(LessonStatus.Locked, (await service.GetStateAsync(UserId, "en")).Lessons[1].Status);

        clock.Now = new DateTimeOffset(NextSaigonMidnight);
        JourneyStateDto state = await service.GetStateAsync(UserId, "en");
        Assert.Equal([LessonStatus.Finished, LessonStatus.Open], state.Lessons.Select(l => l.Status));
        Assert.Null(state.Lessons[1].OpensAtUtc);
    }

    [Fact]
    public async Task GetState_AfterDaysAway_FindsTheNextLessonSimplyOpen()
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);
        JourneyService service = Service(db, clock);
        await service.FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");

        clock.Advance(TimeSpan.FromDays(10));
        JourneyStateDto state = await service.GetStateAsync(UserId, "en");

        Assert.Equal(LessonStatus.Open, state.Lessons[1].Status);
        Assert.Single(state.Lessons, l => l.Status == LessonStatus.Open);
    }

    [Fact]
    public async Task Finish_AcrossAClockChange_UsesTheZoneOfTheFinish()
    {
        // New York leaves daylight time on 1 November 2026 at 02:00: a finish at 01:30 EDT (05:30 UTC) is followed by
        // midnight on 2 November in standard time, 05:00 UTC, so a naive "24 hours later" would open it late.
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero));
        JourneyService service = Service(db, clock);

        JourneyStateDto? state = await service.FinishLessonAsync(UserId, "build-a-cushion",
            new FinishLessonRequest { TimeZone = "America/New_York" }, "en");

        Assert.Equal(new DateTime(2026, 11, 2, 5, 0, 0, DateTimeKind.Utc), state!.Lessons[1].OpensAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Mars/Olympus_Mons")]
    public async Task Finish_WithoutAKnownZone_FallsBackToUtc(string? timeZone)
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);

        JourneyStateDto? state = await Service(db, clock)
            .FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = timeZone }, "en");

        Assert.Equal(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), state!.Lessons[1].OpensAtUtc);
        Assert.Equal("UTC", (await db.LessonCompletions.SingleAsync()).TimeZoneId);
    }

    [Fact]
    public async Task Finish_LockedLesson_IsRefusedWithTheLockedKey()
    {
        await using AppDbContext db = CreateContext();
        JourneyService service = Service(db, new FixedTimeProvider(Finish));

        ApiErrorException error = await Assert.ThrowsAsync<ApiErrorException>(() =>
            service.FinishLessonAsync(UserId, "borrow-wisely", new FinishLessonRequest { TimeZone = Saigon }, "en"));

        Assert.Equal((ApiErrorKeys.Learning.LessonLocked, (int)HttpStatusCode.Conflict), (error.Key, error.StatusCode));
        Assert.Empty(await db.LessonCompletions.ToListAsync());
    }

    [Fact]
    public async Task Finish_UnknownLesson_IsNull()
    {
        await using AppDbContext db = CreateContext();

        Assert.Null(await Service(db, new FixedTimeProvider(Finish))
            .FinishLessonAsync(UserId, "own-a-piece", new FinishLessonRequest(), "en"));
    }

    [Fact]
    public async Task Finish_FinishedLessonAgain_KeepsTheFirstFinish()
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);
        JourneyService service = Service(db, clock);
        await service.FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");

        clock.Advance(TimeSpan.FromHours(3));
        JourneyStateDto? state = await service.FinishLessonAsync(UserId, "build-a-cushion",
            new FinishLessonRequest { TimeZone = "America/New_York" }, "en");

        Assert.Equal(Finish.UtcDateTime, state!.Lessons[0].FinishedAtUtc);
        Assert.Equal(NextSaigonMidnight, state.Lessons[1].OpensAtUtc);
        Assert.Single(await db.LessonCompletions.ToListAsync());
    }

    [Fact]
    public async Task Finish_EveryLesson_EndsTheJourney()
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);
        JourneyService service = Service(db, clock);
        await service.FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");
        clock.Advance(TimeSpan.FromDays(1));

        JourneyStateDto? state = await service.FinishLessonAsync(UserId, "borrow-wisely",
            new FinishLessonRequest { TimeZone = Saigon }, "en");

        Assert.True(state!.Finished);
        Assert.All(state.Lessons, l => Assert.Equal(LessonStatus.Finished, l.Status));
    }

    [Fact]
    public async Task GetState_SoftDeletedCompletion_DoesNotCount()
    {
        await using AppDbContext db = CreateContext();
        db.LessonCompletions.Add(new LessonCompletion
        { UserId = UserId, LessonSlug = "build-a-cushion", FinishedAt = Finish.UtcDateTime, TimeZoneId = Saigon, IsDeleted = true });
        await db.SaveChangesAsync();

        JourneyStateDto state = await Service(db, new FixedTimeProvider(Finish.AddDays(2))).GetStateAsync(UserId, "en");

        Assert.False(state.Started);
        Assert.Equal(LessonStatus.Open, state.Lessons[0].Status);
    }

    [Fact]
    public async Task GetState_NeverShowsAnotherAccountsProgress()
    {
        await using AppDbContext db = CreateContext();
        db.LessonCompletions.Add(new LessonCompletion
        { UserId = "someone-else", LessonSlug = "build-a-cushion", FinishedAt = Finish.UtcDateTime, TimeZoneId = Saigon });
        await db.SaveChangesAsync();

        JourneyStateDto state = await Service(db, new FixedTimeProvider(Finish.AddDays(2))).GetStateAsync(UserId, "en");

        Assert.False(state.Started);
    }

    [Fact]
    public async Task GetState_InVietnamese_NamesTheLessonsAndKeepsakesInVietnamese_FallingBackToEnglish()
    {
        await using AppDbContext db = CreateContext();

        JourneyStateDto state = await Service(db, new FixedTimeProvider(Finish)).GetStateAsync(UserId, "vi");

        Assert.Equal(("Vay tiền khôn ngoan", "Sổ cái"), (state.Lessons[1].Title, state.Lessons[1].Keepsake.Name));
        Assert.Equal("Build a cushion", state.Lessons[0].Title);
    }

    [Fact]
    public async Task GetReview_OnDayOne_HasNoCards()
    {
        await using AppDbContext db = CreateContext();

        Assert.Empty((await Service(db, new FixedTimeProvider(Finish)).GetReviewAsync(UserId, "en")).Cards);
    }

    [Fact]
    public async Task GetReview_AfterDayOne_DrawsThatLessonsTerms_InTeachingOrder()
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);
        JourneyService service = Service(db, clock);
        await service.FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");
        clock.Advance(TimeSpan.FromDays(1));

        ReviewDto review = await service.GetReviewAsync(UserId, "en");

        Assert.Equal(["emergency-fund", "compound-interest"], review.Cards.Select(c => c.Slug));
        ReviewCardDto card = review.Cards[0];
        Assert.Equal(("Emergency fund", "build-a-cushion"), (card.Term, card.LessonSlug));
        Assert.False(string.IsNullOrWhiteSpace(card.OneLine));
        Assert.Equal("Emergency fund", card.EnglishTerm);
    }

    [Fact]
    public async Task GetReview_PutsNotYetCardsFirst_AndRetiresACardAfterTwoKnewItInARow()
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);
        JourneyService service = Service(db, clock);
        await service.FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");
        clock.Advance(TimeSpan.FromDays(1));

        Assert.True(await service.MarkReviewCardAsync(UserId, "emergency-fund", new MarkReviewCardRequest { KnewIt = true }));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await service.MarkReviewCardAsync(UserId, "compound-interest", new MarkReviewCardRequest { KnewIt = false }));
        Assert.Equal(["compound-interest", "emergency-fund"],
            (await service.GetReviewAsync(UserId, "en")).Cards.Select(c => c.Slug));

        clock.Advance(TimeSpan.FromDays(1));
        Assert.True(await service.MarkReviewCardAsync(UserId, "emergency-fund", new MarkReviewCardRequest { KnewIt = true }));
        Assert.Equal(["compound-interest"], (await service.GetReviewAsync(UserId, "en")).Cards.Select(c => c.Slug));

        ReviewCardState retired = await db.ReviewCardStates.SingleAsync(s => s.TermSlug == "emergency-fund");
        Assert.Equal((true, 2, clock.Now.UtcDateTime), (retired.KnewIt, retired.KnewItStreak, retired.LastShownAt));
    }

    [Fact]
    public async Task MarkReviewCard_NotYetAfterKnewIt_ResetsTheStreak()
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);
        JourneyService service = Service(db, clock);
        await service.FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");
        await service.MarkReviewCardAsync(UserId, "emergency-fund", new MarkReviewCardRequest { KnewIt = true });

        await service.MarkReviewCardAsync(UserId, "emergency-fund", new MarkReviewCardRequest { KnewIt = false });

        ReviewCardState state = await db.ReviewCardStates.SingleAsync();
        Assert.Equal((false, 0), (state.KnewIt, state.KnewItStreak));
    }

    [Fact]
    public async Task MarkReviewCard_TermNotTaughtYet_IsRefused()
    {
        await using AppDbContext db = CreateContext();
        JourneyService service = Service(db, new FixedTimeProvider(Finish));
        await service.FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");

        Assert.False(await service.MarkReviewCardAsync(UserId, "apr", new MarkReviewCardRequest { KnewIt = true }));
        Assert.False(await service.MarkReviewCardAsync(UserId, "mortgage", new MarkReviewCardRequest { KnewIt = true }));
        Assert.Empty(await db.ReviewCardStates.ToListAsync());
    }

    [Fact]
    public async Task GetReview_OnceTheJourneyHasEnded_HasNoCards()
    {
        await using AppDbContext db = CreateContext();
        var clock = new FixedTimeProvider(Finish);
        JourneyService service = Service(db, clock);
        await service.FinishLessonAsync(UserId, "build-a-cushion", new FinishLessonRequest { TimeZone = Saigon }, "en");
        clock.Advance(TimeSpan.FromDays(1));
        await service.FinishLessonAsync(UserId, "borrow-wisely", new FinishLessonRequest { TimeZone = Saigon }, "en");

        Assert.Empty((await service.GetReviewAsync(UserId, "en")).Cards);
    }

    [Fact]
    public async Task SetShowOnDashboard_CreatesThePreferenceRow_WithoutChoosingALanguage()
    {
        await using AppDbContext db = CreateContext();
        JourneyService service = Service(db, new FixedTimeProvider(Finish));

        await service.SetShowOnDashboardAsync(UserId, false);

        UserPreference row = await db.UserPreferences.SingleAsync();
        Assert.Equal((string.Empty, false), (row.Language, row.ShowJourneyOnDashboard));
        Assert.False((await service.GetStateAsync(UserId, "en")).ShowOnDashboard);

        await service.SetShowOnDashboardAsync(UserId, true);
        Assert.True((await service.GetStateAsync(UserId, "en")).ShowOnDashboard);
        Assert.Single(await db.UserPreferences.ToListAsync());
    }

    [Fact]
    public async Task SetShowOnDashboard_KeepsTheLanguageOfAnExistingRow()
    {
        await using AppDbContext db = CreateContext();
        db.UserPreferences.Add(new UserPreference { UserId = UserId, Language = "vi" });
        await db.SaveChangesAsync();

        await Service(db, new FixedTimeProvider(Finish)).SetShowOnDashboardAsync(UserId, false);

        UserPreference row = await db.UserPreferences.SingleAsync();
        Assert.Equal(("vi", false), (row.Language, row.ShowJourneyOnDashboard));
    }

    [Fact]
    public void Queries_TranslateToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        JourneyService service = Service(db, new FixedTimeProvider(Finish));

        AzureSqlTranslation.AssertTranslates(service.CompletionsQuery(UserId));
        AzureSqlTranslation.AssertTranslates(service.CardStatesQuery(UserId));
        AzureSqlTranslation.AssertTranslates(service.ShowOnDashboardQuery(UserId));
    }
}
