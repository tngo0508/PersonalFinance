using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Settings of the Company Profile lookup rate limiter, registered in <see cref="ResearchFeature.AddResearchFeature"/>.
/// It keeps one Account from spending the SEC request budget every Account shares. Suggestions are not limited: they
/// are served from the cached ticker list and never reach SEC.
/// </summary>
internal static class CompanyLookupRateLimit
{
    /// <summary>
    /// DI key of the lookup rate limiter, kept separate from the shared <see cref="IRateLimiterService"/> used by other
    /// features so their limits are unchanged.
    /// </summary>
    public const string Key = "CompanyLookup";

    /// <summary>The most lookups an Account may make within <see cref="Window"/>.</summary>
    public const int MaxLookupsPerWindow = 30;

    /// <summary>The sliding window for <see cref="MaxLookupsPerWindow"/>.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
