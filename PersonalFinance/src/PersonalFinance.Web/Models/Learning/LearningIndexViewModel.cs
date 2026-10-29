using PersonalFinance.Shared.DTOs.Learning;

namespace PersonalFinance.Web.Models.Learning;

/// <summary>The Atelier page: the Library's shelves, one per Topic.</summary>
public sealed class LearningIndexViewModel
{
    /// <summary>Every Topic in shelf order, each with its Term Entries.</summary>
    public required IReadOnlyList<LearningTopicDto> Topics { get; init; }

    /// <summary>No Term Entry is Published yet: the page shows a quiet "being written" note instead of the shelves.</summary>
    public bool IsEmpty => Topics.All(t => t.Terms.Count == 0);
}
