using System.Globalization;
using System.Xml;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the funds dataset from the Form N-PORT filings SEC EDGAR holds for each Tracked Fund
/// (<see cref="ResearchSettings.Funds"/>), reading only the filings no earlier refresh has read.
/// </summary>
internal sealed class FundsRefresher : IResearchDatasetRefresher
{
    private readonly ResearchSnapshotStore _store;
    private readonly ISecEdgarClient _secClient;
    private readonly ResearchSettings _settings;
    private readonly ILogger<FundsRefresher> _logger;

    public FundsRefresher(ResearchSnapshotStore store, ISecEdgarClient secClient, ResearchSettings settings,
        ILogger<FundsRefresher> logger)
    {
        _store = store;
        _secClient = secClient;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.Funds;

    /// <inheritdoc />
    public ResearchSources Source => ResearchSources.SecEdgar;

    /// <inheritdoc />
    public bool IsConfigured => _settings.Funds.Count > 0;

    /// <summary>
    /// Reads the N-PORT filings of each Tracked Fund that no earlier refresh has read, then builds the funds snapshot
    /// from every filing read so far and stores both. One fund's filing list or filing failing is logged and skipped
    /// (a filing not read is tried again next time), so it does not hold back the other funds. The filings read are
    /// saved after each fund that added any, so a run stopped part-way (e.g. at the job's time limit, on a first run
    /// over many new funds) keeps what it read and the next run carries on from there.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, FundFilingRecord> records =
            ((await _store.ReadAsync<List<FundFilingRecord>>(ResearchDatasets.FundFilings, cancellationToken)) ?? [])
            .ToDictionary(r => r.AccessionNumber, StringComparer.Ordinal);

        IReadOnlyList<SecFundSeries> series = _settings.Funds.Any(e => e.Cik is null)
            ? await _secClient.GetFundSeriesAsync(cancellationToken)
            : [];
        foreach (TrackedFund fund in _settings.Funds)
        {
            if (await ReadNewFilingsAsync(fund, series, records, cancellationToken) > 0)
            {
                await StoreFilingsAsync(records, cancellationToken);
            }
        }

        FundsSnapshotDto snapshot = FundsSnapshotBuilder.Build(_settings.Funds, records.Values, _settings.FundsFirstQuarter)
            ?? throw new InvalidOperationException("SEC returned no N-PORT report for any fund.");
        string firstPeriod = snapshot.Periods[0].Replace(" ", string.Empty, StringComparison.Ordinal);
        string lastPeriod = snapshot.Periods[^1].Replace(" ", string.Empty, StringComparison.Ordinal);
        await _store.StoreAsync(ResearchDatasets.FundFilings, firstPeriod, lastPeriod, snapshot.AsOf,
            records.Values.OrderBy(r => r.AccessionNumber, StringComparer.Ordinal).ToList(), cancellationToken);
        await _store.StoreAsync(ResearchDatasets.Funds, firstPeriod, lastPeriod, snapshot.AsOf, snapshot,
            cancellationToken);
    }

    /// <summary>
    /// Saves the filings read so far, so they are not fetched again. The cache's period range runs from the quarter
    /// of the earliest filing to that of the latest.
    /// </summary>
    private async Task StoreFilingsAsync(Dictionary<string, FundFilingRecord> records, CancellationToken cancellationToken)
    {
        static string QuarterOf(DateOnly date) =>
            string.Create(CultureInfo.InvariantCulture, $"{date.Year}Q{(date.Month - 1) / 3 + 1}");

        DateOnly first = records.Values.Min(r => r.FiledOn);
        DateOnly last = records.Values.Max(r => r.FiledOn);
        await _store.StoreAsync(ResearchDatasets.FundFilings, QuarterOf(first), QuarterOf(last), last,
            records.Values.OrderBy(r => r.AccessionNumber, StringComparer.Ordinal).ToList(), cancellationToken);
    }

    /// <summary>
    /// Lists one fund's N-PORT filings, under its trust's CIK when one is configured or else under the series SEC's
    /// fund ticker list gives for its ticker (ETFs and mutual funds alike), and reads each filing not read before
    /// into <paramref name="records"/>.
    /// </summary>
    /// <returns>How many filings were added to <paramref name="records"/>.</returns>
    private async Task<int> ReadNewFilingsAsync(TrackedFund fund, IReadOnlyList<SecFundSeries> series,
        Dictionary<string, FundFilingRecord> records, CancellationToken cancellationToken)
    {
        string? seriesId = fund.Cik is null
            ? series.FirstOrDefault(s => string.Equals(s.Ticker, fund.Ticker, StringComparison.OrdinalIgnoreCase))?.SeriesId
            : null;
        string? filer = fund.Cik?.ToString(CultureInfo.InvariantCulture) ?? seriesId;
        if (filer is null)
        {
            _logger.LogWarning("Fund {Ticker} is not in SEC's fund ticker list; its filings are not read", fund.Ticker);
            return 0;
        }

        IReadOnlyList<NportFilingRef> filings;
        try
        {
            filings = await _secClient.GetNportFilingsAsync(filer, _settings.FundsFirstQuarter, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException)
        {
            _logger.LogWarning(ex, "Could not list the N-PORT filings of fund {Ticker}; its earlier ones are kept", fund.Ticker);
            return 0;
        }

        int added = 0;
        foreach (NportFilingRef filing in filings.Where(f => !records.ContainsKey(f.AccessionNumber)))
        {
            FundFilingRecord? record = await ReadFilingAsync(fund, seriesId, filing, cancellationToken);
            if (record is not null)
            {
                records[record.AccessionNumber] = record;
                added++;
            }
        }
        return added;
    }

    /// <summary>
    /// Reads one N-PORT filing. A filing EDGAR does not have, or another series' report, is remembered with no
    /// holdings; a filing that fails to download or parse is logged and returns null, so it is tried again next time.
    /// </summary>
    private async Task<FundFilingRecord?> ReadFilingAsync(TrackedFund fund, string? seriesId, NportFilingRef filing,
        CancellationToken cancellationToken)
    {
        NportReport? report;
        try
        {
            report = await _secClient.GetNportReportAsync(filing, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or FormatException)
        {
            _logger.LogWarning(ex, "Could not read N-PORT filing {Accession} of fund {Ticker}", filing.AccessionNumber,
                fund.Ticker);
            return null;
        }

        bool ours = report is not null
            && (seriesId is null || report.SeriesId is null
                || string.Equals(report.SeriesId, seriesId, StringComparison.OrdinalIgnoreCase));
        return new FundFilingRecord(filing.AccessionNumber, fund.Ticker, filing.FiledOn,
            ours ? FundsSnapshotBuilder.Summarize(fund.Ticker, report!) : null);
    }
}
