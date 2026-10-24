using System.Globalization;
using System.Xml;
using PersonalFinance.ApiService.Features.Research.Adapters;
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
    // How far back to list filings for a fund's latest report: a fund files every quarter, so 15 months is plenty.
    private const int ListMonths = 15;

    // How many of a fund's newest filings to try when the newest is not its own report or cannot be read.
    private const int TriesPerFund = 3;

    private readonly ResearchSnapshotStore _store;
    private readonly ISecEdgarClient _secClient;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FundOverlapRefresher> _logger;

    public FundOverlapRefresher(ResearchSnapshotStore store, ISecEdgarClient secClient, ResearchSettings settings,
        TimeProvider timeProvider, ILogger<FundOverlapRefresher> logger)
    {
        _store = store;
        _secClient = secClient;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
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
        IReadOnlyList<SecFundSeries> series = _settings.Funds.Any(f => f.Cik is null)
            ? await _secClient.GetFundSeriesAsync(cancellationToken)
            : [];
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        DateOnly recent = today.AddMonths(-ListMonths);
        DateOnly filedFrom = recent > _settings.FundsFirstQuarter ? recent : _settings.FundsFirstQuarter;

        var listed = new List<(TrackedFund Fund, string? SeriesId, List<NportFilingRef> NewestFirst)>();
        foreach (TrackedFund fund in _settings.Funds)
        {
            (string? filer, string? seriesId) = FundsRefresher.FilerOf(fund, series);
            if (filer is null)
            {
                _logger.LogWarning("Fund {Ticker} is not in SEC's fund ticker list; it is left out of the overlap", fund.Ticker);
                continue;
            }

            try
            {
                IReadOnlyList<NportFilingRef> filings = await _secClient.GetNportFilingsAsync(filer, filedFrom, cancellationToken);
                List<NportFilingRef> newestFirst = filings
                    .OrderByDescending(f => f.FiledOn)
                    .ThenByDescending(f => f.AccessionNumber, StringComparer.Ordinal)
                    .ToList();
                if (newestFirst.Count > 0)
                {
                    listed.Add((fund, seriesId, newestFirst));
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or XmlException)
            {
                _logger.LogWarning(ex, "Could not list the N-PORT filings of fund {Ticker} for the overlap", fund.Ticker);
            }
        }

        FundOverlapSnapshotDto? stored = await _store.ReadAsync<FundOverlapSnapshotDto>(ResearchDatasets.FundOverlap,
            cancellationToken);
        if (stored is not null && stored.Funds.Count == listed.Count && listed.All(l => stored.Funds.Any(f =>
                f.Ticker == l.Fund.Ticker && f.Accession == l.NewestFirst[0].AccessionNumber)))
        {
            return;
        }

        var reports = new List<FundOverlapBuilder.FundReport>();
        foreach ((TrackedFund fund, string? seriesId, List<NportFilingRef> newestFirst) in listed)
        {
            FundOverlapBuilder.FundReport? report = await ReadLatestAsync(fund, seriesId, newestFirst, cancellationToken);
            if (report is not null)
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

    /// <summary>
    /// A fund's latest report: the newest of its filings that is its own (by series, when known) and reads, trying a
    /// few in turn. Null when none of them does.
    /// </summary>
    private async Task<FundOverlapBuilder.FundReport?> ReadLatestAsync(TrackedFund fund, string? seriesId,
        List<NportFilingRef> newestFirst, CancellationToken cancellationToken)
    {
        foreach (NportFilingRef filing in newestFirst.Take(TriesPerFund))
        {
            try
            {
                NportReport? report = await _secClient.GetNportReportAsync(filing, cancellationToken);
                if (report is not null && (seriesId is null || report.SeriesId is null
                        || string.Equals(report.SeriesId, seriesId, StringComparison.OrdinalIgnoreCase)))
                {
                    return new FundOverlapBuilder.FundReport(fund.Ticker, filing.AccessionNumber, report);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or XmlException or FormatException)
            {
                _logger.LogWarning(ex, "Could not read N-PORT filing {Accession} of fund {Ticker} for the overlap",
                    filing.AccessionNumber, fund.Ticker);
            }
        }

        _logger.LogWarning("Fund {Ticker} has no readable recent N-PORT report; it is left out of the overlap", fund.Ticker);
        return null;
    }
}
