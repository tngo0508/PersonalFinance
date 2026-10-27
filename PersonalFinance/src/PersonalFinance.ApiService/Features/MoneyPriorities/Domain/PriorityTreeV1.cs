using PersonalFinance.Shared.Enums;
using static PersonalFinance.ApiService.Features.MoneyPriorities.Domain.PriorityNodeIds;

namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// Priority Tree version 1, the source of truth for the questions, the leaves and the edges between them (spec #315,
/// draft #300). Changing the wording or the shape means a new version, because a saved Priority Walk keeps the version
/// it was answered against.
/// </summary>
internal static class PriorityTreeV1
{
    public const int Version = 1;

    public static PriorityTree Tree { get; } = new(
        Version,
        [
            new PriorityNode(Start, PriorityNodeKind.Start, "Extra money each month"),
            new PriorityNode(HighInterestDebt, PriorityNodeKind.Question, "Debt with APR of 7% or more?"),
            new PriorityNode(EmergencyFund, PriorityNodeKind.Question, "Emergency fund covers 3 months of expenses?"),
            new PriorityNode(EmployerMatch, PriorityNodeKind.Question, "Getting the full employer match?"),
            new PriorityNode(Horizon, PriorityNodeKind.Question, "When will you need this money?"),
            new PriorityNode(ContributionLimit, PriorityNodeKind.Question, "Already at the IRS limit for your age?"),
            new PriorityNode(PayDebt, PriorityNodeKind.Leaf, "Pay down the high-interest debt first"),
            new PriorityNode(BuildEmergencyFund, PriorityNodeKind.Leaf, "Build the emergency fund to 3 months"),
            new PriorityNode(CaptureMatch, PriorityNodeKind.Leaf, "Raise 401(k) contributions to capture the full match"),
            new PriorityNode(ShortTermSavings, PriorityNodeKind.Leaf, "High-yield savings or short-term Treasuries"),
            new PriorityNode(BalancedBrokerage, PriorityNodeKind.Leaf, "Taxable brokerage: balanced index funds"),
            new PriorityNode(IraThen401k, PriorityNodeKind.Leaf, "Max the IRA, then the 401(k)"),
            new PriorityNode(StockBrokerage, PriorityNodeKind.Leaf, "Taxable brokerage: stock index funds")
        ],
        [
            new PriorityEdge(Start, HighInterestDebt, null),
            new PriorityEdge(HighInterestDebt, PayDebt, "Yes"),
            new PriorityEdge(HighInterestDebt, EmergencyFund, "No"),
            new PriorityEdge(EmergencyFund, BuildEmergencyFund, "No"),
            new PriorityEdge(EmergencyFund, EmployerMatch, "Yes"),
            new PriorityEdge(EmployerMatch, CaptureMatch, "No"),
            new PriorityEdge(EmployerMatch, Horizon, "Yes"),
            new PriorityEdge(Horizon, ShortTermSavings, "Under 3 years"),
            new PriorityEdge(Horizon, BalancedBrokerage, "3 to 10 years"),
            new PriorityEdge(Horizon, ContributionLimit, "Over 10 years"),
            new PriorityEdge(ContributionLimit, IraThen401k, "No"),
            new PriorityEdge(ContributionLimit, StockBrokerage, "Yes")
        ]);
}
