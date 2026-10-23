using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.Analysis;

/// <summary>
///     Information about a conflicting month during sync.
/// </summary>
public sealed class MonthConflictInfo
{
    public int SnapshotId { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}
