using PersonalFinance.Shared.DTOs.Learning;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Atelier (GLOSSARY.md). The Library calls need no signed-in Account; the Journey calls
/// are scoped to the signed-in Account the user-id header names. Each call takes a Language code (<c>en</c>,
/// <c>vi</c>); an unknown one means English.
/// </summary>
public interface ILearningApi
{
    /// <summary>The Library: every Topic in shelf order, each with its Term Entries.</summary>
    [Get("/api/learning/library")]
    Task<LearningLibraryDto> GetLibraryAsync([Query] string language, CancellationToken cancellationToken = default);

    /// <summary>The Term Entry with this slug; an unknown slug gets 404.</summary>
    [Get("/api/learning/terms/{slug}")]
    Task<TermEntryDto> GetTermAsync(string slug, [Query] string language,
        CancellationToken cancellationToken = default);

    /// <summary>The Account's Journey: every Lesson's status and when the next one opens.</summary>
    [Get("/api/learning/journey")]
    Task<JourneyStateDto> GetJourneyAsync([Query] string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finishes the open Lesson with this slug and returns the Journey; a finished one is left as it is. An unknown
    /// slug gets 404 and a locked Lesson 409 with an <c>ApiErrorDto</c>.
    /// </summary>
    [Post("/api/learning/journey/lessons/{slug}/finish")]
    Task<JourneyStateDto> FinishLessonAsync(string slug, [Body] FinishLessonRequest request, [Query] string language,
        CancellationToken cancellationToken = default);

    /// <summary>Today's Review cards, up to three from the finished Lessons.</summary>
    [Get("/api/learning/journey/review")]
    Task<ReviewDto> GetReviewAsync([Query] string language, CancellationToken cancellationToken = default);

    /// <summary>Marks the card with this Term slug "Knew it" or "Not yet"; a Term not in a finished Lesson gets 404.</summary>
    [Post("/api/learning/journey/review/{slug}")]
    Task MarkReviewCardAsync(string slug, [Body] MarkReviewCardRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Shows or hides the quiet Atelier card on the Dashboard.</summary>
    [Put("/api/learning/journey/dashboard")]
    Task SetShowOnDashboardAsync([Body] ShowOnDashboardRequest request,
        CancellationToken cancellationToken = default);
}
