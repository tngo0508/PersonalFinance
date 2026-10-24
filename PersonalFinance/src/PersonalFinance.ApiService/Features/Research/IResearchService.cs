using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The Research page's public market datasets: read from their stored snapshots, and rebuilt from their sources by
/// the Research refresh job.
/// </summary>
public interface IResearchService
{
    /// <summary>
    /// Gets the Sectors snapshot (the largest SEC filers by Public Float per year), or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<SectorsSnapshotDto>> GetSectorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ETFs snapshot (what a few well-known ETFs held each quarter, from Form N-PORT), or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<EtfsSnapshotDto>> GetEtfsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the World snapshot (each country's market cap and market cap as % of GDP per year, from the World Bank),
    /// or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<WorldSnapshotDto>> GetWorldAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds every dataset from its source, each on its own. A dataset's snapshot is replaced only when its build
    /// succeeds; a failed build is logged and leaves the previous snapshot in place.
    /// </summary>
    Task<ResearchRefreshSummary> RefreshAsync(CancellationToken cancellationToken = default);
}
