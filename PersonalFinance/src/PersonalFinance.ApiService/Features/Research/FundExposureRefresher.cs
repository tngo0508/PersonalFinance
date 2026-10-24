using System.Globalization;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the fund exposure dataset from each Tracked Fund's latest Form N-PORT report: country and currency totals
/// over every holding. It reads reports only when some fund has filed a newer one than the stored exposure was built
/// from, the same way as the fund overlap.
/// </summary>
internal sealed class FundExposureRefresher : IResearchDatasetRefresher
{
    private const string DatasetName = "exposure";

    private readonly ResearchSnapshotStore _store;
    private readonly LatestFundReports _latest;
    private readonly ResearchSettings _settings;

    public FundExposureRefresher(ResearchSnapshotStore store, LatestFundReports latest, ResearchSettings settings)
    {
        _store = store;
        _latest = latest;
        _settings = settings;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.FundExposure;

    /// <inheritdoc />
    public ResearchSources Source => ResearchSources.SecEdgar;

    /// <inheritdoc />
    public bool IsConfigured => _settings.Funds.Count > 0;

    /// <summary>
    /// Keeps the stored exposure when every fund's newest filing is the one it was built from; otherwise reads each
    /// fund's latest report and stores the exposure. A fund whose filings cannot be listed or read is left out.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ListedFund> listed = await _latest.ListAsync(DatasetName, cancellationToken);
        FundExposureSnapshotDto? stored = await _store.ReadAsync<FundExposureSnapshotDto>(ResearchDatasets.FundExposure,
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

        if (reports.Count == 0)
        {
            throw new InvalidOperationException("No fund has a readable N-PORT report for the exposure.");
        }

        FundExposureSnapshotDto snapshot = FundExposureBuilder.Build(reports);
        string quarter = string.Create(CultureInfo.InvariantCulture,
            $"{snapshot.AsOf.Year}Q{(snapshot.AsOf.Month - 1) / 3 + 1}");
        await _store.StoreAsync(ResearchDatasets.FundExposure, quarter, quarter, snapshot.AsOf, snapshot, cancellationToken);
    }
}
