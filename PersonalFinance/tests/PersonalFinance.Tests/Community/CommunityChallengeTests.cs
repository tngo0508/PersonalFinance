using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Community;
using PersonalFinance.ApiService.Features.Community.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// Challenges and Check-ins through <see cref="ICommunityService"/> (ADR 0018): an Account joins under its browser's
/// time zone and checks in once per local day or week; its streak and Check-ins are its own, while everyone sees only
/// how many joined and how many kept it this period. Also <see cref="ChallengeCalendar"/> and the Challenge validator.
/// </summary>
public class CommunityChallengeTests
{
    private const string Member = "challenge-member";
    private const string Other = "challenge-other";
    private const string Reader = "challenge-reader";
    private const string Saigon = "Asia/Ho_Chi_Minh";

    /// <summary>20:00 UTC on 29 October: already 30 October in Ho Chi Minh City (UTC+7).</summary>
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 29, 20, 0, 0, TimeSpan.Zero));

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CommunityChallenge_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private CommunityService CreateService(AppDbContext db) =>
        new(db, _clock, NullLogger<CommunityService>.Instance);

    private static CommunityChallengeRequestDto Challenge(DateOnly start, DateOnly end,
        CommunityChallengePeriod period = CommunityChallengePeriod.Daily) => new()
    {
        Title = "Cook at home",
        Habit = "Cook dinner at home instead of ordering in.",
        Period = period,
        StartDate = start,
        EndDate = end,
    };

    private static DateOnly Day(int month, int day) => new(2026, month, day);

    [Fact]
    public void Calendar_CountsDailyPeriodsByDay_AndWeeklyOnesFromTheStartDate()
    {
        Assert.Equal(Day(10, 16), ChallengeCalendar.CurrentPeriod(CommunityChallengePeriod.Daily, Day(10, 1), Day(10, 31), Day(10, 16)));
        Assert.Equal(Day(10, 15), ChallengeCalendar.CurrentPeriod(CommunityChallengePeriod.Weekly, Day(10, 1), Day(10, 31), Day(10, 16)));
        Assert.Equal(Day(10, 29), ChallengeCalendar.CurrentPeriod(CommunityChallengePeriod.Weekly, Day(10, 1), Day(10, 31), Day(10, 31)));
        Assert.Null(ChallengeCalendar.CurrentPeriod(CommunityChallengePeriod.Daily, Day(10, 1), Day(10, 31), Day(9, 30)));
        Assert.Null(ChallengeCalendar.CurrentPeriod(CommunityChallengePeriod.Daily, Day(10, 1), Day(10, 31), Day(11, 1)));
    }

    [Theory]
    [InlineData(new[] { 14, 15 }, 2)]
    [InlineData(new[] { 14, 15, 16 }, 3)]
    [InlineData(new[] { 13, 15 }, 1)]
    [InlineData(new[] { 13, 14 }, 0)]
    [InlineData(new int[0], 0)]
    public void Calendar_Streak_CountsBackFromTodayOrYesterday_UntilAMissedDay(int[] keptDays, int streak)
    {
        var kept = new HashSet<DateOnly>(keptDays.Select(d => Day(10, d)));

        Assert.Equal(streak, ChallengeCalendar.Streak(CommunityChallengePeriod.Daily, Day(10, 1), Day(10, 16), kept));
    }

    [Fact]
    public void Calendar_Streak_StopsAtTheStartDate_AndStepsWeekByWeek()
    {
        var kept = new HashSet<DateOnly> { Day(10, 1), Day(10, 8), Day(9, 24) };

        Assert.Equal(2, ChallengeCalendar.Streak(CommunityChallengePeriod.Weekly, Day(10, 1), Day(10, 15), kept));
    }

    [Fact]
    public async Task Join_UsesTheBrowsersLocalDay_WhileAnAccountThatHasNotJoinedSeesIt_InUtc()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityChallengeAdminItemDto created = await service.CreateChallengeAsync(Challenge(Day(10, 30), Day(11, 5)));

        CommunityChallengeDto joined = await service.JoinChallengeAsync(Member, created.Id, Saigon);

        Assert.True(joined.IsMember);
        Assert.Equal(Day(10, 30), joined.CurrentPeriodStart);
        Assert.Equal(1, joined.MemberCount);
        Assert.Equal(0, joined.Streak);
        Assert.Equal(Saigon, (await db.CommunityChallengeMembers.SingleAsync()).TimeZoneId);

        CommunityChallengeDto seen = Assert.Single((await service.GetCommunityAsync(Reader)).Challenges);
        Assert.False(seen.IsMember);
        Assert.Null(seen.CurrentPeriodStart);
        Assert.Equal(1, seen.MemberCount);
    }

    [Fact]
    public async Task Join_Twice_KeepsOneMembership_AndAnUnknownZoneIsStoredAsUtc()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityChallengeAdminItemDto created = await service.CreateChallengeAsync(Challenge(Day(10, 1), Day(10, 31)));

        await service.JoinChallengeAsync(Member, created.Id, Saigon);
        CommunityChallengeDto again = await service.JoinChallengeAsync(Member, created.Id, "Not/AZone");

        Assert.Equal(1, again.MemberCount);
        Assert.Equal(BrowserTimeZone.Utc, (await db.CommunityChallengeMembers.SingleAsync()).TimeZoneId);
    }

    [Fact]
    public async Task CheckIn_OncePerPeriod_CountsEveryone_ButShowsEachAccountOnlyItsOwn()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityChallengeAdminItemDto created = await service.CreateChallengeAsync(Challenge(Day(10, 20), Day(11, 20)));
        foreach (string account in new[] { Member, Other, Reader })
        {
            await service.JoinChallengeAsync(account, created.Id, null);
        }

        await service.CheckInAsync(Other, created.Id, null);
        await service.CheckInAsync(Member, created.Id, null);
        CommunityChallengeDto mine = await service.CheckInAsync(Member, created.Id, null);

        Assert.Equal(2, await db.CommunityCheckIns.CountAsync());
        Assert.True(mine.CheckedInThisPeriod);
        Assert.Equal(1, mine.Streak);
        Assert.Equal(2, mine.CheckedInCount);
        Assert.Equal(3, mine.MemberCount);

        CommunityChallengeDto theirs = Assert.Single((await service.GetCommunityAsync(Reader)).Challenges);
        Assert.False(theirs.CheckedInThisPeriod);
        Assert.Equal(0, theirs.Streak);
        Assert.Equal(2, theirs.CheckedInCount);
    }

    [Fact]
    public async Task CheckedInCount_CountsEachMemberForItsOwnLocalDay_AcrossTheUtcDayBoundary()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityChallengeAdminItemDto created = await service.CreateChallengeAsync(Challenge(Day(10, 1), Day(10, 31)));
        await service.JoinChallengeAsync(Other, created.Id, null);
        await service.CheckInAsync(Other, created.Id, null);

        // 06:46 UTC on 30 October: still 29 October, 23:46, in Los Angeles; already 30 October in UTC and Saigon.
        _clock.Now = new DateTimeOffset(2026, 10, 30, 6, 46, 0, TimeSpan.Zero);
        await service.JoinChallengeAsync(Member, created.Id, "America/Los_Angeles");
        await service.CheckInAsync(Member, created.Id, "America/Los_Angeles");
        await service.JoinChallengeAsync(Reader, created.Id, Saigon);
        CommunityChallengeDto saigon = await service.CheckInAsync(Reader, created.Id, Saigon);

        // Other kept 29 October in UTC, which is yesterday for it now, so only the two members still on their day count.
        CommunityChallengeDto seen = Assert.Single((await service.GetCommunityAsync("challenge-viewer")).Challenges);
        Assert.False(seen.IsMember);
        Assert.Equal(2, seen.CheckedInCount);
        Assert.Equal(2, saigon.CheckedInCount);
    }

    [Fact]
    public async Task Streak_GrowsDayByDay_AndRestartsAfterAMissedDay()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityChallengeAdminItemDto created = await service.CreateChallengeAsync(Challenge(Day(10, 20), Day(11, 20)));
        await service.JoinChallengeAsync(Member, created.Id, null);

        await service.CheckInAsync(Member, created.Id, null);
        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(1, Assert.Single((await service.GetCommunityAsync(Member)).Challenges).Streak);
        Assert.Equal(2, (await service.CheckInAsync(Member, created.Id, null)).Streak);

        _clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(0, Assert.Single((await service.GetCommunityAsync(Member)).Challenges).Streak);
        Assert.Equal(1, (await service.CheckInAsync(Member, created.Id, null)).Streak);
    }

    [Fact]
    public async Task WeeklyCheckIns_CountOncePerWeek_FromTheStartDate()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityChallengeAdminItemDto created = await service.CreateChallengeAsync(
            Challenge(Day(10, 22), Day(11, 30), CommunityChallengePeriod.Weekly));
        await service.JoinChallengeAsync(Member, created.Id, null);

        CommunityChallengeDto first = await service.CheckInAsync(Member, created.Id, null);
        _clock.Advance(TimeSpan.FromDays(1));
        CommunityChallengeDto sameWeek = await service.CheckInAsync(Member, created.Id, null);

        Assert.Equal(Day(10, 29), first.CurrentPeriodStart);
        Assert.Equal(Day(10, 29), sameWeek.CurrentPeriodStart);
        Assert.Equal(1, await db.CommunityCheckIns.CountAsync());
    }

    [Fact]
    public async Task CheckIn_IsRefused_BeforeJoining_BeforeTheStart_AndOnAnUnknownOrEndedChallenge()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityChallengeAdminItemDto running = await service.CreateChallengeAsync(Challenge(Day(10, 1), Day(10, 31)));
        CommunityChallengeAdminItemDto upcoming = await service.CreateChallengeAsync(Challenge(Day(11, 1), Day(11, 30)));
        CommunityChallengeAdminItemDto ended = await service.CreateChallengeAsync(Challenge(Day(9, 1), Day(9, 30)));
        await service.JoinChallengeAsync(Member, upcoming.Id, null);

        await AssertRefused(ApiErrorKeys.Community.ChallengeNotJoined, 409,
            () => service.CheckInAsync(Member, running.Id, null));
        await AssertRefused(ApiErrorKeys.Community.ChallengeNotStarted, 409,
            () => service.CheckInAsync(Member, upcoming.Id, null));
        await AssertRefused(ApiErrorKeys.Community.ChallengeNotFound, 404,
            () => service.JoinChallengeAsync(Member, ended.Id, null));
        await AssertRefused(ApiErrorKeys.Community.ChallengeNotFound, 404,
            () => service.CheckInAsync(Member, 999, null));
        Assert.Equal([running.Id, upcoming.Id],
            (await service.GetCommunityAsync(Reader)).Challenges.Select(c => c.Id));
        Assert.Empty(db.CommunityCheckIns);
    }

    [Fact]
    public async Task ListChallenges_IsNewestStartFirst_WithMemberCounts()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityChallengeAdminItemDto older = await service.CreateChallengeAsync(Challenge(Day(9, 1), Day(9, 30)));
        CommunityChallengeAdminItemDto newer = await service.CreateChallengeAsync(Challenge(Day(10, 1), Day(10, 31)));
        await service.JoinChallengeAsync(Member, newer.Id, null);

        List<CommunityChallengeAdminItemDto> items = await service.ListChallengesAsync();

        Assert.Equal([newer.Id, older.Id], items.Select(i => i.Id));
        Assert.Equal([1, 0], items.Select(i => i.MemberCount));
    }

    [Theory]
    [InlineData("", "Cook at home", 1, 0, 10, ApiErrorKeys.Community.ChallengeTitle)]
    [InlineData("Save more", "Put $50 aside every week", 1, 0, 10, ApiErrorKeys.Community.ChallengeAmount)]
    [InlineData("Cook at home", "Cook dinner at home", 9, 0, 10, ApiErrorKeys.Community.ChallengePeriod)]
    [InlineData("Cook at home", "Cook dinner at home", 1, 10, 9, ApiErrorKeys.Community.ChallengeDates)]
    [InlineData("Cook at home", "Cook dinner at home", 1, 0, 366, ApiErrorKeys.Community.ChallengeDates)]
    public void ChallengeValidator_RejectsWithTheKeyOfTheRuleBroken(string title, string habit, int period,
        int startOffset, int endOffset, string key)
    {
        DateOnly start = Day(10, 1);
        ValidationResult result = new CommunityChallengeRequestValidator().Validate(new CommunityChallengeRequestDto
        {
            Title = title,
            Habit = habit,
            Period = (CommunityChallengePeriod)period,
            StartDate = start.AddDays(startOffset),
            EndDate = start.AddDays(endOffset),
        });

        Assert.Equal(key, Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public void ChallengeValidator_AcceptsAHabitWithPercentagesAndCounts_OverAFullYear()
    {
        CommunityChallengeRequestDto request = Challenge(Day(1, 1), Day(12, 31), CommunityChallengePeriod.Weekly);
        request.Habit = "Cook five nights a week and cut takeaway by 50%.";

        Assert.True(new CommunityChallengeRequestValidator().Validate(request).IsValid);
    }

    private static async Task AssertRefused(string key, int status, Func<Task> action)
    {
        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(action);
        Assert.Equal(key, ex.Key);
        Assert.Equal(status, ex.StatusCode);
    }
}
