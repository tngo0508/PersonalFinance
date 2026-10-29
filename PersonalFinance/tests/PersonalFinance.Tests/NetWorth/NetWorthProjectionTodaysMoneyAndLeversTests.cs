using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Tests.NetWorth;

/// <summary>
/// The projection in today's money (each year's balance with the assumed inflation taken out) and the levers: what
/// one change to the Base inputs does to the age the FI Target is reached.
/// </summary>
public class NetWorthProjectionTodaysMoneyAndLeversTests
{
    private static NetWorthProjectionDto Project(decimal balance = 0m, decimal savings = 95_000m,
        decimal spending = 40_000m, int currentAge = 30, int retirementAge = 60, decimal baseRoi = 0m,
        decimal inflation = 0m, string? currency = null) =>
        NetWorthProjectionService.ComputeProjection(new NetWorthProjectionRequestDto
        {
            CurrencyCode = currency,
            StartingBalance = balance,
            AnnualSavings = savings,
            AnnualIncome = 150_000m,
            AnnualSpending = spending,
            CurrentAge = currentAge,
            RetirementAge = retirementAge,
            BaseRoi = baseRoi,
            ConservativeRoi = baseRoi,
            AggressiveRoi = baseRoi,
            AggressiveSavings = savings,
            InflationPercent = inflation
        });

    [Fact]
    public void WithoutInflation_TodaysMoneyEqualsTheProjectedAmounts()
    {
        NetWorthProjectionDto projection = Project(baseRoi: 7m);

        Assert.All(projection.Base.Years, year => Assert.Equal(year.EndingBalance, year.RealEndingBalance));
        Assert.Equal(projection.Base.FinalNetWorth, projection.Base.RealFinalNetWorth);
        Assert.Equal(projection.Base.FiReachedAge, projection.Base.RealFiReachedAge);
    }

    [Fact]
    public void TodaysMoney_DividesEachYearEndByTheInflationSinceToday()
    {
        // 0% return: 95,000 after one year, 190,000 after two; at 10% inflation that is 95,000 / 1.1 and 190,000 / 1.21.
        List<NetWorthYearDto> years = Project(inflation: 10m).Base.Years;

        Assert.Equal(1.1m, years[0].InflationFactor);
        Assert.Equal(1.21m, years[1].InflationFactor);
        Assert.Equal(95_000m / 1.1m, years[0].RealEndingBalance);
        Assert.Equal(190_000m / 1.21m, years[1].RealEndingBalance);
    }

    [Fact]
    public void RealFiReachedAge_WhenInflationErodesTheBalance_IsUnavailable()
    {
        // 990,000 today, 50,000 a year at 0%: 1,040,000 at 31 passes the 1,000,000 FI Target as projected, but at 10%
        // inflation that is 945,454 in today's money, and every later year is worth less again.
        NetWorthScenarioDto scenario = Project(balance: 990_000m, savings: 50_000m, inflation: 10m).Base;

        Assert.Equal(31, scenario.FiReachedAge);
        Assert.Null(scenario.RealFiReachedAge);
    }

    [Fact]
    public void FiReachedAges_WhenAlreadyAtTheTargetToday_AreBothTheCurrentAge()
    {
        // Today's money is today's money: a balance at the target now is at the target in both views.
        NetWorthScenarioDto scenario = Project(balance: 1_000_000m, savings: 0m, inflation: 3m).Base;

        Assert.Equal(30, scenario.FiReachedAge);
        Assert.Equal(30, scenario.RealFiReachedAge);
    }

    [Fact]
    public void EachYear_CarriesWhatWasPutInSoFar()
    {
        List<NetWorthYearDto> years = Project(balance: 10_000m, savings: 1_000m, baseRoi: 5m).Base.Years;

        Assert.Equal(11_000m, years[0].TotalContributions);
        Assert.Equal(12_000m, years[1].TotalContributions);
    }

    [Fact]
    public void Levers_AreSaveMoreSpendLessReturnUpAndReturnDown()
    {
        Assert.Equal(
            [NetWorthLevers.SaveMore, NetWorthLevers.SpendLess, NetWorthLevers.ReturnUp, NetWorthLevers.ReturnDown],
            Project().Levers.Select(lever => lever.Key));
    }

    [Fact]
    public void SaveMore_ReachesTheTargetWithFiveThousandMoreEachYear()
    {
        // 1,000,000 target at 95,000 a year is 11 years from 30 (age 41); at 100,000 it is 10 years (age 40).
        NetWorthProjectionDto projection = Project();
        NetWorthLeverDto lever = projection.Levers.Single(l => l.Key == NetWorthLevers.SaveMore);

        Assert.Equal(41, projection.Base.FiReachedAge);
        Assert.Equal(5_000m, lever.Change);
        Assert.Equal(40, lever.FiReachedAge);
        Assert.Equal(projection.Base.FinalNetWorth + 5_000m * 30, lever.FinalNetWorth);
    }

    [Fact]
    public void SpendLess_LowersTheFiTarget()
    {
        // 35,000 a year needs 875,000: 10 years at 95,000 (age 40).
        NetWorthLeverDto lever = Project().Levers.Single(l => l.Key == NetWorthLevers.SpendLess);

        Assert.Equal(40, lever.FiReachedAge);
    }

    [Fact]
    public void SpendLess_NeverGoesBelowNothing()
    {
        NetWorthLeverDto lever = Project(spending: 2_000m).Levers.Single(l => l.Key == NetWorthLevers.SpendLess);

        Assert.Equal(2_000m, lever.Change);
        Assert.Null(lever.FiReachedAge);
    }

    [Fact]
    public void ReturnLevers_MoveTheBaseReturnOnePointEachWay_NotBelowZero()
    {
        NetWorthProjectionDto projection = Project(baseRoi: 0m);
        NetWorthLeverDto up = projection.Levers.Single(l => l.Key == NetWorthLevers.ReturnUp);
        NetWorthLeverDto down = projection.Levers.Single(l => l.Key == NetWorthLevers.ReturnDown);

        Assert.Equal(1m, up.Change);
        Assert.True(up.FinalNetWorth > projection.Base.FinalNetWorth);
        Assert.Equal(0m, down.Change);
        Assert.Equal(projection.Base.FinalNetWorth, down.FinalNetWorth);
    }

    [Fact]
    public void Levers_CarryTodaysMoneyToo()
    {
        NetWorthLeverDto lever = Project(inflation: 3m).Levers.Single(l => l.Key == NetWorthLevers.SaveMore);

        Assert.True(lever.RealFinalNetWorth < lever.FinalNetWorth);
        Assert.True(lever.RealFiReachedAge > lever.FiReachedAge);
    }

    [Fact]
    public void MoneyLeverSteps_AreScaledToTheProjectionCurrency()
    {
        NetWorthProjectionDto projection = Project(savings: 95_000m * 25_000m, spending: 40_000m * 25_000m,
            currency: "VND");

        Assert.Equal(MoneyBounds.Max(5_000, "VND"),
            projection.Levers.Single(l => l.Key == NetWorthLevers.SaveMore).Change);
    }
}
