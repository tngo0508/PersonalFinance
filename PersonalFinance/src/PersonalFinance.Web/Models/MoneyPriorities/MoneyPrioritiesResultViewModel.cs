using PersonalFinance.Shared.DTOs.MoneyPriorities;

namespace PersonalFinance.Web.Models.MoneyPriorities;

/// <summary>
/// A Priority Walk's result as the page's JavaScript reads it: the Walk, with the "Your path" sentences and both
/// diagram texts Web words from its tree and path in the request's Language (#382).
/// </summary>
public class MoneyPrioritiesResultViewModel
{
    /// <summary>The Walk, its leaf labels and disclaimer already in the request's Language.</summary>
    public PriorityWalkResultDto Walk { get; set; } = new();

    /// <summary>The "Your path" sentences, in path order, ending with the suggested priority.</summary>
    public List<string> PathSentences { get; set; } = [];

    /// <summary>Mermaid text of the whole tree with the path highlighted (light palette).</summary>
    public string WholeTreeDiagram { get; set; } = string.Empty;

    /// <summary>Mermaid text of the path nodes only, in one column, highlighted (light palette).</summary>
    public string PathViewDiagram { get; set; } = string.Empty;
}
