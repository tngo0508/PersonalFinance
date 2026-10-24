using PersonalFinance.ApiService.Features.Research.Adapters;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Dependency-injection registrations of the Research Feature (GLOSSARY.md), called from <c>Program.cs</c>
/// (ADR 0007).
/// </summary>
internal static class ResearchFeature
{
    // A Public Float frame is a few megabytes; the refresh job reads dozens of them one after another.
    private static readonly TimeSpan SecTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The Research datasets, their stored snapshots and the SEC EDGAR client that builds them.</summary>
    public static IServiceCollection AddResearchFeature(this IServiceCollection services, IConfiguration configuration)
    {
        var secSettings = new SecEdgarSettings
        {
            UserAgent = configuration.GetValue<string>($"{SecEdgarSettings.Section}:UserAgent") ?? string.Empty,
            MaxRequestsPerSecond = configuration.GetValue($"{SecEdgarSettings.Section}:MaxRequestsPerSecond", 5),
        };
        services.AddSingleton(secSettings);
        services.AddSingleton(new ResearchSettings
        {
            SectorsFirstYear = configuration.GetValue($"{ResearchSettings.Section}:SectorsFirstYear", 2011),
        });
        services.AddSingleton<SecRequestGate>();
        services.AddHttpClient<ISecEdgarClient, SecEdgarClient>(client => client.Timeout = SecTimeout);
        services.AddScoped<IResearchService, ResearchService>();
        return services;
    }
}
