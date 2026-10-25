namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>One directed edge of the Priority Tree, from a node to the node an answer leads to.</summary>
/// <param name="From">The id of the node the edge leaves.</param>
/// <param name="To">The id of the node the edge enters.</param>
/// <param name="Label">The answer wording shown on the edge (<c>Yes</c>, <c>Under 3 years</c>), or null for the start edge.</param>
internal sealed record PriorityEdge(string From, string To, string? Label);
