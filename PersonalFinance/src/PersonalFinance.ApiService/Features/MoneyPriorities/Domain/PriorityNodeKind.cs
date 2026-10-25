namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>What a Priority Tree node is: the one start node, a question, or a leaf (a suggested priority).</summary>
internal enum PriorityNodeKind
{
    Start = 1,
    Question = 2,
    Leaf = 3
}
