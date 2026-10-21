using PersonalFinance.Shared.DTOs.Search;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service for unified search across transactions, budgets, and savings goals.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches across transactions, budgets, and savings goals for the given query.
    /// </summary>
    Task<SearchResultsDto> SearchAsync(string userId, string query, CancellationToken cancellationToken = default);
}
