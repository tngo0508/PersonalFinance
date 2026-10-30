using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.ApiService.Features.Learning;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Tests.Learning;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Dashboard;

/// <summary>
/// The quiet Atelier card on the Dashboard summary (#523), over the real Journey and the fixture content (Lesson 1
/// <c>build-a-cushion</c>, Lesson 2 <c>borrow-wisely</c>): it shows only mid-Journey while today's Lesson is open, and
/// only while the Account has not hidden it.
/// </summary>
public class DashboardAtelierCardTests
{
    private const string UserId = "atelier-reader";

    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly FixedTimeProvider _clock = new(Now);

    private readonly LearningContent _content = LearningContent.Load(LearningContentFolders.Fixture);

    [Fact]
    public async Task LessonTwoOpen_ShowsTheCard_WithLessonOnesKeepsake()
    {
        await using AppDbContext db = CreateContext();
        Finish(db, "build-a-cushion", Now.AddDays(-2));
        await db.SaveChangesAsync();

        DashboardAtelierDto atelier = Assert.IsType<DashboardAtelierDto>((await GetSummaryAsync(db)).Atelier);

        Assert.Equal("borrow-wisely", atelier.LessonSlug);
        Assert.Equal(2, atelier.Day);
        Assert.Equal(_content.FindLesson("borrow-wisely", "en")!.Title, atelier.Title);
        Assert.Equal([_content.FindLesson("build-a-cushion", "en")!.Keepsake.Name], atelier.Keepsakes.Select(k => k.Name));
    }

    [Fact]
    public async Task NotStarted_ShowsNoCard_ThoughLessonOneIsOpen()
    {
        await using AppDbContext db = CreateContext();

        Assert.Null((await GetSummaryAsync(db)).Atelier);
    }

    [Fact]
    public async Task NextLessonLocked_ShowsNoCard()
    {
        await using AppDbContext db = CreateContext();
        Finish(db, "build-a-cushion", Now.AddHours(-1));
        await db.SaveChangesAsync();

        Assert.Null((await GetSummaryAsync(db)).Atelier);
    }

    [Fact]
    public async Task JourneyFinished_ShowsNoCard()
    {
        await using AppDbContext db = CreateContext();
        Finish(db, "build-a-cushion", Now.AddDays(-3));
        Finish(db, "borrow-wisely", Now.AddDays(-2));
        await db.SaveChangesAsync();

        Assert.Null((await GetSummaryAsync(db)).Atelier);
    }

    [Fact]
    public async Task Hidden_ShowsNoCard_UntilShownAgain()
    {
        await using AppDbContext db = CreateContext();
        Finish(db, "build-a-cushion", Now.AddDays(-2));
        await db.SaveChangesAsync();
        var journey = new JourneyService(db, _content, _clock);

        await journey.SetShowOnDashboardAsync(UserId, false);
        Assert.Null((await GetSummaryAsync(db)).Atelier);

        await journey.SetShowOnDashboardAsync(UserId, true);
        Assert.NotNull((await GetSummaryAsync(db)).Atelier);
    }

    [Fact]
    public async Task WithoutTheJourney_ShowsNoCard()
    {
        await using AppDbContext db = CreateContext();
        Finish(db, "build-a-cushion", Now.AddDays(-2));
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await new DashboardService(db, TestBudgetDataProviderFactory.Create(db),
            TestCurrencyConverter.Create(db)).GetDashboardSummaryAsync(UserId);

        Assert.Null(summary.Atelier);
    }

    private Task<DashboardSummaryDto> GetSummaryAsync(AppDbContext db) =>
        new DashboardService(db, TestBudgetDataProviderFactory.Create(db), TestCurrencyConverter.Create(db),
            new JourneyService(db, _content, _clock)).GetDashboardSummaryAsync(UserId, "en");

    private static void Finish(AppDbContext db, string lessonSlug, DateTimeOffset at) =>
        db.LessonCompletions.Add(new LessonCompletion
        {
            UserId = UserId,
            LessonSlug = lessonSlug,
            FinishedAt = at.UtcDateTime,
            TimeZoneId = "UTC",
            CreatedAt = at.UtcDateTime,
        });

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardAtelier_{Guid.NewGuid():N}")
            .Options);
}
