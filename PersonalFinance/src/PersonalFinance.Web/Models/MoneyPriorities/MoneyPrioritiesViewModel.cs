using PersonalFinance.Shared.DTOs.MoneyPriorities;

namespace PersonalFinance.Web.Models.MoneyPriorities;

/// <summary>
/// What the Money Priorities page needs on load: the Priority Tree before a submit, Earlier results and the latest
/// Walk's answers to pre-fill the form. The page's JavaScript reads it from a JSON island and does the rest.
/// </summary>
public class MoneyPrioritiesViewModel
{
    /// <summary>Mermaid text of the whole tree, nothing highlighted.</summary>
    public string WholeTreeDiagram { get; set; } = string.Empty;

    /// <summary>The Priority Tree's thresholds, which decide the follow-up fields the form shows.</summary>
    public PriorityTreeThresholdsDto Thresholds { get; set; } = new();

    /// <summary>The Account's 10 newest Priority Walks, newest first.</summary>
    public List<PriorityWalkSummaryDto> RecentWalks { get; set; } = [];

    /// <summary>The latest Walk's answers, or null when the Account has none yet.</summary>
    public PriorityWalkAnswersDto? LatestAnswers { get; set; }
}
