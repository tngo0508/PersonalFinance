using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// <see cref="IResearchService"/> over the stored snapshots (<see cref="ResearchSnapshotStore"/>) and one
/// <see cref="IResearchDatasetRefresher"/> per source pipeline: SEC Sectors, N-PORT funds and the World Bank.
/// </summary>
internal sealed class ResearchService : IResearchService
{
    private readonly ResearchSnapshotStore _store;
    private readonly IReadOnlyList<IResearchDatasetRefresher> _refreshers;
    private readonly ResearchSettings _settings;
    private readonly ILogger<ResearchService> _logger;

    public ResearchService(ResearchSnapshotStore store, IEnumerable<IResearchDatasetRefresher> refreshers,
        ResearchSettings settings, ILogger<ResearchService> logger)
    {
        _store = store;
        _refreshers = refreshers.ToList();
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<SectorsSnapshotDto>> GetSectorsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SectorsSnapshotDto>(ResearchDatasets.Sectors, cancellationToken);

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<FundsSnapshotDto>> GetFundsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<FundsSnapshotDto>(ResearchDatasets.Funds, cancellationToken);

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<FundOverlapSnapshotDto>> GetFundOverlapAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<FundOverlapSnapshotDto>(ResearchDatasets.FundOverlap, cancellationToken);

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<FundReturnsSnapshotDto>> GetFundReturnsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<FundReturnsSnapshotDto>(ResearchDatasets.FundReturns, cancellationToken);

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<FundFactsSnapshotDto>> GetFundFactsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<FundFactsSnapshotDto>(ResearchDatasets.FundFacts, cancellationToken);

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<FundExposureSnapshotDto>> GetFundExposureAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<FundExposureSnapshotDto>(ResearchDatasets.FundExposure, cancellationToken);

    /// <inheritdoc />
    public Task<ResearchSnapshotResult<FundIndustriesSnapshotDto>> GetFundIndustriesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<FundIndustriesSnapshotDto>(ResearchDatasets.FundIndustries, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// The recessions come from configuration as the snapshot is read, not from the refresh, so a new entry shows at
    /// once. An entry whose months cannot be read is left out.
    /// </remarks>
    public async Task<ResearchSnapshotResult<MacroSnapshotDto>> GetMacroAsync(CancellationToken cancellationToken = default)
    {
        ResearchSnapshotResult<MacroSnapshotDto> result = await GetAsync<MacroSnapshotDto>(ResearchDatasets.Macro, cancellationToken);
        if (result.Snapshot is { } snapshot)
        {
            snapshot.Recessions = _settings.Recessions
                .Select(r => (r.Label, Start: ParseMonth(r.Start), End: ParseMonth(r.End)))
                .Where(r => r.Start is not null && r.End is not null && r.Start <= r.End && !string.IsNullOrWhiteSpace(r.Label))
                .OrderBy(r => r.Start)
                .Select(r => new MacroPeriodDto { Start = r.Start!.Value, End = r.End!.Value, Label = r.Label.Trim() })
                .ToList();
        }

        return result;
    }

    /// <summary>A <c>yyyy-MM</c> month as its first day, or null.</summary>
    private static DateOnly? ParseMonth(string value) =>
        DateOnly.TryParseExact($"{value.Trim()}-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out DateOnly month)
            ? month
            : null;

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
