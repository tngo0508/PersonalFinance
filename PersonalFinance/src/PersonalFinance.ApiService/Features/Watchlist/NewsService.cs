using Microsoft.Extensions.Caching.Memory;
using PersonalFinance.ApiService.Features.Watchlist.Adapters;
using PersonalFinance.Shared.DTOs.News;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// Implementation of <see cref="INewsService"/> that aggregates finance news from RSS feeds. The fetched feed is kept
/// in <see cref="IMemoryCache"/> for five minutes, so every request in that window shares one fetch; an article page
/// read for a summary is kept for thirty minutes.
/// </summary>
internal sealed class NewsService : INewsService
{
    private const int MaxArticles = 50;
    private const string CacheKey = "Watchlist.NewsFeed";
    private const string PageCacheKeyPrefix = "Watchlist.ArticlePage:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PageCacheDuration = TimeSpan.FromMinutes(30);

    /// <summary>Text at least this long counts as the article body rather than an excerpt.</summary>
    private const int FullTextMinLength = 600;

    // The RSS sources, each filed under one of the NewsTopics; the topic filter follows this list. Google News and
    // Yahoo Finance offer these feeds free and without a key; Google News links redirect through news.google.com.
    private static readonly IReadOnlyList<NewsSourceDto> Sources =
    [
        new() { Name = "NPR Personal Finance", Url = "https://feeds.npr.org/510305/podcast.xml", Category = NewsTopics.Budgeting },
        new() { Name = "NerdWallet", Url = "https://www.nerdwallet.com/blog/feed/", Category = NewsTopics.Budgeting },
        new()
        {
            Name = "Google News: Personal finance",
            Url = "https://news.google.com/rss/search?q=(site:kiplinger.com+OR+site:bankrate.com+OR+site:money.com+OR+site:investopedia.com)+when:3d&hl=en-US&gl=US&ceid=US:en",
            Category = NewsTopics.Budgeting,
        },
        new() { Name = "MarketWatch", Url = "https://feeds.content.dowjones.io/public/rss/mw_topstories", Category = NewsTopics.Investing },
        new() { Name = "CNBC Investing", Url = "https://www.cnbc.com/id/15839069/device/rss/rss.html", Category = NewsTopics.Investing },
        new()
        {
            Name = "Yahoo Finance Markets",
            Url = "https://feeds.finance.yahoo.com/rss/2.0/headline?s=%5EGSPC,%5EDJI,%5EIXIC&region=US&lang=en-US",
            Category = NewsTopics.Investing,
        },
        new() { Name = "Retirement Researcher", Url = "https://retirementresearcher.com/feed/", Category = NewsTopics.Retirement },
        new()
        {
            Name = "Google News: Retirement",
            Url = "https://news.google.com/rss/search?q=%22retirement+savings%22+OR+%22retirement+planning%22+OR+401k+OR+%22Roth+IRA%22+OR+%22Social+Security%22+when:3d&hl=en-US&gl=US&ceid=US:en",
            Category = NewsTopics.Retirement,
        },
        new() { Name = "BBC Business", Url = "http://feeds.bbci.co.uk/news/business/rss.xml", Category = NewsTopics.General },
    ];

    // The topics some configured source covers, in source order.
    private static readonly IReadOnlyList<string> Topics = Sources.Select(s => s.Category).Distinct().ToList();

    private readonly INewsFeedClient _feedClient;
    private readonly IArticlePageClient _pageClient;
    private readonly IMemoryCache _cache;

    public NewsService(INewsFeedClient feedClient, IArticlePageClient pageClient, IMemoryCache cache)
    {
        _feedClient = feedClient;
        _pageClient = pageClient;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<NewsFeedDto> GetNewsAsync(NewsQueryDto query, CancellationToken cancellationToken = default)
    {
        List<NewsFeedItem> items = await GetCachedItemsAsync(cancellationToken);

        NewsFeedDto feed = FilterOptions();
        feed.Articles = FilterArticles(items.Select(i => i.Article), query);
        return feed;
    }

    /// <inheritdoc />
    public Task<NewsFeedDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(FilterOptions());

    /// <inheritdoc />
    public async Task<NewsArticleText?> GetArticleTextAsync(string url, CancellationToken cancellationToken = default)
    {
        List<NewsFeedItem> items = await GetCachedItemsAsync(cancellationToken);
        NewsFeedItem? item = items.FirstOrDefault(i => i.Article.Url == url);
        if (item is null)
        {
            return null;
        }

        string title = item.Article.Title;
        if (item.FullText.Length >= FullTextMinLength)
        {
            return new NewsArticleText(title, item.FullText, NewsSummaryCoverage.FullArticle);
        }

        if (!IsGoogleNewsLink(url))
        {
            string? page = await GetCachedPageTextAsync(url, cancellationToken);
            if (page is not null && page.Length >= FullTextMinLength)
            {
                return new NewsArticleText(title, page, NewsSummaryCoverage.FullArticle);
            }
        }

        return item.FullText.Length > 0
            ? new NewsArticleText(title, item.FullText, NewsSummaryCoverage.FeedExcerpt)
            : new NewsArticleText(title, title, NewsSummaryCoverage.HeadlineOnly);
    }

    /// <summary>A Google News link redirects through a script page, so there is no article there to read.</summary>
    private static bool IsGoogleNewsLink(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && uri.Host.EndsWith("news.google.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The filter options, with no articles: the configured sources and the topics they cover.
    /// </summary>
    private static NewsFeedDto FilterOptions() => new()
    {
        Sources = Sources.Select(s => new NewsSourceDto { Name = s.Name, Url = s.Url, Category = s.Category }).ToList(),
        Topics = Topics.ToList(),
    };

    /// <summary>
    /// Returns the cached feed, or fetches every source concurrently, keeps the newest items and caches them.
    /// </summary>
    private async Task<List<NewsFeedItem>> GetCachedItemsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out List<NewsFeedItem>? cached) && cached is not null)
        {
            return cached;
        }

        List<NewsFeedItem>[] results =
            await Task.WhenAll(Sources.Select(source => _feedClient.FetchAsync(source, cancellationToken)));

        // Sort by published date, newest first, and limit results
        List<NewsFeedItem> items = results
            .SelectMany(a => a)
            .OrderByDescending(a => a.Article.PublishedAt)
            .Take(MaxArticles)
            .ToList();

        _cache.Set(CacheKey, items, CacheDuration);
        return items;
    }

    /// <summary>
    /// The article page's text, read once per URL; a page that cannot be read is remembered as such for as long, so a
    /// blocked site is not asked again on every click.
    /// </summary>
    private async Task<string?> GetCachedPageTextAsync(string url, CancellationToken cancellationToken)
    {
        string key = PageCacheKeyPrefix + url;
        if (_cache.TryGetValue(key, out string? cached))
        {
            return string.IsNullOrEmpty(cached) ? null : cached;
        }

        string? text = await _pageClient.ReadTextAsync(url, cancellationToken);
        _cache.Set(key, text ?? string.Empty, PageCacheDuration);
        return text;
    }

    /// <summary>
    /// Filters the articles by category, source and free text, each ignoring case.
    /// </summary>
    private static List<NewsArticleDto> FilterArticles(IEnumerable<NewsArticleDto> articles, NewsQueryDto query)
    {
        IEnumerable<NewsArticleDto> filtered = articles;

        if (!string.IsNullOrEmpty(query.Category))
        {
            filtered = filtered.Where(a => a.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query.Source))
        {
            filtered = filtered.Where(a => a.Source.Equals(query.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            string search = query.Search;
            filtered = filtered.Where(a =>
                a.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Source.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }
}
