using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>One node of the Priority Tree.</summary>
/// <param name="Id">Stable id stored on a Priority Walk's path, e.g. <c>Q1</c> or <c>L5</c> (see <see cref="PriorityNodeIds"/>).</param>
/// <param name="Kind">Start, question or leaf.</param>
/// <param name="Label">The authored wording, plain text; never holds a user-entered value.</param>
internal sealed record PriorityNode(string Id, PriorityNodeKind Kind, string Label);
