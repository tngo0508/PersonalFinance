using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.Analysis;

/// <summary>
///     Result of a sync operation.
/// </summary>
public sealed class SyncSpreadsheetBudgetResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasConflict { get; set; }
    public List<MonthConflictInfo> Conflicts { get; set; } = new();
    public int SnapshotsCreated { get; set; }
    public int TransactionsCreated { get; set; }
}
