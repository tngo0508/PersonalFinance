using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.DTOs.Search;

namespace PersonalFinance.ApiService.Features.Search;

/// <summary>
/// Service for unified search across transactions, Budget Categories, and Savings Goals.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches across transactions, Budget Categories, and Savings Goals for the given query.
    /// </summary>
    Task<SearchResultsDto> SearchAsync(string userId, string query, CancellationToken cancellationToken = default);
}
