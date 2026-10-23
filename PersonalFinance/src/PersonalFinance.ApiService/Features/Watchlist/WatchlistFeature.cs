using PersonalFinance.ApiService.Features.Watchlist.Adapters;

namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// Dependency-injection registrations of the Watchlist Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class WatchlistFeature
{
    private static readonly TimeSpan NewsFeedTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The Account's Watchlist, and the news feed its articles are found in.</summary>
    public static IServiceCollection AddWatchlistFeature(this IServiceCollection services)
    {
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddMemoryCache();
        services.AddScoped<INewsService, NewsService>();
        services.AddHttpClient<INewsFeedClient, RssNewsFeedClient>(client => client.Timeout = NewsFeedTimeout);
        return services;
    }
}
