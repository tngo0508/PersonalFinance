using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The "Your path" sentences in English: one sentence per question on the path with the answer taken ("Debt with APR of
/// 7% or more? No."), then the suggested priority. Built from the tree's wording and the path's node ids only, never the
/// answers' values. They are facts for the Purpose Note's prompt; the page builds its own, in the request's Language,
/// from the tree and the path's node ids (#382).
/// </summary>
internal static class PriorityPathSentences
{
    /// <summary>The sentences for a path, in path order.</summary>
    /// <param name="tree">The tree the path was walked through.</param>
    /// <param name="pathNodeIds">A <see cref="PriorityPath.NodeIds"/>: the start node to a leaf, along the tree's edges.</param>
    public static IReadOnlyList<string> For(PriorityTree tree, IReadOnlyList<string> pathNodeIds)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ValidatePath(tree, pathNodeIds);

        List<string> sentences = [];
        for (int index = 1; index < pathNodeIds.Count; index++)
        {
            PriorityNode from = tree.Node(pathNodeIds[index - 1]);
            if (from.Kind == PriorityNodeKind.Question)
            {
                PriorityEdge step = Step(tree, from.Id, pathNodeIds[index])!;
                sentences.Add($"{from.Label} {step.Label}.");
            }
        }

        sentences.Add($"Suggested priority: {tree.Node(pathNodeIds[^1]).Label}.");
        return sentences;
    }

    private static PriorityEdge? Step(PriorityTree tree, string from, string to) =>
        tree.Edges.FirstOrDefault(edge => edge.From == from && edge.To == to);

    /// <summary>
    /// Checks <paramref name="pathNodeIds"/> is a walk of <paramref name="tree"/>: the start node, then nodes the tree's
    /// edges lead to, ending at a leaf. So no string from outside the tree reaches the sentences.
    /// </summary>
    private static void ValidatePath(PriorityTree tree, IReadOnlyList<string> pathNodeIds)
    {
        ArgumentNullException.ThrowIfNull(pathNodeIds);
        HashSet<string> known = tree.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        bool isWalk = pathNodeIds.Count >= 2
            && pathNodeIds.All(id => id is not null && known.Contains(id))
            && tree.Node(pathNodeIds[0]).Kind == PriorityNodeKind.Start
            && tree.Node(pathNodeIds[^1]).Kind == PriorityNodeKind.Leaf
            && Enumerable.Range(1, pathNodeIds.Count - 1).All(index => Step(tree, pathNodeIds[index - 1], pathNodeIds[index]) is not null);
        if (!isWalk)
        {
            throw new ArgumentException($"The path is not a walk of Priority Tree version {tree.Version}.", nameof(pathNodeIds));
        }
    }
}
