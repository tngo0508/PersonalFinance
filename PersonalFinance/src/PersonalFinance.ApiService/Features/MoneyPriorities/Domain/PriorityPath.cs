namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>The walk through a Priority Tree for one set of answers.</summary>
/// <param name="TreeVersion">The version of the tree that was walked.</param>
/// <param name="NodeIds">The node ids from the start node to the leaf, in order.</param>
/// <param name="Answers">The answers with every answer off this path cleared to null.</param>
internal sealed record PriorityPath(int TreeVersion, IReadOnlyList<string> NodeIds, PriorityAnswers Answers)
{
    /// <summary>The leaf the path ends at, e.g. <c>L5</c>.</summary>
    public string LeafId => NodeIds[^1];

    /// <summary>The node ids joined by commas, as a Priority Walk stores them, e.g. <c>S,Q1,Q2,Q3,Q4,L5</c>.</summary>
    public string StoredPath => string.Join(',', NodeIds);
}
