using PersonalFinance.Shared.DTOs.MoneyPriorities;

namespace PersonalFinance.Web.Models.MoneyPriorities;

/// <summary>
/// What the Money Priorities page needs on load: the Priority Tree before a submit, Earlier results and the latest
/// Walk's answers to pre-fill the form. The page's JavaScript reads it from a JSON island and does the rest.
/// </summary>
public class MoneyPrioritiesViewModel
{
    /// <summary>Mermaid text of the whole tree, nothing highlighted, in the request's Language.</summary>
    public string WholeTreeDiagram { get; set; } = string.Empty;

    /// <summary>The Priority Tree's thresholds, which decide the follow-up fields the form shows.</summary>
    public PriorityTreeThresholdsDto Thresholds { get; set; } = new();

    /// <summary>The Account's 10 newest Priority Walks, newest first.</summary>
    public List<PriorityWalkSummaryDto> RecentWalks { get; set; } = [];

    /// <summary>The latest Walk's answers, or null when the Account has none yet.</summary>
    public PriorityWalkAnswersDto? LatestAnswers { get; set; }

    /// <summary>
    /// The AI disclaimer shown under a Purpose Note, including a saved one the page shows without a request, in the
    /// request's Language.
    /// </summary>
    public string PurposeNoteDisclaimer { get; set; } = string.Empty;

    /// <summary>
    /// The message the page shows when the Purpose Note request fails before the server can answer, in the request's
    /// Language.
    /// </summary>
    public string PurposeNoteUnavailable { get; set; } = string.Empty;

    /// <summary>The ISO 4217 currency the form starts in: the latest Walk's, else the Reporting Currency (ADR 0016).</summary>
    public string CurrencyCode { get; set; } = "USD";
}
