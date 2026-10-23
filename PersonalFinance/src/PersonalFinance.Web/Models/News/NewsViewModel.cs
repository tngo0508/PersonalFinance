using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.Web.Models.News;

/// <summary>
/// View model for the Finance News page, passed from <c>NewsController</c> to the view.
/// </summary>
public sealed class NewsViewModel
{
    /// <summary>
    /// Gets or sets the list of news articles to display, after filtering is applied.
    /// </summary>
    public List<NewsArticleDto> Articles { get; set; } = new();

    /// <summary>
    /// Gets or sets the available news sources for the source filter dropdown.
    /// </summary>
    public List<NewsSourceDto> Sources { get; set; } = new();

    /// <summary>
    /// Gets or sets the available categories derived from the fetched articles.
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Gets or sets the personal finance categories for filtering (Budgeting, Saving, Investing, Debt, Retirement).
    /// </summary>
    public List<string> PersonalFinanceCategories { get; set; } = new();

    /// <summary>
    /// Gets or sets the currently selected category filter value.
    /// </summary>
    public string? SelectedCategory { get; set; }

    /// <summary>
    /// Gets or sets the currently selected source filter value.
    /// </summary>
    public string? SelectedSource { get; set; }

    /// <summary>
    /// Gets or sets the free-text search query entered by the user.
    /// </summary>
    public string? SearchQuery { get; set; }

    /// <summary>
    /// Gets or sets the user-facing error message displayed when feeds fail to load.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the data is still loading.
    /// </summary>
    public bool IsLoading { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp of the last successful data refresh.
    /// </summary>
    public DateTime? LastRefreshed { get; set; }
}
