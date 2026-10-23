using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;

namespace PersonalFinance.ApiService.Features.GoogleDrive;

/// <summary>
/// Service interface for reading and parsing spreadsheet data from Google Drive.
/// Every read is for one Account (<c>userId</c>); a <c>connectionId</c> only resolves for that Account.
/// </summary>
public interface ISpreadsheetReader
{
    /// <summary>
    /// Reads and parses a spreadsheet from Google Drive (or database cache / sample data) into a monthly budget report with actual data.
    /// </summary>
    Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(
        string userId,
        string fileId,
        string? fileName = null,
        int? connectionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the transaction log (e.g. the "Transactions" sheet) from a spreadsheet on Google Drive.
    /// </summary>
    Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(
        string userId,
        string fileId,
        string? fileName = null,
        int? connectionId = null,
        CancellationToken cancellationToken = default);
}
