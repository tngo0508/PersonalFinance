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
    public void FiReachedAge_IsTheFirstAgeWhoseEndingBalanceReachesTheTarget()
    {
        // FI Target 1,000,000; 0% return; 100,000 start + 100,000 a year → exactly 1,000,000 at the end of age 43.
        NetWorthProjectionDto projection = Project(balance: 100_000m, savings: 100_000m, spending: 40_000m,
            currentAge: 35, retirementAge: 60, baseRoi: 0m, conservativeRoi: 0m);

        Assert.Equal(43, projection.Base.FiReachedAge);
        Assert.Equal(43, projection.Conservative.FiReachedAge);
    }

    [Fact]
    public void FiReachedAge_WhenAlreadyAboveTarget_IsCurrentAge()
    {
        Assert.Equal(34, Project(balance: 2_000_000m).Base.FiReachedAge);
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
        // Base saves nothing at 0% and never gets there; Aggressive saves 200,000 a year: 300k, 500k, ... 1.1M at 39.
        NetWorthProjectionDto projection = Project(balance: 100_000m, savings: 0m, spending: 40_000m, currentAge: 35,
            retirementAge: 60, baseRoi: 0m, aggressiveRoi: 0m, aggressiveSavings: 200_000m);

        Assert.Null(projection.Base.FiReachedAge);
        Assert.Equal(39, projection.Aggressive.FiReachedAge);
    }
}
