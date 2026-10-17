using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using Xunit;

namespace PersonalFinance.Tests;

/// <summary>
/// Net Worth summary on the Dashboard: FI Target, Progress to FI, and Savings Rate,
/// exercised against an in-memory database.
/// </summary>
public class DashboardNetWorthSummaryTests
{
    private const string UserId = "dashboard-user";
    private const string OtherUserId = "other-user";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardNetWorth_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<NetWorthSummaryDto?> GetNetWorthAsync(AppDbContext db, params NetWorthParameters[] rows)
    {
        db.NetWorthParameters.AddRange(rows);
        await db.SaveChangesAsync();
        var summary = await new DashboardService(db).GetDashboardSummaryAsync(UserId);
        return summary.NetWorth;
    }

    private static NetWorthParameters Parameters(decimal balance = 100_000m, decimal spending = 40_000m,
        decimal income = 80_000m, decimal savings = 20_000m, string userId = UserId) => new()
    {
        UserId = userId,
        StartingBalance = balance,
        AnnualSpending = spending,
        AnnualIncome = income,
        AnnualSavings = savings,
        CurrentAge = 35,
        RetirementAge = 60
    };

    [Fact]
    public async Task FiTarget_IsAnnualSpendingTimesTwentyFive()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(spending: 40_000m));

        Assert.Equal(1_000_000m, netWorth!.FiTarget);
    }

    [Fact]
    public async Task ProgressToFi_IsNetWorthOverFiTarget()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(balance: 250_000m, spending: 40_000m));

        Assert.Equal(25m, netWorth!.ProgressToFiPercent);
        Assert.False(netWorth.FiTargetReached);
    }

    [Fact]
    public async Task ProgressToFi_AboveOneHundredPercent_IsReportedUncappedAndMarkedReached()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(balance: 1_500_000m, spending: 40_000m));

        Assert.Equal(150m, netWorth!.ProgressToFiPercent);
        Assert.True(netWorth.FiTargetReached);
    }

    [Fact]
    public async Task ProgressToFi_ExactlyAtTarget_IsMarkedReached()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(balance: 1_000_000m, spending: 40_000m));

        Assert.Equal(100m, netWorth!.ProgressToFiPercent);
        Assert.True(netWorth.FiTargetReached);
    }

    [Fact]
    public async Task ProgressToFi_WhenSpendingIsZero_IsUnavailable()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(balance: 500_000m, spending: 0m));

        Assert.Equal(0m, netWorth!.FiTarget);
        Assert.Null(netWorth.ProgressToFiPercent);
        Assert.False(netWorth.FiTargetReached);
    }

    [Fact]
    public async Task SavingsRate_IsAnnualSavingsOverAnnualIncome()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(income: 80_000m, savings: 20_000m));

        Assert.Equal(25m, netWorth!.SavingsRatePercent);
    }

    [Fact]
    public async Task SavingsRate_WhenIncomeIsZero_IsUnavailable()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(income: 0m, savings: 20_000m));

        Assert.Null(netWorth!.SavingsRatePercent);
    }

    [Fact]
    public async Task Summary_ExposesCurrentNetWorthAndAges()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(balance: 123_456.78m));

        Assert.Equal(123_456.78m, netWorth!.CurrentNetWorth);
        Assert.Equal(35, netWorth.CurrentAge);
        Assert.Equal(60, netWorth.RetirementAge);
    }

    [Fact]
    public async Task Summary_UsesOnlyTheRequestingUsersParameters()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db,
            Parameters(balance: 999_999m, spending: 1m, userId: OtherUserId),
            Parameters(balance: 100_000m, spending: 40_000m));

        Assert.Equal(100_000m, netWorth!.CurrentNetWorth);
        Assert.Equal(10m, netWorth.ProgressToFiPercent);
    }

    [Fact]
    public async Task Summary_ExcludesSoftDeletedParameters()
    {
        await using var db = CreateContext();
        var deleted = Parameters();
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;

        var netWorth = await GetNetWorthAsync(db, deleted);

        Assert.Null(netWorth);
    }
}
