namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// Dependency-injection registrations of the Watchlist Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class WatchlistFeature
{
    /// <summary>The Account's Watchlist.</summary>
    public static IServiceCollection AddWatchlistFeature(this IServiceCollection services)
    {
        services.AddScoped<IWatchlistService, WatchlistService>();
        return services;
    }
}
