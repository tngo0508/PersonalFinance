using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for Spreadsheet Analysis (synced Google Sheet budgets).
/// </summary>
public interface ISpreadsheetAnalysisApi
{
    /// <summary>
    ///     Gets the full analysis dashboard data for a selected period.
    /// </summary>
    [Get("/api/SpreadsheetAnalysis/dashboard")]
    Task<AnalysisDashboardDto> GetAnalysisDashboardAsync(
        [Query] int? year = null,
        [Query] int? month = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Syncs budget data from a Google Drive spreadsheet to the database.
    /// </summary>
    [Post("/api/SpreadsheetAnalysis/sync")]
    Task<SyncSpreadsheetResponseDto> SyncSpreadsheetAsync(
        [Body] SyncSpreadsheetRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets available years for the current user.
    /// </summary>
    [Get("/api/SpreadsheetAnalysis/years")]
    Task<List<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets available months for a given year.
    /// </summary>
    [Get("/api/SpreadsheetAnalysis/months/{year}")]
    Task<List<int>> GetAvailableMonthsAsync(int year, CancellationToken cancellationToken = default);
}
