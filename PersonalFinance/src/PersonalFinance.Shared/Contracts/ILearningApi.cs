using PersonalFinance.Shared.DTOs.Learning;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Atelier's public content (GLOSSARY.md). Needs no signed-in Account. Each call takes a
/// Language code (<c>en</c>, <c>vi</c>); an unknown one means English.
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
}
