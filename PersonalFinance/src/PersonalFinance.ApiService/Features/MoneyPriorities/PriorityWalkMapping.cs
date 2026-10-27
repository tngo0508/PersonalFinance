using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.MoneyPriorities;

/// <summary>
/// Manual mapping between the Money Priorities request, the <see cref="PriorityWalk"/> entity, the Domain types and
/// the result DTOs. Copies values as they are: nothing is rounded here.
/// </summary>
internal static class PriorityWalkMapping
{
    /// <summary>The authored tree a Walk was answered against; throws for a version this build does not know.</summary>
    public static PriorityTree TreeFor(int version) =>
        version == PriorityTreeV1.Version
            ? PriorityTreeV1.Tree
            : throw new InvalidOperationException($"Priority Tree version {version} is not authored.");

    /// <summary>The tree as the result DTOs carry it: its nodes and edges in authored order, with their English labels.</summary>
    public static PriorityTreeDto ToDto(PriorityTree tree) => new()
    {
        Version = tree.Version,
        Nodes = tree.Nodes.Select(node => new PriorityTreeNodeDto { Id = node.Id, Kind = node.Kind, Label = node.Label }).ToList(),
        Edges = tree.Edges.Select(edge => new PriorityTreeEdgeDto { From = edge.From, To = edge.To, Label = edge.Label }).ToList()
    };

    /// <summary>The answers of a validated request (the validator guarantees the two required answers).</summary>
    public static PriorityAnswers ToAnswers(SubmitPriorityWalkRequest request) => new()
    {
        MonthlyAmount = request.MonthlyAmount ?? throw new ArgumentException("The request has no monthly amount.", nameof(request)),
        DebtApr = request.DebtApr ?? throw new ArgumentException("The request has no debt APR.", nameof(request)),
        DebtBalance = request.DebtBalance,
        EmergencyFundMonths = request.EmergencyFundMonths,
        MonthlyExpenses = request.MonthlyExpenses,
        HasFullEmployerMatch = request.HasFullEmployerMatch,
        EmployerMatchPercent = request.EmployerMatchPercent,
        HorizonYears = request.HorizonYears,
        AgeBand = request.AgeBand,
        AtContributionLimit = request.AtContributionLimit
    };

    /// <summary>The answers a Walk stored.</summary>
    public static PriorityAnswers ToAnswers(PriorityWalk walk) => new()
    {
        MonthlyAmount = walk.MonthlyAmount,
        DebtApr = walk.DebtApr,
        DebtBalance = walk.DebtBalance,
        EmergencyFundMonths = walk.EmergencyFundMonths,
        MonthlyExpenses = walk.MonthlyExpenses,
        HasFullEmployerMatch = walk.HasFullEmployerMatch,
        EmployerMatchPercent = walk.EmployerMatchPercent,
        HorizonYears = walk.HorizonYears,
        AgeBand = walk.AgeBand is null ? null : ParseAgeBand(walk.AgeBand),
        AtContributionLimit = walk.AtContributionLimit
    };

    /// <summary>
    /// Whether two Walks hold exactly the same answers, Purpose, tree version and limits year, so a new submit would
    /// show the same result. The answers compare by <see cref="PriorityAnswers"/> record equality, so a new answer
    /// field takes part without a change here.
    /// </summary>
    public static bool SameAnswers(PriorityWalk a, PriorityWalk b) =>
        ToAnswers(a) == ToAnswers(b)
        && string.Equals(a.Purpose, b.Purpose, StringComparison.Ordinal)
        && a.TreeVersion == b.TreeVersion
        && a.LimitsTaxYear == b.LimitsTaxYear;

    public static PriorityWalkAnswersDto ToAnswersDto(string? purpose, PriorityAnswers answers) => new()
    {
        MonthlyAmount = answers.MonthlyAmount,
        Purpose = purpose,
        DebtApr = answers.DebtApr,
        DebtBalance = answers.DebtBalance,
        EmergencyFundMonths = answers.EmergencyFundMonths,
        MonthlyExpenses = answers.MonthlyExpenses,
        HasFullEmployerMatch = answers.HasFullEmployerMatch,
        EmployerMatchPercent = answers.EmployerMatchPercent,
        HorizonYears = answers.HorizonYears,
        AgeBand = answers.AgeBand,
        AtContributionLimit = answers.AtContributionLimit
    };

    /// <summary>The figures as the API sends them; <paramref name="limits"/> supplies the Roth footnote of L6.</summary>
    public static PriorityLeafFiguresDto ToDto(PriorityLeafFigures figures, RetirementContributionLimits limits) => new()
    {
        LeafId = figures.LeafId,
        Debt = figures.Debt is { } debt ? ToDto(debt) : null,
        EmergencyFund = figures.EmergencyFund is { } fund
            ? new PriorityEmergencyFundFiguresDto
            {
                TargetMonths = fund.TargetMonths,
                MonthsCovered = fund.MonthsCovered,
                MonthlyExpenses = fund.MonthlyExpenses,
                TargetAmount = fund.TargetAmount,
                Gap = fund.Gap,
                MonthsToClose = fund.MonthsToClose
            }
            : null,
        EmployerMatch = figures.EmployerMatch is { } match
            ? new PriorityEmployerMatchFiguresDto
            {
                MatchPercent = match.MatchPercent,
                YearlyMatch = match.YearlyMatch,
                AssumedReturnPercent = match.AssumedReturnPercent
            }
            : null,
        RetirementSplit = figures.RetirementSplit is { } split ? ToDto(split, limits) : null,
        Scenarios = figures.Scenarios
            .Select(s => new PriorityScenarioBalanceDto
            {
                Scenario = ToDto(s.Scenario),
                RatePercent = s.RatePercent,
                Balance = s.Balance
            })
            .ToList(),
        HorizonYears = figures.HorizonYears,
        TotalContributed = figures.TotalContributed
    };

    private static PriorityDebtFiguresDto ToDto(DebtPayoffFigures debt) => new()
    {
        Balance = debt.Balance,
        AprPercent = debt.AprPercent,
        AssumedReturnPercent = debt.AssumedReturnPercent,
        Outcome = debt.Payoff.Outcome switch
        {
            DebtPayoffOutcome.PaidOff => PriorityDebtOutcome.PaidOff,
            DebtPayoffOutcome.AmountDoesNotCoverInterest => PriorityDebtOutcome.AmountDoesNotCoverInterest,
            DebtPayoffOutcome.LongerThanMaximum => PriorityDebtOutcome.LongerThanMaximum,
            _ => throw new InvalidOperationException($"Unknown debt payoff outcome '{debt.Payoff.Outcome}'.")
        },
        FirstMonthInterest = debt.Payoff.FirstMonthInterest,
        MaximumMonths = DebtPayoff.MaxMonths,
        MaximumYears = DebtPayoff.MaxMonths / 12,
        Months = debt.Payoff.Months,
        TotalInterest = debt.Payoff.TotalInterest,
        FinalPayment = debt.Payoff.FinalPayment
    };

    private static PriorityRetirementSplitDto ToDto(RetirementSplitFigures split, RetirementContributionLimits limits) => new()
    {
        TaxYear = split.TaxYear,
        AgeBand = split.AgeBand,
        YearlyAmount = split.YearlyAmount,
        IraLimit = split.IraLimit,
        EmployeeDeferralLimit = split.EmployeeDeferralLimit,
        ToIra = split.ToIra,
        ToEmployeePlan = split.ToEmployeePlan,
        ToTaxable = split.ToTaxable,
        RothPhaseOutSingleStart = limits.RothPhaseOutSingleStart,
        RothPhaseOutSingleEnd = limits.RothPhaseOutSingleEnd,
        RothPhaseOutJointStart = limits.RothPhaseOutJointStart,
        RothPhaseOutJointEnd = limits.RothPhaseOutJointEnd
    };

    private static PriorityGrowthScenario ToDto(GrowthScenario scenario) => scenario switch
    {
        GrowthScenario.Cash => PriorityGrowthScenario.Cash,
        GrowthScenario.Conservative => PriorityGrowthScenario.Conservative,
        GrowthScenario.Base => PriorityGrowthScenario.Base,
        GrowthScenario.Aggressive => PriorityGrowthScenario.Aggressive,
        _ => throw new InvalidOperationException($"Unknown growth scenario '{scenario}'.")
    };

    private static RetirementAgeBand ParseAgeBand(string stored) =>
        Enum.TryParse(stored, ignoreCase: false, out RetirementAgeBand band) && Enum.IsDefined(band)
            ? band
            : throw new InvalidOperationException($"A Priority Walk stores an unknown age band '{stored}'.");
}
