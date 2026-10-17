using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Shared.DTOs.NetWorth;
using Xunit;

namespace PersonalFinance.Tests;

/// <summary>
/// Net worth projection on the Dashboard: points come from the Net Worth projection calculation for the stored
/// parameters, plus FI years and the age at which the Base scenario reaches the FI Target.
/// </summary>
public class DashboardProjectionTests
{
    private const string UserId = "dashboard-user";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardProjection_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<NetWorthSummaryDto?> GetNetWorthAsync(AppDbContext db, NetWorthParameters row)
    {
        db.NetWorthParameters.Add(row);
        await db.SaveChangesAsync();
        var summary = await new DashboardService(db).GetDashboardSummaryAsync(UserId);
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
        await using var db = CreateContext();
        var parameters = Parameters();
        var expected = NetWorthPageProjection(parameters);

        var netWorth = await GetNetWorthAsync(db, parameters);

        var points = netWorth!.ProjectionPoints;
        Assert.Equal(expected.Base.Years.Count, points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            Assert.Equal(expected.Base.Years[i].Year, points[i].Year);
            Assert.Equal(expected.Base.Years[i].Age, points[i].Age);
            Assert.Equal(expected.Base.Years[i].EndingBalance, points[i].Base);
            Assert.Equal(expected.Conservative.Years[i].EndingBalance, points[i].Conservative);
            Assert.Equal(expected.Aggressive.Years[i].EndingBalance, points[i].Aggressive);
        }
    }

    [Fact]
    public async Task ProjectionPoints_RunFromCurrentAgeThroughRetirementAge()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(currentAge: 35, retirementAge: 60));

        var points = netWorth!.ProjectionPoints;
        Assert.Equal(26, points.Count);
        Assert.Equal(35, points[0].Age);
        Assert.Equal(60, points[^1].Age);
        Assert.Equal(DateTime.Now.Year, points[0].Year);
    }

    [Fact]
    public async Task ProjectionPoints_FirstYear_AppliesSavingsAndReturn()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(balance: 100_000m, savings: 20_000m, baseRoi: 7m,
            conservativeRoi: 4m, aggressiveRoi: 10m, aggressiveSavings: 30_000m));

        var first = netWorth!.ProjectionPoints[0];
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
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(currentAge: currentAge, retirementAge: retirementAge));

        Assert.Empty(netWorth!.ProjectionPoints);
        Assert.Null(netWorth.FiReachedAge);
        Assert.Null(netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_IsTheFirstAgeWhereBaseReachesTheFiTarget()
    {
        await using var db = CreateContext();
        var parameters = Parameters(spending: 40_000m);
        var expected = NetWorthPageProjection(parameters).Base.Years
            .First(y => y.EndingBalance >= 1_000_000m).Age;

        var netWorth = await GetNetWorthAsync(db, parameters);

        Assert.Equal(expected, netWorth!.FiReachedAge);
        Assert.Equal(expected - 35, netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_ZeroReturnAndExactTarget_ReachesOnTheBoundary()
    {
        await using var db = CreateContext();

        // FI Target 1,000,000; 0% return; 100,000 start + 100,000/yr → ending balance hits exactly 1,000,000 at age 43.
        var netWorth = await GetNetWorthAsync(db, Parameters(balance: 100_000m, savings: 100_000m, spending: 40_000m,
            currentAge: 35, retirementAge: 60, baseRoi: 0m));

        Assert.Equal(43, netWorth!.FiReachedAge);
        Assert.Equal(8, netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_WhenAlreadyAboveTarget_IsCurrentAge()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(balance: 2_000_000m, spending: 40_000m));

        Assert.Equal(35, netWorth!.FiReachedAge);
        Assert.Equal(0, netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_WhenBaseNeverReachesTheTargetBeforeRetirement_IsNull()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(balance: 0m, savings: 1_000m, spending: 200_000m,
            retirementAge: 40, baseRoi: 0m));

        Assert.NotEmpty(netWorth!.ProjectionPoints);
        Assert.Null(netWorth.FiReachedAge);
        Assert.Null(netWorth.FiYears);
    }

    [Fact]
    public async Task FiReachedAge_WhenFiTargetIsZero_IsNull()
    {
        await using var db = CreateContext();

        var netWorth = await GetNetWorthAsync(db, Parameters(spending: 0m));

        Assert.NotEmpty(netWorth!.ProjectionPoints);
        Assert.Null(netWorth.FiReachedAge);
        Assert.Null(netWorth.FiYears);
    }
}
