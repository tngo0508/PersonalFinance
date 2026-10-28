using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.MoneyPriorities;

/// <summary>
/// Puts the ApiService's fixed Money Priorities wording into the request's Language before the page shows it. The
/// ApiService writes English; this swaps in the translation by what identifies the text, never by the English itself:
/// a tree node's label by its node id and an answer by its edge (one it has no key for passes through as written, so a
/// newer tree still shows), a Purpose Note message by the note's state, and each disclaimer by its fixed key. Keys
/// follow the node ids, not the tree version, so a saved Walk shows the current wording of its nodes.
/// </summary>
public static class MoneyPrioritiesText
{
    private const string LeafKeyPrefix = "MoneyPriorities.Leaf.";
    private const string NodeKeyPrefix = "MoneyPriorities.Node.";
    private const string EdgeKeyPrefix = "MoneyPriorities.Edge.";

    /// <summary>The leaf label of <paramref name="leafId"/>, or <paramref name="english"/> when it has no key.</summary>
    public static string LeafLabel(IStringLocalizer localizer, string leafId, string english) =>
        TryLeafLabel(localizer, leafId, out string label) ? label : english;

    /// <summary>The resource key of a tree node's label: <c>MoneyPriorities.Leaf.L1</c>, <c>MoneyPriorities.Node.Q1</c>.</summary>
    public static string NodeKey(PriorityTreeNodeDto node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return (node.Kind == PriorityNodeKind.Leaf ? LeafKeyPrefix : NodeKeyPrefix) + node.Id;
    }

    /// <summary>The resource key of an edge's answer, by the nodes it joins: <c>MoneyPriorities.Edge.Q1.L1</c>.</summary>
    public static string EdgeKey(PriorityTreeEdgeDto edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return EdgeKeyPrefix + edge.From + "." + edge.To;
    }

    /// <summary>A tree node's label in the request's Language, or its English when it has no key.</summary>
    public static string NodeLabel(IStringLocalizer localizer, PriorityTreeNodeDto node) =>
        Translate(localizer, NodeKey(node), node.Label);

    /// <summary>An edge's answer in the request's Language, its English when it has no key, or null for the start edge.</summary>
    public static string? EdgeLabel(IStringLocalizer localizer, PriorityTreeEdgeDto edge) =>
        edge.Label is null ? null : Translate(localizer, EdgeKey(edge), edge.Label);

    /// <summary>
    /// The "Your path" sentences in the request's Language: the text equivalent of the highlighted diagram. One
    /// sentence per question on the path with the answer taken ("Debt with APR of 7% or more? No."), then the suggested
    /// priority. Built from the tree's wording and the path's node ids only, never the answers' values.
    /// </summary>
    /// <param name="localizer">The request's Language.</param>
    /// <param name="tree">The tree the path was walked through.</param>
    /// <param name="pathNodeIds">The start node to a leaf, along the tree's edges.</param>
    public static List<string> PathSentences(IStringLocalizer localizer, PriorityTreeDto tree,
        IReadOnlyList<string> pathNodeIds)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        Dictionary<string, PriorityTreeNodeDto> nodes = PriorityDiagramText.ValidatePath(tree, pathNodeIds);

        List<string> sentences = [];
        foreach (PriorityTreeEdgeDto? step in PriorityDiagramText.PathSteps(tree, pathNodeIds))
        {
            PriorityTreeNodeDto from = nodes[step!.From];
            if (from.Kind == PriorityNodeKind.Question)
            {
                sentences.Add(localizer.Named("MoneyPriorities.Path.Step",
                    ("question", NodeLabel(localizer, from)), ("answer", EdgeLabel(localizer, step) ?? string.Empty)));
            }
        }

        sentences.Add(localizer.Named("MoneyPriorities.Path.Suggested",
            ("leaf", NodeLabel(localizer, nodes[pathNodeIds[^1]]))));
        return sentences;
    }

    /// <summary>
    /// A Walk's result as the page shows it, in the request's Language: the Walk translated in place
    /// (<see cref="Localize(IStringLocalizer, PriorityWalkResultDto)"/>), the "Your path" sentences and both diagrams.
    /// </summary>
    public static MoneyPrioritiesResultViewModel Result(IStringLocalizer localizer, PriorityWalkResultDto walk)
    {
        ArgumentNullException.ThrowIfNull(walk);
        return new MoneyPrioritiesResultViewModel
        {
            Walk = Localize(localizer, walk),
            PathSentences = PathSentences(localizer, walk.Tree, walk.PathNodeIds),
            WholeTreeDiagram = PriorityDiagramText.WholeTree(localizer, walk.Tree, walk.PathNodeIds, PriorityDiagramPalette.Light),
            PathViewDiagram = PriorityDiagramText.PathView(localizer, walk.Tree, walk.PathNodeIds, PriorityDiagramPalette.Light),
            WholeTreeDiagramDark = PriorityDiagramText.WholeTree(localizer, walk.Tree, walk.PathNodeIds, PriorityDiagramPalette.Dark),
            PathViewDiagramDark = PriorityDiagramText.PathView(localizer, walk.Tree, walk.PathNodeIds, PriorityDiagramPalette.Dark)
        };
    }

    /// <summary>The whole tree before a submit, unhighlighted, in the request's Language and the given palette.</summary>
    internal static string WholeTreeDiagram(IStringLocalizer localizer, PriorityTreeDto tree, PriorityDiagramPalette palette) =>
        PriorityDiagramText.WholeTree(localizer, tree, palette);

    /// <summary>Translates a Walk's result in place: its leaf labels and its estimate disclaimer.</summary>
    public static PriorityWalkResultDto Localize(IStringLocalizer localizer, PriorityWalkResultDto walk)
    {
        ArgumentNullException.ThrowIfNull(walk);
        if (TryLeafLabel(localizer, walk.LeafId, out string label))
        {
            walk.LeafLabel = label;
            walk.HeadlineLeafLabel = localizer.Named("MoneyPriorities.Result.HeadlineLeaf", ("leaf", label));
        }

        walk.Disclaimer = localizer["MoneyPriorities.Result.EstimateDisclaimer"];
        return walk;
    }

    /// <summary>
    /// Translates a Purpose Note reply in place: its message by the note's state (a quota refusal by its keyed reason,
    /// which names the daily limit or the monthly budget), and its disclaimer. The note itself
    /// is AI-written and shown as written, and so is the message of a written note.
    /// </summary>
    public static PriorityWalkPurposeNoteDto Localize(IStringLocalizer localizer, PriorityWalkPurposeNoteDto note)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(note);
        string? key = note.State switch
        {
            PurposeNoteState.NoPurpose => "MoneyPriorities.Note.Invite",
            PurposeNoteState.Unavailable => "MoneyPriorities.Note.Unavailable",
            PurposeNoteState.QuotaReached => "MoneyPriorities.Note.QuotaReached",
            PurposeNoteState.Refused => "MoneyPriorities.Note.Refused",
            _ => null
        };
        if (note.State == PurposeNoteState.QuotaReached && note.Reason is not null)
        {
            note.Message = ApiErrorMessages.Resolve(localizer, note.Reason);
        }
        else if (key is not null)
        {
            note.Message = localizer[key];
        }

        note.Disclaimer = localizer["MoneyPriorities.Note.Disclaimer"];
        return note;
    }

    /// <summary>Translates the leaf label of each Earlier results row in place.</summary>
    public static List<PriorityWalkSummaryDto> Localize(IStringLocalizer localizer, List<PriorityWalkSummaryDto> walks)
    {
        ArgumentNullException.ThrowIfNull(walks);
        foreach (PriorityWalkSummaryDto walk in walks)
        {
            walk.LeafLabel = LeafLabel(localizer, walk.LeafId, walk.LeafLabel);
        }

        return walks;
    }

    private static bool TryLeafLabel(IStringLocalizer localizer, string leafId, out string label)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        label = string.Empty;
        return !string.IsNullOrEmpty(leafId) && TryTranslate(localizer, LeafKeyPrefix + leafId, out label);
    }

    private static string Translate(IStringLocalizer localizer, string key, string english) =>
        TryTranslate(localizer, key, out string text) ? text : english;

    private static bool TryTranslate(IStringLocalizer localizer, string key, out string text)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        LocalizedString found = localizer[key];
        text = found.Value;
        return !found.ResourceNotFound;
    }
}
