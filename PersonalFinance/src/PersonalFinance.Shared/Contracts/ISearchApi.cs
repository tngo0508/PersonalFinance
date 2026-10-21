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

// =========================================================================
// DTOs
// =========================================================================

public class SearchResultsDto
{
    public List<SearchResultItemDto> Transactions { get; set; } = new();
    public List<SearchResultItemDto> BudgetItems { get; set; } = new();
    public List<SearchResultItemDto> SavingsGoals { get; set; } = new();
}

public class SearchResultItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
