using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// <see cref="IResearchService"/> over the stored <see cref="ResearchSnapshot"/> rows and the SEC EDGAR client.
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

    private readonly AppDbContext _db;
    private readonly ISecEdgarClient _secClient;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ResearchService> _logger;

    public ResearchService(AppDbContext db, ISecEdgarClient secClient, ResearchSettings settings,
        TimeProvider timeProvider, ILogger<ResearchService> logger)
    {
        _db = db;
        _secClient = secClient;
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
    public async Task<ResearchRefreshSummary> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var summary = new ResearchRefreshSummary();
        try
        {
            await RefreshSectorsAsync(cancellationToken);
            summary.Built.Add(SectorsDataset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The source is down or answered something unreadable: keep the previous snapshot.
            _logger.LogError(ex, "Could not rebuild the Research {Dataset} snapshot; the previous one is kept",
                SectorsDataset);
            summary.Failed.Add(SectorsDataset);
        }

        return summary;
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

        List<SectorsRankedYear> ranked = SectorsSnapshotBuilder.Rank(factsByYear);
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
