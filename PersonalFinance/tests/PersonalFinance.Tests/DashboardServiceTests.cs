using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;
using Xunit;

namespace PersonalFinance.Tests;

/// <summary>
/// Behaviour of the Dashboard service's summary, exercised against an in-memory database.
/// </summary>
public class DashboardServiceTests
{
    private const string UserId = "dashboard-user";
    private const string OtherUserId = "other-user";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Dashboard_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Task<DashboardSummaryDto> GetSummaryAsync(AppDbContext db, string userId = UserId)
    {
        return new DashboardService(db).GetDashboardSummaryAsync(userId);
    }

    [Fact]
    public async Task EmptyAccount_ReturnsStructuredEmptySummary()
    {
        await using var db = CreateContext();

        var summary = await GetSummaryAsync(db);

        Assert.NotNull(summary);
        Assert.Null(summary.NetWorth);
        Assert.Null(summary.Budget);
        Assert.Empty(summary.SavingsGoals);
        Assert.Empty(summary.RecentActivity);
    }

    [Fact]
    public async Task Budget_WhenTwoAllocationsShareThePeriod_UsesTheNewestRecord()
    {
        await using var db = CreateContext();
        db.UserBudgetAllocations.AddRange(
            new UserBudgetAllocation { Id = 1, UserId = UserId, Period = "2026-10", MonthlyIncome = 4_000m },
            new UserBudgetAllocation { Id = 2, UserId = UserId, Period = "2026-10", MonthlyIncome = 5_000m },
            new UserBudgetAllocation { Id = 3, UserId = UserId, Period = "2026-09", MonthlyIncome = 9_000m });
        await db.SaveChangesAsync();

        var summary = await GetSummaryAsync(db);

        Assert.Equal(5_000m, summary.Budget!.MonthlyIncome);
    }

    [Fact]
    public async Task SavingsGoals_WhenCreatedAtTies_OrdersNewestRecordFirst()
    {
        await using var db = CreateContext();
        var createdAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        db.SavingsGoals.AddRange(
            new SavingsGoal { Id = 1, UserId = UserId, Name = "Older", TargetAmount = 100m, CreatedAt = createdAt },
            new SavingsGoal { Id = 2, UserId = UserId, Name = "Newer", TargetAmount = 100m, CreatedAt = createdAt });
        await db.SaveChangesAsync();

        var summary = await GetSummaryAsync(db);

        Assert.Equal(new[] { "Newer", "Older" }, summary.SavingsGoals.Select(g => g.Name));
    }

    [Fact]
    public async Task Summary_ExcludesOtherUsersAndSoftDeletedRecords()
    {
        await using var db = CreateContext();
        db.NetWorthParameters.Add(new NetWorthParameters { UserId = OtherUserId, StartingBalance = 1m });
        db.SavingsGoals.AddRange(
            new SavingsGoal { UserId = OtherUserId, Name = "Not mine", TargetAmount = 100m },
            new SavingsGoal { UserId = UserId, Name = "Deleted", TargetAmount = 100m, IsDeleted = true });
        await db.SaveChangesAsync();

        var summary = await GetSummaryAsync(db);

        Assert.Null(summary.NetWorth);
        Assert.Empty(summary.SavingsGoals);
        Assert.Empty(summary.RecentActivity);
    }
}
