using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;

namespace PersonalFinance.Tests.Dashboard;

/// <summary>
/// Behaviour of the Dashboard goals overview (aggregated across all goals) and recent activity ordering.
/// </summary>
public class DashboardGoalsOverviewTests
{
    private const string UserId = "goals-user";
    private const string OtherUserId = "other-goals-user";

    private static readonly int[] ExpectedNewestFiveGoalIds = [7, 6, 5, 4, 3];
    private static readonly string[] ExpectedMixedActivityTitles = ["New income", "Goal 1", "Old expense"];
    private static readonly string[] ExpectedMixedActivityTypes = ["Income", "SavingsGoal", "Expense"];
    private static readonly string[] ExpectedCappedActivityTitles = ["Tx 6", "Tx 5", "Tx 4", "Goal 4", "Tx 3"];
    private static readonly string[] ExpectedSameDateActivityTitles = ["Tx B", "Tx A", "Goal B", "Goal A"];

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardGoals_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Task<DashboardSummaryDto> GetSummaryAsync(AppDbContext db, string userId = UserId)
    {
        return new DashboardService(db, TestBudgetDataProviderFactory.Create(db), TestCurrencyConverter.Create(db)).GetDashboardSummaryAsync(userId);
    }

    private static SavingsGoal Goal(int id, decimal target, decimal current, bool completed = false,
        string userId = UserId, bool deleted = false)
    {
        return new SavingsGoal
        {
            Id = id,
            UserId = userId,
            Name = $"Goal {id}",
            TargetAmount = target,
            CurrentAmount = current,
            IsCompleted = completed,
            IsDeleted = deleted,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(id)
        };
    }

    [Fact]
    public async Task Overview_WithSevenGoals_CountsAllSevenWhileListReturnsFive()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.AddRange(Enumerable.Range(1, 7).Select(i => Goal(i, 100m, 25m, completed: i <= 2)));
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(ExpectedNewestFiveGoalIds, summary.SavingsGoals.Select(g => g.Id));
        Assert.Equal(7, summary.GoalsOverview.TotalCount);
        Assert.Equal(2, summary.GoalsOverview.CompletedCount);
        Assert.Equal(175m, summary.GoalsOverview.TotalSaved);
        Assert.Equal(700m, summary.GoalsOverview.TotalTarget);
        Assert.Equal(25m, summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task Overview_WhenSavedExceedsTarget_CapsFundedPercentAt100_ButReportsActualTotalSaved()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.AddRange(Goal(1, 100m, 250m, completed: true), Goal(2, 100m, 100m, completed: true));
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(350m, summary.GoalsOverview.TotalSaved);
        Assert.Equal(100m, summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task Overview_WhenTotalTargetIsZero_FundedPercentIsNull()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.Add(Goal(1, 0m, 0m));
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(1, summary.GoalsOverview.TotalCount);
        Assert.Equal(0m, summary.GoalsOverview.TotalTarget);
        Assert.Null(summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task Overview_WithNoGoals_IsEmptyWithNullFundedPercent()
    {
        await using AppDbContext db = CreateContext();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.NotNull(summary.GoalsOverview);
        Assert.Equal(0, summary.GoalsOverview.TotalCount);
        Assert.Equal(0, summary.GoalsOverview.CompletedCount);
        Assert.Equal(0m, summary.GoalsOverview.TotalSaved);
        Assert.Equal(0m, summary.GoalsOverview.TotalTarget);
        Assert.Null(summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task Overview_ExcludesOtherUsersAndSoftDeletedGoals()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.AddRange(
            Goal(1, 200m, 50m),
            Goal(2, 1_000m, 1_000m, completed: true, userId: OtherUserId),
            Goal(3, 500m, 500m, completed: true, deleted: true));
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(1, summary.GoalsOverview.TotalCount);
        Assert.Equal(0, summary.GoalsOverview.CompletedCount);
        Assert.Equal(50m, summary.GoalsOverview.TotalSaved);
        Assert.Equal(200m, summary.GoalsOverview.TotalTarget);
        Assert.Equal(25m, summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task RecentActivity_MixesTransactionsAndGoalsNewestFirst()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.Add(Goal(1, 100m, 10m));
        db.SpreadsheetTransactionRecords.AddRange(
            new SpreadsheetTransactionRecord
            {
                Id = 1,
                UserId = UserId,
                Description = "Old expense",
                Category = "Food",
                Amount = 5m,
                Type = "Expense",
                Date = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new SpreadsheetTransactionRecord
            {
                Id = 2,
                UserId = UserId,
                Description = "New income",
                Category = "Salary",
                Amount = 50m,
                Type = "Income",
                Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(ExpectedMixedActivityTitles, summary.RecentActivity.Select(a => a.Title));
        Assert.Equal(ExpectedMixedActivityTypes, summary.RecentActivity.Select(a => a.Type));
    }

    [Fact]
    public async Task Overview_CapsEachGoalAtItsTarget_SoOverFundedGoalsCannotMaskUnfundedOnes()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.AddRange(Goal(1, 1_000m, 2_000m, completed: true), Goal(2, 1_000m, 0m));
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(50m, summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task RecentActivity_ReturnsAtMostFiveItemsNewestFirst()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.AddRange(Enumerable.Range(1, 4).Select(i => Goal(i, 100m, 10m)));
        db.SpreadsheetTransactionRecords.AddRange(Enumerable.Range(1, 6).Select(i => new SpreadsheetTransactionRecord
        {
            Id = i,
            UserId = UserId,
            Description = $"Tx {i}",
            Category = "Food",
            Amount = 5m,
            Type = "Expense",
            Date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i).AddHours(12)
        }));
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        // Goals are created at midnight on day i, transactions at noon on day i; only three goals are considered.
        Assert.Equal(ExpectedCappedActivityTitles, summary.RecentActivity.Select(a => a.Title));
    }

    [Fact]
    public async Task RecentActivity_WhenDatesTie_OrdersDeterministically()
    {
        await using AppDbContext db = CreateContext();
        var date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        db.SavingsGoals.AddRange(
            new SavingsGoal { Id = 1, UserId = UserId, Name = "Goal A", TargetAmount = 100m, CreatedAt = date },
            new SavingsGoal { Id = 2, UserId = UserId, Name = "Goal B", TargetAmount = 100m, CreatedAt = date });
        db.SpreadsheetTransactionRecords.AddRange(
            new SpreadsheetTransactionRecord
            {
                Id = 1,
                UserId = UserId,
                Description = "Tx A",
                Category = "Food",
                Amount = 5m,
                Type = "Expense",
                Date = date
            },
            new SpreadsheetTransactionRecord
            {
                Id = 2,
                UserId = UserId,
                Description = "Tx B",
                Category = "Food",
                Amount = 5m,
                Type = "Expense",
                Date = date
            });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        // Same date: transactions before goals, then newest record (highest Id) first within each source.
        Assert.Equal(ExpectedSameDateActivityTitles, summary.RecentActivity.Select(a => a.Title));
    }
}
