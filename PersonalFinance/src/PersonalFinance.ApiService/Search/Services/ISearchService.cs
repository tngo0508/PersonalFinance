namespace PersonalFinance.ApiService.Search.Services;

/// <summary>
/// DTO for a single search result.
/// </summary>
public class SearchResultDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// DTO for search results grouped by source.
/// </summary>
public class SearchResultsDto
{
    public List<SearchResultDto> Transactions { get; set; } = new();
    public List<SearchResultDto> BudgetItems { get; set; } = new();
    public List<SearchResultDto> SavingsGoals { get; set; } = new();
}

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