using static PersonalFinance.ApiService.Features.MoneyPriorities.Domain.PriorityNodeIds;

namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// Walks Priority Tree version 1 from a set of answers: the only place the path is computed. Pure: no I/O, no clock.
/// Answers to questions off the path are never read.
/// </summary>
internal static class PriorityPathEvaluator
{
    /// <summary>
    /// The ordered path from the start node to a leaf for <paramref name="answers"/>. Throws
    /// <see cref="ArgumentException"/> when a question on the path has no answer (the validators prevent that).
    /// </summary>
    public static PriorityPath Evaluate(PriorityAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        PriorityTree tree = PriorityTreeV1.Tree;

        var nodeIds = new List<string> { Start };
        string current = Start;
        while (tree.Node(current).Kind != PriorityNodeKind.Leaf)
        {
            string next = Next(current, answers);
            if (!tree.Edges.Any(edge => edge.From == current && edge.To == next))
            {
                throw new InvalidOperationException($"Priority Tree version {tree.Version} has no edge {current} -> {next}.");
            }

            nodeIds.Add(next);
            current = next;
        }

        return new PriorityPath(tree.Version, nodeIds, OnPath(answers, nodeIds));
    }

    /// <summary>
    /// <paramref name="answers"/> with every answer the path did not ask cleared to null: a question's answer is kept when
    /// the question is on the path, a leaf's follow-up only when the path ends at that leaf.
    /// </summary>
    private static PriorityAnswers OnPath(PriorityAnswers answers, List<string> nodeIds)
    {
        string leafId = nodeIds[^1];
        bool asksFund = nodeIds.Contains(EmergencyFund);
        bool asksMatch = nodeIds.Contains(EmployerMatch);
        bool asksHorizon = nodeIds.Contains(Horizon);
        bool asksLimit = nodeIds.Contains(ContributionLimit);

        return answers with
        {
            DebtBalance = leafId == PayDebt ? answers.DebtBalance : null,
            EmergencyFundMonths = asksFund ? answers.EmergencyFundMonths : null,
            MonthlyExpenses = leafId == BuildEmergencyFund ? answers.MonthlyExpenses : null,
            HasFullEmployerMatch = asksMatch ? answers.HasFullEmployerMatch : null,
            EmployerMatchPercent = leafId == CaptureMatch ? answers.EmployerMatchPercent : null,
            HorizonYears = asksHorizon ? answers.HorizonYears : null,
            AgeBand = asksLimit ? answers.AgeBand : null,
            AtContributionLimit = asksLimit ? answers.AtContributionLimit : null
        };
    }

    private static string Next(string nodeId, PriorityAnswers answers) => nodeId switch
    {
        Start => HighInterestDebt,
        HighInterestDebt => answers.DebtApr >= PriorityTreeThresholds.HighInterestAprPercent ? PayDebt : EmergencyFund,
        EmergencyFund => Required(answers.EmergencyFundMonths, nameof(answers.EmergencyFundMonths))
            >= PriorityTreeThresholds.EmergencyFundTargetMonths
                ? EmployerMatch
                : BuildEmergencyFund,
        EmployerMatch => Required(answers.HasFullEmployerMatch, nameof(answers.HasFullEmployerMatch)) ? Horizon : CaptureMatch,
        Horizon => Required(answers.HorizonYears, nameof(answers.HorizonYears)) switch
        {
            < PriorityTreeThresholds.ShortHorizonYears => ShortTermSavings,
            <= PriorityTreeThresholds.LongHorizonYears => BalancedBrokerage,
            _ => ContributionLimit
        },
        ContributionLimit => Required(answers.AtContributionLimit, nameof(answers.AtContributionLimit)) ? StockBrokerage : IraThen401k,
        _ => throw new InvalidOperationException($"No decision is authored for node '{nodeId}'.")
    };

    private static T Required<T>(T? answer, string name)
        where T : struct =>
        answer ?? throw new ArgumentException($"The path asks for {name}, but it has no answer.");
}
