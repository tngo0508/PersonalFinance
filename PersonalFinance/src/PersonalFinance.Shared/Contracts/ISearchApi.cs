using PersonalFinance.Shared.DTOs.Search;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for unified search operations.
/// </summary>
public interface ISearchApi
{
    /// <summary>
    /// Searches across transactions, budgets, and savings goals.
    /// </summary>
    [Get("/api/Search")]
    Task<SearchResultsDto> SearchAsync([Query] string q, CancellationToken cancellationToken = default);
}
