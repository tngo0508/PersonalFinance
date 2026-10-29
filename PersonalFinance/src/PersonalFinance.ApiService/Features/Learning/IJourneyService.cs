using PersonalFinance.Shared.DTOs.Learning;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// An Account's Journey (GLOSSARY.md): which Lessons are finished, which one is open, today's Review cards and the
/// Dashboard preference. Every call is scoped to one Account. Check answers are never stored.
/// </summary>
public interface IJourneyService
{
    /// <summary>Every Published Lesson with its status for the Account, titles in <paramref name="language"/>.</summary>
    Task<JourneyStateDto> GetStateAsync(string userId, string? language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the Account answered the Check of the Lesson with <paramref name="lessonSlug"/>, and returns the
    /// Journey. A Lesson already finished is left as it is, so its Check can be retaken.
    /// </summary>
    /// <returns><c>null</c> when no Published Lesson has that slug.</returns>
    /// <exception cref="Infrastructure.ApiErrorException">The Lesson is locked (409).</exception>
    Task<JourneyStateDto?> FinishLessonAsync(string userId, string lessonSlug, FinishLessonRequest request,
        string? language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Today's Review: up to three cards from the finished Lessons' Term Entries, "Not yet" cards first, then the
    /// least recently marked. Empty on Day 1 and once the Journey has ended.
    /// </summary>
    Task<ReviewDto> GetReviewAsync(string userId, string? language, CancellationToken cancellationToken = default);

    /// <summary>Marks the card for <paramref name="termSlug"/> "Knew it" or "Not yet".</summary>
    /// <returns><c>false</c> when the Term is not taught in one of the Account's finished Lessons.</returns>
    Task<bool> MarkReviewCardAsync(string userId, string termSlug, MarkReviewCardRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Shows or hides the quiet Atelier card on the Dashboard, creating the preference row if needed.</summary>
    Task SetShowOnDashboardAsync(string userId, bool show, CancellationToken cancellationToken = default);
}
