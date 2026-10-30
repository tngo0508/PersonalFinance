using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Web.Models.Learning;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// What a signed-in learner's Lesson page shows (#519): the sheets for an open or finished Lesson, the opening for a
/// locked one, and what the finished note says about the next Lesson.
/// </summary>
public class LearningLessonViewModelTests
{
    private static readonly DateTime Midnight = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OpenLesson_FinishingDatesTheNextForTomorrow()
    {
        LearningLessonViewModel model = For("one", Journey(LessonStatus.Open, LessonStatus.Locked));

        Assert.Equal(LessonAccess.Open, model.Access);
        Assert.Equal(NextLessonNote.OpensTomorrow, model.NextNote);
        Assert.Equal("two", model.Next!.Slug);
    }

    [Fact]
    public void FinishedLesson_WithTheNextOpen_SaysItIsOpenToday()
    {
        LearningLessonViewModel model = For("one", Journey(LessonStatus.Finished, LessonStatus.Open));

        Assert.Equal(LessonAccess.Finished, model.Access);
        Assert.Equal(NextLessonNote.OpenToday, model.NextNote);
    }

    [Fact]
    public void FinishedLesson_WithTheNextFinished_SaysNothingMore()
    {
        LearningLessonViewModel model = For("one", Journey(LessonStatus.Finished, LessonStatus.Finished));

        Assert.Equal(NextLessonNote.None, model.NextNote);
    }

    [Fact]
    public void LastLesson_EndsTheJourney()
    {
        LearningLessonViewModel model = For("two", Journey(LessonStatus.Finished, LessonStatus.Open));

        Assert.Equal(NextLessonNote.JourneyEnd, model.NextNote);
        Assert.Null(model.Next);
    }

    [Fact]
    public void LockedLesson_WithoutADate_WaitsOnThePreviousLesson()
    {
        LearningLessonViewModel model = For("two", Journey(LessonStatus.Open, LessonStatus.Locked));

        Assert.Equal(LessonAccess.Locked, model.Access);
        Assert.False(model.OpensTomorrow);
        Assert.Equal("one", model.Previous!.Slug);
    }

    [Fact]
    public void LessonTheJourneyDoesNotList_IsLocked()
    {
        LearningLessonViewModel model = For("three", Journey(LessonStatus.Open, LessonStatus.Locked));

        Assert.Equal(LessonAccess.Locked, model.Access);
    }

    private static LearningLessonViewModel For(string slug, JourneyStateDto journey) =>
        LearningLessonViewModel.ForJourney(new LessonDto { Slug = slug }, journey);

    private static JourneyStateDto Journey(LessonStatus first, LessonStatus second) => new()
    {
        Lessons =
        [
            new LessonProgressDto { Slug = "one", Day = 1, Status = first },
            new LessonProgressDto
            {
                Slug = "two", Day = 2, Status = second,
                OpensAtUtc = second == LessonStatus.Locked && first == LessonStatus.Finished ? Midnight : null,
            },
        ],
    };
}
