using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.SpreadsheetAnalysis;
using PersonalFinance.ApiService.SpreadsheetAnalysis.Services;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;

namespace PersonalFinance.ApiService.SpreadsheetAnalysis;

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
    private readonly ISender _mediator;
    private readonly ISpreadsheetReader _spreadsheetReader;

    public SpreadsheetAnalysisController(
        ISpreadsheetAnalysisService analysisService,
        ISender mediator,
        ISpreadsheetReader spreadsheetReader,
        ILogger<SpreadsheetAnalysisController> logger)
    {
        _analysisService = analysisService;
        _mediator = mediator;
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
        var userId = GetCurrentUserId();
        var query = new GetAnalysisDashboardQuery
        {
            UserId = userId,
            Year = year ?? 0,
            Month = month
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    ///     Syncs budget data from a Google Drive spreadsheet to the database.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromBody] SyncSpreadsheetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} requested sync for spreadsheet {FileId}", userId, request.FileId);

        var command = new SyncSpreadsheetBudgetCommand
        {
            ConnectionId = request.ConnectionId,
            UserId = userId,
            FileId = request.FileId,
            FileName = request.FileName,
            Overwrite = request.Overwrite ?? false
        };

        var result = await _mediator.Send(command, cancellationToken);
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
        var userId = GetCurrentUserId();
        var years = await _analysisService.GetAvailableYearsAsync(userId, cancellationToken);
        return Ok(years);
    }

    /// <summary>
    ///     Gets available months for a given year.
    /// </summary>
    [HttpGet("months/{year}")]
    public async Task<IActionResult> GetAvailableMonths(int year, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var months = await _analysisService.GetAvailableMonthsAsync(userId, year, cancellationToken);
        return Ok(months);
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.Identity?.Name
               ?? "default-user";
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

        var userId = GetCurrentUserId();
        var report = await _spreadsheetReader.GetSpreadsheetBudgetReportAsync(
            fileId, fileName, connectionId, userId, cancellationToken);
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

        var userId = GetCurrentUserId();
        var transactions = await _spreadsheetReader.GetSpreadsheetTransactionsAsync(
            fileId, fileName, connectionId, userId, cancellationToken);
        return Ok(transactions);
    }
}