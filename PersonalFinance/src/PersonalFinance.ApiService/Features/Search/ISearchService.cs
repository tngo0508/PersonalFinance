using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Search;

namespace PersonalFinance.ApiService.Features.Search;

/// <summary>
/// Service for unified search across transactions, Budget Categories, and Savings Goals.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches across transactions, Budget Categories, and Savings Goals for the given query, then the Atelier's
    /// Term Entries, named in <paramref name="language"/> (English when missing or unknown).
    /// </summary>
    Task<SearchResultsDto> SearchAsync(string userId, string query, string? language = null,
        CancellationToken cancellationToken = default);
}
