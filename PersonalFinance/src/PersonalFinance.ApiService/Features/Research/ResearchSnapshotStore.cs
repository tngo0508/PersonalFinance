using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The stored <see cref="ResearchSnapshot"/> rows, one per dataset (<see cref="ResearchDatasets"/>), each holding its
/// snapshot as JSON.
/// </summary>
internal sealed class ResearchSnapshotStore
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ResearchSnapshotStore(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <summary>The dataset's stored payload, or null when no refresh has stored it yet.</summary>
    public async Task<T?> ReadAsync<T>(string dataset, CancellationToken cancellationToken) where T : class
    {
        string? payload = await PayloadQuery(_db, dataset).FirstOrDefaultAsync(cancellationToken);
        return payload is null ? null : JsonSerializer.Deserialize<T>(payload, JsonSerializerOptions.Web);
    }

    /// <summary>Every stored dataset's SourceAsOf and BuiltAt, ordered by name; the payloads are not read.</summary>
    public Task<List<ResearchDatasetStatusDto>> ReadStatusAsync(CancellationToken cancellationToken) =>
        ResearchStatusQuery.Build(_db).ToListAsync(cancellationToken);

    /// <summary>
    /// Replaces the dataset's row with the new payload, or adds it on the first store. A save that fails leaves
    /// nothing tracked, so the next dataset's store does not try to save this one's changes again.
    /// </summary>
    public async Task StoreAsync<T>(string dataset, string firstPeriod, string lastPeriod, DateOnly sourceAsOf,
        T payload, CancellationToken cancellationToken)
    {
        try
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
            row.Payload = JsonSerializer.Serialize(payload, JsonSerializerOptions.Web);
            row.BuiltAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>The stored payload of one dataset; a query of its own so tests can check it translates to Azure SQL.</summary>
    internal static IQueryable<string> PayloadQuery(AppDbContext db, string dataset) =>
        db.ResearchSnapshots.AsNoTracking()
            .Where(s => s.Dataset == dataset)
            .Select(s => s.Payload);
}
