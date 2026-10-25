using static PersonalFinance.ApiService.Features.MoneyPriorities.Domain.PriorityNodeIds;

namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The decisions of Priority Tree version 1: which node each answer leads to, and so which questions and follow-ups a
/// path asks. The single source for both <see cref="PriorityPathEvaluator"/> and the submit validator. Pure.
/// </summary>
internal static class PriorityQuestions
{
    /// <summary>
    /// The node ids from the start node, following <paramref name="answers"/>, up to a leaf or up to the first question
    /// whose answer is missing (then the last id is that question).
    /// </summary>
    public static IReadOnlyList<string> Walk(PriorityAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        var nodeIds = new List<string> { Start };
        string? next = Next(Start, answers);
        while (next is not null)
        {
            nodeIds.Add(next);
            next = Next(next, answers);
        }

        return nodeIds;
    }

    /// <summary>The name of the answer question <paramref name="nodeId"/> reads, e.g. <c>HorizonYears</c> for Q4.</summary>
    public static string AnswerAt(string nodeId) => nodeId switch
    {
        HighInterestDebt => nameof(PriorityAnswers.DebtApr),
        EmergencyFund => nameof(PriorityAnswers.EmergencyFundMonths),
        EmployerMatch => nameof(PriorityAnswers.HasFullEmployerMatch),
        Horizon => nameof(PriorityAnswers.HorizonYears),
        ContributionLimit => nameof(PriorityAnswers.AtContributionLimit),
        _ => throw new InvalidOperationException($"Node '{nodeId}' is not a question.")
    };

    /// <summary>
    /// Which answers the nodes <paramref name="nodeIds"/> ask for: a question's answer when the question is among them,
    /// a leaf's follow-up when that leaf is.
    /// </summary>
    public static PriorityAskedAnswers Asked(IReadOnlyCollection<string> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        bool asksLimit = nodeIds.Contains(ContributionLimit);
        return new PriorityAskedAnswers(
            DebtBalance: nodeIds.Contains(PayDebt),
            EmergencyFundMonths: nodeIds.Contains(EmergencyFund),
            MonthlyExpenses: nodeIds.Contains(BuildEmergencyFund),
            HasFullEmployerMatch: nodeIds.Contains(EmployerMatch),
            EmployerMatchPercent: nodeIds.Contains(CaptureMatch),
            HorizonYears: nodeIds.Contains(Horizon),
            AgeBand: asksLimit,
            AtContributionLimit: asksLimit);
    }

    /// <summary>The node <paramref name="nodeId"/> leads to for <paramref name="answers"/>; null at a leaf or when the answer it reads is missing.</summary>
    private static string? Next(string nodeId, PriorityAnswers answers) => nodeId switch
    {
        Start => HighInterestDebt,
        HighInterestDebt => answers.DebtApr >= PriorityTreeThresholds.HighInterestAprPercent ? PayDebt : EmergencyFund,
        EmergencyFund => answers.EmergencyFundMonths switch
        {
            null => null,
            >= PriorityTreeThresholds.EmergencyFundTargetMonths => EmployerMatch,
            _ => BuildEmergencyFund
        },
        EmployerMatch => answers.HasFullEmployerMatch switch
        {
            null => null,
            true => Horizon,
            false => CaptureMatch
        },
        Horizon => answers.HorizonYears switch
        {
            null => null,
            < PriorityTreeThresholds.ShortHorizonYears => ShortTermSavings,
            <= PriorityTreeThresholds.LongHorizonYears => BalancedBrokerage,
            _ => ContributionLimit
        },
        ContributionLimit => answers.AtContributionLimit switch
        {
            null => null,
            true => StockBrokerage,
            false => IraThen401k
        },
        PayDebt or BuildEmergencyFund or CaptureMatch or ShortTermSavings or BalancedBrokerage or IraThen401k
            or StockBrokerage => null,
        _ => throw new InvalidOperationException($"No decision is authored for node '{nodeId}'.")
    };
}
