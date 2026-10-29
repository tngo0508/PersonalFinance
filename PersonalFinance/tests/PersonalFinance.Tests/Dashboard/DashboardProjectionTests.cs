using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.Tests.Dashboard;

/// <summary>
/// Net worth projection on the Dashboard: points come from the Net Worth projection calculation for the stored
/// parameters, plus FI years and the age at which the Base scenario reaches the FI Target.
/// </summary>
public class DashboardProjectionTests
{
    private const string UserId = "dashboard-user";

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardProjection_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<NetWorthSummaryDto?> GetNetWorthAsync(AppDbContext db, NetWorthParameters row)
    {
        db.NetWorthParameters.Add(row);
        await db.SaveChangesAsync();
        DashboardSummaryDto summary =
            await new DashboardService(db, TestBudgetDataProviderFactory.Create(db), TestCurrencyConverter.Create(db)).GetDashboardSummaryAsync(UserId);
        return summary.NetWorth;
    }

    private static NetWorthParameters Parameters(decimal balance = 100_000m, decimal savings = 20_000m,
        decimal spending = 40_000m, int currentAge = 35, int retirementAge = 60, decimal baseRoi = 7m,
        decimal conservativeRoi = 4m, decimal aggressiveRoi = 10m, decimal aggressiveSavings = 30_000m) => new()
        {
            UserId = UserId,
            StartingBalance = balance,
            AnnualSavings = savings,
            AnnualIncome = 80_000m,
            AnnualSpending = spending,
            CurrentAge = currentAge,
            RetirementAge = retirementAge,
            BaseRoi = baseRoi,
            ConservativeRoi = conservativeRoi,
            AggressiveRoi = aggressiveRoi,
            AggressiveSavings = aggressiveSavings
        };

    private static NetWorthProjectionDto NetWorthPageProjection(NetWorthParameters p) =>
        NetWorthProjectionService.ComputeProjection(new NetWorthProjectionRequestDto
        {
            StartingBalance = p.StartingBalance,
            AnnualSavings = p.AnnualSavings,
            AnnualIncome = p.AnnualIncome,
            AnnualSpending = p.AnnualSpending,
            CurrentAge = p.CurrentAge,
            RetirementAge = p.RetirementAge,
            BaseRoi = p.BaseRoi,
            ConservativeRoi = p.ConservativeRoi,
            AggressiveRoi = p.AggressiveRoi,
            AggressiveSavings = p.AggressiveSavings
        });

    [Fact]
    public async Task ProjectionPoints_MatchTheNetWorthPageProjectionForTheSameParameters()
    {
        await using AppDbContext db = CreateContext();
        NetWorthParameters parameters = Parameters();
        NetWorthProjectionDto expected = NetWorthPageProjection(parameters);

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, parameters);

        // The first point is today; each later one is a projected year.
        List<ProjectionPointDto> points = netWorth!.ProjectionPoints;
        Assert.Equal(expected.Base.Years.Count + 1, points.Count);
        for (int i = 0; i < expected.Base.Years.Count; i++)
        {
            Assert.Equal(expected.Base.Years[i].Year, points[i + 1].Year);
            Assert.Equal(expected.Base.Years[i].Age, points[i + 1].Age);
            Assert.Equal(expected.Base.Years[i].EndingBalance, points[i + 1].Base);
            Assert.Equal(expected.Conservative.Years[i].EndingBalance, points[i + 1].Conservative);
            Assert.Equal(expected.Aggressive.Years[i].EndingBalance, points[i + 1].Aggressive);
        }
    }

    [Fact]
    public async Task ProjectionPoints_RunFromTodayAtCurrentAgeThroughRetirementAge()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 100_000m, currentAge: 35, retirementAge: 60));

        List<ProjectionPointDto> points = netWorth!.ProjectionPoints;
        Assert.Equal(26, points.Count);
        Assert.Equal(35, points[0].Age);
        Assert.Equal(DateTime.Now.Year, points[0].Year);
        Assert.Equal(100_000m, points[0].Base);
        Assert.Equal(100_000m, points[0].Conservative);
        Assert.Equal(100_000m, points[0].Aggressive);
        Assert.Equal(36, points[1].Age);
        Assert.Equal(60, points[^1].Age);
    }

    [Fact]
    public async Task ProjectionPoints_FirstYear_AppliesSavingsAndReturn()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 100_000m, savings: 20_000m, baseRoi: 7m,
            conservativeRoi: 4m, aggressiveRoi: 10m, aggressiveSavings: 30_000m));

        ProjectionPointDto first = netWorth!.ProjectionPoints[1];
        Assert.Equal(127_000m, first.Base);
        Assert.Equal(124_000m, first.Conservative);
        Assert.Equal(140_000m, first.Aggressive);
    }

    [Theory]
    [InlineData(60, 60)]
    [InlineData(65, 60)]
    public async Task ProjectionPoints_WhenRetirementAgeIsAtOrBelowCurrentAge_AreEmpty(int currentAge,
        int retirementAge)
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(currentAge: currentAge, retirementAge: retirementAge));

        Assert.Empty(netWorth!.ProjectionPoints);
        Assert.Null(netWorth.FiReachedAge);
        Assert.Null(netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_IsTheFirstAgeWhereBaseReachesTheFiTarget()
    {
        await using AppDbContext db = CreateContext();
        NetWorthParameters parameters = Parameters(spending: 40_000m);
        int expected = NetWorthPageProjection(parameters).Base.Years
            .First(y => y.EndingBalance >= 1_000_000m).Age;

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, parameters);

        Assert.Equal(expected, netWorth!.FiReachedAge);
        Assert.Equal(expected - 35, netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_ZeroReturnAndExactTarget_ReachesOnTheBoundary()
    {
        await using AppDbContext db = CreateContext();

        // FI Target 1,000,000; 0% return; 100,000 today at 35 + 100,000/yr → nine years of saving, exactly 1,000,000 at 44.
        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 100_000m, savings: 100_000m, spending: 40_000m,
            currentAge: 35, retirementAge: 60, baseRoi: 0m));

        Assert.Equal(44, netWorth!.FiReachedAge);
        Assert.Equal(9, netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_WhenAlreadyAboveTarget_IsCurrentAge()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 2_000_000m, spending: 40_000m));

        Assert.Equal(35, netWorth!.FiReachedAge);
        Assert.Equal(0, netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_WhenBaseNeverReachesTheTargetBeforeRetirement_IsNull()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(balance: 0m, savings: 1_000m, spending: 200_000m,
            retirementAge: 40, baseRoi: 0m));

        Assert.NotEmpty(netWorth!.ProjectionPoints);
        Assert.Null(netWorth.FiReachedAge);
        Assert.Null(netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_WhenFiTargetIsZero_IsNull()
    {
        await using AppDbContext db = CreateContext();

        NetWorthSummaryDto? netWorth = await GetNetWorthAsync(db, Parameters(spending: 0m));

        Assert.NotEmpty(netWorth!.ProjectionPoints);
        Assert.Null(netWorth.FiReachedAge);
        Assert.Null(netWorth.FiYears);
    }
}
