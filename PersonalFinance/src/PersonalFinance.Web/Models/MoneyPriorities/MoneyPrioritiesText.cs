using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.MoneyPriorities;

/// <summary>
/// Puts the ApiService's fixed Money Priorities wording into the request's Language before the page shows it. The
/// ApiService writes English; this swaps in the translation by what identifies the text, never by the English itself:
/// a leaf label by its leaf id (one it has no key for passes through as written, so a newer tree still shows), a
/// Purpose Note message by the note's state, and each disclaimer by its fixed key.
/// </summary>
public static class MoneyPrioritiesText
{
    private const string LeafKeyPrefix = "MoneyPriorities.Leaf.";

    /// <summary>The leaf label of <paramref name="leafId"/>, or <paramref name="english"/> when it has no key.</summary>
    public static string LeafLabel(IStringLocalizer localizer, string leafId, string english) =>
        TryLeafLabel(localizer, leafId, out string label) ? label : english;

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
    /// Translates a Purpose Note reply in place: its message by the note's state, and its disclaimer. The note itself
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
        if (key is not null)
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
        if (string.IsNullOrEmpty(leafId))
        {
            return false;
        }

        LocalizedString text = localizer[LeafKeyPrefix + leafId];
        label = text.Value;
        return !text.ResourceNotFound;
    }
}
