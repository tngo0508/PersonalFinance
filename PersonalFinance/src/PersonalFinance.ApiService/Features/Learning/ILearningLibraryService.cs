using PersonalFinance.Shared.DTOs.Learning;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// The public Library and Term Entry pages' content, as DTOs in one Language. Needs no Account.
/// </summary>
public interface ILearningLibraryService
{
    /// <summary>Every Topic in shelf order, each with its Term Entries by Term.</summary>
    LearningLibraryDto GetLibrary(string? language);

    /// <summary>The Term Entry with this slug, or <c>null</c>.</summary>
    TermEntryDto? FindTerm(string slug, string? language);

    /// <summary>The Journey's outline: every Lesson by day.</summary>
    List<LessonSummaryDto> GetLessons(string? language);

    /// <summary>The Lesson with this slug, with its Term Entries in teaching order, or <c>null</c>.</summary>
    LessonDto? FindLesson(string slug, string? language);
}
