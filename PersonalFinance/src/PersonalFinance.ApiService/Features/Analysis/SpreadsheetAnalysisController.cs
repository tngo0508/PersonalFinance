using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.Contracts;
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
    private readonly ILogger<SpreadsheetAnalysisController> _logger;
    private readonly ISpreadsheetReader _spreadsheetReader;

    public SpreadsheetAnalysisController(
        ISpreadsheetAnalysisService analysisService,
        ISpreadsheetReader spreadsheetReader,
        ILogger<SpreadsheetAnalysisController> logger)
    {
        _analysisService = analysisService;
        _spreadsheetReader = spreadsheetReader;
        _logger = logger;
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

        if (availableYears.Count == 0)
        {
            return Ok(new AnalysisDashboardResult
            {
                HasData = false,
                Year = year ?? 0,
                Month = month,
                AvailableYears = availableYears,
                AvailableMonths = new List<int>(),
                ActiveSource = activeSource
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
            HasData = summary != null,
            Year = selectedYear,
            Month = month,
            AvailableYears = availableYears,
            AvailableMonths = availableMonths,
            ActiveSource = activeSource,
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
            return BadRequest("fileId or fileName is required.");
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
            return BadRequest("fileId is required.");
        }

        string userId = User.GetUserId();
        SpreadsheetTransactionsDto transactions = await _spreadsheetReader.GetSpreadsheetTransactionsAsync(
            userId, fileId, fileName, connectionId, cancellationToken);
        return Ok(transactions);
    }
}
