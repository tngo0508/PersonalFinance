using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// <see cref="IResearchService"/> over the stored <see cref="ResearchSnapshot"/> rows, the SEC EDGAR client and the
/// World Bank client.
/// </summary>
internal sealed class ResearchService : IResearchService
{
    /// <summary>The dataset key of the Sectors snapshot.</summary>
    public const string SectorsDataset = "sectors";

    /// <summary>
    /// The dataset key of the SEC submissions read for the Sectors snapshot, kept so each refresh reads only the
    /// filers it has not read before (and the latest year's largest). Not served to the page.
    /// </summary>
    public const string FilersDataset = "sec-filers";

    /// <summary>The dataset key of the World snapshot.</summary>
    public const string WorldDataset = "world";

    /// <summary>The World Bank WDI indicator of market capitalisation of listed domestic companies (current US$).</summary>
    public const string MarketCapIndicator = "CM.MKT.LCAP.CD";

    /// <summary>The World Bank WDI indicator of GDP (current US$).</summary>
    public const string GdpIndicator = "NY.GDP.MKTP.CD";

    private readonly AppDbContext _db;
    private readonly ISecEdgarClient _secClient;
    private readonly IWorldBankClient _worldBankClient;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ResearchService> _logger;

    public ResearchService(AppDbContext db, ISecEdgarClient secClient, IWorldBankClient worldBankClient,
        ResearchSettings settings, TimeProvider timeProvider, ILogger<ResearchService> logger)
    {
        _db = db;
        _secClient = secClient;
        _worldBankClient = worldBankClient;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ResearchSnapshotResult<SectorsSnapshotDto>> GetSectorsAsync(
        CancellationToken cancellationToken = default)
    {
        string? payload = await SnapshotPayloadQuery(_db, SectorsDataset).FirstOrDefaultAsync(cancellationToken);
        SectorsSnapshotDto? snapshot = payload is null
            ? null
            : JsonSerializer.Deserialize<SectorsSnapshotDto>(payload, JsonSerializerOptions.Web);
        return new ResearchSnapshotResult<SectorsSnapshotDto>(snapshot);
    }

    /// <inheritdoc />
    public async Task<ResearchSnapshotResult<WorldSnapshotDto>> GetWorldAsync(
        CancellationToken cancellationToken = default)
    {
        string? payload = await SnapshotPayloadQuery(_db, WorldDataset).FirstOrDefaultAsync(cancellationToken);
        WorldSnapshotDto? snapshot = payload is null
            ? null
            : JsonSerializer.Deserialize<WorldSnapshotDto>(payload, JsonSerializerOptions.Web);
        return new ResearchSnapshotResult<WorldSnapshotDto>(snapshot);
    }

    /// <inheritdoc />
    public async Task<ResearchRefreshSummary> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var summary = new ResearchRefreshSummary();
        await RefreshDatasetAsync(SectorsDataset, RefreshSectorsAsync, summary, cancellationToken);
        await RefreshDatasetAsync(WorldDataset, RefreshWorldAsync, summary, cancellationToken);
        return summary;
    }

    /// <summary>
    /// Rebuilds one dataset, recording in the summary whether it was built or failed. A failure other than the
    /// refresh being cancelled is logged and leaves the previous snapshot in place.
    /// </summary>
    private async Task RefreshDatasetAsync(string dataset, Func<CancellationToken, Task> refresh,
        ResearchRefreshSummary summary, CancellationToken cancellationToken)
    {
        try
        {
            await refresh(cancellationToken);
            summary.Built.Add(dataset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The source is down or answered something unreadable: keep the previous snapshot.
            _logger.LogError(ex, "Could not rebuild the Research {Dataset} snapshot; the previous one is kept",
                dataset);
            summary.Failed.Add(dataset);
        }
    }

    /// <summary>
    /// Reads the World Bank's country list and its market-cap and GDP indicators from
    /// <see cref="ResearchSettings.WorldFirstYear"/> to last year, builds the World snapshot and stores it.
    /// </summary>
    private async Task RefreshWorldAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        int lastYear = now.Year - 1;
        IReadOnlyList<WorldBankCountry> countries = await _worldBankClient.GetCountriesAsync(cancellationToken);
        WorldBankIndicator marketCap = await _worldBankClient.GetIndicatorAsync(MarketCapIndicator,
            _settings.WorldFirstYear, lastYear, cancellationToken);
        WorldBankIndicator gdp = await _worldBankClient.GetIndicatorAsync(GdpIndicator,
            _settings.WorldFirstYear, lastYear, cancellationToken);

        WorldSnapshotDto snapshot = WorldSnapshotBuilder.Build(countries, marketCap, gdp, DateOnly.FromDateTime(now.UtcDateTime))
            ?? throw new InvalidOperationException("The World Bank returned no market cap for any country.");
        await StoreAsync(WorldDataset, snapshot.Periods[0], snapshot.Periods[^1], snapshot.AsOf,
            JsonSerializer.Serialize(snapshot, JsonSerializerOptions.Web), cancellationToken);
    }

    /// <summary>
    /// Reads the four quarterly Public Float frames of every year from <see cref="ResearchSettings.SectorsFirstYear"/>
    /// to last year (this year's filings are still coming in), builds the snapshot and stores it.
    /// </summary>
    private async Task RefreshSectorsAsync(CancellationToken cancellationToken)
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
        await StoreAsync(FilersDataset, snapshot.Periods[0], snapshot.Periods[^1], snapshot.AsOf,
            JsonSerializer.Serialize(filers.Values.OrderBy(f => f.Cik), JsonSerializerOptions.Web), cancellationToken);
        await StoreAsync(SectorsDataset, snapshot.Periods[0], snapshot.Periods[^1], snapshot.AsOf,
            JsonSerializer.Serialize(snapshot, JsonSerializerOptions.Web), cancellationToken);
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

        PublicFloatCleaning cleaning =
            PublicFloatCleaner.Clean(factsByYear, totalAssets, _settings.PublicFloatExclusions);
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
        string? payload = await SnapshotPayloadQuery(_db, FilersDataset).FirstOrDefaultAsync(cancellationToken);
        Dictionary<long, SecFiler> filers = (payload is null
                ? []
                : JsonSerializer.Deserialize<List<SecFiler>>(payload, JsonSerializerOptions.Web) ?? [])
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

    /// <summary>
    /// Replaces the dataset's row with the new snapshot, or adds it on the first build.
    /// </summary>
    private async Task StoreAsync(string dataset, string firstPeriod, string lastPeriod, DateOnly sourceAsOf,
        string payload, CancellationToken cancellationToken)
    {
        ResearchSnapshot? row = await _db.ResearchSnapshots
            .FirstOrDefaultAsync(s => s.Dataset == dataset, cancellationToken);
        if (row is null)
        {
            row = new ResearchSnapshot { Dataset = dataset };
            _db.ResearchSnapshots.Add(row);
        }

        row.FirstPeriod = firstPeriod;
        row.LastPeriod = lastPeriod;
        row.SourceAsOf = sourceAsOf;
        row.Payload = payload;
        row.BuiltAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The stored payload of one dataset; a query of its own so tests can check it translates to Azure SQL.</summary>
    internal static IQueryable<string> SnapshotPayloadQuery(AppDbContext db, string dataset) =>
        db.ResearchSnapshots.AsNoTracking()
            .Where(s => s.Dataset == dataset)
            .Select(s => s.Payload);
}
