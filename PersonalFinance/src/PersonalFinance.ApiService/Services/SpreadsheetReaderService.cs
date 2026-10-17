using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Data.Helpers;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service implementation for reading and parsing spreadsheet data from Google Drive.
/// Handles downloading spreadsheets (XLSX, CSV, Google Sheets export), parsing budget reports,
/// and extracting transaction records.
/// </summary>
public class SpreadsheetReaderService : ISpreadsheetReader
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SpreadsheetReaderService> _logger;

    public SpreadsheetReaderService(
        HttpClient httpClient,
        IConfiguration configuration,
        AppDbContext dbContext,
        ILogger<SpreadsheetReaderService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(
        string fileId,
        string? fileName = null,
        int? connectionId = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return GoogleDriveHelper.GenerateMonthlyBudgetReport(fileName, fileId);
        }

        _logger.LogInformation("Generating monthly budget report from spreadsheet file ID '{FileId}', fileName '{FileName}'...", fileId, fileName);

        // Check if sample / preview file
        if (fileId.StartsWith("sample-", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("File '{FileId}' is sample data. Parsing actual sample spreadsheet content...", fileId);
            var sampleCsv = GoogleDriveHelper.GetSampleMonthlyBudgetSpreadsheetCsv();
            var sampleReport = GoogleDriveHelper.ParseSpreadsheetBudgetReport(
                sampleCsv,
                fileName ?? "Monthly budget 2026.xlsx",
                fileId,
                dataSource: "Google Drive Spreadsheet (Actual Sample Data)");
            return sampleReport;
        }

        var (actualFileName, mimeType, isGoogleSheet, apiKey) =
            await ResolveSpreadsheetSourceAsync(fileId, fileName, "Monthly budget.xlsx", connectionId, userId, cancellationToken);

        // 1. Attempt Live Download / Export from Google Drive
        try
        {
            // Option A: Google Sheet -> Export to XLSX first (to capture all sheets including Summary & Transactions), fallback to CSV
            if (isGoogleSheet)
            {
                var xlsxExportUrl = !string.IsNullOrWhiteSpace(apiKey)
                    ? $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}/export?mimeType=application%2Fvnd.openxmlformats-officedocument.spreadsheetml.sheet&key={Uri.EscapeDataString(apiKey)}"
                    : $"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(fileId)}/export?format=xlsx";

                try
                {
                    _logger.LogInformation("Exporting Google Spreadsheet '{FileId}' to XLSX via '{Url}'...", fileId, xlsxExportUrl);
                    using var xlsxResponse = await _httpClient.GetAsync(xlsxExportUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    if (xlsxResponse.IsSuccessStatusCode)
                    {
                        await using var stream = await xlsxResponse.Content.ReadAsStreamAsync(cancellationToken);
                        var report = GoogleDriveHelper.ParseXlsxBudgetReport(
                            stream,
                            actualFileName,
                            fileId,
                            dataSource: "Google Drive Live Spreadsheet (Exported XLSX)");

                        if (report.IsLiveSpreadsheetData)
                        {
                            return report;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "XLSX export for Google Sheet '{FileId}' failed or not binary archive. Falling back to CSV export.", fileId);
                }

                // Fallback to CSV if XLSX export is unavailable or fails
                var exportUrl = !string.IsNullOrWhiteSpace(apiKey)
                    ? $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}/export?mimeType=text%2Fcsv&key={Uri.EscapeDataString(apiKey)}"
                    : $"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(fileId)}/export?format=csv";

                _logger.LogInformation("Exporting Google Spreadsheet '{FileId}' to CSV via '{Url}'...", fileId, exportUrl);
                using var response = await _httpClient.GetAsync(exportUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var csvContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(csvContent))
                    {
                        var report = GoogleDriveHelper.ParseSpreadsheetBudgetReport(
                            csvContent,
                            actualFileName,
                            fileId,
                            dataSource: "Google Drive Live Spreadsheet (Exported Data)");
                        return report;
                    }
                }
            }

            // Option B: Excel / CSV Binary File Download
            var isXlsx = actualFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                         mimeType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var downloadUrl = !string.IsNullOrWhiteSpace(apiKey)
                ? $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}?alt=media&key={Uri.EscapeDataString(apiKey)}"
                : $"https://drive.google.com/uc?export=download&id={Uri.EscapeDataString(fileId)}";

            _logger.LogInformation("Downloading spreadsheet file '{FileId}' via '{Url}'...", fileId, downloadUrl);
            using var fileResponse = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (fileResponse.IsSuccessStatusCode)
            {
                if (isXlsx)
                {
                    await using var stream = await fileResponse.Content.ReadAsStreamAsync(cancellationToken);
                    var report = GoogleDriveHelper.ParseXlsxBudgetReport(
                        stream,
                        actualFileName,
                        fileId,
                        dataSource: "Google Drive Live XLSX (Actual Data)");
                    return report;
                }
                else
                {
                    var csvContent = await fileResponse.Content.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(csvContent))
                    {
                        var report = GoogleDriveHelper.ParseSpreadsheetBudgetReport(
                            csvContent,
                            actualFileName,
                            fileId,
                            dataSource: "Google Drive Live CSV (Actual Data)");
                        return report;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live download of spreadsheet file '{FileId}' failed. Falling back to structured budget report.", fileId);
        }

        // Fallback to structured report calculation
        return GoogleDriveHelper.GenerateMonthlyBudgetReport(actualFileName, fileId);
    }

    /// <inheritdoc />
    public async Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(
        string fileId,
        string? fileName = null,
        int? connectionId = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reading transactions from spreadsheet file ID '{FileId}', fileName '{FileName}'...", fileId, fileName);

        if (fileId.StartsWith("sample-", StringComparison.OrdinalIgnoreCase))
        {
            return GoogleDriveHelper.ParseCsvTransactions(
                GoogleDriveHelper.GetSampleTransactionsCsv(),
                fileName ?? "Monthly budget 2026.xlsx",
                fileId,
                dataSource: "Google Drive Spreadsheet (Actual Sample Data)");
        }

        var (actualFileName, mimeType, isGoogleSheet, apiKey) =
            await ResolveSpreadsheetSourceAsync(fileId, fileName, "Spreadsheet.xlsx", connectionId, userId, cancellationToken);

        string? xlsxParseError = null;
        try
        {
            // Google Sheet: the XLSX export keeps every sheet; the CSV export only contains the first one
            if (isGoogleSheet)
            {
                var xlsxExportUrl = !string.IsNullOrWhiteSpace(apiKey)
                    ? $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}/export?mimeType=application%2Fvnd.openxmlformats-officedocument.spreadsheetml.sheet&key={Uri.EscapeDataString(apiKey)}"
                    : $"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(fileId)}/export?format=xlsx";

                using var xlsxResponse = await _httpClient.GetAsync(xlsxExportUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (xlsxResponse.IsSuccessStatusCode)
                {
                    await using var stream = await xlsxResponse.Content.ReadAsStreamAsync(cancellationToken);
                    var result = GoogleDriveHelper.ParseXlsxTransactions(stream, actualFileName, fileId, "Google Drive Live Spreadsheet (Exported XLSX)");
                    if (result.Transactions.Count > 0)
                    {
                        return result;
                    }
                    // XLSX export succeeded but no transactions found — preserve the parse error for diagnostics
                    xlsxParseError = result.ErrorMessage;
                    _logger.LogInformation("XLSX export of Google Sheet '{FileId}' succeeded but found no transactions. {ParseError}", fileId, xlsxParseError);
                }
                else
                {
                    _logger.LogWarning("XLSX export of Google Sheet '{FileId}' returned HTTP {StatusCode}.", fileId, (int)xlsxResponse.StatusCode);
                }

                // CSV export only contains the first sheet — it cannot find a Transactions sheet
                // that lives on a different sheet, so skip it when XLSX already parsed without error.
                if (string.IsNullOrEmpty(xlsxParseError))
                {
                    // XLSX parsed fine but found no matching sheet/headers — CSV won't help either
                    // (it only has the first sheet). Return the XLSX parse error directly.
                    return new SpreadsheetTransactionsDto
                    {
                        FileName = actualFileName,
                        FileId = fileId,
                        IsLiveSpreadsheetData = true,
                        DataSource = "Google Drive Live Spreadsheet (Exported XLSX)",
                        ErrorMessage = "No transactions table was found in any sheet. Ensure the spreadsheet has a sheet named 'Transactions' with Date and Amount column headers."
                    };
                }

                // XLSX export failed — try CSV as a last resort
                var csvExportUrl = !string.IsNullOrWhiteSpace(apiKey)
                    ? $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}/export?mimeType=text%2Fcsv&key={Uri.EscapeDataString(apiKey)}"
                    : $"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(fileId)}/export?format=csv";

                using var csvResponse = await _httpClient.GetAsync(csvExportUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (csvResponse.IsSuccessStatusCode)
                {
                    var csvContent = await csvResponse.Content.ReadAsStringAsync(cancellationToken);
                    var csvResult = GoogleDriveHelper.ParseCsvTransactions(csvContent, actualFileName, fileId, "Google Drive Live Spreadsheet (Exported Data)");
                    if (csvResult.Transactions.Count > 0)
                    {
                        return csvResult;
                    }
                }
            }
            else
            {
                var isXlsx = actualFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                             mimeType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                var downloadUrl = !string.IsNullOrWhiteSpace(apiKey)
                    ? $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}?alt=media&key={Uri.EscapeDataString(apiKey)}"
                    : $"https://drive.google.com/uc?export=download&id={Uri.EscapeDataString(fileId)}";

                using var fileResponse = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (fileResponse.IsSuccessStatusCode)
                {
                    if (isXlsx)
                    {
                        await using var stream = await fileResponse.Content.ReadAsStreamAsync(cancellationToken);
                        return GoogleDriveHelper.ParseXlsxTransactions(stream, actualFileName, fileId, "Google Drive Live XLSX (Actual Data)");
                    }

                    var csvContent = await fileResponse.Content.ReadAsStringAsync(cancellationToken);
                    return GoogleDriveHelper.ParseCsvTransactions(csvContent, actualFileName, fileId, "Google Drive Live CSV (Actual Data)");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live download of spreadsheet file '{FileId}' for transactions failed.", fileId);
        }

        // No fabricated fallback here: made-up transactions would be indistinguishable from real ones
        var baseError = "The spreadsheet could not be downloaded from Google Drive. Check that the drive is still shared and your API key is valid.";
        return new SpreadsheetTransactionsDto
        {
            FileName = actualFileName,
            FileId = fileId,
            IsLiveSpreadsheetData = false,
            DataSource = "Unavailable",
            ErrorMessage = baseError
        };
    }

    /// <summary>
    /// Looks up the connection's cached file metadata and API key needed to download a spreadsheet.
    /// </summary>
    private async Task<(string FileName, string MimeType, bool IsGoogleSheet, string? ApiKey)> ResolveSpreadsheetSourceAsync(
        string fileId,
        string? fileName,
        string defaultFileName,
        int? connectionId,
        string? userId,
        CancellationToken cancellationToken)
    {
        GoogleDriveConnection? connection = null;
        if (connectionId.HasValue && connectionId.Value > 0)
        {
            connection = await _dbContext.GoogleDriveConnections
                .Include(c => c.CachedFiles)
                .FirstOrDefaultAsync(c => c.Id == connectionId.Value && (userId == null || c.UserId == userId), cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(userId))
        {
            connection = await _dbContext.GoogleDriveConnections
                .Include(c => c.CachedFiles)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.CachedFiles.Any(f => f.DriveFileId == fileId), cancellationToken);
        }

        var cachedFile = connection?.CachedFiles.FirstOrDefault(f => f.DriveFileId == fileId);
        var actualFileName = fileName ?? cachedFile?.Name ?? defaultFileName;
        var mimeType = cachedFile?.MimeType ?? string.Empty;

        var apiKey = connection != null && !string.IsNullOrWhiteSpace(connection.EncryptedApiKey)
            ? CredentialProtector.Decrypt(connection.EncryptedApiKey)
            : _configuration["GoogleDrive:ApiKey"]?.Trim();

        var isGoogleSheet = mimeType == "application/vnd.google-apps.spreadsheet" ||
                            Path.GetExtension(actualFileName).Equals(".gsheet", StringComparison.OrdinalIgnoreCase) ||
                            (string.IsNullOrEmpty(Path.GetExtension(actualFileName)) && cachedFile?.FileType == "Spreadsheet");

        return (actualFileName, mimeType, isGoogleSheet, apiKey);
    }
}
