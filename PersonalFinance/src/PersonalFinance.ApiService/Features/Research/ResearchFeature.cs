using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Dependency-injection registrations of the Research Feature (GLOSSARY.md), called from <c>Program.cs</c>
/// (ADR 0007).
/// </summary>
internal static class ResearchFeature
{
    // A Public Float frame is a few megabytes; the refresh job reads dozens of them one after another.
    private static readonly TimeSpan SecTimeout = TimeSpan.FromSeconds(60);

    // A World Bank indicator page holds every country for 50 years, a megabyte or two.
    private static readonly TimeSpan WorldBankTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The Research datasets, their stored snapshots and the SEC EDGAR and World Bank clients that build them.</summary>
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
            SubmissionsPerRefresh = configuration.GetValue($"{ResearchSettings.Section}:SubmissionsPerRefresh", 2_500),
            PublicFloatExclusions = configuration
                .GetSection($"{ResearchSettings.Section}:PublicFloatExclusions")
                .Get<List<FilerYear>>() ?? [],
            PublicFloatKeeps = configuration
                .GetSection($"{ResearchSettings.Section}:PublicFloatKeeps")
                .Get<List<FilerYear>>() ?? [],
            Etfs = configuration.GetSection($"{ResearchSettings.Section}:Etfs").Get<List<ResearchEtf>>() ?? [],
            EtfsFirstQuarter = configuration.GetValue($"{ResearchSettings.Section}:EtfsFirstQuarter",
                new DateOnly(2019, 10, 1)),
            WorldFirstYear = configuration.GetValue($"{ResearchSettings.Section}:WorldFirstYear", 1975),
        });
        services.AddSingleton<SecRequestGate>();
        services.AddHttpClient<ISecEdgarClient, SecEdgarClient>(client => client.Timeout = SecTimeout);
        services.AddHttpClient<IWorldBankClient, WorldBankClient>(client => client.Timeout = WorldBankTimeout);
        services.AddScoped<IResearchService, ResearchService>();
        return services;
    }
}
