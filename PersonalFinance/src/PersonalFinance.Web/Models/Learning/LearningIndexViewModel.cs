using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Learning;

/// <summary>The Atelier page: the Journey's road and the Library's shelves, one per Topic.</summary>
public sealed class LearningIndexViewModel
{
    /// <summary>Every Topic in shelf order, each with its Term Entries.</summary>
    public required IReadOnlyList<LearningTopicDto> Topics { get; init; }

    /// <summary>The road's stops, by day; empty while no Lesson is Published.</summary>
    public IReadOnlyList<LearningRoadStop> Road { get; init; } = [];

    /// <summary>The reader is signed in, so the road shows their progress rather than a Start the Journey link.</summary>
    public bool IsSignedIn { get; init; }

    /// <summary>No Term Entry is Published yet: the page shows a quiet "being written" note instead of the shelves.</summary>
    public bool IsEmpty => Topics.All(t => t.Terms.Count == 0);

    /// <summary>The keepsakes of the finished Lessons, by day, for the desk.</summary>
    public IEnumerable<KeepsakeDto> Keepsakes =>
        Road.Where(s => s.Status == LessonStatus.Finished).Select(s => s.Keepsake);
}
