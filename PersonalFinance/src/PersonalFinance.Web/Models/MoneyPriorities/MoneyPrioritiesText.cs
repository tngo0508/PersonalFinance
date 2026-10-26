using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.MoneyPriorities;

/// <summary>
/// Puts the ApiService's fixed Money Priorities wording into the request's Language before the page shows it. The
/// ApiService writes English; this swaps in the translation of what it recognizes (a leaf label by its leaf id, the
/// shared message constants) and passes anything else through as written, so a newer tree or message still shows.
/// </summary>
public static class MoneyPrioritiesText
{
    private const string LeafKeyPrefix = "MoneyPriorities.Leaf.";

    /// <summary>
    /// How to translate each fixed ApiService message this page knows, by its English text (the shared constants, so
    /// the two cannot drift apart). Amounts in a message go through the money formatter in the request culture.
    /// </summary>
    private static readonly Dictionary<string, Func<IStringLocalizer, string>> Messages = new(StringComparer.Ordinal)
    {
        [AiAssistantMessages.Disclaimer] = localizer => localizer["MoneyPriorities.Note.Disclaimer"],
        [AiAssistantMessages.PurposeNoteUnavailable] = localizer => localizer["MoneyPriorities.Note.Unavailable"],
        [AiAssistantMessages.PurposeNoteRefused] = localizer => localizer["MoneyPriorities.Note.Refused"],
        [MoneyPrioritiesMessages.PurposeNoteInvite] = localizer => localizer["MoneyPriorities.Note.Invite"],
        [MoneyPrioritiesMessages.EstimateDisclaimer] =
            localizer => localizer["MoneyPriorities.Result.EstimateDisclaimer"],
        [MoneyPrioritiesMessages.MonthlyAmountRequired] =
            localizer => localizer["MoneyPriorities.Validation.MonthlyAmountRequired"],
        [MoneyPrioritiesMessages.MonthlyAmountRange] =
            localizer => localizer.Named("MoneyPriorities.Validation.MonthlyAmountRange",
                AmountRange(MoneyPrioritiesLimits.MonthlyAmountMax)),
        [MoneyPrioritiesMessages.WholeCents] = localizer => localizer["MoneyPriorities.Validation.WholeCents"],
        [MoneyPrioritiesMessages.PurposeTooLong] = localizer => localizer["MoneyPriorities.Validation.PurposeTooLong"],
        [MoneyPrioritiesMessages.DebtAprRequired] =
            localizer => localizer["MoneyPriorities.Validation.DebtAprRequired"],
        [MoneyPrioritiesMessages.DebtAprRange] = localizer => localizer["MoneyPriorities.Validation.DebtAprRange"],
        [MoneyPrioritiesMessages.DebtBalanceRequired] =
            localizer => localizer["MoneyPriorities.Validation.DebtBalanceRequired"],
        [MoneyPrioritiesMessages.DebtBalanceRange] =
            localizer => localizer.Named("MoneyPriorities.Validation.DebtBalanceRange",
                AmountRange(MoneyPrioritiesLimits.DebtBalanceMax)),
        [MoneyPrioritiesMessages.EmergencyFundMonthsRequired] =
            localizer => localizer["MoneyPriorities.Validation.EmergencyFundMonthsRequired"],
        [MoneyPrioritiesMessages.EmergencyFundMonthsRange] =
            localizer => localizer["MoneyPriorities.Validation.EmergencyFundMonthsRange"],
        [MoneyPrioritiesMessages.MonthlyExpensesRequired] =
            localizer => localizer["MoneyPriorities.Validation.MonthlyExpensesRequired"],
        [MoneyPrioritiesMessages.MonthlyExpensesRange] =
            localizer => localizer.Named("MoneyPriorities.Validation.MonthlyExpensesRange",
                AmountRange(MoneyPrioritiesLimits.MonthlyExpensesMax)),
        [MoneyPrioritiesMessages.HasFullEmployerMatchRequired] =
            localizer => localizer["MoneyPriorities.Validation.HasFullEmployerMatchRequired"],
        [MoneyPrioritiesMessages.EmployerMatchPercentRequired] =
            localizer => localizer["MoneyPriorities.Validation.EmployerMatchPercentRequired"],
        [MoneyPrioritiesMessages.EmployerMatchPercentRange] =
            localizer => localizer["MoneyPriorities.Validation.EmployerMatchPercentRange"],
        [MoneyPrioritiesMessages.HorizonYearsRequired] =
            localizer => localizer["MoneyPriorities.Validation.HorizonYearsRequired"],
        [MoneyPrioritiesMessages.HorizonYearsRange] =
            localizer => localizer["MoneyPriorities.Validation.HorizonYearsRange"],
        [MoneyPrioritiesMessages.AgeBandRequired] =
            localizer => localizer["MoneyPriorities.Validation.AgeBandRequired"],
        [MoneyPrioritiesMessages.AtContributionLimitRequired] =
            localizer => localizer["MoneyPriorities.Validation.AtContributionLimitRequired"]
    };

    /// <summary>
    /// <paramref name="english"/> in the request's Language when it is a fixed message this page knows; otherwise as
    /// written (null stays null).
    /// </summary>
    public static string? Message(IStringLocalizer localizer, string? english)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return english is not null && Messages.TryGetValue(english, out Func<IStringLocalizer, string>? translate)
            ? translate(localizer)
            : english;
    }

    /// <summary>
    /// Translates a rejected submit in place: every message, in the list and under its field. Messages the page does
    /// not know stay as the ApiService wrote them.
    /// </summary>
    public static MoneyPrioritiesValidationErrorsDto Localize(IStringLocalizer localizer,
        MoneyPrioritiesValidationErrorsDto errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        errors.Errors = errors.Errors.Select(message => Message(localizer, message) ?? string.Empty).ToList();
        foreach (string field in errors.FieldErrors.Keys.ToList())
        {
            errors.FieldErrors[field] =
                errors.FieldErrors[field].Select(message => Message(localizer, message) ?? string.Empty).ToList();
        }

        return errors;
    }

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

        walk.Disclaimer = Message(localizer, walk.Disclaimer) ?? string.Empty;
        return walk;
    }

    /// <summary>
    /// Translates a Purpose Note reply in place: its state message and disclaimer. The note itself is AI-written and
    /// shown as written.
    /// </summary>
    public static PriorityWalkPurposeNoteDto Localize(IStringLocalizer localizer, PriorityWalkPurposeNoteDto note)
    {
        ArgumentNullException.ThrowIfNull(note);
        note.Message = Message(localizer, note.Message);
        note.Disclaimer = Message(localizer, note.Disclaimer) ?? string.Empty;
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

    /// <summary>The placeholders of an "over {min} and at most {max}" message, in whole dollars of the request culture.</summary>
    private static (string Name, object? Value)[] AmountRange(decimal max) =>
    [
        ("min", MoneyFormatter.FormatWhole(0m, SupportedCurrencies.Default.Code)),
        ("max", MoneyFormatter.FormatWhole(max, SupportedCurrencies.Default.Code))
    ];

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
