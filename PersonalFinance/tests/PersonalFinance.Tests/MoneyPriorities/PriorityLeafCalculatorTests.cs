using System.Globalization;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The figures each leaf shows (#303, spec #315 "Leaf math"), computed from a walked path. Expected values are worked
/// by hand from the spec's formulas or taken from the 50-digit golden values.
/// </summary>
public class PriorityLeafCalculatorTests
{
    private static readonly RetirementContributionLimits Limits2026 = RetirementContributionLimits.ForYear(2026);

    private static readonly PriorityAnswers NoDebt = new()
    {
        MonthlyAmount = 500m,
        DebtApr = 0m,
        EmergencyFundMonths = 6m,
        HasFullEmployerMatch = true
    };

    private static PriorityLeafFigures Calculate(PriorityAnswers answers) =>
        PriorityLeafCalculator.Calculate(PriorityPathEvaluator.Evaluate(answers), Limits2026);

    [Fact]
    public void PayDebt_ShowsTheGoldenPayoff_AndComparesTheAprWithTheBaseReturn()
    {
        PriorityLeafFigures figures = Calculate(NoDebt with { DebtApr = 22m, DebtBalance = 5_000m });

        Assert.Equal(PriorityNodeIds.PayDebt, figures.LeafId);
        DebtPayoffFigures debt = Assert.IsType<DebtPayoffFigures>(figures.Debt);
        Assert.Equal(5_000m, debt.Balance);
        Assert.Equal(22m, debt.AprPercent);
        Assert.Equal(7m, debt.AssumedReturnPercent);
        Assert.Equal(DebtPayoffOutcome.PaidOff, debt.Payoff.Outcome);
        Assert.Equal(12, debt.Payoff.Months);
        Assert.Equal(574.44m, decimal.Round(debt.Payoff.TotalInterest!.Value, 2, MidpointRounding.AwayFromZero));
        Assert.Equal(74.44m, decimal.Round(debt.Payoff.FinalPayment!.Value, 2, MidpointRounding.AwayFromZero));
        Assert.Null(figures.EmergencyFund);
        Assert.Null(figures.EmployerMatch);
        Assert.Null(figures.RetirementSplit);
        Assert.Empty(figures.Scenarios);
        Assert.Null(figures.TotalContributed);
    }

    [Fact]
    public void PayDebt_WhenTheAmountCannotCoverAMonthsInterest_HasNoPayoffFigures()
    {
        // 30,000 at 20% accrues 500 in the first month: exactly the amount.
        PriorityLeafFigures figures = Calculate(NoDebt with { DebtApr = 20m, DebtBalance = 30_000m });

        DebtPayoffFigures debt = Assert.IsType<DebtPayoffFigures>(figures.Debt);
        Assert.Equal(DebtPayoffOutcome.AmountDoesNotCoverInterest, debt.Payoff.Outcome);
        Assert.Equal(500m, debt.Payoff.FirstMonthInterest);
        Assert.Null(debt.Payoff.Months);
        Assert.Null(debt.Payoff.TotalInterest);
        Assert.Null(debt.Payoff.FinalPayment);
    }

    [Fact]
    public void BuildEmergencyFund_ShowsTheGapToThreeMonths_AndTheMonthsToCloseItRoundedUp()
    {
        // Target 3 × 2,000 = 6,000; covered 1.5 × 2,000 = 3,000; gap 3,000; 3,000 / 450 = 6.67, so 7 months.
        PriorityLeafFigures figures = Calculate(NoDebt with
        {
            MonthlyAmount = 450m, EmergencyFundMonths = 1.5m, MonthlyExpenses = 2_000m
        });

        Assert.Equal(PriorityNodeIds.BuildEmergencyFund, figures.LeafId);
        EmergencyFundFigures fund = Assert.IsType<EmergencyFundFigures>(figures.EmergencyFund);
        Assert.Equal(3m, fund.TargetMonths);
        Assert.Equal(1.5m, fund.MonthsCovered);
        Assert.Equal(2_000m, fund.MonthlyExpenses);
        Assert.Equal(6_000m, fund.TargetAmount);
        Assert.Equal(3_000m, fund.Gap);
        Assert.Equal(7, fund.MonthsToClose);
        Assert.Null(figures.Debt);
        Assert.Empty(figures.Scenarios);
    }

    [Fact]
    public void BuildEmergencyFund_WhenTheGapIsAWholeNumberOfMonths_DoesNotRoundUpFurther()
    {
        // Gap 3 × 1,000 = 3,000 with nothing saved; 3,000 / 500 is exactly 6.
        PriorityLeafFigures figures = Calculate(NoDebt with { EmergencyFundMonths = 0m, MonthlyExpenses = 1_000m });

        Assert.Equal(6, figures.EmergencyFund!.MonthsToClose);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void BuildEmergencyFund_WhenTheFundIsAlreadyAtTarget_HasNoGap(int monthsCovered)
    {
        // The tree never reaches L2 at 3 months or more; the math still never shows a negative gap.
        var path = new PriorityPath(1, ["S", "Q1", "Q2", "L2"], NoDebt with
        {
            EmergencyFundMonths = monthsCovered, MonthlyExpenses = 2_000m
        });

        EmergencyFundFigures fund = PriorityLeafCalculator.Calculate(path, Limits2026).EmergencyFund!;

        Assert.Equal(0m, fund.Gap);
        Assert.Equal(0, fund.MonthsToClose);
    }

    [Fact]
    public void CaptureMatch_ShowsUpToTwelveMonthsOfMatch_AndComparesTheMatchWithTheBaseReturn()
    {
        // 12 × 500 × 4% = 240.
        PriorityLeafFigures figures = Calculate(NoDebt with { HasFullEmployerMatch = false, EmployerMatchPercent = 4m });

        Assert.Equal(PriorityNodeIds.CaptureMatch, figures.LeafId);
        EmployerMatchFigures match = Assert.IsType<EmployerMatchFigures>(figures.EmployerMatch);
        Assert.Equal(4m, match.MatchPercent);
        Assert.Equal(240m, match.YearlyMatch);
        Assert.Equal(7m, match.AssumedReturnPercent);
        Assert.Empty(figures.Scenarios);
        Assert.Null(figures.HorizonYears);
    }

    [Fact]
    public void CaptureMatch_KeepsFractionsOfACent_ForDisplayToRound()
    {
        // 12 × 333.33 × 3.5% = 139.9986: not rounded inside the math.
        PriorityLeafFigures figures = Calculate(NoDebt with
        {
            MonthlyAmount = 333.33m, HasFullEmployerMatch = false, EmployerMatchPercent = 3.5m
        });

        Assert.Equal(139.9986m, figures.EmployerMatch!.YearlyMatch);
    }

    [Fact]
    public void ShortTermSavings_AtAOneYearHorizon_ShowsCashBesideConservative()
    {
        PriorityLeafFigures figures = Calculate(NoDebt with { HorizonYears = 1 });

        Assert.Equal(PriorityNodeIds.ShortTermSavings, figures.LeafId);
        AssertScenarios(figures, (GrowthScenario.Cash, 3.5m, 6_097.19m), (GrowthScenario.Conservative, 4m, 6_111.23m));
        Assert.Equal(1, figures.HorizonYears);
        Assert.Equal(6_000m, figures.TotalContributed);
        Assert.Null(figures.RetirementSplit);
    }

    [Fact]
    public void BalancedBrokerage_AtTenYears_ShowsAllThreeRates_WithTheGoldenBaseBalance()
    {
        PriorityLeafFigures figures = Calculate(NoDebt with { HorizonYears = 10 });

        Assert.Equal(PriorityNodeIds.BalancedBrokerage, figures.LeafId);
        AssertScenarios(figures,
            (GrowthScenario.Conservative, 4m, 73_624.90m),
            (GrowthScenario.Base, 7m, 86_542.40m),
            (GrowthScenario.Aggressive, 10m, 102_422.49m));
        Assert.Equal(10, figures.HorizonYears);
        Assert.Equal(60_000m, figures.TotalContributed);
    }

    [Fact]
    public void StockBrokerage_AtFiftyYears_ShowsAllThreeRates()
    {
        PriorityLeafFigures figures = Calculate(NoDebt with
        {
            HorizonYears = 50, AgeBand = RetirementAgeBand.Under50, AtContributionLimit = true
        });

        Assert.Equal(PriorityNodeIds.StockBrokerage, figures.LeafId);
        AssertScenarios(figures,
            (GrowthScenario.Conservative, 4m, 954_678.20m),
            (GrowthScenario.Base, 7m, 2_724_035.46m),
            (GrowthScenario.Aggressive, 10m, 8_662_195.40m));
        Assert.Equal(300_000m, figures.TotalContributed);
        Assert.Null(figures.RetirementSplit);
    }

    /// <summary>The scenarios in order, each balance matching a closed-form value to the cent.</summary>
    private static void AssertScenarios(
        PriorityLeafFigures figures, params (GrowthScenario Scenario, decimal Rate, decimal Balance)[] expected)
    {
        Assert.Equal(expected.Select(e => (e.Scenario, e.Rate)), figures.Scenarios.Select(s => (s.Scenario, s.RatePercent)));
        Assert.Equal(
            expected.Select(e => e.Balance),
            figures.Scenarios.Select(s => decimal.Round(s.Balance, 2, MidpointRounding.AwayFromZero)));
    }

    [Theory]
    // 2026 limits (Notice 2025-67): IRA 7,500 (8,600 at 50+); 401(k) 24,500, 32,500 at 50 to 59 or 64+, 35,750 at 60 to 63.
    [InlineData(RetirementAgeBand.Under50, "500", "6000", "0", "0")]
    [InlineData(RetirementAgeBand.Under50, "625", "7500", "0", "0")]
    [InlineData(RetirementAgeBand.Under50, "2000", "7500", "16500", "0")]
    [InlineData(RetirementAgeBand.Under50, "3000", "7500", "24500", "4000")]
    [InlineData(RetirementAgeBand.Age50To59Or64Up, "2000", "8600", "15400", "0")]
    [InlineData(RetirementAgeBand.Age50To59Or64Up, "3500", "8600", "32500", "900")]
    [InlineData(RetirementAgeBand.Age60To63, "2000", "8600", "15400", "0")]
    [InlineData(RetirementAgeBand.Age60To63, "4000", "8600", "35750", "3650")]
    public void IraThen401k_FillsTheIraLimitThenThe401kLimit_ThenATaxableAccount(
        RetirementAgeBand ageBand, string monthly, string toIra, string toPlan, string toTaxable)
    {
        PriorityLeafFigures figures = Calculate(NoDebt with
        {
            MonthlyAmount = Parse(monthly), HorizonYears = 25, AgeBand = ageBand, AtContributionLimit = false
        });

        Assert.Equal(PriorityNodeIds.IraThen401k, figures.LeafId);
        RetirementSplitFigures split = Assert.IsType<RetirementSplitFigures>(figures.RetirementSplit);
        Assert.Equal(2026, split.TaxYear);
        Assert.Equal(ageBand, split.AgeBand);
        Assert.Equal(12 * Parse(monthly), split.YearlyAmount);
        Assert.Equal(Parse(toIra), split.ToIra);
        Assert.Equal(Parse(toPlan), split.ToEmployeePlan);
        Assert.Equal(Parse(toTaxable), split.ToTaxable);
        Assert.Equal(split.YearlyAmount, split.ToIra + split.ToEmployeePlan + split.ToTaxable);
    }

    [Theory]
    [InlineData(RetirementAgeBand.Under50, "7500", "24500")]
    [InlineData(RetirementAgeBand.Age50To59Or64Up, "8600", "32500")]
    [InlineData(RetirementAgeBand.Age60To63, "8600", "35750")]
    public void IraThen401k_ShowsTheLimitsForTheAgeRange(RetirementAgeBand ageBand, string iraLimit, string planLimit)
    {
        PriorityLeafFigures figures = Calculate(NoDebt with
        {
            HorizonYears = 25, AgeBand = ageBand, AtContributionLimit = false
        });

        Assert.Equal(Parse(iraLimit), figures.RetirementSplit!.IraLimit);
        Assert.Equal(Parse(planLimit), figures.RetirementSplit.EmployeeDeferralLimit);
    }

    [Fact]
    public void IraThen401k_ShowsBalancesUnderAllThreeRates_ForTheWholeAmount()
    {
        PriorityLeafFigures figures = Calculate(NoDebt with
        {
            MonthlyAmount = 2_000m, HorizonYears = 25, AgeBand = RetirementAgeBand.Under50, AtContributionLimit = false
        });

        AssertScenarios(figures,
            (GrowthScenario.Conservative, 4m, 1_028_259.09m),
            (GrowthScenario.Base, 7m, 1_620_143.39m),
            (GrowthScenario.Aggressive, 10m, 2_653_666.81m));
        Assert.Equal(25, figures.HorizonYears);
        Assert.Equal(600_000m, figures.TotalContributed);
    }

    [Fact]
    public void IraThen401k_UsesTheLimitsItIsGiven()
    {
        RetirementContributionLimits made = Limits2026 with { TaxYear = 2099, Ira = 1_000m, EmployeeDeferral = 2_000m };
        PriorityPath path = PriorityPathEvaluator.Evaluate(NoDebt with
        {
            HorizonYears = 25, AgeBand = RetirementAgeBand.Under50, AtContributionLimit = false
        });

        RetirementSplitFigures split = PriorityLeafCalculator.Calculate(path, made).RetirementSplit!;

        Assert.Equal(2099, split.TaxYear);
        Assert.Equal(1_000m, split.ToIra);
        Assert.Equal(2_000m, split.ToEmployeePlan);
        Assert.Equal(3_000m, split.ToTaxable);
    }

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Fact]
    public void Calculate_WhenTheLeafsFollowUpAnswerIsMissing_Throws()
    {
        PriorityPath path = PriorityPathEvaluator.Evaluate(NoDebt with { DebtApr = 22m });

        Assert.Throws<ArgumentException>(() => PriorityLeafCalculator.Calculate(path, Limits2026));
    }
}
