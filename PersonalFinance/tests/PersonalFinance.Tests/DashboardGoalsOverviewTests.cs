using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using Xunit;

namespace PersonalFinance.Tests;

/// <summary>
/// Behaviour of the Dashboard goals overview (aggregated across all goals) and recent activity ordering.
/// </summary>
public class DashboardGoalsOverviewTests
{
    private const string UserId = "goals-user";
    private const string OtherUserId = "other-goals-user";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardGoals_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Task<DashboardSummaryDto> GetSummaryAsync(AppDbContext db, string userId = UserId)
    {
        return new DashboardService(db).GetDashboardSummaryAsync(userId);
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
        await using var db = CreateContext();
        db.SavingsGoals.AddRange(Enumerable.Range(1, 7).Select(i => Goal(i, 100m, 25m, completed: i <= 2)));
        await db.SaveChangesAsync();

        var summary = await GetSummaryAsync(db);

        Assert.Equal(new[] { 7, 6, 5, 4, 3 }, summary.SavingsGoals.Select(g => g.Id));
        Assert.Equal(7, summary.GoalsOverview.TotalCount);
        Assert.Equal(2, summary.GoalsOverview.CompletedCount);
        Assert.Equal(175m, summary.GoalsOverview.TotalSaved);
        Assert.Equal(700m, summary.GoalsOverview.TotalTarget);
        Assert.Equal(25m, summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task Overview_WhenSavedExceedsTarget_CapsFundedPercentAt100()
    {
        await using var db = CreateContext();
        db.SavingsGoals.AddRange(Goal(1, 100m, 250m, completed: true), Goal(2, 100m, 50m));
        await db.SaveChangesAsync();

        var summary = await GetSummaryAsync(db);

        Assert.Equal(300m, summary.GoalsOverview.TotalSaved);
        Assert.Equal(100m, summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task Overview_WhenTotalTargetIsZero_FundedPercentIsNull()
    {
        await using var db = CreateContext();
        db.SavingsGoals.Add(Goal(1, 0m, 0m));
        await db.SaveChangesAsync();

        var summary = await GetSummaryAsync(db);

        Assert.Equal(1, summary.GoalsOverview.TotalCount);
        Assert.Equal(0m, summary.GoalsOverview.TotalTarget);
        Assert.Null(summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task Overview_WithNoGoals_IsEmptyWithNullFundedPercent()
    {
        await using var db = CreateContext();

        var summary = await GetSummaryAsync(db);

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
        await using var db = CreateContext();
        db.SavingsGoals.AddRange(
            Goal(1, 200m, 50m),
            Goal(2, 1_000m, 1_000m, completed: true, userId: OtherUserId),
            Goal(3, 500m, 500m, completed: true, deleted: true));
        await db.SaveChangesAsync();

        var summary = await GetSummaryAsync(db);

        Assert.Equal(1, summary.GoalsOverview.TotalCount);
        Assert.Equal(0, summary.GoalsOverview.CompletedCount);
        Assert.Equal(50m, summary.GoalsOverview.TotalSaved);
        Assert.Equal(200m, summary.GoalsOverview.TotalTarget);
        Assert.Equal(25m, summary.GoalsOverview.FundedPercent);
    }

    [Fact]
    public async Task RecentActivity_MixesTransactionsAndGoalsNewestFirst()
    {
        await using var db = CreateContext();
        db.SavingsGoals.Add(Goal(1, 100m, 10m));
        db.SpreadsheetTransactionRecords.AddRange(
            new SpreadsheetTransactionRecord
            {
                Id = 1, UserId = UserId, Description = "Old expense", Category = "Food", Amount = 5m,
                Type = "Expense", Date = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new SpreadsheetTransactionRecord
            {
                Id = 2, UserId = UserId, Description = "New income", Category = "Salary", Amount = 50m,
                Type = "Income", Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();

        var summary = await GetSummaryAsync(db);

        Assert.Equal(new[] { "New income", "Goal 1", "Old expense" }, summary.RecentActivity.Select(a => a.Title));
        Assert.Equal(new[] { "Income", "SavingsGoal", "Expense" }, summary.RecentActivity.Select(a => a.Type));
    }
}
