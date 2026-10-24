using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// <see cref="IResearchService"/> over the stored snapshots (<see cref="ResearchSnapshotStore"/>) and one
/// <see cref="IResearchDatasetRefresher"/> per source pipeline: SEC Sectors, N-PORT ETFs and the World Bank.
/// </summary>
internal sealed class ResearchService : IResearchService
{
    private readonly ResearchSnapshotStore _store;
    private readonly IReadOnlyList<IResearchDatasetRefresher> _refreshers;
    private readonly ILogger<ResearchService> _logger;

    public ResearchService(ResearchSnapshotStore store, IEnumerable<IResearchDatasetRefresher> refreshers,
        ILogger<ResearchService> logger)
    {
        _store = store;
        _refreshers = refreshers.ToList();
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<SectorsSnapshotDto>> GetSectorsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SectorsSnapshotDto>(ResearchDatasets.Sectors, cancellationToken);

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<EtfsSnapshotDto>> GetEtfsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<EtfsSnapshotDto>(ResearchDatasets.Etfs, cancellationToken);

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<WorldSnapshotDto>> GetWorldAsync(CancellationToken cancellationToken = default) =>
        GetAsync<WorldSnapshotDto>(ResearchDatasets.World, cancellationToken);

    /// <inheritdoc />
    public async Task<ResearchRefreshSummary> RefreshAsync(ResearchSources sources = ResearchSources.All,
        CancellationToken cancellationToken = default)
    {
        var summary = new ResearchRefreshSummary();
        foreach (IResearchDatasetRefresher refresher in _refreshers.Where(r => r.IsConfigured))
        {
            if (sources.HasFlag(refresher.Source))
            {
                await RefreshDatasetAsync(refresher, summary, cancellationToken);
            }
            else
            {
                summary.Skipped.Add(refresher.Dataset);
            }
        }

        return summary;
    }

    private async Task<ResearchSnapshotResult<T>> GetAsync<T>(string dataset, CancellationToken cancellationToken)
        where T : class =>
        new(await _store.ReadAsync<T>(dataset, cancellationToken));

    /// <summary>
    /// Rebuilds one dataset, recording in the summary whether it was built or failed. A failure other than the
    /// refresh being cancelled is logged and leaves the previous snapshot in place.
    /// </summary>
    private async Task RefreshDatasetAsync(IResearchDatasetRefresher refresher, ResearchRefreshSummary summary,
        CancellationToken cancellationToken)
    {
        try
        {
            await refresher.RefreshAsync(cancellationToken);
            summary.Built.Add(refresher.Dataset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The source is down or answered something unreadable: keep the previous snapshot.
            _logger.LogError(ex, "Could not rebuild the Research {Dataset} snapshot; the previous one is kept",
                refresher.Dataset);
            summary.Failed.Add(refresher.Dataset);
        }
    }
}
