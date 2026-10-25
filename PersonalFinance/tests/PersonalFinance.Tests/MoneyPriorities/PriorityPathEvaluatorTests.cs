using System.Globalization;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// Walking Priority Tree version 1 from a set of answers. Expected paths are read off the spec's tree (#315), not
/// derived from the model.
/// </summary>
public class PriorityPathEvaluatorTests
{
    /// <summary>Every answer filled in, leading to L6; each test overrides what it needs.</summary>
    private static readonly PriorityAnswers Everything = new()
    {
        MonthlyAmount = 500m,
        DebtApr = 0m,
        DebtBalance = 5_000m,
        EmergencyFundMonths = 6m,
        MonthlyExpenses = 3_000m,
        HasFullEmployerMatch = true,
        EmployerMatchPercent = 4m,
        HorizonYears = 25,
        AgeBand = RetirementAgeBand.Under50,
        AtContributionLimit = false
    };

    /// <summary>Answers that lead to each leaf: the full set, with the one answer that turns off the L6 path changed.</summary>
    private static PriorityAnswers AnswersFor(string leafId) => leafId switch
    {
        "L1" => Everything with { DebtApr = 22m },
        "L2" => Everything with { EmergencyFundMonths = 1m },
        "L3" => Everything with { HasFullEmployerMatch = false },
        "L4" => Everything with { HorizonYears = 2 },
        "L5" => Everything with { HorizonYears = 5 },
        "L6" => Everything,
        "L7" => Everything with { AtContributionLimit = true },
        _ => throw new ArgumentOutOfRangeException(nameof(leafId))
    };

    [Theory]
    [InlineData("L1", "S,Q1,L1")]
    [InlineData("L2", "S,Q1,Q2,L2")]
    [InlineData("L3", "S,Q1,Q2,Q3,L3")]
    [InlineData("L4", "S,Q1,Q2,Q3,Q4,L4")]
    [InlineData("L5", "S,Q1,Q2,Q3,Q4,L5")]
    [InlineData("L6", "S,Q1,Q2,Q3,Q4,Q5,L6")]
    [InlineData("L7", "S,Q1,Q2,Q3,Q4,Q5,L7")]
    public void Evaluate_ReachesEveryLeaf_ByItsOrderedPath(string expectedLeaf, string expectedPath)
    {
        PriorityPath path = PriorityPathEvaluator.Evaluate(AnswersFor(expectedLeaf));

        Assert.Equal(expectedPath.Split(','), path.NodeIds);
        Assert.Equal(expectedPath, path.StoredPath);
        Assert.Equal(expectedLeaf, path.LeafId);
        Assert.Equal(1, path.TreeVersion);
    }

    [Fact]
    public void Evaluate_ClearsEveryAnswerOffTheDebtPath()
    {
        PriorityPath path = PriorityPathEvaluator.Evaluate(Everything with { DebtApr = 22m });

        Assert.Equal(
            new PriorityAnswers { MonthlyAmount = 500m, DebtApr = 22m, DebtBalance = 5_000m },
            path.Answers);
    }

    [Fact]
    public void Evaluate_KeepsMonthlyExpenses_OnlyOnTheFundLeaf()
    {
        PriorityPath path = PriorityPathEvaluator.Evaluate(Everything with { EmergencyFundMonths = 1m });

        Assert.Equal(
            new PriorityAnswers { MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 1m, MonthlyExpenses = 3_000m },
            path.Answers);
    }

    [Fact]
    public void Evaluate_KeepsTheMatchPercent_OnlyOnTheMatchLeaf()
    {
        PriorityPath path = PriorityPathEvaluator.Evaluate(Everything with { HasFullEmployerMatch = false });

        Assert.Equal(
            new PriorityAnswers
            {
                MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 6m, HasFullEmployerMatch = false,
                EmployerMatchPercent = 4m
            },
            path.Answers);
    }

    [Fact]
    public void Evaluate_ClearsTheAgeBandAndLimit_OnAMediumHorizon()
    {
        PriorityPath path = PriorityPathEvaluator.Evaluate(Everything with { HorizonYears = 5 });

        Assert.Equal(
            new PriorityAnswers
            {
                MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 6m, HasFullEmployerMatch = true,
                HorizonYears = 5
            },
            path.Answers);
    }

    [Fact]
    public void Evaluate_OnTheLongestPath_KeepsTheQuestionsButClearsTheOtherLeavesFollowUps()
    {
        PriorityPath path = PriorityPathEvaluator.Evaluate(Everything);

        Assert.Equal(
            new PriorityAnswers
            {
                MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 6m, HasFullEmployerMatch = true,
                HorizonYears = 25, AgeBand = RetirementAgeBand.Under50, AtContributionLimit = false
            },
            path.Answers);
    }

    [Fact]
    public void Evaluate_IgnoresAnswersOffThePath_WhenTheyAreMissing()
    {
        // Only the two always-asked answers: Q1 Yes never reads the rest.
        PriorityPath path = PriorityPathEvaluator.Evaluate(new PriorityAnswers { MonthlyAmount = 500m, DebtApr = 7m });

        Assert.Equal("S,Q1,L1", path.StoredPath);
    }

    [Theory]
    [InlineData("6.99", "S,Q1,Q2,Q3,Q4,Q5,L6")]
    [InlineData("7", "S,Q1,L1")]
    [InlineData("7.00", "S,Q1,L1")]
    public void Q1_IsHighInterest_FromExactly7PercentApr(string apr, string expectedPath)
    {
        PriorityAnswers answers = Everything with { DebtApr = decimal.Parse(apr, CultureInfo.InvariantCulture) };

        Assert.Equal(expectedPath, PriorityPathEvaluator.Evaluate(answers).StoredPath);
    }

    [Theory]
    [InlineData("2.9", "L2")]
    [InlineData("3", "L6")]
    [InlineData("3.0", "L6")]
    public void Q2_IsCovered_FromExactly3Months(string months, string expectedLeaf)
    {
        PriorityAnswers answers = Everything with { EmergencyFundMonths = decimal.Parse(months, CultureInfo.InvariantCulture) };

        Assert.Equal(expectedLeaf, PriorityPathEvaluator.Evaluate(answers).LeafId);
    }

    [Theory]
    [InlineData(1, "L4")]
    [InlineData(2, "L4")]
    [InlineData(3, "L5")]
    [InlineData(10, "L5")]
    [InlineData(11, "L6")]
    [InlineData(50, "L6")]
    public void Q4_SplitsTheHorizonAt3And10Years(int horizonYears, string expectedLeaf)
    {
        PriorityPath path = PriorityPathEvaluator.Evaluate(Everything with { HorizonYears = horizonYears });

        Assert.Equal(expectedLeaf, path.LeafId);
    }

    [Fact]
    public void Thresholds_AreTheSpecsValues()
    {
        Assert.Equal(7m, PriorityTreeThresholds.HighInterestAprPercent);
        Assert.Equal(3m, PriorityTreeThresholds.EmergencyFundTargetMonths);
        Assert.Equal(3, PriorityTreeThresholds.ShortHorizonYears);
        Assert.Equal(10, PriorityTreeThresholds.LongHorizonYears);
    }

    [Theory]
    [InlineData("EmergencyFundMonths")]
    [InlineData("HasFullEmployerMatch")]
    [InlineData("HorizonYears")]
    [InlineData("AtContributionLimit")]
    public void Evaluate_WithAQuestionOnThePathUnanswered_Throws(string missing)
    {
        PriorityAnswers answers = missing switch
        {
            "EmergencyFundMonths" => Everything with { EmergencyFundMonths = null },
            "HasFullEmployerMatch" => Everything with { HasFullEmployerMatch = null },
            "HorizonYears" => Everything with { HorizonYears = null },
            _ => Everything with { AtContributionLimit = null }
        };

        ArgumentException error = Assert.Throws<ArgumentException>(() => PriorityPathEvaluator.Evaluate(answers));
        Assert.Contains(missing, error.Message);
    }

    [Fact]
    public void AgeBandNames_FitThePriorityWalkColumn()
    {
        Assert.All(Enum.GetNames<RetirementAgeBand>(), name => Assert.InRange(name.Length, 1, 16));
    }
}
