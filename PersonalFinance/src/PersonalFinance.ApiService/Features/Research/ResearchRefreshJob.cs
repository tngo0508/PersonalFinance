using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Data;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The <c>--refresh-research</c> mode (or <c>REFRESH_RESEARCH=true</c>): the ApiService image run by the daily
/// off-peak Container Apps Job (<c>infra/modules/research-refresh-job.bicep</c>). It applies pending migrations,
/// rebuilds every Research dataset from its public source and exits, without starting the web server. The ApiService
/// itself scales to zero, so it cannot be relied on to refresh every day; until the job has run once, the Research
/// page shows the Unavailable State.
/// </summary>
internal static class ResearchRefreshJob
{
    public const string Argument = "--refresh-research";

    // The committed appsettings.json carries a contact on this reserved domain; SEC needs a real one.
    private const string PlaceholderContactDomain = "@example.com";

    /// <summary>True when the process was started to refresh the Research datasets and exit.</summary>
    public static bool IsRequested(string[] args) =>
        args.Contains(Argument) ||
        string.Equals(Environment.GetEnvironmentVariable("REFRESH_RESEARCH"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Migrates, then refreshes. Returns the process exit code.</summary>
    public static async Task<int> RunAsync(IHost host, CancellationToken cancellationToken = default)
    {
        await host.MigrateAndSeedDatabaseAsync(cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return await RefreshAsync(scope.ServiceProvider, cancellationToken);
    }

    /// <summary>
    /// Rebuilds every dataset. When SEC's User-Agent contact is not configured or is still the committed placeholder,
    /// SEC's fair-access rules forbid asking it anything, so its datasets (Sectors, funds) are skipped and keep their
    /// snapshots while the World Bank's and the exchange rates are still refreshed. Non-zero, so the job execution shows as failed, when SEC
    /// was skipped or any dataset failed; a failed dataset keeps its previous snapshot.
    /// </summary>
    internal static async Task<int> RefreshAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ResearchRefreshJob));
        string userAgent = services.GetRequiredService<SecEdgarSettings>().UserAgent;
        bool secConfigured = !string.IsNullOrWhiteSpace(userAgent)
            && !userAgent.Contains(PlaceholderContactDomain, StringComparison.OrdinalIgnoreCase);
        if (!secConfigured)
        {
            logger.LogError("Research refresh skips the SEC datasets: {Setting} is not configured with a real contact.",
                $"{SecEdgarSettings.Section}:UserAgent");
        }

        ResearchRefreshSummary summary;
        try
        {
            summary = await services.GetRequiredService<IResearchService>().RefreshAsync(
                secConfigured ? ResearchSources.All : ResearchSources.WorldBank | ResearchSources.CurrencyRates,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Stopped before the end (e.g. the job's replicaTimeout): the stored snapshots are unchanged.
            logger.LogWarning("Research refresh stopped before it finished; the previous snapshots are kept.");
            return StartupExit.Failure;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Research refresh finished: built {Built}; failed {Failed}; skipped {Skipped}.",
                string.Join(", ", summary.Built), string.Join(", ", summary.Failed), string.Join(", ", summary.Skipped));
        }

        if (!secConfigured || summary.Failed.Count > 0)
        {
            return StartupExit.Failure;
        }

        logger.LogInformation(StartupExit.JobSucceededMessage);
        return StartupExit.Success;
    }
}
