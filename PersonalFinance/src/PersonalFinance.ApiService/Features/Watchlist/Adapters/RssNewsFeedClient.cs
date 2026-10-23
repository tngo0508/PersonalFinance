using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist.Adapters;

/// <summary>
/// <see cref="INewsFeedClient"/> over HTTP, registered as a typed <see cref="HttpClient"/>.
/// </summary>
internal sealed class RssNewsFeedClient : INewsFeedClient
{
    private const int ArticlesPerSource = 5;
    private const int MaxDescriptionLength = 250;

    // Pre-compiled regex for HTML stripping — avoids recompilation on every call
    private static readonly Regex HtmlStripRegex = new("<.*?>", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RssNewsFeedClient> _logger;

    public RssNewsFeedClient(HttpClient httpClient, TimeProvider timeProvider, ILogger<RssNewsFeedClient> logger)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<NewsArticleDto>> FetchAsync(NewsSourceDto source, CancellationToken cancellationToken)
    {
        try
        {
            return await FetchRssFeedAsync(source, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Timeout fetching feed from {SourceName} at {SourceUrl}", source.Name, source.Url);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Error fetching feed from {SourceName} at {SourceUrl}", source.Name, source.Url);
        }

        return [];
    }

    /// <summary>
    /// Downloads and parses an RSS 2.0 feed, extracting up to 5 articles per source.
    /// </summary>
    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Over the ADR 0007 complexity ceiling before it was set; follow-up: news feed parser split.")]
    private async Task<List<NewsArticleDto>> FetchRssFeedAsync(NewsSourceDto source, CancellationToken cancellationToken)
    {
        var articles = new List<NewsArticleDto>();

        using HttpResponseMessage response = await _httpClient.GetAsync(source.Url, cancellationToken);
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
            var items = doc.Descendants("item").Take(ArticlesPerSource).ToList();

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
                description = Truncate(StripHtml(description), MaxDescriptionLength);

                var article = new NewsArticleDto
                {
                    Title = title.Trim(),
                    Description = description.Trim(),
                    Url = link.Trim(),
                    Source = source.Name,
                    ImageUrl = imageUrl,
                    PublishedAt = DateTime.TryParse(pubDateStr, out DateTime parsedDate) ? parsedDate : _timeProvider.GetUtcNow().UtcDateTime,
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
