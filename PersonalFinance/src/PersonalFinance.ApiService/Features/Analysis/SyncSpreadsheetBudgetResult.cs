using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Features.Analysis;

/// <summary>
///     Result of a sync operation.
/// </summary>
public sealed class SyncSpreadsheetBudgetResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary><see cref="ErrorMessage"/> keyed, so the Web app can show it in the reader's language (#405).</summary>
    public ApiErrorEntryDto? Error { get; set; }

    public bool HasConflict { get; set; }
    public List<MonthConflictInfo> Conflicts { get; set; } = new();
    public int SnapshotsCreated { get; set; }
    public int TransactionsCreated { get; set; }

    /// <summary>A failed sync and why, in English and keyed.</summary>
    public static SyncSpreadsheetBudgetResult Failed(ApiErrorEntryDto error) => new()
    {
        Success = false,
        ErrorMessage = error.Message,
        Error = error
    };
}
