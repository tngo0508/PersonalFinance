using PersonalFinance.ApiService.Features.Watchlist.Adapters;

namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// Dependency-injection registrations of the Watchlist Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class WatchlistFeature
{
    private static readonly TimeSpan NewsFeedTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ArticlePageTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The Account's Watchlist, and the news feed its articles are found in.</summary>
    public static IServiceCollection AddWatchlistFeature(this IServiceCollection services)
    {
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddMemoryCache();
        services.AddScoped<INewsService, NewsService>();
        services.AddHttpClient<INewsFeedClient, RssNewsFeedClient>(client => client.Timeout = NewsFeedTimeout);
        services.AddHttpClient<IArticlePageClient, ArticlePageClient>(client =>
        {
            client.Timeout = ArticlePageTimeout;
            // Publishers answer a bare .NET client with 403; a named browser-style agent is read like a feed reader.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PersonalFinanceNews/1.0)");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });
        return services;
    }
}
