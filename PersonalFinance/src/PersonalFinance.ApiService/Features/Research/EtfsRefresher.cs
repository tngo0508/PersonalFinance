using System.Globalization;
using System.Xml;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the ETFs dataset from the Form N-PORT filings SEC EDGAR holds for each configured ETF
/// (<see cref="ResearchSettings.Etfs"/>), reading only the filings no earlier refresh has read.
/// </summary>
internal sealed class EtfsRefresher : IResearchDatasetRefresher
{
    private readonly ResearchSnapshotStore _store;
    private readonly ISecEdgarClient _secClient;
    private readonly ResearchSettings _settings;
    private readonly ILogger<EtfsRefresher> _logger;

    public EtfsRefresher(ResearchSnapshotStore store, ISecEdgarClient secClient, ResearchSettings settings,
        ILogger<EtfsRefresher> logger)
    {
        _store = store;
        _secClient = secClient;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.Etfs;

    /// <inheritdoc />
    public ResearchSources Source => ResearchSources.SecEdgar;

    /// <inheritdoc />
    public bool IsConfigured => _settings.Etfs.Count > 0;

    /// <summary>
    /// Reads the N-PORT filings of each configured ETF that no earlier refresh has read, then builds the ETFs
    /// snapshot from every filing read so far and stores both. One fund's filing list or filing failing is logged and
    /// skipped (a filing not read is tried again next time), so it does not hold back the other funds.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, EtfFilingRecord> records =
            ((await _store.ReadAsync<List<EtfFilingRecord>>(ResearchDatasets.EtfFilings, cancellationToken)) ?? [])
            .ToDictionary(r => r.AccessionNumber, StringComparer.Ordinal);

        IReadOnlyList<SecFundSeries> series = _settings.Etfs.Any(e => e.Cik is null)
            ? await _secClient.GetFundSeriesAsync(cancellationToken)
            : [];
        foreach (ResearchEtf etf in _settings.Etfs)
        {
            await ReadNewFilingsAsync(etf, series, records, cancellationToken);
        }

        EtfsSnapshotDto snapshot = EtfsSnapshotBuilder.Build(_settings.Etfs, records.Values, _settings.EtfsFirstQuarter)
            ?? throw new InvalidOperationException("SEC returned no N-PORT report for any ETF.");
        string firstPeriod = snapshot.Periods[0].Replace(" ", string.Empty, StringComparison.Ordinal);
        string lastPeriod = snapshot.Periods[^1].Replace(" ", string.Empty, StringComparison.Ordinal);
        await _store.StoreAsync(ResearchDatasets.EtfFilings, firstPeriod, lastPeriod, snapshot.AsOf,
            records.Values.OrderBy(r => r.AccessionNumber, StringComparer.Ordinal).ToList(), cancellationToken);
        await _store.StoreAsync(ResearchDatasets.Etfs, firstPeriod, lastPeriod, snapshot.AsOf, snapshot,
            cancellationToken);
    }

    /// <summary>
    /// Lists one ETF's N-PORT filings, under its trust's CIK when one is configured or else under the series SEC's
    /// fund ticker list gives for its ticker, and reads each filing not read before into <paramref name="records"/>.
    /// </summary>
    private async Task ReadNewFilingsAsync(ResearchEtf etf, IReadOnlyList<SecFundSeries> series,
        Dictionary<string, EtfFilingRecord> records, CancellationToken cancellationToken)
    {
        string? seriesId = etf.Cik is null
            ? series.FirstOrDefault(s => string.Equals(s.Ticker, etf.Ticker, StringComparison.OrdinalIgnoreCase))?.SeriesId
            : null;
        string? filer = etf.Cik?.ToString(CultureInfo.InvariantCulture) ?? seriesId;
        if (filer is null)
        {
            _logger.LogWarning("ETF {Ticker} is not in SEC's fund ticker list; its filings are not read", etf.Ticker);
            return;
        }

        IReadOnlyList<NportFilingRef> filings;
        try
        {
            filings = await _secClient.GetNportFilingsAsync(filer, _settings.EtfsFirstQuarter, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException)
        {
            _logger.LogWarning(ex, "Could not list the N-PORT filings of ETF {Ticker}; its earlier ones are kept", etf.Ticker);
            return;
        }

        foreach (NportFilingRef filing in filings.Where(f => !records.ContainsKey(f.AccessionNumber)))
        {
            EtfFilingRecord? record = await ReadFilingAsync(etf, seriesId, filing, cancellationToken);
            if (record is not null)
            {
                records[record.AccessionNumber] = record;
            }
        }
    }

    /// <summary>
    /// Reads one N-PORT filing. A filing EDGAR does not have, or another series' report, is remembered with no
    /// holdings; a filing that fails to download or parse is logged and returns null, so it is tried again next time.
    /// </summary>
    private async Task<EtfFilingRecord?> ReadFilingAsync(ResearchEtf etf, string? seriesId, NportFilingRef filing,
        CancellationToken cancellationToken)
    {
        NportReport? report;
        try
        {
            report = await _secClient.GetNportReportAsync(filing, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or FormatException)
        {
            _logger.LogWarning(ex, "Could not read N-PORT filing {Accession} of ETF {Ticker}", filing.AccessionNumber,
                etf.Ticker);
            return null;
        }

        bool ours = report is not null
            && (seriesId is null || report.SeriesId is null
                || string.Equals(report.SeriesId, seriesId, StringComparison.OrdinalIgnoreCase));
        return new EtfFilingRecord(filing.AccessionNumber, etf.Ticker, filing.FiledOn,
            ours ? EtfsSnapshotBuilder.Summarize(etf.Ticker, report!) : null);
    }
}
