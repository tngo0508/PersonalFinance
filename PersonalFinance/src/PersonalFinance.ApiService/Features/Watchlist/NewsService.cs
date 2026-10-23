using Microsoft.Extensions.Caching.Memory;
using PersonalFinance.ApiService.Features.Watchlist.Adapters;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// Implementation of <see cref="INewsService"/> that aggregates finance news from RSS feeds. The fetched feed is kept
/// in <see cref="IMemoryCache"/> for five minutes, so every request in that window shares one fetch.
/// </summary>
internal sealed class NewsService : INewsService
{
    private const int MaxArticles = 50;
    private const string CacheKey = "Watchlist.NewsFeed";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    // Personal finance RSS sources — focused on budgeting, saving, investing, debt, retirement
    private static readonly IReadOnlyList<NewsSourceDto> Sources =
    [
        // Budgeting & Saving
        new() { Name = "NPR Personal Finance", Url = "https://feeds.npr.org/510305/podcast.xml", Category = "Budgeting" },
        new() { Name = "CNN Business", Url = "https://rss.cnn.com/rss/money_latest.rss", Category = "Budgeting" },
        // Investing
        new() { Name = "Investopedia", Url = "https://www.investopedia.com/news/rssFeedType.latest/rss", Category = "Investing" },
        new() { Name = "MarketWatch", Url = "https://feeds.content.dowjones.io/public/rss/mw_topstories", Category = "Investing" },
        // Retirement & Planning
        new() { Name = "Forbes Finance", Url = "https://feeds.feedburner.com/Forbes/finance-wires", Category = "Retirement" },
        // General Finance
        new() { Name = "BBC Business", Url = "http://feeds.bbci.co.uk/news/business/rss.xml", Category = "General" },
    ];

    private static readonly IReadOnlyList<string> PersonalFinanceCategories =
        ["Budgeting", "Saving", "Investing", "Debt", "Retirement"];

    private readonly INewsFeedClient _feedClient;
    private readonly IMemoryCache _cache;

    public NewsService(INewsFeedClient feedClient, IMemoryCache cache)
    {
        _feedClient = feedClient;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<NewsFeedDto> GetNewsAsync(NewsQueryDto query, CancellationToken cancellationToken = default)
    {
        List<NewsArticleDto> articles = await GetCachedArticlesAsync(cancellationToken);

        NewsFeedDto feed = FilterOptions(articles);
        feed.Articles = FilterArticles(articles, query);
        return feed;
    }

    /// <inheritdoc />
    public async Task<NewsFeedDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default) =>
        FilterOptions(await GetCachedArticlesAsync(cancellationToken));

    /// <summary>
    /// The filter options of the whole feed, with no articles.
    /// </summary>
    private static NewsFeedDto FilterOptions(List<NewsArticleDto> articles) => new()
    {
        Categories = articles.Select(a => a.Category).Distinct().Order(StringComparer.Ordinal).ToList(),
        Sources = Sources.Select(s => new NewsSourceDto { Name = s.Name, Url = s.Url, Category = s.Category }).ToList(),
        PersonalFinanceCategories = PersonalFinanceCategories.ToList(),
    };

    /// <summary>
    /// Returns the cached feed, or fetches every source concurrently, keeps the newest articles and caches them.
    /// </summary>
    private async Task<List<NewsArticleDto>> GetCachedArticlesAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out List<NewsArticleDto>? cached) && cached is not null)
        {
            return cached;
        }

        List<NewsArticleDto>[] results =
            await Task.WhenAll(Sources.Select(source => _feedClient.FetchAsync(source, cancellationToken)));

        // Sort by published date, newest first, and limit results
        List<NewsArticleDto> articles = results
            .SelectMany(a => a)
            .OrderByDescending(a => a.PublishedAt)
            .Take(MaxArticles)
            .ToList();

        _cache.Set(CacheKey, articles, CacheDuration);
        return articles;
    }

    /// <summary>
    /// Filters the articles by category, source and free text, each ignoring case.
    /// </summary>
    private static List<NewsArticleDto> FilterArticles(List<NewsArticleDto> articles, NewsQueryDto query)
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
