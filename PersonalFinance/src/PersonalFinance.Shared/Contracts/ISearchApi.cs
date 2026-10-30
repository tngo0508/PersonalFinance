using PersonalFinance.Shared.DTOs.Search;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for unified search operations.
/// </summary>
public interface ISearchApi
{
    /// <summary>
    /// Searches across transactions, Budget Categories, and Savings Goals.
    /// </summary>
    [Get("/api/Search")]
    Task<SearchResultsDto> SearchAsync([Query] string q, [Query] string language,
        CancellationToken cancellationToken = default);
}
