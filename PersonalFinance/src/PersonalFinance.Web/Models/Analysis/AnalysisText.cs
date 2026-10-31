using System.Globalization;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Analysis;

/// <summary>
///     The Analysis page's own wording in the request's Language: Data Source names, status badges and month names
///     (spec §2.1, §2.2). The ApiService sends statuses and month names in English; the page translates the ones it
///     knows and formats month names from the month number in the request culture. Category names are the user's own
///     and are never translated.
/// </summary>
public static class AnalysisText
{
    /// <summary>The English statuses the ApiService sends, with their label.</summary>
    private static readonly Dictionary<string, Func<IStringLocalizer, string>> StatusLabels = new(StringComparer.Ordinal)
    {
        ["On Track"] = localizer => localizer["Analysis.Status.OnTrack"],
        ["Over Budget"] = localizer => localizer["Analysis.Status.OverBudget"],
        ["Under Budget"] = localizer => localizer["Analysis.Status.UnderBudget"],
        ["Within Budget"] = localizer => localizer["Analysis.Status.WithinBudget"],
        ["Near Limit"] = localizer => localizer["Analysis.Status.NearLimit"],
        ["Ahead of Schedule"] = localizer => localizer["Analysis.Status.AheadOfSchedule"],
        ["Behind Schedule"] = localizer => localizer["Analysis.Status.BehindSchedule"]
    };

    /// <summary>The label of a status the ApiService sent: translated when the page knows it, otherwise as sent.</summary>
    public static string Status(IStringLocalizer localizer, string status)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return StatusLabels.TryGetValue(status, out Func<IStringLocalizer, string>? label) ? label(localizer) : status;
    }

    /// <summary>The name the Data Source toggle shows for <paramref name="source"/>.</summary>
    public static string SourceName(IStringLocalizer localizer, BudgetDataSource source)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return source == BudgetDataSource.BudgetTracker
            ? localizer["Analysis.Source.BudgetTracker"]
            : localizer["Analysis.Source.GoogleSheet"];
    }

    /// <summary>A month's name in the request culture, e.g. "March"; empty for a number outside 1–12.</summary>
    public static string MonthName(int month) =>
        month is >= 1 and <= 12 ? CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month) : string.Empty;

    /// <summary>A month's short name in the request culture, e.g. "Mar"; empty for a number outside 1–12.</summary>
    public static string ShortMonthName(int month) =>
        month is >= 1 and <= 12 ? CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month) : string.Empty;

    /// <summary>A month and year in the request culture's year-month pattern, e.g. "March 2026".</summary>
    public static string MonthYear(int year, int month) =>
        new DateTime(year, month, 1).ToString("Y", CultureInfo.CurrentCulture);
}
