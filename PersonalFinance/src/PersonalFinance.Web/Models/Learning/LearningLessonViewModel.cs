using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Learning;

/// <summary>What the Lesson page may show the reader.</summary>
public enum LessonAccess
{
    /// <summary>A guest: the title and a sign-in prompt.</summary>
    SignIn,

    /// <summary>Not open yet: the title and when it opens.</summary>
    Locked,

    /// <summary>Today's Lesson: the sheets and the Check.</summary>
    Open,

    /// <summary>Finished: the sheets, the keepsake, and the Check to retake.</summary>
    Finished,
}

/// <summary>What the finished note says about the Lesson after this one.</summary>
public enum NextLessonNote
{
    /// <summary>Nothing: the next Lesson is finished already.</summary>
    None,

    /// <summary>The next Lesson opens at the next local midnight.</summary>
    OpensTomorrow,

    /// <summary>The next Lesson is open now.</summary>
    OpenToday,

    /// <summary>This was the last Lesson: the Journey ends here.</summary>
    JourneyEnd,
}

/// <summary>A Lesson page: stacked paper sheets, or the title with a sign-in prompt or its opening.</summary>
public sealed class LearningLessonViewModel
{
    /// <summary>The Lesson in the reader's Language.</summary>
    public required LessonDto Lesson { get; init; }

    /// <summary>What the page may show.</summary>
    public required LessonAccess Access { get; init; }

    /// <summary>A locked Lesson whose opening is dated: it opens at the next local midnight.</summary>
    public bool OpensTomorrow { get; init; }

    /// <summary>The Lesson before this one, which a locked Lesson waits on; null on Day 1.</summary>
    public LessonProgressDto? Previous { get; init; }

    /// <summary>The Lesson after this one; null on the last day.</summary>
    public LessonProgressDto? Next { get; init; }

    /// <summary>What the finished note says about <see cref="Next"/> once this Lesson's Check is answered.</summary>
    public NextLessonNote NextNote { get; init; }

    /// <summary>
    /// Today's Review cards (#520), shown on the first sheet of an open Lesson from Day 2 on; empty otherwise.
    /// </summary>
    public IReadOnlyList<ReviewCardDto> Review { get; init; } = [];

    /// <summary>Whether the page opens with the Review sheet.</summary>
    public bool ShowsReview => Review.Count > 0;

    /// <summary>The page as a guest sees it.</summary>
    public static LearningLessonViewModel ForGuest(LessonDto lesson) =>
        new() { Lesson = lesson, Access = LessonAccess.SignIn };

    /// <summary>
    /// The page as a signed-in Account sees it. A Lesson the Journey does not list is treated as locked. The finished
    /// note is worked out for after the Check: finishing an open Lesson dates the next one for tomorrow, while
    /// retaking a finished one leaves the next as it is. The <paramref name="review"/> cards are kept only for an open
    /// Lesson after Day 1.
    /// </summary>
    public static LearningLessonViewModel ForJourney(LessonDto lesson, JourneyStateDto journey,
        IReadOnlyList<ReviewCardDto>? review = null)
    {
        List<LessonProgressDto> road = journey.Lessons.OrderBy(l => l.Day).ToList();
        int index = road.FindIndex(l => l.Slug == lesson.Slug);
        LessonProgressDto? progress = index >= 0 ? road[index] : null;
        LessonProgressDto? previous = index > 0 ? road[index - 1] : null;
        LessonProgressDto? next = index >= 0 && index + 1 < road.Count ? road[index + 1] : null;
        LessonAccess access = progress?.Status switch
        {
            LessonStatus.Open => LessonAccess.Open,
            LessonStatus.Finished => LessonAccess.Finished,
            _ => LessonAccess.Locked,
        };

        NextLessonNote note = next switch
        {
            null => NextLessonNote.JourneyEnd,
            _ when access == LessonAccess.Open => NextLessonNote.OpensTomorrow,
            { Status: LessonStatus.Open } => NextLessonNote.OpenToday,
            { Status: LessonStatus.Locked, OpensAtUtc: not null } => NextLessonNote.OpensTomorrow,
            _ => NextLessonNote.None,
        };

        return new LearningLessonViewModel
        {
            Lesson = lesson,
            Access = access,
            OpensTomorrow = access == LessonAccess.Locked && progress?.OpensAtUtc is not null,
            Previous = previous,
            Next = next,
            NextNote = note,
            Review = ReviewIsDue(lesson, journey) ? review ?? [] : [],
        };
    }

    /// <summary>
    /// Whether the Lesson opens with a Review: it is the Account's open Lesson and not Day 1. The controller asks for the
    /// cards only then.
    /// </summary>
    public static bool ReviewIsDue(LessonDto lesson, JourneyStateDto journey) =>
        lesson.Day > 1 && journey.Lessons.Any(l => l.Slug == lesson.Slug && l.Status == LessonStatus.Open);
}
