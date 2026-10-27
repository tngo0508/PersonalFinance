using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>What a Priority Tree node is: the one start node, a question, or a leaf (a suggested priority).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PriorityNodeKind
{
    Start = 1,
    Question = 2,
    Leaf = 3
}
