using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the Sectors dataset from SEC EDGAR: the four quarterly Public Float frames of every year from
/// <see cref="ResearchSettings.SectorsFirstYear"/> to last year (this year's filings are still coming in), cleaned and
/// ranked, with the filers' submissions to group them by.
/// </summary>
internal sealed class SectorsRefresher : IResearchDatasetRefresher
{
    private readonly ResearchSnapshotStore _store;
    private readonly ISecEdgarClient _secClient;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SectorsRefresher> _logger;

    public SectorsRefresher(ResearchSnapshotStore store, ISecEdgarClient secClient, ResearchSettings settings,
        TimeProvider timeProvider, ILogger<SectorsRefresher> logger)
    {
        _store = store;
        _secClient = secClient;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.Sectors;

    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        int lastYear = _timeProvider.GetUtcNow().Year - 1;
        var factsByYear = new Dictionary<int, IReadOnlyList<PublicFloatFact>>();
        for (int year = _settings.SectorsFirstYear; year <= lastYear; year++)
        {
            var facts = new List<PublicFloatFact>();
            for (int quarter = 1; quarter <= 4; quarter++)
            {
                facts.AddRange(await _secClient.GetPublicFloatFrameAsync(year, quarter, cancellationToken));
            }

            factsByYear[year] = facts;
        }

        IReadOnlyDictionary<int, IReadOnlyList<PublicFloatFact>> cleaned =
            await CleanPublicFloatAsync(factsByYear, lastYear, cancellationToken);
        List<SectorsRankedYear> ranked = SectorsSnapshotBuilder.Rank(cleaned);
        if (ranked.Count == 0)
        {
            throw new InvalidOperationException("SEC returned no Public Float for any year.");
        }

        Dictionary<long, SecFiler> filers = await ReadFilersAsync(ranked, cancellationToken);
        SectorsSnapshotDto snapshot = SectorsSnapshotBuilder.Build(ranked, filers)
            ?? throw new InvalidOperationException("SEC returned no Public Float for any year.");
        await _store.StoreAsync(ResearchDatasets.SecFilers, snapshot.Periods[0], snapshot.Periods[^1], snapshot.AsOf,
            filers.Values.OrderBy(f => f.Cik).ToList(), cancellationToken);
        await _store.StoreAsync(ResearchDatasets.Sectors, snapshot.Periods[0], snapshot.Periods[^1], snapshot.AsOf,
            snapshot, cancellationToken);
    }

    /// <summary>
    /// Drops the Public Float values SEC holds wrongly (<see cref="PublicFloatCleaner"/>), checking them against the
    /// filers' total assets in the Q2 and Q4 frames from the year before the first to last year. Dropped values are
    /// logged by filer CIK and year only, never with their amount or name.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, IReadOnlyList<PublicFloatFact>>> CleanPublicFloatAsync(
        Dictionary<int, IReadOnlyList<PublicFloatFact>> factsByYear, int lastYear, CancellationToken cancellationToken)
    {
        var totalAssets = new List<TotalAssetsFact>();
        for (int year = _settings.SectorsFirstYear - 1; year <= lastYear; year++)
        {
            foreach (int quarter in (int[])[2, 4])
            {
                totalAssets.AddRange(await _secClient.GetTotalAssetsFrameAsync(year, quarter, cancellationToken));
            }
        }

        PublicFloatCleaning cleaning = PublicFloatCleaner.Clean(factsByYear, totalAssets,
            _settings.PublicFloatExclusions, _settings.PublicFloatKeeps);
        // One line a year: a refresh drops a hundred or more values a year, mostly from small filers.
        foreach (IGrouping<int, FilerYear> year in cleaning.Dropped.GroupBy(d => d.Year))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Left out the Public Float of {Count} filers for {Year} from the Sectors snapshot, CIKs {Ciks}",
                    year.Count(), year.Key, string.Join(',', year.Select(d => d.Cik)));
            }
        }

        return cleaning.Kept;
    }

    /// <summary>
    /// The SEC submissions of the filers in the snapshot, from the stored list of filers read before plus new reads:
    /// the latest year's ranked filers are read again on every refresh, so their names, tickers and offices stay
    /// current; ranked filers of earlier years are read once; filers outside the largest are read once, largest
    /// first, up to <see cref="ResearchSettings.SubmissionsPerRefresh"/>. A filer SEC has no submissions for is
    /// remembered with no fields, so it is not asked for again.
    /// </summary>
    private async Task<Dictionary<long, SecFiler>> ReadFilersAsync(List<SectorsRankedYear> ranked,
        CancellationToken cancellationToken)
    {
        Dictionary<long, SecFiler> filers =
            ((await _store.ReadAsync<List<SecFiler>>(ResearchDatasets.SecFilers, cancellationToken)) ?? [])
            .ToDictionary(f => f.Cik);

        HashSet<long> rankedCiks = ranked.SelectMany(y => y.Ranked).Select(f => f.Cik).ToHashSet();
        IEnumerable<long> latestRanked = ranked[^1].Ranked.Select(f => f.Cik).Order();
        IEnumerable<long> newRanked = rankedCiks.Where(cik => !filers.ContainsKey(cik)).Order();
        IEnumerable<long> newOthers = ranked
            .SelectMany(y => y.Others)
            .Where(f => !rankedCiks.Contains(f.Cik) && !filers.ContainsKey(f.Cik))
            .GroupBy(f => f.Cik)
            .Select(g => (Cik: g.Key, Largest: g.Max(f => f.Value)))
            .OrderByDescending(f => f.Largest)
            .ThenBy(f => f.Cik)
            .Take(Math.Max(0, _settings.SubmissionsPerRefresh))
            .Select(f => f.Cik);

        foreach (long cik in latestRanked.Concat(newRanked).Concat(newOthers).Distinct().ToList())
        {
            filers[cik] = await _secClient.GetFilerAsync(cik, cancellationToken)
                ?? new SecFiler(cik, string.Empty, null, null, null, null);
        }

        return filers;
    }
}
