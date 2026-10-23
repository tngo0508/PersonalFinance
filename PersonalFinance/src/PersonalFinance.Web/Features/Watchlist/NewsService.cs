using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PersonalFinance.Web.Features.Watchlist;

/// <summary>
/// Represents a single news article from an external RSS/JSON source.
/// </summary>
public sealed class NewsArticle
{
    /// <summary>Gets or sets the article title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the article description with HTML stripped, truncated to 250 characters.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the URL to the full article on the source site.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the news source (e.g., "CNN Business").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional image URL for the article thumbnail.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Gets or sets the published date and time of the article.</summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>Gets or sets the article category (e.g., "General", "Markets", "Finance").</summary>
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Represents a configured news source with its RSS/JSON feed URL.
/// </summary>
public sealed class NewsSource
{
    /// <summary>Gets or sets the display name of the source.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the URL of the RSS/JSON feed.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the default category for articles from this source.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the feed type: "rss" or "json". Defaults to "rss".</summary>
    public string Type { get; set; } = "rss";
}

/// <summary>
/// Aggregates finance news from multiple free RSS/JSON sources.
/// Fetches RSS feeds concurrently and returns a combined, sorted list of articles.
/// </summary>
public interface INewsService
{
    /// <summary>
    /// Retrieves finance news articles, optionally filtered by the specified criteria.
    /// Results are cached for a short duration to improve repeated query performance.
    /// </summary>
    /// <param name="options">Optional filter criteria for category, source, and search query.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of news articles sorted by publish date, newest first.</returns>
    Task<List<NewsArticle>> GetFinanceNewsAsync(
        NewsQueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves finance news articles filtered by a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of news articles in the specified category.</returns>
    Task<List<NewsArticle>> GetFinanceNewsByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a defensive copy of the configured news sources.
    /// </summary>
    List<NewsSource> GetAvailableSources();

    /// <summary>
    /// Returns the list of personal finance categories for filtering.
    /// </summary>
    List<string> GetPersonalFinanceCategories();
}

/// <summary>
/// Implementation of <see cref="INewsService"/> that aggregates finance news from RSS feeds.
/// Uses in-memory caching with a 5-minute TTL to reduce network I/O.
/// </summary>
public sealed class NewsService : INewsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NewsService> _logger;
    private readonly List<NewsSource> _sources;

    // In-memory cache for RSS feed results — protected by _cacheLock
    private List<NewsArticle>? _cachedArticles;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private readonly object _cacheLock = new();

    // Personal finance categories for filtering
    private readonly List<string> _personalFinanceCategories;

    // Pre-compiled regex for HTML stripping — avoids recompilation on every call
    private static readonly Regex HtmlStripRegex = new("<.*?>", RegexOptions.Compiled);

    public NewsService(HttpClient httpClient, ILogger<NewsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(5);

        // Configure personal finance RSS sources — focused on budgeting, saving, investing, debt, retirement
        _sources = new List<NewsSource>
        {
            // Budgeting & Saving
            new() { Name = "NPR Personal Finance", Url = "https://feeds.npr.org/510305/podcast.xml", Category = "Budgeting", Type = "rss" },
            new() { Name = "CNN Business", Url = "https://rss.cnn.com/rss/money_latest.rss", Category = "Budgeting", Type = "rss" },
            // Investing
            new() { Name = "Investopedia", Url = "https://www.investopedia.com/news/rssFeedType.latest/rss", Category = "Investing", Type = "rss" },
            new() { Name = "MarketWatch", Url = "https://feeds.content.dowjones.io/public/rss/mw_topstories", Category = "Investing", Type = "rss" },
            // Retirement & Planning
            new() { Name = "Forbes Finance", Url = "https://feeds.feedburner.com/Forbes/finance-wires", Category = "Retirement", Type = "rss" },
            // General Finance
            new() { Name = "BBC Business", Url = "http://feeds.bbci.co.uk/news/business/rss.xml", Category = "General", Type = "rss" },
        };

        // Personal finance categories for filtering
        _personalFinanceCategories = new List<string>
        {
            "Budgeting", "Saving", "Investing", "Debt", "Retirement"
        };
    }

    /// <inheritdoc />
    public async Task<List<NewsArticle>> GetFinanceNewsAsync(
        NewsQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Return cached articles if still valid — avoids blocking on network I/O
        List<NewsArticle>? cached = null;
        lock (_cacheLock)
        {
            if (_cachedArticles != null && DateTime.UtcNow < _cacheExpiry)
            {
                cached = _cachedArticles;
            }
        }

        // If cache hit, use cached articles; otherwise fetch fresh
        List<NewsArticle> articles = cached ?? await FetchAndCacheAsync(cancellationToken);

        // Apply filters if specified
        return options is not null && options.HasFilters
            ? FilterArticles(articles, options)
            : articles;
    }

    /// <inheritdoc />
    public async Task<List<NewsArticle>> GetFinanceNewsByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        List<NewsArticle> allArticles = await GetFinanceNewsAsync(cancellationToken: cancellationToken);
        return allArticles
            .Where(a => a.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc />
    public List<NewsSource> GetAvailableSources()
    {
        return new List<NewsSource>(_sources);
    }

    /// <inheritdoc />
    public List<string> GetPersonalFinanceCategories()
    {
        return new List<string>(_personalFinanceCategories);
    }

    /// <summary>
    /// Fetches fresh articles from all sources and updates the in-memory cache.
    /// </summary>
    private async Task<List<NewsArticle>> FetchAndCacheAsync(CancellationToken cancellationToken)
    {
        // Fetch fresh data from all sources concurrently
        List<NewsArticle> allArticles = await FetchAllSourcesAsync(cancellationToken);

        // Sort by published date, newest first, and limit results
        var sortedArticles = allArticles
            .OrderByDescending(a => a.PublishedAt)
            .Take(50)
            .ToList();

        // Update cache
        lock (_cacheLock)
        {
            _cachedArticles = sortedArticles;
            _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
        }

        return sortedArticles;
    }

    /// <summary>
    /// Filters a list of articles by the provided query options.
    /// </summary>
    private static List<NewsArticle> FilterArticles(
        List<NewsArticle> articles,
        NewsQueryOptions options)
    {
        IEnumerable<NewsArticle> filtered = articles.AsEnumerable();

        if (!string.IsNullOrEmpty(options.Category))
        {
            filtered = filtered.Where(a =>
                a.Category.Equals(options.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(options.Source))
        {
            filtered = filtered.Where(a =>
                a.Source.Equals(options.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(options.Search))
        {
            string query = options.Search;
            filtered = filtered.Where(a =>
                a.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Source.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }

    /// <summary>
    /// Fetches all RSS sources concurrently. Each source is fetched independently,
    /// so a slow or failing feed does not block the others.
    /// </summary>
    private async Task<List<NewsArticle>> FetchAllSourcesAsync(CancellationToken cancellationToken)
    {
        var allArticles = new List<NewsArticle>();
        IEnumerable<Task<List<NewsArticle>>> tasks = _sources.Select(source => FetchSourceAsync(source, cancellationToken));

        List<NewsArticle>[] results = await Task.WhenAll(tasks);

        foreach (List<NewsArticle>? articles in results)
        {
            allArticles.AddRange(articles);
        }

        return allArticles;
    }

    /// <summary>
    /// Fetches articles from a single news source. Swallows exceptions and logs warnings
    /// so that one failing source does not prevent others from being fetched.
    /// </summary>
    private async Task<List<NewsArticle>> FetchSourceAsync(NewsSource source, CancellationToken cancellationToken)
    {
        var articles = new List<NewsArticle>();

        try
        {
            if (source.Type == "rss")
            {
                articles = await FetchRssFeedAsync(source, cancellationToken);
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout fetching feed from {SourceName} at {SourceUrl}", source.Name, source.Url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching feed from {SourceName} at {SourceUrl}", source.Name, source.Url);
        }

        return articles;
    }

    /// <summary>
    /// Downloads and parses an RSS 2.0 feed, extracting up to 5 articles per source.
    /// </summary>
    private async Task<List<NewsArticle>> FetchRssFeedAsync(NewsSource source, CancellationToken cancellationToken)
    {
        var articles = new List<NewsArticle>();

        HttpResponseMessage response = await _httpClient.GetAsync(source.Url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Feed {SourceName} returned HTTP {StatusCode}", source.Name, response.StatusCode);
            return articles;
        }

        string xml = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var doc = XDocument.Parse(xml);

            // Handle standard RSS 2.0 format — reduced to 5 items per source for faster page loads
            var items = doc.Descendants("item").Take(5).ToList();

            foreach (XElement? item in items)
            {
                string title = item.Element("title")?.Value ?? string.Empty;
                string link = item.Element("link")?.Value ?? string.Empty;
                string description = item.Element("description")?.Value ?? string.Empty;
                string pubDateStr = item.Element("pubDate")?.Value ?? string.Empty;
                string category = item.Element("category")?.Value ?? source.Category;

                // Try to extract image from media:content or enclosure
                string? imageUrl = item.Descendants().FirstOrDefault(e => e.Name.LocalName == "content" && e.Name.Namespace?.NamespaceName == "http://search.yahoo.com/mrss/")?.Attribute("url")?.Value
                              ?? item.Element("enclosure")?.Attribute("url")?.Value;

                // Clean up description (strip HTML tags)
                description = Truncate(StripHtml(description), 250);

                var article = new NewsArticle
                {
                    Title = title.Trim(),
                    Description = description.Trim(),
                    Url = link.Trim(),
                    Source = source.Name,
                    ImageUrl = imageUrl,
                    PublishedAt = DateTime.TryParse(pubDateStr, out DateTime parsedDate) ? parsedDate : DateTime.UtcNow,
                    Category = category.Trim()
                };

                articles.Add(article);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse RSS feed from {SourceName}", source.Name);
        }

        return articles;
    }

    /// <summary>
    /// Strips HTML tags from the input string using a pre-compiled regex.
    /// </summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return HtmlStripRegex.Replace(html, string.Empty);
    }

    /// <summary>
    /// Truncates the input string to the specified maximum length, appending "..." if truncated.
    /// </summary>
    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
