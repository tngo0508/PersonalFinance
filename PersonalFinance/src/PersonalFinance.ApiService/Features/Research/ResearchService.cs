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

        IReadOnlyDictionary<int, IReadOnlyList<PublicFloatFact>> cleaned =
            await CleanPublicFloatAsync(factsByYear, lastYear, cancellationToken);
        SectorsSnapshotDto snapshot = SectorsSnapshotBuilder.Build(cleaned)
            ?? throw new InvalidOperationException("SEC returned no Public Float for any year.");
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
