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

    private static readonly XName MediaContent = XName.Get("content", "http://search.yahoo.com/mrss/");
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
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // A timeout, an unreachable host or a feed that is not RSS: skip this source, keep the others.
            _logger.LogWarning(ex, "Could not read the feed from {SourceName} at {SourceUrl}", source.Name, source.Url);
            return [];
        }
    }

    /// <summary>
    /// Downloads the source's RSS 2.0 feed and maps its first <see cref="ArticlesPerSource"/> items to articles.
    /// </summary>
    private async Task<List<NewsArticleDto>> FetchRssFeedAsync(NewsSourceDto source, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(source.Url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Feed {SourceName} returned HTTP {StatusCode}", source.Name, response.StatusCode);
            return [];
        }

        string xml = await response.Content.ReadAsStringAsync(cancellationToken);
        return XDocument.Parse(xml)
            .Descendants("item")
            .Take(ArticlesPerSource)
            .Select(item => ToArticle(item, source))
            .ToList();
    }

    /// <summary>
    /// Maps one RSS <c>item</c> to an article; the source's category stands in for a missing <c>category</c>.
    /// </summary>
    private NewsArticleDto ToArticle(XElement item, NewsSourceDto source) => new()
    {
        Title = ElementValue(item, "title").Trim(),
        Description = Truncate(StripHtml(ElementValue(item, "description")), MaxDescriptionLength).Trim(),
        Url = ElementValue(item, "link").Trim(),
        Source = source.Name,
        ImageUrl = ImageUrl(item),
        PublishedAt = PublishedAt(ElementValue(item, "pubDate")),
        Category = (item.Element("category")?.Value ?? source.Category).Trim(),
    };

    private static string ElementValue(XElement item, string name) => item.Element(name)?.Value ?? string.Empty;

    /// <summary>
    /// The image from <c>media:content</c>, or else from <c>enclosure</c>.
    /// </summary>
    private static string? ImageUrl(XElement item) =>
        item.Descendants().FirstOrDefault(e => e.Name == MediaContent)?.Attribute("url")?.Value
        ?? item.Element("enclosure")?.Attribute("url")?.Value;

    /// <summary>
    /// The item's <c>pubDate</c> in UTC, or the current time when it has none or it cannot be read.
    /// </summary>
    private DateTime PublishedAt(string pubDate) =>
        Rfc822Date.TryParseUtc(pubDate, out DateTime utc) ? utc : _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Strips HTML tags from the input string.
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
