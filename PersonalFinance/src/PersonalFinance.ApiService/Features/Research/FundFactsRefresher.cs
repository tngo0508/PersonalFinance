using System.Xml;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the fund facts dataset: each Tracked Fund's net assets, from the latest quarter of the stored funds
/// snapshot (so it runs after the funds refresh), and its expense ratio, from the XBRL of its newest shareholder report
/// (Form N-CSR or N-CSRS). The ratio taken is the share class matching the ticker (from SEC's fund ticker list), or a
/// report's only ratio. A fund whose newest report is the one already read keeps its stored ratio without a download.
/// </summary>
internal sealed class FundFactsRefresher : IResearchDatasetRefresher
{
    // How far back to look for a shareholder report: funds file one every six months.
    private const int ReportMonths = 18;

    // How many of a fund's newest reports to try when the newest has no ratio for its class.
    private const int TriesPerFund = 3;

    private readonly ResearchSnapshotStore _store;
    private readonly ISecEdgarClient _secClient;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FundFactsRefresher> _logger;

    public FundFactsRefresher(ResearchSnapshotStore store, ISecEdgarClient secClient, ResearchSettings settings,
        TimeProvider timeProvider, ILogger<FundFactsRefresher> logger)
    {
        _store = store;
        _secClient = secClient;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.FundFacts;

    /// <inheritdoc />
    public ResearchSources Source => ResearchSources.SecEdgar;

    /// <inheritdoc />
    public bool IsConfigured => _settings.Funds.Count > 0;

    /// <summary>
    /// Builds each fund's facts and stores them. A fund whose reports cannot be listed or read keeps its stored ratio
    /// and is logged; the dataset fails only when no fund has any fact.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        FundsSnapshotDto? funds = await _store.ReadAsync<FundsSnapshotDto>(ResearchDatasets.Funds, cancellationToken);
        FundFactsSnapshotDto? stored = await _store.ReadAsync<FundFactsSnapshotDto>(ResearchDatasets.FundFacts, cancellationToken);
        Dictionary<string, FundFactsDto> previous = (stored?.Funds ?? [])
            .ToDictionary(f => f.Ticker, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<SecFundSeries> series = await _secClient.GetFundSeriesAsync(cancellationToken);
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var facts = new List<FundFactsDto>();
        foreach (TrackedFund fund in _settings.Funds)
        {
            FundHoldingsDto? latest = funds?.Values
                .SelectMany(v => v.Funds)
                .Where(f => string.Equals(f.Ticker, fund.Ticker, StringComparison.OrdinalIgnoreCase) && f.NetAssets > 0)
                .MaxBy(f => f.ReportDate);
            FundFactsDto entry = await ExpenseRatioAsync(fund, series, today.AddMonths(-ReportMonths),
                previous.GetValueOrDefault(fund.Ticker), cancellationToken);
            entry.NetAssets = latest?.NetAssets;
            entry.NetAssetsAsOf = latest?.ReportDate;
            facts.Add(entry);
        }

        if (!facts.Any(f => f.NetAssets is not null || f.ExpenseRatio is not null))
        {
            throw new InvalidOperationException("No Tracked Fund has net assets or an expense ratio.");
        }

        DateOnly asOf = facts
            .SelectMany(f => new[] { f.NetAssetsAsOf, f.ExpenseRatioAsOf })
            .OfType<DateOnly>()
            .DefaultIfEmpty(today)
            .Max();
        var snapshot = new FundFactsSnapshotDto { AsOf = asOf, Funds = facts };
        string period = asOf.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        await _store.StoreAsync(ResearchDatasets.FundFacts, period, period, asOf, snapshot, cancellationToken);
    }

    /// <summary>
    /// A fund's expense ratio from its newest shareholder report with a ratio for its class; the stored one when the
    /// newest report is the one already read, or when the reports cannot be listed or read.
    /// </summary>
    private async Task<FundFactsDto> ExpenseRatioAsync(TrackedFund fund, IReadOnlyList<SecFundSeries> series,
        DateOnly filedFrom, FundFactsDto? previous, CancellationToken cancellationToken)
    {
        var kept = new FundFactsDto
        {
            Ticker = fund.Ticker,
            ExpenseRatio = previous?.ExpenseRatio,
            ExpenseRatioAsOf = previous?.ExpenseRatioAsOf,
            ShareholderReport = previous?.ShareholderReport,
        };
        (string? filer, _) = FundsRefresher.FilerOf(fund, series);
        if (filer is null)
        {
            _logger.LogWarning("Fund {Ticker} is not in SEC's fund ticker list; its expense ratio is not read", fund.Ticker);
            return kept;
        }

        string? classId = series
            .FirstOrDefault(s => string.Equals(s.Ticker, fund.Ticker, StringComparison.OrdinalIgnoreCase))?.ClassId;
        try
        {
            List<NportFilingRef> reports = (await _secClient.GetShareholderReportFilingsAsync(filer, filedFrom, cancellationToken))
                .OrderByDescending(f => f.FiledOn)
                .ThenByDescending(f => f.AccessionNumber, StringComparer.Ordinal)
                .ToList();
            if (reports.Count == 0 || reports[0].AccessionNumber == previous?.ShareholderReport)
            {
                return kept;
            }

            foreach (NportFilingRef report in reports.Take(TriesPerFund))
            {
                IReadOnlyList<ClassExpenseRatio>? ratios = await _secClient.GetExpenseRatiosAsync(report, cancellationToken);
                ClassExpenseRatio? ratio = ratios is null ? null : RatioFor(ratios, classId);
                if (ratio is not null)
                {
                    return new FundFactsDto
                    {
                        Ticker = fund.Ticker,
                        ExpenseRatio = ratio.Percent,
                        ExpenseRatioAsOf = ratio.PeriodEnd,
                        ShareholderReport = reports[0].AccessionNumber,
                    };
                }
            }

            _logger.LogWarning("Fund {Ticker} has no expense ratio for its class in its recent shareholder reports", fund.Ticker);
            return kept;
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Could not read the shareholder reports of fund {Ticker}; its stored expense ratio is kept",
                fund.Ticker);
            return kept;
        }
    }

    /// <summary>
    /// The ratio of the class with <paramref name="classId"/> (the latest period, if several); when the class is not
    /// there, the report's only class's ratio. Null when neither is.
    /// </summary>
    internal static ClassExpenseRatio? RatioFor(IReadOnlyList<ClassExpenseRatio> ratios, string? classId)
    {
        ClassExpenseRatio? match = ratios
            .Where(r => classId is not null && string.Equals(r.ClassId, classId, StringComparison.OrdinalIgnoreCase))
            .MaxBy(r => r.PeriodEnd);
        if (match is not null)
        {
            return match;
        }

        List<string?> classes = ratios.Select(r => r.ClassId).Distinct().ToList();
        return classes.Count == 1 ? ratios.MaxBy(r => r.PeriodEnd) : null;
    }
}
