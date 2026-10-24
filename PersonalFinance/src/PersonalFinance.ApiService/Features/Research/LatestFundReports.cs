using System.Xml;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Finds and reads each Tracked Fund's latest Form N-PORT report, for the datasets built from each fund's latest
/// holdings (the fund overlap and the fund exposure). Listing is cheap; a report is read whole only when a dataset has
/// not yet been built from it, so a refresh downloads anything only after a fund files a newer report.
/// </summary>
internal sealed class LatestFundReports
{
    // How far back to list filings for a fund's latest report: a fund files every quarter, so 15 months is plenty.
    private const int ListMonths = 15;

    // How many of a fund's newest filings to try when the newest is not its own report or cannot be read.
    private const int TriesPerFund = 3;

    private readonly ISecEdgarClient _secClient;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LatestFundReports> _logger;

    public LatestFundReports(ISecEdgarClient secClient, ResearchSettings settings, TimeProvider timeProvider,
        ILogger<LatestFundReports> logger)
    {
        _secClient = secClient;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Each Tracked Fund with its recent N-PORT filings, newest first. A fund not in SEC's fund ticker list, or whose
    /// filings cannot be listed or are none, is left out and logged with <paramref name="dataset"/>.
    /// </summary>
    public async Task<IReadOnlyList<ListedFund>> ListAsync(string dataset, CancellationToken cancellationToken)
    {
        IReadOnlyList<SecFundSeries> series = _settings.Funds.Any(f => f.Cik is null)
            ? await _secClient.GetFundSeriesAsync(cancellationToken)
            : [];
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        DateOnly recent = today.AddMonths(-ListMonths);
        DateOnly filedFrom = recent > _settings.FundsFirstQuarter ? recent : _settings.FundsFirstQuarter;

        var listed = new List<ListedFund>();
        foreach (TrackedFund fund in _settings.Funds)
        {
            (string? filer, string? seriesId) = FundsRefresher.FilerOf(fund, series);
            if (filer is null)
            {
                _logger.LogWarning("Fund {Ticker} is not in SEC's fund ticker list; it is left out of the {Dataset}",
                    fund.Ticker, dataset);
                continue;
            }

            try
            {
                List<NportFilingRef> newestFirst = (await _secClient.GetNportFilingsAsync(filer, filedFrom, cancellationToken))
                    .OrderByDescending(f => f.FiledOn)
                    .ThenByDescending(f => f.AccessionNumber, StringComparer.Ordinal)
                    .ToList();
                if (newestFirst.Count > 0)
                {
                    listed.Add(new ListedFund(fund, seriesId, newestFirst));
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or XmlException)
            {
                _logger.LogWarning(ex, "Could not list the N-PORT filings of fund {Ticker} for the {Dataset}",
                    fund.Ticker, dataset);
            }
        }

        return listed;
    }

    /// <summary>
    /// True when <paramref name="built"/> (each fund's ticker and the accession a dataset was built from) is exactly
    /// the listed funds at their newest filings, so the dataset is up to date.
    /// </summary>
    public static bool UpToDate(IReadOnlyList<ListedFund> listed, IReadOnlyCollection<(string Ticker, string Accession)> built) =>
        built.Count == listed.Count
        && listed.All(l => built.Any(b => b.Ticker == l.Fund.Ticker && b.Accession == l.NewestFirst[0].AccessionNumber));

    /// <summary>
    /// A fund's latest report: the newest of its filings that is its own (by series, when known) and reads, trying a
    /// few in turn. Null, logged with <paramref name="dataset"/>, when none of them does.
    /// </summary>
    public async Task<LatestFundReport?> ReadAsync(ListedFund listed, string dataset, CancellationToken cancellationToken)
    {
        foreach (NportFilingRef filing in listed.NewestFirst.Take(TriesPerFund))
        {
            try
            {
                NportReport? report = await _secClient.GetNportReportAsync(filing, cancellationToken);
                if (report is not null && (listed.SeriesId is null || report.SeriesId is null
                        || string.Equals(report.SeriesId, listed.SeriesId, StringComparison.OrdinalIgnoreCase)))
                {
                    return new LatestFundReport(listed.Fund.Ticker, filing.AccessionNumber, report);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or XmlException or FormatException)
            {
                _logger.LogWarning(ex, "Could not read N-PORT filing {Accession} of fund {Ticker} for the {Dataset}",
                    filing.AccessionNumber, listed.Fund.Ticker, dataset);
            }
        }

        _logger.LogWarning("Fund {Ticker} has no readable recent N-PORT report; it is left out of the {Dataset}",
            listed.Fund.Ticker, dataset);
        return null;
    }
}
