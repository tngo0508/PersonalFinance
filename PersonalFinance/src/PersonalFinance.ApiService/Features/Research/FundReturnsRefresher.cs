using System.Globalization;
using System.Xml;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the fund returns dataset from the monthly total returns (Item B.5) in each Tracked Fund's Form N-PORT
/// filings, reading only the head of each filing no earlier refresh has read. The return taken is the share class
/// matching the fund's ticker (from SEC's fund ticker list), or a report's only return: so QQQ's history runs on from
/// its trust's filings, which give no class, to its ETF class's after the 2025 conversion, under the same filer.
/// Sharpe ratios use the 3-month Treasury bill rate from the Fed's H.15 release; without it they are left out.
/// </summary>
internal sealed class FundReturnsRefresher : IResearchDatasetRefresher
{
    private readonly ResearchSnapshotStore _store;
    private readonly ISecEdgarClient _secClient;
    private readonly IFedRatesClient _fedClient;
    private readonly ResearchSettings _settings;
    private readonly ILogger<FundReturnsRefresher> _logger;

    public FundReturnsRefresher(ResearchSnapshotStore store, ISecEdgarClient secClient, IFedRatesClient fedClient,
        ResearchSettings settings, ILogger<FundReturnsRefresher> logger)
    {
        _store = store;
        _secClient = secClient;
        _fedClient = fedClient;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.FundReturns;

    /// <inheritdoc />
    public ResearchSources Source => ResearchSources.SecEdgar;

    /// <inheritdoc />
    public bool IsConfigured => _settings.Funds.Count > 0;

    /// <summary>
    /// Reads the head of each Tracked Fund's N-PORT filings that no earlier refresh has read (saving them after each
    /// fund, so a run stopped part-way carries on next time), then builds and stores the snapshot. One fund's filing
    /// list or filing failing is logged and skipped; a filing not read is tried again next time.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, FundReturnFilingRecord> records =
            ((await _store.ReadAsync<List<FundReturnFilingRecord>>(ResearchDatasets.FundReturnFilings, cancellationToken)) ?? [])
            .ToDictionary(r => r.AccessionNumber, StringComparer.Ordinal);

        // The ticker list gives every fund's share class, and the series of a fund with no configured trust CIK.
        IReadOnlyList<SecFundSeries> series = await _secClient.GetFundSeriesAsync(cancellationToken);
        foreach (TrackedFund fund in _settings.Funds)
        {
            if (await ReadNewFilingsAsync(fund, series, records, cancellationToken) > 0)
            {
                await StoreFilingsAsync(records, cancellationToken);
            }
        }

        IReadOnlyList<(DateOnly Month, decimal Rate)> bills = await TreasuryBillsAsync(cancellationToken);
        FundReturnsSnapshotDto snapshot = FundReturnsBuilder.Build(_settings.Funds, records.Values, bills)
            ?? throw new InvalidOperationException("SEC returned no monthly return for any fund.");
        string first = MonthLabel(snapshot.Funds.Min(f => f.FirstMonth));
        string last = MonthLabel(snapshot.Funds.Max(f => f.LastMonth));
        await _store.StoreAsync(ResearchDatasets.FundReturns, first, last, snapshot.AsOf, snapshot, cancellationToken);
    }

    private static string MonthLabel(DateOnly month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>The monthly 3-month Treasury bill rates; empty (no Sharpe ratios) when the Fed's release cannot be read.</summary>
    private async Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> TreasuryBillsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _fedClient.GetTreasuryBillRatesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or InvalidDataException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Could not read the Treasury bill rates from the Fed's H.15 release; Sharpe ratios are left out");
            return [];
        }
    }

    /// <summary>Saves the filings read so far, so they are not fetched again.</summary>
    private async Task StoreFilingsAsync(Dictionary<string, FundReturnFilingRecord> records,
        CancellationToken cancellationToken)
    {
        DateOnly first = records.Values.Min(r => r.FiledOn);
        DateOnly last = records.Values.Max(r => r.FiledOn);
        await _store.StoreAsync(ResearchDatasets.FundReturnFilings, MonthLabel(first), MonthLabel(last), last,
            records.Values.OrderBy(r => r.AccessionNumber, StringComparer.Ordinal).ToList(), cancellationToken);
    }

    /// <summary>Lists one fund's N-PORT filings and reads the head of each one not read before.</summary>
    /// <returns>How many filings were added to <paramref name="records"/>.</returns>
    private async Task<int> ReadNewFilingsAsync(TrackedFund fund, IReadOnlyList<SecFundSeries> series,
        Dictionary<string, FundReturnFilingRecord> records, CancellationToken cancellationToken)
    {
        (string? filer, string? seriesId) = FundsRefresher.FilerOf(fund, series);
        if (filer is null)
        {
            _logger.LogWarning("Fund {Ticker} is not in SEC's fund ticker list; its returns are not read", fund.Ticker);
            return 0;
        }

        string? classId = series
            .FirstOrDefault(s => string.Equals(s.Ticker, fund.Ticker, StringComparison.OrdinalIgnoreCase))?.ClassId;
        IReadOnlyList<NportFilingRef> filings;
        try
        {
            filings = await _secClient.GetNportFilingsAsync(filer, _settings.FundsFirstQuarter, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException)
        {
            _logger.LogWarning(ex, "Could not list the N-PORT filings of fund {Ticker} for its returns", fund.Ticker);
            return 0;
        }

        int added = 0;
        foreach (NportFilingRef filing in filings.Where(f => !records.ContainsKey(f.AccessionNumber)))
        {
            try
            {
                NportReturns? head = await _secClient.GetNportReturnsAsync(filing, cancellationToken);
                bool ours = head is not null
                    && (seriesId is null || head.SeriesId is null
                        || string.Equals(head.SeriesId, seriesId, StringComparison.OrdinalIgnoreCase));
                NportClassReturns? returns = ours ? head!.For(classId) : null;
                records[filing.AccessionNumber] = new FundReturnFilingRecord(filing.AccessionNumber, fund.Ticker,
                    filing.FiledOn, returns is null ? null : head!.ReportDate,
                    returns is null ? null : [returns.Month1, returns.Month2, returns.Month3]);
                added++;
            }
            catch (Exception ex) when (ex is HttpRequestException or XmlException or FormatException)
            {
                _logger.LogWarning(ex, "Could not read the returns in N-PORT filing {Accession} of fund {Ticker}",
                    filing.AccessionNumber, fund.Ticker);
            }
        }

        return added;
    }
}
