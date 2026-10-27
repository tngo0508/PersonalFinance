using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.MoneyPriorities;

/// <summary>
/// Generates the Mermaid flowchart texts of a Priority Tree (spec #315, "Diagram texts") in the request's Language: the
/// whole tree with diamond questions, and the path view with only the path's nodes in one column and rounded-rectangle
/// questions. The path is highlighted with <c>classDef</c>/<c>class</c> on nodes and <c>linkStyle</c> by edge index,
/// because external CSS cannot override the id-scoped styles Mermaid writes into the SVG.
/// </summary>
/// <remarks>
/// The inputs are the localizer, the tree the ApiService sent, a path's node ids and a palette, never the answers. Every
/// node id must be one of the tree's and every colour a hex value, so every word in the text comes from the authored
/// tree or the resource files (<see cref="MoneyPrioritiesText"/>) and no user-entered value can reach the diagram
/// (ADR 0014).
/// </remarks>
internal static class PriorityDiagramText
{
    /// <summary>The whole tree, unhighlighted, for before a submit.</summary>
    public static string WholeTree(IStringLocalizer localizer, PriorityTreeDto tree, PriorityDiagramPalette palette)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(tree);
        ValidatePalette(palette);

        return Generate(localizer, tree.Nodes, tree.Edges, path: null, QuestionShape.Diamond, palette,
            localizer["MoneyPriorities.Diagram.WholeTree"]);
    }

    /// <summary>The whole tree with the path highlighted: <c>linkStyle</c> indices follow <see cref="PriorityTreeDto.Edges"/>.</summary>
    /// <param name="localizer">The request's Language.</param>
    /// <param name="tree">The tree the path was walked through.</param>
    /// <param name="pathNodeIds">The start node to a leaf, along the tree's edges.</param>
    /// <param name="palette">The colours.</param>
    public static string WholeTree(IStringLocalizer localizer, PriorityTreeDto tree, IReadOnlyList<string> pathNodeIds,
        PriorityDiagramPalette palette)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ValidatePalette(palette);
        Dictionary<string, PriorityTreeNodeDto> nodes = ValidatePath(tree, pathNodeIds);

        return Generate(localizer, tree.Nodes, tree.Edges, pathNodeIds, QuestionShape.Diamond, palette,
            localizer.Named("MoneyPriorities.Diagram.WholeTreePath",
                ("leaf", MoneyPrioritiesText.NodeLabel(localizer, nodes[pathNodeIds[^1]]))));
    }

    /// <summary>
    /// The path view: only the path's nodes, in one column, questions as rounded rectangles. Its <c>linkStyle</c>
    /// indices are its own edges' positions, which run along the path.
    /// </summary>
    /// <param name="localizer">The request's Language.</param>
    /// <param name="tree">The tree the path was walked through.</param>
    /// <param name="pathNodeIds">The start node to a leaf, along the tree's edges.</param>
    /// <param name="palette">The colours.</param>
    public static string PathView(IStringLocalizer localizer, PriorityTreeDto tree, IReadOnlyList<string> pathNodeIds,
        PriorityDiagramPalette palette)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ValidatePalette(palette);
        Dictionary<string, PriorityTreeNodeDto> nodes = ValidatePath(tree, pathNodeIds);

        PriorityTreeNodeDto[] pathNodes = pathNodeIds.Select(id => nodes[id]).ToArray();
        PriorityTreeEdgeDto[] edges = PathSteps(tree, pathNodeIds).Select(edge => edge!).ToArray();
        return Generate(localizer, pathNodes, edges, pathNodeIds, QuestionShape.RoundedRectangle, palette,
            localizer.Named("MoneyPriorities.Diagram.PathView",
                ("leaf", MoneyPrioritiesText.NodeLabel(localizer, nodes[pathNodeIds[^1]]))));
    }

    /// <summary>The tree's edge for each step of the path, in path order; null for a step the tree has no edge for.</summary>
    internal static IEnumerable<PriorityTreeEdgeDto?> PathSteps(PriorityTreeDto tree, IReadOnlyList<string> pathNodeIds)
    {
        for (int index = 1; index < pathNodeIds.Count; index++)
        {
            string from = pathNodeIds[index - 1];
            string to = pathNodeIds[index];
            yield return tree.Edges.FirstOrDefault(edge => edge.From == from && edge.To == to);
        }
    }

    /// <summary>
    /// Checks <paramref name="pathNodeIds"/> is a walk of <paramref name="tree"/>: the start node, then nodes the tree's
    /// edges lead to, ending at a leaf. So no string from outside the tree reaches the text.
    /// </summary>
    /// <returns>The tree's nodes by id.</returns>
    internal static Dictionary<string, PriorityTreeNodeDto> ValidatePath(PriorityTreeDto tree,
        IReadOnlyList<string> pathNodeIds)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(pathNodeIds);
        Dictionary<string, PriorityTreeNodeDto> nodes = tree.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        string? reason = PathProblem(tree, nodes, pathNodeIds);
        if (reason is not null)
        {
            throw new ArgumentException(
                $"The path is not a walk of Priority Tree version {tree.Version}: {reason}", nameof(pathNodeIds));
        }

        return nodes;
    }

    private enum QuestionShape
    {
        Diamond,
        RoundedRectangle
    }

    private static string Generate(
        IStringLocalizer localizer,
        IReadOnlyList<PriorityTreeNodeDto> nodes,
        IReadOnlyList<PriorityTreeEdgeDto> edges,
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
            "  accTitle: " + OneLine(localizer["MoneyPriorities.Diagram.Title"]),
            "  accDescr: " + OneLine(accessibleDescription)
        ];

        foreach (PriorityTreeNodeDto node in nodes)
        {
            lines.Add("  " + Shape(node, MoneyPrioritiesText.NodeLabel(localizer, node), questionShape));
        }

        List<int> pathEdgeIndices = [];
        for (int index = 0; index < edges.Count; index++)
        {
            PriorityTreeEdgeDto edge = edges[index];
            string? label = MoneyPrioritiesText.EdgeLabel(localizer, edge);
            lines.Add(label is null
                ? $"  {edge.From} --> {edge.To}"
                : $"  {edge.From} -->|{Quote(label)}| {edge.To}");
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

        foreach (PriorityTreeNodeDto node in nodes)
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

    private static bool IsPathStep(IReadOnlyList<string> path, PriorityTreeEdgeDto edge)
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

    private static string Shape(PriorityTreeNodeDto node, string label, QuestionShape questionShape)
    {
        string quoted = Quote(label);
        return node.Kind switch
        {
            PriorityNodeKind.Start => $"{node.Id}([{quoted}])",
            PriorityNodeKind.Question when questionShape == QuestionShape.Diamond => $"{node.Id}{{{quoted}}}",
            PriorityNodeKind.Question => $"{node.Id}({quoted})",
            _ => $"{node.Id}[{quoted}]"
        };
    }

    /// <summary>Always quoted: labels hold characters Mermaid would parse, such as the parentheses in "401(k)".</summary>
    private static string Quote(string label) =>
        "\"" + OneLine(label).Replace("\"", "#quot;", StringComparison.Ordinal) + "\"";

    /// <summary>Text on one line, so a translation cannot start a Mermaid statement of its own.</summary>
    private static string OneLine(string text) =>
        string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

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

    private static string? PathProblem(PriorityTreeDto tree, Dictionary<string, PriorityTreeNodeDto> nodes,
        IReadOnlyList<string> pathNodeIds)
    {
        if (pathNodeIds.Count < 2)
        {
            return "it needs the start node and a leaf.";
        }

        if (pathNodeIds.Any(id => id is null || !nodes.ContainsKey(id)))
        {
            return "it holds a node id the tree does not have.";
        }

        if (nodes[pathNodeIds[0]].Kind != PriorityNodeKind.Start)
        {
            return "it does not begin at the start node.";
        }

        if (nodes[pathNodeIds[^1]].Kind != PriorityNodeKind.Leaf)
        {
            return "it does not end at a leaf.";
        }

        return PathSteps(tree, pathNodeIds).Any(edge => edge is null)
            ? "two of its consecutive nodes have no edge between them."
            : null;
    }
}
