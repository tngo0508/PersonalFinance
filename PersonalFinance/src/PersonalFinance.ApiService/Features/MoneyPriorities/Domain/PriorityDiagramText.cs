namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// Generates the Mermaid flowchart texts of a Priority Tree (spec #315, "Diagram texts"): the whole tree with diamond
/// questions, and the path view with only the path's nodes in one column and rounded-rectangle questions. The path is
/// highlighted with <c>classDef</c>/<c>class</c> on nodes and <c>linkStyle</c> by edge index, because external CSS
/// cannot override the id-scoped styles Mermaid writes into the SVG.
/// </summary>
/// <remarks>
/// The inputs are the tree, a path's node ids and a palette, never the answers. Every node id must be one of the
/// tree's and every colour a hex value, so every word in the text comes from the authored tree and no user-entered
/// value can reach the diagram.
/// </remarks>
internal static class PriorityDiagramText
{
    /// <summary>The accessible title of every Priority Tree diagram.</summary>
    public const string AccessibleTitle = "Priority Tree";

    /// <summary>The whole tree, unhighlighted, for before a submit.</summary>
    public static string WholeTree(PriorityTree tree, PriorityDiagramPalette palette)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ValidatePalette(palette);

        return Generate(tree.Nodes, tree.Edges, path: null, QuestionShape.Diamond, palette,
            "The whole tree before any answers");
    }

    /// <summary>The whole tree with the path highlighted: <c>linkStyle</c> indices follow <see cref="PriorityTree.Edges"/>.</summary>
    /// <param name="tree">The tree the path was walked through.</param>
    /// <param name="pathNodeIds">A <see cref="PriorityPath.NodeIds"/>: the start node to a leaf, along the tree's edges.</param>
    /// <param name="palette">The colours.</param>
    public static string WholeTree(PriorityTree tree, IReadOnlyList<string> pathNodeIds, PriorityDiagramPalette palette)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ValidatePalette(palette);
        ValidatePath(tree, pathNodeIds);

        return Generate(tree.Nodes, tree.Edges, pathNodeIds, QuestionShape.Diamond, palette,
            "Suggested path ending at: " + tree.Node(pathNodeIds[^1]).Label);
    }

    /// <summary>
    /// The path view: only the path's nodes, in one column, questions as rounded rectangles. Its <c>linkStyle</c>
    /// indices are its own edges' positions, which run along the path.
    /// </summary>
    /// <param name="tree">The tree the path was walked through.</param>
    /// <param name="pathNodeIds">A <see cref="PriorityPath.NodeIds"/>: the start node to a leaf, along the tree's edges.</param>
    /// <param name="palette">The colours.</param>
    public static string PathView(PriorityTree tree, IReadOnlyList<string> pathNodeIds, PriorityDiagramPalette palette)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ValidatePalette(palette);
        ValidatePath(tree, pathNodeIds);

        PriorityNode[] nodes = pathNodeIds.Select(tree.Node).ToArray();
        PriorityEdge[] edges = PathEdges(tree, pathNodeIds).ToArray();
        return Generate(nodes, edges, pathNodeIds, QuestionShape.RoundedRectangle, palette,
            "Your path, ending at: " + tree.Node(pathNodeIds[^1]).Label);
    }

    /// <summary>The tree's edge for each step of the path, in path order; null for a step the tree has no edge for.</summary>
    internal static IEnumerable<PriorityEdge?> PathSteps(PriorityTree tree, IReadOnlyList<string> pathNodeIds)
    {
        for (int index = 1; index < pathNodeIds.Count; index++)
        {
            string from = pathNodeIds[index - 1];
            string to = pathNodeIds[index];
            yield return tree.Edges.FirstOrDefault(edge => edge.From == from && edge.To == to);
        }
    }

    private static IEnumerable<PriorityEdge> PathEdges(PriorityTree tree, IReadOnlyList<string> pathNodeIds) =>
        PathSteps(tree, pathNodeIds).Select(edge => edge!);

    private enum QuestionShape
    {
        Diamond,
        RoundedRectangle
    }

    private static string Generate(
        IReadOnlyList<PriorityNode> nodes,
        IReadOnlyList<PriorityEdge> edges,
        IReadOnlyList<string>? path,
        QuestionShape questionShape,
        PriorityDiagramPalette palette,
        string accessibleDescription)
    {
        HashSet<string> onPath = path is null ? [] : new HashSet<string>(path, StringComparer.Ordinal);
        string? leafId = path?[^1];

        List<string> lines =
        [
            "flowchart TD",
            "  accTitle: " + AccessibleTitle,
            "  accDescr: " + accessibleDescription
        ];

        foreach (PriorityNode node in nodes)
        {
            lines.Add("  " + Shape(node, questionShape));
        }

        List<int> pathEdgeIndices = [];
        for (int index = 0; index < edges.Count; index++)
        {
            PriorityEdge edge = edges[index];
            lines.Add(edge.Label is null
                ? $"  {edge.From} --> {edge.To}"
                : $"  {edge.From} -->|{Quote(edge.Label)}| {edge.To}");
            if (path is not null && IsPathStep(path, edge))
            {
                pathEdgeIndices.Add(index);
            }
        }

        lines.Add($"  classDef question fill:{palette.QuestionFill},stroke:{palette.QuestionStroke},color:{palette.Ink}");
        lines.Add($"  classDef leaf fill:{palette.LeafFill},stroke:{palette.LeafStroke},color:{palette.SoftText}");
        lines.Add($"  classDef start fill:{palette.Background},stroke:{palette.Line},color:{palette.Ink}");
        lines.Add($"  classDef onPath fill:{palette.OnPathFill},stroke:{palette.OnPathStroke},stroke-width:2px,color:{palette.Ink}");
        lines.Add($"  classDef chosen fill:{palette.ChosenFill},stroke:{palette.ChosenStroke},stroke-width:3px,color:{palette.ChosenText}");
        lines.Add($"  classDef dim fill:{palette.DimFill},stroke:{palette.DimStroke},color:{palette.DimText}");

        foreach (PriorityNode node in nodes)
        {
            string nodeClass = path is null ? KindClass(node.Kind)
                : node.Id == leafId ? "chosen"
                : onPath.Contains(node.Id) ? "onPath"
                : "dim";
            lines.Add($"  class {node.Id} {nodeClass}");
        }

        if (path is null)
        {
            lines.Add($"  linkStyle default stroke:{palette.Line},stroke-width:1.5px");
        }
        else
        {
            lines.Add($"  linkStyle default stroke:{palette.DimLine},stroke-width:1px,color:{palette.DimText}");
            lines.Add($"  linkStyle {string.Join(',', pathEdgeIndices)} stroke:{palette.OnPathStroke},stroke-width:3px,color:{palette.Ink}");
        }

        return string.Join('\n', lines);
    }

    private static bool IsPathStep(IReadOnlyList<string> path, PriorityEdge edge)
    {
        for (int index = 0; index < path.Count - 1; index++)
        {
            if (path[index] == edge.From && path[index + 1] == edge.To)
            {
                return true;
            }
        }

        return false;
    }

    private static string Shape(PriorityNode node, QuestionShape questionShape)
    {
        string label = Quote(node.Label);
        return node.Kind switch
        {
            PriorityNodeKind.Start => $"{node.Id}([{label}])",
            PriorityNodeKind.Question when questionShape == QuestionShape.Diamond => $"{node.Id}{{{label}}}",
            PriorityNodeKind.Question => $"{node.Id}({label})",
            _ => $"{node.Id}[{label}]"
        };
    }

    /// <summary>Always quoted: labels hold characters Mermaid would parse, such as the parentheses in "401(k)".</summary>
    private static string Quote(string label) => "\"" + label.Replace("\"", "#quot;", StringComparison.Ordinal) + "\"";

    private static string KindClass(PriorityNodeKind kind) => kind switch
    {
        PriorityNodeKind.Start => "start",
        PriorityNodeKind.Question => "question",
        _ => "leaf"
    };

    private static void ValidatePalette(PriorityDiagramPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        foreach (string colour in palette.Colours)
        {
            if (!IsHexColour(colour))
            {
                throw new ArgumentException(
                    $"Every palette colour must be a #RRGGBB hex value; '{colour}' is not.", nameof(palette));
            }
        }
    }

    private static bool IsHexColour(string? colour) =>
        colour is { Length: 7 } && colour[0] == '#' && colour.Skip(1).All(char.IsAsciiHexDigit);

    /// <summary>
    /// Checks <paramref name="pathNodeIds"/> is a walk of <paramref name="tree"/>: the start node, then nodes the tree's
    /// edges lead to, ending at a leaf. So no string from outside the tree reaches the text.
    /// </summary>
    internal static void ValidatePath(PriorityTree tree, IReadOnlyList<string> pathNodeIds)
    {
        ArgumentNullException.ThrowIfNull(pathNodeIds);
        string? reason = PathProblem(tree, pathNodeIds);
        if (reason is not null)
        {
            throw new ArgumentException(
                $"The path is not a walk of Priority Tree version {tree.Version}: {reason}", nameof(pathNodeIds));
        }
    }

    private static string? PathProblem(PriorityTree tree, IReadOnlyList<string> pathNodeIds)
    {
        if (pathNodeIds.Count < 2)
        {
            return "it needs the start node and a leaf.";
        }

        HashSet<string> known = tree.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        if (pathNodeIds.Any(id => id is null || !known.Contains(id)))
        {
            return "it holds a node id the tree does not have.";
        }

        if (tree.Node(pathNodeIds[0]).Kind != PriorityNodeKind.Start)
        {
            return "it does not begin at the start node.";
        }

        if (tree.Node(pathNodeIds[^1]).Kind != PriorityNodeKind.Leaf)
        {
            return "it does not end at a leaf.";
        }

        return PathSteps(tree, pathNodeIds).Any(edge => edge is null)
            ? "two of its consecutive nodes have no edge between them."
            : null;
    }
}
