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

    /// <summary>The most of an item's text kept for the AI summary; about 1,500 tokens.</summary>
    private const int MaxFullTextLength = 6000;

    private static readonly XName MediaContent = XName.Get("content", "http://search.yahoo.com/mrss/");
    private static readonly XName ContentEncoded = XName.Get("encoded", "http://purl.org/rss/1.0/modules/content/");

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
    public async Task<List<NewsFeedItem>> FetchAsync(NewsSourceDto source, CancellationToken cancellationToken)
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
    /// Downloads the source's RSS 2.0 feed and maps its <see cref="ArticlesPerSource"/> newest items, whatever order
    /// the feed lists them in (a search feed lists by relevance).
    /// </summary>
    private async Task<List<NewsFeedItem>> FetchRssFeedAsync(NewsSourceDto source, CancellationToken cancellationToken)
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
            .Select(item => ToItem(item, source))
            .Where(item => item.Article.Title.Length > 0)
            .OrderByDescending(item => item.Article.PublishedAt)
            .Take(ArticlesPerSource)
            .ToList();
    }

    /// <summary>
    /// Maps one RSS <c>item</c> to an article, filed under its source's topic (see <see cref="NewsTopics"/>); the
    /// item's own <c>category</c> elements are ignored. An aggregator item (Google News) names its publisher in
    /// <c>source</c> and repeats it after the headline, so the suffix is dropped and the publisher kept apart; its
    /// description, which only repeats the headline, is dropped. An item with no headline left (an aggregator item
    /// whose title is only "- Publisher") has an empty <see cref="NewsArticleDto.Title"/> and is skipped by the caller.
    /// </summary>
    private NewsFeedItem ToItem(XElement item, NewsSourceDto source)
    {
        string? publisher = NullIfEmpty(item.Element("source")?.Value.Trim());
        string title = WithoutPublisherSuffix(ElementValue(item, "title").Trim(), publisher);
        string description = HtmlText.Strip(ElementValue(item, "description"));
        if (publisher is not null && description.StartsWith(title, StringComparison.Ordinal))
        {
            description = string.Empty;
        }

        string content = HtmlText.Strip(item.Element(ContentEncoded)?.Value ?? string.Empty);
        return new NewsFeedItem
        {
            Article = new NewsArticleDto
            {
                Title = title,
                Description = Truncate(description, MaxDescriptionLength).Trim(),
                Url = ElementValue(item, "link").Trim(),
                Source = source.Name,
                Publisher = publisher,
                ImageUrl = ImageUrl(item),
                PublishedAt = PublishedAt(ElementValue(item, "pubDate")),
                Category = source.Category,
            },
            FullText = HtmlText.Cap(content.Length > description.Length ? content : description, MaxFullTextLength),
        };
    }

    private static string ElementValue(XElement item, string name) => item.Element(name)?.Value ?? string.Empty;

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>"Headline - Publisher" without the publisher; the headline as given when it does not end that way.</summary>
    private static string WithoutPublisherSuffix(string title, string? publisher)
    {
        if (publisher is null)
        {
            return title;
        }

        string suffix = " - " + publisher;
        if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return title[..^suffix.Length].TrimEnd();
        }

        // A headline-less item: the title is the suffix alone, trimmed to "- Publisher".
        return title.Equals(suffix.TrimStart(), StringComparison.OrdinalIgnoreCase) ? string.Empty : title;
    }

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
    /// Truncates the input string to the specified maximum length, appending "..." if truncated.
    /// </summary>
    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
