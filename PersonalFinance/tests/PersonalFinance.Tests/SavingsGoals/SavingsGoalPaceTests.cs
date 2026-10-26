using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.SavingsGoals;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.SavingsGoals;

/// <summary>
/// The Pace and "saved this month" the goal list returns, worked out at a fixed today (2026-10-25) from goals and
/// Contributions seeded in an in-memory database (spec #395, "Pace rules").
/// </summary>
public sealed class SavingsGoalPaceTests : IDisposable
{
    private const string UserId = "saver";

    private static readonly DateOnly Today = new(2026, 10, 25);
    private static readonly DateTime YearAgo = new(2025, 10, 25, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InTwoYearsFromStart = new(2027, 10, 25);

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SavingsGoalPace_{Guid.NewGuid():N}")
        .Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AGoalAtItsTarget_IsReached_WithNothingStillNeededOrProjected()
    {
        Seed(1_000m, YearAgo, InTwoYearsFromStart, Regular(1_000m, new DateOnly(2026, 10, 1)));

        SavingsGoalPaceDto pace = await PaceAsync();

        Assert.Equal(PaceStatus.Reached, pace.Status);
        Assert.Null(pace.NeededPerMonth);
        Assert.Null(pace.ProjectedFinish);
    }

    [Fact]
    public async Task AGoalWithoutATargetDate_StillGetsAProjection_FromItsRecentRate()
    {
        // 600 of Regular Contributions in the last 90 days: about 203 a month, so 600 more takes about 90 days.
        Seed(1_200m, new DateTime(2026, 6, 1), null,
            Regular(300m, new DateOnly(2026, 9, 1)), Regular(300m, new DateOnly(2026, 10, 1)));

        SavingsGoalPaceDto pace = await PaceAsync();

        Assert.Equal(PaceStatus.NoTargetDate, pace.Status);
        Assert.Null(pace.NeededPerMonth);
        Assert.Equal(PaceRateState.Positive, pace.RateState);
        Assert.Equal(202.93m, pace.RatePerMonth);
        Assert.InRange(pace.ProjectedFinish!.Value, new DateOnly(2027, 1, 23), new DateOnly(2027, 1, 24));
    }

    [Fact]
    public async Task AGoalPastItsTargetDate_IsPastDue_WithNoNeededPerMonth()
    {
        Seed(1_000m, YearAgo, new DateTime(2026, 10, 24), Regular(400m, new DateOnly(2026, 10, 1)));

        SavingsGoalPaceDto pace = await PaceAsync();

        Assert.Equal(PaceStatus.PastDue, pace.Status);
        Assert.Null(pace.NeededPerMonth);
    }

    [Fact]
    public async Task AGoalDueToday_IsNotPastDue()
    {
        Seed(1_000m, YearAgo, new DateTime(2026, 10, 25), Regular(400m, new DateOnly(2026, 10, 1)));

        SavingsGoalPaceDto pace = await PaceAsync();

        Assert.NotEqual(PaceStatus.PastDue, pace.Status);
        Assert.Equal(600m, pace.NeededPerMonth);
    }

    [Fact]
    public async Task AGoalUnder30DaysOld_IsJustStarted_WithAnUnknownRate_ButANeededPerMonth()
    {
        // A week-old goal at 0 would otherwise read as On track.
        Seed(1_000m, new DateTime(2026, 10, 18), new DateTime(2027, 10, 25), Regular(50m, new DateOnly(2026, 10, 20)));

        SavingsGoalPaceDto pace = await PaceAsync();

        Assert.Equal(PaceStatus.JustStarted, pace.Status);
        Assert.Equal(PaceRateState.Unknown, pace.RateState);
        Assert.Null(pace.ProjectedFinish);
        // 950 left over 365 days (11.99 months), rounded up to the cent.
        Assert.Equal(79.23m, pace.NeededPerMonth);
    }

    [Fact]
    public async Task AGoal30DaysOld_IsNoLongerJustStarted()
    {
        Seed(1_000m, new DateTime(2026, 9, 25), new DateTime(2027, 10, 25), Regular(50m, new DateOnly(2026, 10, 20)));

        Assert.NotEqual(PaceStatus.JustStarted, (await PaceAsync()).Status);
    }

    [Theory]
    // Halfway from 2025-10-25 to 2027-10-25, the ideal line is at 500 of 1,000; the band is 5% of the target (50).
    [InlineData(550.00, PaceStatus.Ahead)]
    [InlineData(549.99, PaceStatus.OnTrack)]
    [InlineData(450.00, PaceStatus.OnTrack)]
    [InlineData(449.99, PaceStatus.Behind)]
    public async Task ADatedGoal_IsComparedWithTheIdealLine_WithinFivePercentOfTheTarget(double saved, PaceStatus status)
    {
        Seed(1_000m, YearAgo, InTwoYearsFromStart, Regular((decimal)saved, new DateOnly(2025, 10, 25)));

        Assert.Equal(status, (await PaceAsync()).Status);
    }

    [Theory]
    // The ideal line starts at the Opening amount: 200 + 800 × 0.5 = 600 today.
    [InlineData(600.00, PaceStatus.OnTrack)]
    [InlineData(650.00, PaceStatus.Ahead)]
    [InlineData(549.99, PaceStatus.Behind)]
    public async Task TheIdealLine_StartsAtTheOpeningAmount(double saved, PaceStatus status)
    {
        Seed(1_000m, YearAgo, InTwoYearsFromStart, Opening(200m, new DateOnly(2025, 10, 25)),
            Regular((decimal)saved - 200m, new DateOnly(2026, 10, 1)));

        Assert.Equal(status, (await PaceAsync()).Status);
    }

    [Fact]
    public async Task ABackdatedContribution_MovesTheStartOfTheIdealLine()
    {
        // Created a year ago, but the first deposit is dated two years ago: the line starts there, so two thirds are
        // due today, not half (which would make 700 Ahead).
        Seed(1_000m, YearAgo, InTwoYearsFromStart, Regular(700m, new DateOnly(2024, 10, 25)));

        Assert.Equal(PaceStatus.OnTrack, (await PaceAsync()).Status);
    }

    [Fact]
    public async Task NoRegularContributionInTheLast90Days_IsStalled()
    {
        Seed(1_000m, YearAgo, null, Regular(300m, new DateOnly(2026, 7, 1)));

        SavingsGoalPaceDto pace = await PaceAsync();

        Assert.Equal(PaceRateState.Stalled, pace.RateState);
        Assert.Null(pace.ProjectedFinish);
    }

    [Fact]
    public async Task ANetWithdrawalInTheLast90Days_IsStalled()
    {
        Seed(1_000m, YearAgo, null, Regular(500m, new DateOnly(2026, 1, 1)),
            Regular(100m, new DateOnly(2026, 9, 1)), Regular(-150m, new DateOnly(2026, 10, 1)));

        Assert.Equal(PaceRateState.Stalled, (await PaceAsync()).RateState);
    }

    [Fact]
    public async Task AGoalWithoutAnyRegularContribution_HasAnUnknownRate()
    {
        Seed(1_000m, YearAgo, null, Opening(300m, new DateOnly(2025, 10, 25)), Adjustment(50m, new DateOnly(2026, 10, 1)));

        Assert.Equal(PaceRateState.Unknown, (await PaceAsync()).RateState);
    }

    [Fact]
    public async Task TheOpeningAmountAndBalanceCorrections_AreLeftOutOfTheRecentRate()
    {
        Seed(10_000m, new DateTime(2026, 9, 1), null, Opening(5_000m, new DateOnly(2026, 9, 1)),
            Adjustment(1_000m, new DateOnly(2026, 10, 1)), Regular(100m, new DateOnly(2026, 10, 2)));

        SavingsGoalPaceDto pace = await PaceAsync();

        // The goal is 54 days old, so the window is its own age: 100 over 54 days.
        Assert.Equal(56.37m, pace.RatePerMonth);
    }

    [Fact]
    public async Task NeededPerMonth_HasAFloorOfOneMonth()
    {
        Seed(1_000m, YearAgo, new DateTime(2026, 11, 4), Regular(500m, new DateOnly(2026, 10, 1)));

        Assert.Equal(500m, (await PaceAsync()).NeededPerMonth);
    }

    [Fact]
    public async Task SavedThisMonth_IsTheNetOfThisMonthsRegularContributions()
    {
        Seed(1_000m, YearAgo, null, Regular(70m, new DateOnly(2026, 9, 30)), Regular(50m, new DateOnly(2026, 10, 3)),
            Regular(-20m, new DateOnly(2026, 10, 20)), Adjustment(100m, new DateOnly(2026, 10, 10)),
            Opening(10m, new DateOnly(2026, 10, 1)));

        SavingsGoalResponseDto goal = Assert.Single(await Service().GetSavingsGoalsAsync(UserId));

        Assert.Equal(30m, goal.SavedThisMonth);
    }

    private void Seed(decimal target, DateTime createdAt, DateTime? targetDate, params Contribution[] contributions)
    {
        var goal = new SavingsGoal
        {
            UserId = UserId,
            Name = "Emergency Fund",
            TargetAmount = target,
            CurrentAmount = contributions.Sum(c => c.Amount),
            TargetDate = targetDate,
            CreatedAt = createdAt
        };
        goal.IsCompleted = goal.CurrentAmount >= target;
        _db.SavingsGoals.Add(goal);
        foreach (Contribution contribution in contributions)
        {
            contribution.SavingsGoal = goal;
            _db.Contributions.Add(contribution);
        }

        _db.SaveChanges();
    }

    private static Contribution Regular(decimal amount, DateOnly date) => New(amount, date, ContributionKind.Regular);

    private static Contribution Opening(decimal amount, DateOnly date) => New(amount, date, ContributionKind.Opening);

    private static Contribution Adjustment(decimal amount, DateOnly date) =>
        New(amount, date, ContributionKind.Adjustment);

    private static Contribution New(decimal amount, DateOnly date, ContributionKind kind) =>
        new() { UserId = UserId, Amount = amount, Date = date, Kind = kind };

    private async Task<SavingsGoalPaceDto> PaceAsync() =>
        Assert.Single(await Service().GetSavingsGoalsAsync(UserId)).Pace;

    private SavingsGoalService Service() =>
        new(_db, NullLogger<SavingsGoalService>.Instance, new NoNotifications(),
            new FixedTimeProvider(new DateTimeOffset(Today.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero)));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NoNotifications : INotificationService
    {
        public Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<List<NotificationDto>> GetNotificationsAsync(string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<NotificationDto>());

        public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> MarkAsReadAsync(string userId, int notificationId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
