using System.Text;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;

namespace PersonalFinance.ApiService.Features.GoogleDrive;

/// <summary>
/// Reads budget reports and transaction logs from an Account's Google Drive spreadsheets: downloads through
/// <see cref="IGoogleDriveFileSource"/>, parses with <see cref="BudgetSpreadsheetParser"/>, and returns the first
/// download that yields data. When nothing can be read the result says why; figures are never made up.
/// </summary>
internal sealed class SpreadsheetReaderService : ISpreadsheetReader
{
    private const string SampleDataSource = "Google Drive Spreadsheet (Actual Sample Data)";
    private const string SampleFileName = "Monthly budget 2026.xlsx";

    private readonly IGoogleDriveFileSource _fileSource;
    private readonly ILogger<SpreadsheetReaderService> _logger;

    public SpreadsheetReaderService(IGoogleDriveFileSource fileSource, ILogger<SpreadsheetReaderService> logger)
    {
        _fileSource = fileSource;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(
        string userId,
        string fileId,
        string? fileName = null,
        int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return BudgetSpreadsheetParser.Unavailable(fileName, fileId, DriveResultMessages.NoFileSelected);
        }

        if (IsSampleFile(fileId))
        {
            return BudgetSpreadsheetParser.ParseSpreadsheetBudgetReport(
                SampleSpreadsheets.GetSampleMonthlyBudgetSpreadsheetCsv(), fileName ?? SampleFileName, fileId,
                SampleDataSource);
        }

        MonthlyBudgetReportDto? unreadable = null;
        await foreach (SpreadsheetDownload download in _fileSource.DownloadAsync(userId, fileId, fileName, connectionId, cancellationToken))
        {
            MonthlyBudgetReportDto report = download.Format == SpreadsheetFormat.Xlsx
                ? BudgetSpreadsheetParser.ParseXlsxBudgetReport(new MemoryStream(download.Content), download.FileName,
                    fileId, download.DataSource)
                : BudgetSpreadsheetParser.ParseSpreadsheetBudgetReport(ReadText(download.Content), download.FileName,
                    fileId, download.DataSource);

            if (report.Months.Count > 0)
            {
                return report;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("{Format} copy of spreadsheet '{FileId}' had no budget data: {Reason}",
                    download.Format, fileId, report.ErrorMessage);
            }
            unreadable = report;
        }

        return unreadable ?? BudgetSpreadsheetParser.Unavailable(fileName, fileId, DriveResultMessages.DownloadFailed);
    }

    /// <inheritdoc />
    public async Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(
        string userId,
        string fileId,
        string? fileName = null,
        int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (IsSampleFile(fileId))
        {
            return BudgetSpreadsheetParser.ParseCsvTransactions(
                SampleSpreadsheets.GetSampleTransactionsCsv(), fileName ?? SampleFileName, fileId, SampleDataSource);
        }

        SpreadsheetTransactionsDto? unreadable = null;
        await foreach (SpreadsheetDownload download in _fileSource.DownloadAsync(userId, fileId, fileName, connectionId, cancellationToken))
        {
            SpreadsheetTransactionsDto result = download.Format == SpreadsheetFormat.Xlsx
                ? BudgetSpreadsheetParser.ParseXlsxTransactions(new MemoryStream(download.Content), download.FileName,
                    fileId, download.DataSource)
                : BudgetSpreadsheetParser.ParseCsvTransactions(ReadText(download.Content), download.FileName, fileId,
                    download.DataSource);

            if (result.Transactions.Count > 0)
            {
                return result;
            }

            // A workbook that opened fine but has no transactions table will not do better as a CSV export,
            // which only contains its first sheet.
            if (download.Format == SpreadsheetFormat.Xlsx && string.IsNullOrEmpty(result.ErrorMessage))
            {
                BudgetSpreadsheetParser.SetError(result, DriveResultMessages.NoTransactionsSheet);
                return result;
            }

            unreadable = result;
        }

        return unreadable ?? new SpreadsheetTransactionsDto
        {
            FileName = fileName ?? string.Empty,
            FileId = fileId,
            IsLiveSpreadsheetData = false,
            DataSource = "Unavailable",
            ErrorMessage = DriveResultMessages.DownloadFailed.Message,
            Error = DriveResultMessages.DownloadFailed
        };
    }

    private static bool IsSampleFile(string? fileId)
    {
        return fileId?.StartsWith("sample-", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>Decodes CSV bytes, honouring a byte-order mark (UTF-8 when there is none).</summary>
    private static string ReadText(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
