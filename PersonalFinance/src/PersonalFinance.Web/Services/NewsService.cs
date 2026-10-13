using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;

namespace PersonalFinance.Web.Services;

/// <summary>
/// Represents a single news article from an external source.
/// </summary>
public class NewsArticle
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime PublishedAt { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Represents a news source configuration.
/// </summary>
public class NewsSource
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = "rss"; // "rss" or "json"
}

/// <summary>
/// Aggregates finance news from multiple free RSS/JSON sources.
/// Fetches RSS feeds concurrently and returns a combined, sorted list of articles.
/// </summary>
public interface INewsService
{
    Task<List<NewsArticle>> GetFinanceNewsAsync(CancellationToken cancellationToken = default);
    Task<List<NewsArticle>> GetFinanceNewsByCategoryAsync(string category, CancellationToken cancellationToken = default);
    List<NewsSource> GetAvailableSources();
}

public class NewsService : INewsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NewsService> _logger;
    private readonly List<NewsSource> _sources;

    public NewsService(HttpClient httpClient, ILogger<NewsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        // Configure free finance news RSS sources
        _sources = new List<NewsSource>
        {
            new() { Name = "CNN Business", Url = "https://rss.cnn.com/rss/money_latest.rss", Category = "General", Type = "rss" },
            new() { Name = "MarketWatch", Url = "https://feeds.content.dowjones.io/public/rss/mw_topstories", Category = "Markets", Type = "rss" },
            new() { Name = "NPR Business", Url = "https://feeds.npr.org/1001/business/rss", Category = "General", Type = "rss" },
            new() { Name = "BBC Business", Url = "http://feeds.bbci.co.uk/news/business/rss.xml", Category = "General", Type = "rss" },
            new() { Name = "Investopedia", Url = "https://www.investopedia.com/news/rssFeedType.latest/rss", Category = "Education", Type = "rss" },
            new() { Name = "Forbes Finance", Url = "https://feeds.feedburner.com/Forbes/finance-wires", Category = "Finance", Type = "rss" },
            new() { Name = "CNBC Top News", Url = "https://search.cnbc.com/rs/search/combinedcms/view.xml?partnerId=wrss01&id=100003114", Category = "Markets", Type = "rss" },
            new() { Name = "Reuters Business", Url = "https://www.rssmix.com/rss?q=topic%3Abusiness", Category = "General", Type = "rss" }
        };
    }

    public async Task<List<NewsArticle>> GetFinanceNewsAsync(CancellationToken cancellationToken = default)
    {
        var allArticles = new List<NewsArticle>();
        var tasks = _sources.Select(source => FetchSourceAsync(source, cancellationToken));

        var results = await Task.WhenAll(tasks);

        foreach (var articles in results)
        {
            allArticles.AddRange(articles);
        }

        // Sort by published date, newest first
        return allArticles
            .OrderByDescending(a => a.PublishedAt)
            .Take(50)
            .ToList();
    }

    public async Task<List<NewsArticle>> GetFinanceNewsByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var allArticles = await GetFinanceNewsAsync(cancellationToken);
        return allArticles
            .Where(a => a.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public List<NewsSource> GetAvailableSources()
    {
        return new List<NewsSource>(_sources);
    }

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

    private async Task<List<NewsArticle>> FetchRssFeedAsync(NewsSource source, CancellationToken cancellationToken)
    {
        var articles = new List<NewsArticle>();

        var response = await _httpClient.GetAsync(source.Url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Feed {SourceName} returned HTTP {StatusCode}", source.Name, response.StatusCode);
            return articles;
        }

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var doc = XDocument.Parse(xml);

            // Handle standard RSS 2.0 format
            var items = doc.Descendants("item").ToList();

            foreach (var item in items.Take(10))
            {
                var title = item.Element("title")?.Value ?? string.Empty;
                var link = item.Element("link")?.Value ?? string.Empty;
                var description = item.Element("description")?.Value ?? string.Empty;
                var pubDateStr = item.Element("pubDate")?.Value ?? string.Empty;
                var category = item.Element("category")?.Value ?? source.Category;

                // Try to extract image from media:content or enclosure
                var imageUrl = item.Descendants().FirstOrDefault(e => e.Name.LocalName == "content" && e.Name.Namespace?.NamespaceName == "http://search.yahoo.com/mrss/")?.Attribute("url")?.Value
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
                    PublishedAt = DateTime.TryParse(pubDateStr, out var parsedDate) ? parsedDate : DateTime.UtcNow,
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

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
