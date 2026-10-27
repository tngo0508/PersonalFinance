using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.SavingsGoals;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.SavingsGoals;

/// <summary>
/// The Savings Goals service's Contribution log, exercised against an in-memory database: the saved amount is the total
/// of a goal's Contributions, Reached follows it both ways, and each milestone Notification goes out at most once.
/// </summary>
public sealed class SavingsGoalServiceTests : IDisposable
{
    private const string UserId = "saver";
    private const string OtherUserId = "someone-else";

    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 25);

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SavingsGoals_{Guid.NewGuid():N}")
        .Options);

    private readonly RecordingNotificationService _notifications = new();

    public void Dispose() => _db.Dispose();

    private SavingsGoalService Service() =>
        new(_db, NullLogger<SavingsGoalService>.Instance, _notifications, new FixedTimeProvider(Now),
            TestCurrencyConverter.Create(_db));

    [Fact]
    public async Task Create_WithAStartingAmount_RecordsItAsTheOpeningContribution_DatedToday()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 150m);

        ContributionDto opening = Assert.Single((await Service().GetContributionsAsync(UserId, goalId))!);
        Assert.Equal((150m, Today, ContributionKind.Opening), (opening.Amount, opening.Date, opening.Kind));
        Assert.Equal(150m, (await GoalAsync(goalId)).CurrentAmount);
    }

    [Fact]
    public async Task Create_WithNothingSaved_RecordsNoContribution()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 0m);

        Assert.Empty((await Service().GetContributionsAsync(UserId, goalId))!);
        Assert.Equal(0m, (await GoalAsync(goalId)).CurrentAmount);
    }

    [Fact]
    public async Task AddContribution_Deposits_AndWithdrawals_KeepTheSavedAmountAsTheirTotal()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 100m);

        await AddAsync(goalId, 250m);
        await AddAsync(goalId, -75m);

        Assert.Equal(275m, (await GoalAsync(goalId)).CurrentAmount);
        Assert.Equal(275m, (await Service().GetSavingsGoalsAsync(UserId)).Single().CurrentAmount);
    }

    [Fact]
    public async Task AddContribution_WithoutADate_IsDatedToday_AndKeepsATrimmedNote()
    {
        int goalId = await CreateGoalAsync(target: 1_000m);

        ContributionResult result = await AddAsync(goalId, 40m, note: "  Tax refund  ");

        ContributionDto saved = Assert.Single((await Service().GetContributionsAsync(UserId, goalId))!);
        Assert.Equal(result.ContributionId, saved.Id);
        Assert.Equal((Today, "Tax refund", ContributionKind.Regular), (saved.Date, saved.Note, saved.Kind));
    }

    [Fact]
    public async Task Contributions_AreListedNewestFirst_ThenByIdWithinADay()
    {
        int goalId = await CreateGoalAsync(target: 1_000m);
        ContributionResult older = await AddAsync(goalId, 10m, new DateOnly(2026, 9, 1));
        ContributionResult sameDayFirst = await AddAsync(goalId, 20m, new DateOnly(2026, 10, 1));
        ContributionResult sameDaySecond = await AddAsync(goalId, 30m, new DateOnly(2026, 10, 1));

        List<ContributionDto> history = (await Service().GetContributionsAsync(UserId, goalId))!;

        Assert.Equal(new[] { sameDaySecond.ContributionId, sameDayFirst.ContributionId, older.ContributionId },
            history.Select(c => (int?)c.Id));
        Assert.Equal(history.Select(c => c.Id),
            (await Service().GetSavingsGoalsAsync(UserId)).Single().Contributions.Select(c => c.Id));
    }

    [Fact]
    public async Task Withdrawal_OfMoreThanTheGoalHolds_IsRefused_AndChangesNothing()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 100m);

        ContributionResult result = await AddAsync(goalId, -100.01m);

        Assert.Equal(ContributionOutcome.BelowZero, result.Outcome);
        Assert.Equal(100m, (await GoalAsync(goalId)).CurrentAmount);
        Assert.Single((await Service().GetContributionsAsync(UserId, goalId))!);
    }

    [Fact]
    public async Task Withdrawal_OfEverythingTheGoalHolds_LeavesItAtZero()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 100m);

        ContributionResult result = await AddAsync(goalId, -100m);

        Assert.Equal(ContributionOutcome.Saved, result.Outcome);
        Assert.Equal(0m, (await GoalAsync(goalId)).CurrentAmount);
    }

    [Fact]
    public async Task EditContribution_ChangesItsAmountDateAndNote_AndRewritesTheSavedAmount()
    {
        int goalId = await CreateGoalAsync(target: 1_000m);
        int id = (await AddAsync(goalId, 50m, new DateOnly(2026, 10, 1))).ContributionId!.Value;

        ContributionResult result = await Service().UpdateContributionAsync(UserId, goalId, id,
            new SaveContributionRequestDto { Amount = 80m, Date = new DateOnly(2026, 10, 3), Note = "Bonus" });

        Assert.Equal(ContributionOutcome.Saved, result.Outcome);
        ContributionDto edited = Assert.Single((await Service().GetContributionsAsync(UserId, goalId))!);
        Assert.Equal((80m, new DateOnly(2026, 10, 3), "Bonus"), (edited.Amount, edited.Date, edited.Note));
        Assert.Equal(80m, (await GoalAsync(goalId)).CurrentAmount);
    }

    [Fact]
    public async Task EditContribution_WithoutADate_KeepsItsDate()
    {
        int goalId = await CreateGoalAsync(target: 1_000m);
        int id = (await AddAsync(goalId, 50m, new DateOnly(2026, 10, 1))).ContributionId!.Value;

        await Service().UpdateContributionAsync(UserId, goalId, id, new SaveContributionRequestDto { Amount = 60m });

        Assert.Equal(new DateOnly(2026, 10, 1), Assert.Single((await Service().GetContributionsAsync(UserId, goalId))!).Date);
    }

    [Fact]
    public async Task EditContribution_ThatWouldLeaveTheGoalBelowZero_IsRefused()
    {
        int goalId = await CreateGoalAsync(target: 1_000m);
        int deposit = (await AddAsync(goalId, 100m)).ContributionId!.Value;
        await AddAsync(goalId, -60m);

        ContributionResult result = await Service().UpdateContributionAsync(UserId, goalId, deposit,
            new SaveContributionRequestDto { Amount = 50m });

        Assert.Equal(ContributionOutcome.BelowZero, result.Outcome);
        Assert.Equal(40m, (await GoalAsync(goalId)).CurrentAmount);
    }

    [Fact]
    public async Task DeleteContribution_RewritesTheSavedAmount()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 100m);
        int id = (await AddAsync(goalId, 30m)).ContributionId!.Value;

        ContributionResult result = await Service().DeleteContributionAsync(UserId, goalId, id);

        Assert.Equal(ContributionOutcome.Saved, result.Outcome);
        Assert.Equal(100m, (await GoalAsync(goalId)).CurrentAmount);
        Assert.Single((await Service().GetContributionsAsync(UserId, goalId))!);
    }

    [Fact]
    public async Task DeleteContribution_ThatTheBalanceDependsOn_IsRefused()
    {
        int goalId = await CreateGoalAsync(target: 1_000m);
        int deposit = (await AddAsync(goalId, 100m)).ContributionId!.Value;
        await AddAsync(goalId, -60m);

        ContributionResult result = await Service().DeleteContributionAsync(UserId, goalId, deposit);

        Assert.Equal(ContributionOutcome.BelowZero, result.Outcome);
        Assert.Equal(2, (await Service().GetContributionsAsync(UserId, goalId))!.Count);
    }

    [Fact]
    public async Task CorrectBalance_RecordsAnAdjustmentOfTheDifference_DatedToday()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 300m);

        ContributionResult result = await Service().CorrectBalanceAsync(UserId, goalId,
            new CorrectBalanceRequestDto { Balance = 260.5m });

        ContributionDto adjustment = (await Service().GetContributionsAsync(UserId, goalId))!
            .Single(c => c.Kind == ContributionKind.Adjustment);
        Assert.Equal(result.ContributionId, adjustment.Id);
        Assert.Equal((-39.5m, Today), (adjustment.Amount, adjustment.Date));
        Assert.Equal(260.5m, (await GoalAsync(goalId)).CurrentAmount);
    }

    [Fact]
    public async Task CorrectBalance_ToTheSameFigure_RecordsNothing()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 300m);

        ContributionResult result = await Service().CorrectBalanceAsync(UserId, goalId,
            new CorrectBalanceRequestDto { Balance = 300m });

        Assert.Equal(ContributionOutcome.Saved, result.Outcome);
        Assert.Null(result.ContributionId);
        Assert.Single((await Service().GetContributionsAsync(UserId, goalId))!);
    }

    [Fact]
    public async Task Adjustment_CanBeDeleted_ButNotEdited()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 300m);
        int id = (await Service().CorrectBalanceAsync(UserId, goalId, new CorrectBalanceRequestDto { Balance = 400m }))
            .ContributionId!.Value;

        ContributionResult edit = await Service().UpdateContributionAsync(UserId, goalId, id,
            new SaveContributionRequestDto { Amount = 5m });
        ContributionResult delete = await Service().DeleteContributionAsync(UserId, goalId, id);

        Assert.Equal(ContributionOutcome.NotEditable, edit.Outcome);
        Assert.Equal(ContributionOutcome.Saved, delete.Outcome);
        Assert.Equal(300m, (await GoalAsync(goalId)).CurrentAmount);
    }

    [Fact]
    public async Task OpeningContribution_CanBeEdited()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 300m);
        int id = Assert.Single((await Service().GetContributionsAsync(UserId, goalId))!).Id;

        ContributionResult result = await Service().UpdateContributionAsync(UserId, goalId, id,
            new SaveContributionRequestDto { Amount = 320m });

        Assert.Equal(ContributionOutcome.Saved, result.Outcome);
        Assert.Equal(320m, (await GoalAsync(goalId)).CurrentAmount);
    }

    [Fact]
    public async Task Reached_IsSetWhenTheSavedAmountMeetsTheTarget_AndClearedWhenItDropsBelow()
    {
        int goalId = await CreateGoalAsync(target: 500m, starting: 400m);
        Assert.False((await GoalAsync(goalId)).IsCompleted);

        await AddAsync(goalId, 100m);
        Assert.True((await GoalAsync(goalId)).IsCompleted);

        await AddAsync(goalId, -0.01m);
        Assert.False((await GoalAsync(goalId)).IsCompleted);
    }

    [Fact]
    public async Task UpdateGoal_LoweringTheTargetBelowTheSavedAmount_ReachesIt()
    {
        int goalId = await CreateGoalAsync(target: 500m, starting: 400m);

        await Service().UpdateSavingsGoalAsync(UserId, goalId, new UpdateSavingsGoalRequestDto { TargetAmount = 400m });

        SavingsGoalResponseDto goal = await GoalAsync(goalId);
        Assert.True(goal.IsCompleted);
        Assert.Equal(400m, goal.CurrentAmount);
    }

    [Fact]
    public async Task Milestones_AreEachSentOnce_EvenAcrossAWithdrawalAndRedeposit()
    {
        int goalId = await CreateGoalAsync(target: 1_000m);

        await AddAsync(goalId, 500m);
        await AddAsync(goalId, -200m);
        await AddAsync(goalId, 200m);
        await AddAsync(goalId, 300m);
        await AddAsync(goalId, -300m);
        await AddAsync(goalId, 300m);
        await AddAsync(goalId, 200m);

        Assert.Equal(["50", "75", "100"], _notifications.Created.Select(n => n.Arguments!["percent"]));
        Assert.Equal("$1,000.00", _notifications.Created[^1].Arguments!["current"]);
    }

    [Fact]
    public async Task Create_PartWayToTheTarget_SendsNoNotification_ForTheMilestonesItStartsPast()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 600m);

        await AddAsync(goalId, 100m);
        await AddAsync(goalId, 50m);

        Assert.Equal(["75"], _notifications.Created.Select(n => n.Arguments!["percent"]));
    }

    [Fact]
    public async Task AnotherUsersGoal_AndContributions_AreNeverVisibleOrChangeable()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 100m);
        int id = Assert.Single((await Service().GetContributionsAsync(UserId, goalId))!).Id;
        var request = new SaveContributionRequestDto { Amount = 5m };

        Assert.Null(await Service().GetContributionsAsync(OtherUserId, goalId));
        Assert.Empty(await Service().GetSavingsGoalsAsync(OtherUserId));
        Assert.Equal(ContributionOutcome.NotFound, (await Service().AddContributionAsync(OtherUserId, goalId, request)).Outcome);
        Assert.Equal(ContributionOutcome.NotFound,
            (await Service().UpdateContributionAsync(OtherUserId, goalId, id, request)).Outcome);
        Assert.Equal(ContributionOutcome.NotFound, (await Service().DeleteContributionAsync(OtherUserId, goalId, id)).Outcome);
        Assert.Equal(ContributionOutcome.NotFound, (await Service().CorrectBalanceAsync(OtherUserId, goalId,
            new CorrectBalanceRequestDto { Balance = 0m })).Outcome);
        Assert.Equal(100m, (await GoalAsync(goalId)).CurrentAmount);
    }

    [Fact]
    public async Task AContribution_IsOnlyReachableThroughItsOwnGoal()
    {
        int firstGoal = await CreateGoalAsync(target: 1_000m, starting: 100m);
        int secondGoal = await CreateGoalAsync(target: 1_000m, starting: 100m);
        int firstGoalsOpening = Assert.Single((await Service().GetContributionsAsync(UserId, firstGoal))!).Id;

        ContributionResult result = await Service().DeleteContributionAsync(UserId, secondGoal, firstGoalsOpening);

        Assert.Equal(ContributionOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task DeleteGoal_HidesItAndItsContributions()
    {
        int goalId = await CreateGoalAsync(target: 1_000m, starting: 100m);

        await Service().DeleteSavingsGoalAsync(UserId, goalId);

        Assert.Empty(await Service().GetSavingsGoalsAsync(UserId));
        Assert.Null(await Service().GetContributionsAsync(UserId, goalId));
        Assert.All(await _db.Contributions.IgnoreQueryFilters().ToListAsync(), c => Assert.True(c.IsDeleted));
    }

    [Fact]
    public async Task GoalList_GivesEachGoalItsOwnContributions()
    {
        int first = await CreateGoalAsync(target: 1_000m, starting: 100m);
        int second = await CreateGoalAsync(target: 1_000m);
        await AddAsync(second, 20m);
        await AddAsync(second, 30m);

        List<SavingsGoalResponseDto> goals = await Service().GetSavingsGoalsAsync(UserId);

        Assert.Single(goals.Single(g => g.Id == first).Contributions);
        Assert.Equal(2, goals.Single(g => g.Id == second).Contributions.Count);
    }

    private async Task<int> CreateGoalAsync(decimal target, decimal starting = 0m) =>
        await Service().CreateSavingsGoalAsync(UserId,
            new CreateSavingsGoalRequestDto { Name = "Emergency Fund", TargetAmount = target, CurrentAmount = starting });

    private Task<ContributionResult> AddAsync(int goalId, decimal amount, DateOnly? date = null, string? note = null) =>
        Service().AddContributionAsync(UserId, goalId,
            new SaveContributionRequestDto { Amount = amount, Date = date, Note = note });

    private async Task<SavingsGoalResponseDto> GoalAsync(int goalId) =>
        (await Service().GetSavingsGoalsAsync(UserId)).Single(g => g.Id == goalId);

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<CreateNotificationRequestDto> Created { get; } = [];

        public Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Created.Add(request);
            return Task.FromResult(Created.Count);
        }

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
