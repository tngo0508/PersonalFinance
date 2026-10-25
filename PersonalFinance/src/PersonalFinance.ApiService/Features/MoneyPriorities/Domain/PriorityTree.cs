namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// An authored Priority Tree: its nodes and labelled edges in a stable authored order. The order is part of the
/// contract: the diagram texts index edges by their position in <see cref="Edges"/> (Mermaid <c>linkStyle</c>).
/// </summary>
internal sealed class PriorityTree
{
    private readonly Dictionary<string, PriorityNode> nodesById;

    public PriorityTree(int version, IReadOnlyList<PriorityNode> nodes, IReadOnlyList<PriorityEdge> edges)
    {
        Version = version;
        Nodes = nodes;
        Edges = edges;
        nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
    }

    /// <summary>The tree version stored on each Priority Walk.</summary>
    public int Version { get; }

    /// <summary>The start node, then the questions, then the leaves.</summary>
    public IReadOnlyList<PriorityNode> Nodes { get; }

    /// <summary>Every edge, in authored order.</summary>
    public IReadOnlyList<PriorityEdge> Edges { get; }

    /// <summary>The node with <paramref name="id"/>; throws <see cref="KeyNotFoundException"/> when there is none.</summary>
    public PriorityNode Node(string id) =>
        nodesById.TryGetValue(id, out PriorityNode? node)
            ? node
            : throw new KeyNotFoundException($"Priority Tree version {Version} has no node '{id}'.");
}
