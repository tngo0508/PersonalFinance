namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The "Your path" sentences: the text equivalent of the highlighted diagram, shown as an ordered list under the result.
/// One sentence per question on the path with the answer taken ("Debt with APR of 7% or more? No."), then the
/// suggested priority. Built from the tree's wording and the path's node ids only, never the answers' values.
/// </summary>
internal static class PriorityPathSentences
{
    /// <summary>The sentences for a path, in path order.</summary>
    /// <param name="tree">The tree the path was walked through.</param>
    /// <param name="pathNodeIds">A <see cref="PriorityPath.NodeIds"/>: the start node to a leaf, along the tree's edges.</param>
    public static IReadOnlyList<string> For(PriorityTree tree, IReadOnlyList<string> pathNodeIds)
    {
        ArgumentNullException.ThrowIfNull(tree);
        PriorityDiagramText.ValidatePath(tree, pathNodeIds);

        List<string> sentences = [];
        foreach (PriorityEdge? step in PriorityDiagramText.PathSteps(tree, pathNodeIds))
        {
            PriorityNode from = tree.Node(step!.From);
            if (from.Kind == PriorityNodeKind.Question)
            {
                sentences.Add($"{from.Label} {step.Label}.");
            }
        }

        sentences.Add($"Suggested priority: {tree.Node(pathNodeIds[^1]).Label}.");
        return sentences;
    }
}
