using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;

namespace PersonalFinance.ApiService.SpreadsheetAnalysis.Services;

/// <summary>
/// Service interface for reading and parsing spreadsheet data from Google Drive.
/// </summary>
public interface ISpreadsheetReader
{
    /// <summary>
    /// Reads and parses a spreadsheet from Google Drive (or database cache / sample data) into a monthly budget report with actual data.
    /// </summary>
    Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(
        string fileId,
        string? fileName = null,
        int? connectionId = null,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the transaction log (e.g. the "Transactions" sheet) from a spreadsheet on Google Drive.
    /// </summary>
    Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(
        string fileId,
        string? fileName = null,
        int? connectionId = null,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
