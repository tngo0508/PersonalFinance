using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.Currency;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Analysis;

/// <summary>
///     API controller for spreadsheet analysis operations.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SpreadsheetAnalysisController : ControllerBase
{
    private readonly ISpreadsheetAnalysisService _analysisService;
    private readonly IBudgetDataSourceService _dataSourceService;
    private readonly IGoogleSheetRemovalService _removalService;
    private readonly ILogger<SpreadsheetAnalysisController> _logger;
    private readonly ISpreadsheetReader _spreadsheetReader;
    private readonly ICurrencyConverter _currencyConverter;

    public SpreadsheetAnalysisController(
        ISpreadsheetAnalysisService analysisService,
        ISpreadsheetReader spreadsheetReader,
        ILogger<SpreadsheetAnalysisController> logger,
        IBudgetDataSourceService dataSourceService,
        IGoogleSheetRemovalService removalService,
        ICurrencyConverter currencyConverter)
    {
        _analysisService = analysisService;
        _dataSourceService = dataSourceService;
        _removalService = removalService;
        _spreadsheetReader = spreadsheetReader;
        _logger = logger;
        _currencyConverter = currencyConverter;
    }

    /// <summary>
    ///     Gets the full analysis dashboard data for a selected period.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        List<int> availableYears = await _analysisService.GetAvailableYearsAsync(userId, cancellationToken);
        BudgetDataSource activeSource = await _analysisService.GetActiveSourceAsync(userId, cancellationToken);
        List<BudgetDataSourceSummaryDto> sourceSummaries =
            await _dataSourceService.GetDataSummariesAsync(userId, cancellationToken);
        string reportingCurrency = await _currencyConverter.GetReportingCurrencyAsync(userId, cancellationToken);

        if (availableYears.Count == 0)
        {
            return Ok(new AnalysisDashboardResult
            {
                ReportingCurrency = reportingCurrency,
                HasData = false,
                Year = year ?? 0,
                Month = month,
                AvailableYears = availableYears,
                AvailableMonths = new List<int>(),
                ActiveSource = activeSource,
                SourceSummaries = sourceSummaries
            });
        }

        int selectedYear = (year ?? 0) != 0 ? (year ?? 0) : availableYears.First();
        List<int> availableMonths = await _analysisService.GetAvailableMonthsAsync(userId, selectedYear, cancellationToken);
        SummaryStatisticsDto? summary = await _analysisService.GetSummaryStatisticsAsync(userId, selectedYear, month, cancellationToken);
        SpendingVelocityDto? velocity = await _analysisService.GetSpendingVelocityAsync(userId, selectedYear, month, cancellationToken);
        TrendDataDto trends = await _analysisService.GetTrendDataAsync(userId, selectedYear, cancellationToken);
        CategoryInsightsDto? categories =
            await _analysisService.GetCategoryInsightsAsync(userId, selectedYear, month, cancellationToken);
        BudgetVarianceDto? variance = await _analysisService.GetBudgetVarianceAsync(userId, selectedYear, month, cancellationToken);

        return Ok(new AnalysisDashboardResult
        {
            ReportingCurrency = reportingCurrency,
            Conversion = ConversionDto.Combine([summary?.Conversion, categories?.Conversion]),
            HasData = summary != null,
            Year = selectedYear,
            Month = month,
            AvailableYears = availableYears,
            AvailableMonths = availableMonths,
            ActiveSource = activeSource,
            SourceSummaries = sourceSummaries,
            Summary = summary,
            Velocity = velocity,
            Trends = trends,
            Categories = categories,
            Variance = variance
        });
    }

    /// <summary>
    ///     Syncs budget data from a Google Drive spreadsheet to the database.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromBody] SyncSpreadsheetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} requested sync for spreadsheet {FileId}", userId, request.FileId);
        }

        SyncSpreadsheetBudgetResult result = await _analysisService.SyncSpreadsheetBudgetAsync(userId, request.ConnectionId, request.FileId,
            request.FileName, request.Overwrite ?? false, cancellationToken);
        return Ok(new SyncSpreadsheetResponseDto
        {
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            Error = result.Error,
            HasConflict = result.HasConflict,
            Conflicts = result.Conflicts.Select(c => new MonthConflictDto
            {
                SnapshotId = c.SnapshotId,
                MonthNumber = c.MonthNumber,
                MonthName = c.MonthName,
                Year = c.Year,
                SyncedAtUtc = c.SyncedAtUtc
            }).ToList(),
            SnapshotsCreated = result.SnapshotsCreated,
            TransactionsCreated = result.TransactionsCreated
        });
    }

    /// <summary>
    ///     Removes (soft-deletes) all of the user's synced Google Sheet data, keeping the Google Drive connection:
    ///     the Data Source Removal of ADR 0002.
    /// </summary>
    [HttpDelete("data")]
    [ProducesResponseType(typeof(BudgetDataSourceSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveSyncedData(CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        BudgetDataSourceSummaryDto removed = await _removalService.RemoveAsync(userId, cancellationToken);
        return Ok(removed);
    }

    /// <summary>
    ///     Gets available years for the current user.
    /// </summary>
    [HttpGet("years")]
    public async Task<IActionResult> GetAvailableYears(CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        List<int> years = await _analysisService.GetAvailableYearsAsync(userId, cancellationToken);
        return Ok(years);
    }

    /// <summary>
    ///     Gets available months for a given year.
    /// </summary>
    [HttpGet("months/{year}")]
    public async Task<IActionResult> GetAvailableMonths(int year, CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        List<int> months = await _analysisService.GetAvailableMonthsAsync(userId, year, cancellationToken);
        return Ok(months);
    }

    /// <summary>
    ///     Reads and parses a spreadsheet from Google Drive into a monthly budget report with actual data.
    /// </summary>
    [HttpGet("spreadsheet-report")]
    [ProducesResponseType(typeof(MonthlyBudgetReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MonthlyBudgetReportDto>> GetSpreadsheetBudgetReport(
        [FromQuery] string fileId,
        [FromQuery] string? fileName = null,
        [FromQuery] int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId) && string.IsNullOrWhiteSpace(fileName))
        {
            throw new ApiErrorException(ApiErrorKeys.Analysis.FileRequired, "fileId or fileName is required.", "fileId");
        }

        string userId = User.GetUserId();
        MonthlyBudgetReportDto report = await _spreadsheetReader.GetSpreadsheetBudgetReportAsync(
            userId, fileId, fileName, connectionId, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    ///     Reads the transaction log (e.g. the "Transactions" sheet) from a spreadsheet on Google Drive.
    /// </summary>
    [HttpGet("spreadsheet-transactions")]
    [ProducesResponseType(typeof(SpreadsheetTransactionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SpreadsheetTransactionsDto>> GetSpreadsheetTransactions(
        [FromQuery] string fileId,
        [FromQuery] string? fileName = null,
        [FromQuery] int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new ApiErrorException(ApiErrorKeys.Analysis.FileIdRequired, "fileId is required.", "fileId");
        }

        string userId = User.GetUserId();
        SpreadsheetTransactionsDto transactions = await _spreadsheetReader.GetSpreadsheetTransactionsAsync(
            userId, fileId, fileName, connectionId, cancellationToken);
        return Ok(transactions);
    }
}
