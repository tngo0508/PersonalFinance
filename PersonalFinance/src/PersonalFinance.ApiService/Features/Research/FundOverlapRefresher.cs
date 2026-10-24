using System.Globalization;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the fund overlap dataset from each Tracked Fund's latest Form N-PORT report. Securities are matched by
/// their identifiers in memory only, so the overlap cannot be kept per quarter from stored data: it is recomputed from
/// the latest reports, and only when some fund has filed a newer report than the stored overlap was built from.
/// </summary>
internal sealed class FundOverlapRefresher : IResearchDatasetRefresher
{
    private const string DatasetName = "overlap";

    private readonly ResearchSnapshotStore _store;
    private readonly LatestFundReports _latest;
    private readonly ResearchSettings _settings;

    public FundOverlapRefresher(ResearchSnapshotStore store, LatestFundReports latest, ResearchSettings settings)
    {
        _store = store;
        _latest = latest;
        _settings = settings;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.FundOverlap;

    /// <inheritdoc />
    public ResearchSources Source => ResearchSources.SecEdgar;

    /// <inheritdoc />
    public bool IsConfigured => _settings.Funds.Count >= 2;

    /// <summary>
    /// Lists each fund's recent N-PORT filings. When every fund's newest filing is the one the stored overlap was
    /// built from, there is nothing new and the stored overlap is kept without downloading anything. Otherwise it
    /// reads each fund's latest report (its newest filing that is its own and readable), builds the overlap of every
    /// pair and stores it. A fund whose filings cannot be listed or read is left out and logged.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ListedFund> listed = await _latest.ListAsync(DatasetName, cancellationToken);
        FundOverlapSnapshotDto? stored = await _store.ReadAsync<FundOverlapSnapshotDto>(ResearchDatasets.FundOverlap,
            cancellationToken);
        if (stored is not null && LatestFundReports.UpToDate(listed, stored.Funds.Select(f => (f.Ticker, f.Accession)).ToList()))
        {
            return;
        }

        var reports = new List<LatestFundReport>();
        foreach (ListedFund fund in listed)
        {
            if (await _latest.ReadAsync(fund, DatasetName, cancellationToken) is { } report)
            {
                reports.Add(report);
            }
        }

        if (reports.Count < 2)
        {
            throw new InvalidOperationException("Fewer than two funds have a readable N-PORT report for the overlap.");
        }

        FundOverlapSnapshotDto snapshot = FundOverlapBuilder.Build(reports);
        string quarter = string.Create(CultureInfo.InvariantCulture,
            $"{snapshot.AsOf.Year}Q{(snapshot.AsOf.Month - 1) / 3 + 1}");
        await _store.StoreAsync(ResearchDatasets.FundOverlap, quarter, quarter, snapshot.AsOf, snapshot, cancellationToken);
    }
}
