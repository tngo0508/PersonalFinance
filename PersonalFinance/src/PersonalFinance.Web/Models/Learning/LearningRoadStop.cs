using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Learning;

/// <summary>One stop on the Journey's road: a Lesson, with its status for a signed-in Account.</summary>
public sealed class LearningRoadStop
{
    /// <summary>The Lesson's slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Its day on the Journey.</summary>
    public required int Day { get; init; }

    /// <summary>Its title in the reader's Language.</summary>
    public required string Title { get; init; }

    /// <summary>The keepsake it places on the desk.</summary>
    public required KeepsakeDto Keepsake { get; init; }

    /// <summary>Locked, open or finished; null for a guest, who sees only the outline.</summary>
    public LessonStatus? Status { get; init; }

    /// <summary>A locked stop whose opening is dated: it opens at the next local midnight.</summary>
    public bool OpensTomorrow { get; init; }

    /// <summary>The road as a guest sees it: the outline, no statuses.</summary>
    public static IReadOnlyList<LearningRoadStop> Outline(IEnumerable<LessonSummaryDto> lessons) =>
        lessons.OrderBy(l => l.Day)
            .Select(l => new LearningRoadStop { Slug = l.Slug, Day = l.Day, Title = l.Title, Keepsake = l.Keepsake })
            .ToList();

    /// <summary>The road as a signed-in Account sees it.</summary>
    public static IReadOnlyList<LearningRoadStop> ForJourney(JourneyStateDto journey) =>
        journey.Lessons.OrderBy(l => l.Day)
            .Select(l => new LearningRoadStop
            {
                Slug = l.Slug,
                Day = l.Day,
                Title = l.Title,
                Keepsake = l.Keepsake,
                Status = l.Status,
                OpensTomorrow = l.Status == LessonStatus.Locked && l.OpensAtUtc is not null,
            })
            .ToList();
}
