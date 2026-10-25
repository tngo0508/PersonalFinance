using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.Analysis;

/// <summary>
///     The Data Source Removal of the Google Sheet (GLOSSARY.md, ADR 0002 section 4).
/// </summary>
public interface IGoogleSheetRemovalService
{
    /// <summary>
    ///     Soft-deletes all of the user's synced months and transaction records, leaving the Google Drive connection
    ///     in place, and returns what was removed.
    /// </summary>
    Task<BudgetDataSourceSummaryDto> RemoveAsync(string userId, CancellationToken cancellationToken = default);
}
