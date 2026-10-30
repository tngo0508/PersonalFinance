namespace PersonalFinance.Shared.DTOs.Search;

/// <summary>
/// Global Search results for the signed-in Account, grouped by source.
/// </summary>
public class SearchResultsDto
{
    public List<SearchResultItemDto> Transactions { get; set; } = new();
    public List<SearchResultItemDto> Categories { get; set; } = new();
    public List<SearchResultItemDto> SavingsGoals { get; set; } = new();

    /// <summary>
    /// Published Term Entries of the Atelier (#522) whose Term, English Term, aliases or Vietnamese name match; shown
    /// after the user's own results. Public content, the same for every Account.
    /// </summary>
    public List<SearchResultItemDto> Learning { get; set; } = new();
}

/// <summary>
/// A single Global Search result. <see cref="Date"/> and <see cref="Amount"/> are set for transactions only.
/// </summary>
public class SearchResultItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public decimal? Amount { get; set; }
}
