using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.Tests.NetWorth;

/// <summary>
/// The figures derived from the Net Worth projection (Savings Rate, Progress to FI and the age each scenario reaches
/// the FI Target) are computed once, in the projection, so the Net Worth page and the Dashboard always agree.
/// </summary>
public class NetWorthProjectionDerivedFiguresTests
{
    private static NetWorthProjectionDto Project(decimal balance = 180_000m, decimal savings = 22_000m,
        decimal income = 85_000m, decimal spending = 48_000m, int currentAge = 34, int retirementAge = 65,
        decimal baseRoi = 7m, decimal conservativeRoi = 6m, decimal aggressiveRoi = 10m,
        decimal aggressiveSavings = 45_000m) =>
        NetWorthProjectionService.ComputeProjection(new NetWorthProjectionRequestDto
        {
            StartingBalance = balance,
            AnnualSavings = savings,
            AnnualIncome = income,
            AnnualSpending = spending,
            CurrentAge = currentAge,
            RetirementAge = retirementAge,
            BaseRoi = baseRoi,
            ConservativeRoi = conservativeRoi,
            AggressiveRoi = aggressiveRoi,
            AggressiveSavings = aggressiveSavings
        });

    [Fact]
    public void Projection_EchoesTheInputsTheBreakdownNames()
    {
        NetWorthProjectionDto projection = Project();

        Assert.Equal(180_000m, projection.StartingBalance);
        Assert.Equal(22_000m, projection.AnnualSavings);
        Assert.Equal(65, projection.RetirementAge);
    }

    [Fact]
    public void SavingsRate_IsAnnualSavingsOverIncome_RoundedToOneDecimal()
    {
        Assert.Equal(25.9m, Project().SavingsRatePercent);
    }

    [Fact]
    public void SavingsRate_WithNoIncome_IsUnavailable()
    {
        Assert.Null(Project(income: 0m).SavingsRatePercent);
    }

    [Fact]
    public void ProgressToFi_IsStartingBalanceOverFiTarget()
    {
        Assert.Equal(15m, Project().ProgressToFiPercent);
    }

    [Fact]
    public void ProgressToFiAndFiAge_WithNoSpending_AreUnavailable()
    {
        NetWorthProjectionDto projection = Project(spending: 0m);

        Assert.Null(projection.ProgressToFiPercent);
        Assert.Null(projection.Base.FiReachedAge);
        Assert.Null(projection.Aggressive.FiReachedAge);
    }

    [Fact]
    public void Years_RunFromTheAgeAfterNowToRetirementAge_OneYearOfSavingAndGrowthEach()
    {
        // 34 today, retiring at 65: 31 years of saving, the first ending at 35 next year and the last at 65.
        NetWorthScenarioDto scenario = Project(currentAge: 34, retirementAge: 65).Base;

        Assert.Equal(31, scenario.Years.Count);
        Assert.Equal(35, scenario.Years[0].Age);
        Assert.Equal(DateTime.Now.Year + 1, scenario.Years[0].Year);
        Assert.Equal(65, scenario.Years[^1].Age);
        Assert.Equal(scenario.Years[^1].EndingBalance, scenario.FinalNetWorth);
    }

    [Fact]
    public void Years_CompoundExactlyInDecimal_CentsAndAllAreKept()
    {
        // 195,033.14 at 7% earns 13,652.3198; with 32,000 saved that ends the first year at 240,685.4598, and the
        // second year starts from exactly that.
        List<NetWorthYearDto> years = Project(balance: 195_033.14m, savings: 32_000m, baseRoi: 7m).Base.Years;

        Assert.Equal(195_033.14m, years[0].StartingBalance);
        Assert.Equal(13_652.3198m, years[0].Roi);
        Assert.Equal(240_685.4598m, years[0].EndingBalance);
        Assert.Equal(240_685.4598m, years[1].StartingBalance);
        Assert.Equal(240_685.4598m * 0.07m, years[1].Roi);
    }

    [Fact]
    public void FiReachedAge_IsTheFirstAgeAtWhichTheBalanceReachesTheTarget()
    {
        // FI Target 1,000,000; 0% return; 100,000 today at 35 + 100,000 a year → nine years of saving, so exactly
        // 1,000,000 at 44.
        NetWorthProjectionDto projection = Project(balance: 100_000m, savings: 100_000m, spending: 40_000m,
            currentAge: 35, retirementAge: 60, baseRoi: 0m, conservativeRoi: 0m);

        Assert.Equal(44, projection.Base.FiReachedAge);
        Assert.Equal(44, projection.Conservative.FiReachedAge);
    }

    [Fact]
    public void FiReachedAge_WhenAlreadyAtOrAboveTarget_IsCurrentAge()
    {
        Assert.Equal(34, Project(balance: 2_000_000m).Base.FiReachedAge);
        Assert.Equal(34, Project(balance: 1_200_000m).Base.FiReachedAge);
    }

    [Fact]
    public void FiReachedAge_JustBelowTheTargetToday_IsNextYear()
    {
        // 1,199,999.99 today is short of the 1,200,000 target; a year of saving gets there at 35.
        Assert.Equal(35, Project(balance: 1_199_999.99m).Base.FiReachedAge);
    }

    [Fact]
    public void FiReachedAge_WhenNotReachedByRetirement_IsUnavailable()
    {
        Assert.Null(Project(currentAge: 34, retirementAge: 36).Base.FiReachedAge);
    }

    [Theory]
    [InlineData(40, 40)]
    [InlineData(45, 40)]
    public void FiReachedAge_WhenRetirementIsNotAfterCurrentAge_IsUnavailable(int currentAge, int retirementAge)
    {
        // Matches the Dashboard, which draws no projection in this case.
        Assert.Null(Project(balance: 2_000_000m, currentAge: currentAge, retirementAge: retirementAge).Base.FiReachedAge);
    }

    [Fact]
    public void FiReachedAge_ForAggressive_UsesAggressiveSavings()
    {
        // Base saves nothing at 0% and never gets there; Aggressive saves 200,000 a year: 300k at 36, 500k at 37, ...
        // 1.1M at 40.
        NetWorthProjectionDto projection = Project(balance: 100_000m, savings: 0m, spending: 40_000m, currentAge: 35,
            retirementAge: 60, baseRoi: 0m, aggressiveRoi: 0m, aggressiveSavings: 200_000m);

        Assert.Null(projection.Base.FiReachedAge);
        Assert.Equal(40, projection.Aggressive.FiReachedAge);
    }
}
