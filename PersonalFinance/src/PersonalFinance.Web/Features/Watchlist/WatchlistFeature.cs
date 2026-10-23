namespace PersonalFinance.Web.Features.Watchlist;

/// <summary>
/// Dependency-injection registrations of the Watchlist Feature's Web services, called from <c>Program.cs</c>
/// (ADR 0007). Its Refit client is registered with the others in Infrastructure.
/// </summary>
internal static class WatchlistFeature
{
    private static readonly TimeSpan NewsTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The news feed that articles are found in and saved from.</summary>
    public static IServiceCollection AddWatchlistFeature(this IServiceCollection services)
    {
        services.AddHttpClient<INewsService, NewsService>(client => client.Timeout = NewsTimeout);
        return services;
    }
}
