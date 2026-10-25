using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Enums;
using static PersonalFinance.ApiService.Features.MoneyPriorities.Domain.PriorityNodeIds;

namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The dollar figures for the leaf a Priority Walk ends at (#303, spec #315 "Leaf math"): the one entry point the API
/// calls. Pure: no I/O, no clock. Every value is <see cref="decimal"/> and nothing is rounded.
/// </summary>
internal static class PriorityLeafCalculator
{
    private const int MonthsPerYear = 12;

    /// <summary>L4: cash beside the Conservative scenario.</summary>
    private static readonly GrowthScenario[] CashScenarios = [GrowthScenario.Cash, GrowthScenario.Conservative];

    /// <summary>L5, L6 and L7: the three investment scenarios, lowest rate first.</summary>
    private static readonly GrowthScenario[] InvestmentScenarios =
        [GrowthScenario.Conservative, GrowthScenario.Base, GrowthScenario.Aggressive];

    /// <summary>
    /// The figures for <paramref name="path"/>'s leaf, from its on-path answers. <paramref name="limits"/> are the IRS
    /// limits for the year being shown (used by L6 only). Throws <see cref="ArgumentException"/> when the leaf's
    /// follow-up answer is missing (the validators prevent that).
    /// </summary>
    public static PriorityLeafFigures Calculate(PriorityPath path, RetirementContributionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(limits);
        PriorityAnswers answers = path.Answers;

        return path.LeafId switch
        {
            PayDebt => new PriorityLeafFigures { LeafId = PayDebt, Debt = Debt(answers) },
            BuildEmergencyFund => new PriorityLeafFigures { LeafId = BuildEmergencyFund, EmergencyFund = EmergencyFund(answers) },
            CaptureMatch => new PriorityLeafFigures { LeafId = CaptureMatch, EmployerMatch = EmployerMatch(answers) },
            ShortTermSavings => Growth(ShortTermSavings, answers, CashScenarios),
            BalancedBrokerage => Growth(BalancedBrokerage, answers, InvestmentScenarios),
            IraThen401k => Growth(IraThen401k, answers, InvestmentScenarios) with
            {
                RetirementSplit = RetirementSplit(answers, limits)
            },
            StockBrokerage => Growth(StockBrokerage, answers, InvestmentScenarios),
            _ => throw new InvalidOperationException($"No figures are authored for leaf '{path.LeafId}'.")
        };
    }

    private static DebtPayoffFigures Debt(PriorityAnswers answers)
    {
        decimal balance = Required(answers.DebtBalance, nameof(answers.DebtBalance));
        return new DebtPayoffFigures
        {
            Balance = balance,
            AprPercent = answers.DebtApr,
            AssumedReturnPercent = PriorityAssumptions.BaseReturnPercent,
            Payoff = DebtPayoff.Simulate(balance, answers.DebtApr, answers.MonthlyAmount)
        };
    }

    private static EmergencyFundFigures EmergencyFund(PriorityAnswers answers)
    {
        decimal monthsCovered = Required(answers.EmergencyFundMonths, nameof(answers.EmergencyFundMonths));
        decimal expenses = Required(answers.MonthlyExpenses, nameof(answers.MonthlyExpenses));
        const decimal targetMonths = PriorityTreeThresholds.EmergencyFundTargetMonths;
        decimal gap = Math.Max(0m, targetMonths * expenses - monthsCovered * expenses);
        return new EmergencyFundFigures
        {
            TargetMonths = targetMonths,
            MonthsCovered = monthsCovered,
            MonthlyExpenses = expenses,
            TargetAmount = targetMonths * expenses,
            Gap = gap,
            MonthsToClose = decimal.ToInt32(Math.Ceiling(gap / answers.MonthlyAmount))
        };
    }

    private static EmployerMatchFigures EmployerMatch(PriorityAnswers answers)
    {
        decimal matchPercent = Required(answers.EmployerMatchPercent, nameof(answers.EmployerMatchPercent));
        return new EmployerMatchFigures
        {
            MatchPercent = matchPercent,
            YearlyMatch = MonthsPerYear * answers.MonthlyAmount * matchPercent / 100m,
            AssumedReturnPercent = PriorityAssumptions.BaseReturnPercent
        };
    }

    /// <summary>The yearly amount fills the IRA limit for the age range, then the 401(k) limit; the rest is taxable.</summary>
    private static RetirementSplitFigures RetirementSplit(PriorityAnswers answers, RetirementContributionLimits limits)
    {
        RetirementAgeBand ageBand = Required(answers.AgeBand, nameof(answers.AgeBand));
        decimal yearly = MonthsPerYear * answers.MonthlyAmount;
        decimal iraLimit = limits.IraFor(ageBand);
        decimal planLimit = limits.EmployeeDeferralFor(ageBand);
        decimal toIra = Math.Min(yearly, iraLimit);
        decimal toPlan = Math.Min(yearly - toIra, planLimit);
        return new RetirementSplitFigures
        {
            TaxYear = limits.TaxYear,
            AgeBand = ageBand,
            YearlyAmount = yearly,
            IraLimit = iraLimit,
            EmployeeDeferralLimit = planLimit,
            ToIra = toIra,
            ToEmployeePlan = toPlan,
            ToTaxable = yearly - toIra - toPlan
        };
    }

    /// <summary>The monthly amount grown to the horizon under each of <paramref name="scenarios"/>.</summary>
    private static PriorityLeafFigures Growth(string leafId, PriorityAnswers answers, GrowthScenario[] scenarios)
    {
        int horizonYears = Required(answers.HorizonYears, nameof(answers.HorizonYears));
        int months = horizonYears * MonthsPerYear;
        return new PriorityLeafFigures
        {
            LeafId = leafId,
            HorizonYears = horizonYears,
            TotalContributed = answers.MonthlyAmount * months,
            Scenarios = scenarios
                .Select(scenario =>
                {
                    decimal rate = PriorityAssumptions.RatePercent(scenario);
                    return new ScenarioBalance(scenario, rate, CompoundGrowth.BalanceAfter(answers.MonthlyAmount, rate, months));
                })
                .ToList()
        };
    }

    private static T Required<T>(T? answer, string name)
        where T : struct =>
        answer ?? throw new ArgumentException($"The leaf needs {name}, but it has no answer.");
}
