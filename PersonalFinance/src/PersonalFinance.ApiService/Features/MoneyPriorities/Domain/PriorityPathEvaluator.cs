namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// Walks Priority Tree version 1 from a set of answers: the only place the path is computed. The decisions themselves
/// live in <see cref="PriorityQuestions"/>, which the submit validator shares. Pure: no I/O, no clock. Answers to
/// questions off the path are never read.
/// </summary>
internal static class PriorityPathEvaluator
{
    /// <summary>
    /// The ordered path from the start node to a leaf for <paramref name="answers"/>. Throws
    /// <see cref="ArgumentException"/> when a question on the path has no answer (the validators prevent that).
    /// </summary>
    public static PriorityPath Evaluate(PriorityAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        PriorityTree tree = PriorityTreeV1.Tree;

        IReadOnlyList<string> nodeIds = PriorityQuestions.Walk(answers);
        string last = nodeIds[^1];
        if (tree.Node(last).Kind != PriorityNodeKind.Leaf)
        {
            throw PriorityAnswerGuard.Missing(PriorityQuestions.AnswerAt(last), "The path asks for");
        }

        for (int i = 1; i < nodeIds.Count; i++)
        {
            string from = nodeIds[i - 1];
            string to = nodeIds[i];
            if (!tree.Edges.Any(edge => edge.From == from && edge.To == to))
            {
                throw new InvalidOperationException($"Priority Tree version {tree.Version} has no edge {from} -> {to}.");
            }
        }

        // An answer is kept when its question is on the path, a leaf's follow-up only when the path ends at that leaf.
        return new PriorityPath(tree.Version, nodeIds, PriorityQuestions.Asked(nodeIds).KeepOnly(answers));
    }
}
