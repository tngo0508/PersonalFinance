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
    /// Gets the funds snapshot (what each Tracked Fund held each quarter, from Form N-PORT), or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<FundsSnapshotDto>> GetFundsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the fund overlap snapshot (every pair of Tracked Funds' overlap by weight, from each fund's latest
    /// Form N-PORT report), or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<FundOverlapSnapshotDto>> GetFundOverlapAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the fund returns snapshot (each Tracked Fund's monthly total returns, with per-window figures and
    /// correlations), or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<FundReturnsSnapshotDto>> GetFundReturnsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the macro snapshot (monthly CPI-U from BLS, and the effective federal funds rate and 3-month Treasury bill
    /// rate from the Fed's H.15 release), or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<MacroSnapshotDto>> GetMacroAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the fund facts snapshot (each Tracked Fund's net assets and expense ratio), or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<FundFactsSnapshotDto>> GetFundFactsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the fund exposure snapshot (each Tracked Fund's share of net assets by issuer country and currency), or
    /// "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<FundExposureSnapshotDto>> GetFundExposureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the fund industries snapshot (each Tracked Fund's share of net assets by SEC industry office and SIC
    /// industry, and the rest by issuer country, bond issuer type and cash), or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<FundIndustriesSnapshotDto>> GetFundIndustriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the World snapshot (each country's market cap and market cap as % of GDP per year, from the World Bank),
    /// or "never loaded".
    /// </summary>
    Task<ResearchSnapshotResult<WorldSnapshotDto>> GetWorldAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the search index of the Research page's search box (Tracked Funds, industry offices, industries and
    /// countries), built from the funds, Sectors and World snapshots; a part whose dataset has never loaded is empty.
    /// </summary>
    Task<ResearchSearchIndexDto> GetSearchIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets each stored dataset's SourceAsOf and BuiltAt without reading its payload; empty when nothing has loaded.
    /// </summary>
    Task<ResearchStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds every dataset built from one of <paramref name="sources"/>, each on its own; the others are skipped.
    /// A dataset's snapshot is replaced only when its build succeeds; a failed build is logged and leaves the previous
    /// snapshot in place.
    /// </summary>
    Task<ResearchRefreshSummary> RefreshAsync(ResearchSources sources = ResearchSources.All,
        CancellationToken cancellationToken = default);
}
