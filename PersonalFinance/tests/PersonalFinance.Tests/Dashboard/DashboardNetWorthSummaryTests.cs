using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;

namespace PersonalFinance.Tests.Dashboard;

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
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardNetWorth_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<NetWorthSummaryDto?> GetNetWorthAsync(AppDbContext db, params NetWorthParameters[] rows)
    {
        db.NetWorthParameters.AddRange(rows);
        await db.SaveChangesAsync();
        DashboardSummaryDto summary =
            await new DashboardService(db, TestBudgetDataProviderFactory.Create(db), TestCurrencyConverter.Create(db)).GetDashboardSummaryAsync(UserId);
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
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(spending: 40_000m));

        Assert.Equal(1_000_000m, netWorth!.FiTarget);
    }

    [Fact]
    public async Task ProgressToFi_IsNetWorthOverFiTarget()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 250_000m, spending: 40_000m));

        Assert.Equal(25m, netWorth!.ProgressToFiPercent);
        Assert.False(netWorth.FiTargetReached);
    }

    [Fact]
    public async Task ProgressToFi_AboveOneHundredPercent_IsReportedUncappedAndMarkedReached()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 1_500_000m, spending: 40_000m));

        Assert.Equal(150m, netWorth!.ProgressToFiPercent);
        Assert.True(netWorth.FiTargetReached);
    }

    [Fact]
    public async Task ProgressToFi_ExactlyAtTarget_IsMarkedReached()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 1_000_000m, spending: 40_000m));

        Assert.Equal(100m, netWorth!.ProgressToFiPercent);
        Assert.True(netWorth.FiTargetReached);
    }

    [Fact]
    public async Task ProgressToFi_WhenSpendingIsZero_IsUnavailable()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 500_000m, spending: 0m));

        Assert.Equal(0m, netWorth!.FiTarget);
        Assert.Null(netWorth.ProgressToFiPercent);
        Assert.False(netWorth.FiTargetReached);
    }

    [Fact]
    public async Task SavingsRate_IsAnnualSavingsOverAnnualIncome()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(income: 80_000m, savings: 20_000m));

        Assert.Equal(25m, netWorth!.SavingsRatePercent);
    }

    [Fact]
    public async Task SavingsRate_WhenIncomeIsZero_IsUnavailable()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(income: 0m, savings: 20_000m));

        Assert.Null(netWorth!.SavingsRatePercent);
    }

    [Fact]
    public async Task Summary_ExposesCurrentNetWorthAndAges()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 123_456.78m));

        Assert.Equal(123_456.78m, netWorth!.CurrentNetWorth);
        Assert.Equal(35, netWorth.CurrentAge);
        Assert.Equal(60, netWorth.RetirementAge);
    }

    [Fact]
    public async Task Summary_UsesOnlyTheRequestingUsersParameters()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db,
            Parameters(balance: 999_999m, spending: 1m, userId: OtherUserId),
            Parameters(balance: 100_000m, spending: 40_000m));

        Assert.Equal(100_000m, netWorth!.CurrentNetWorth);
        Assert.Equal(10m, netWorth.ProgressToFiPercent);
    }

    [Fact]
    public async Task Summary_ExcludesSoftDeletedParameters()
    {
        await using AppDbContext db = CreateContext();
        NetWorthParameters deleted = Parameters();
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, deleted);

        Assert.Null(netWorth);
    }
}
