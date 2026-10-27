using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.GoogleDrive;

/// <summary>
/// RESTful API controller for Google Drive folder exploration, connection management,
/// database caching, incremental synchronization, and re-authentication.
/// Every connection is scoped to the authenticated Account; callers cannot name another Account.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GoogleDriveController : ControllerBase
{
    private readonly IDriveConnectionService _driveConnectionService;
    private readonly ILogger<GoogleDriveController> _logger;
    private readonly ISpreadsheetReader _spreadsheetReader;

    public GoogleDriveController(
        IDriveConnectionService driveConnectionService,
        ISpreadsheetReader spreadsheetReader,
        ILogger<GoogleDriveController> logger)
    {
        _driveConnectionService = driveConnectionService;
        _spreadsheetReader = spreadsheetReader;
        _logger = logger;
    }

    /// <summary>
    /// Explores a Google Drive folder and returns the list of files (standalone query without persistence).
    /// </summary>
    [HttpPost("explore")]
    [ProducesResponseType(typeof(GoogleDriveFolderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GoogleDriveFolderResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GoogleDriveFolderResponseDto>> Explore(
        [FromBody] GoogleDriveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.FolderUrl))
        {
            throw new ApiErrorException(ApiErrorKeys.GoogleDrive.FolderUrlRequired,
                "Please provide a valid Google Drive folder URL or Folder ID.", nameof(GoogleDriveRequestDto.FolderUrl));
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Processing Google Drive explore request for input '{Url}'", request.FolderUrl);
        }

        GoogleDriveFolderResponseDto result = await _driveConnectionService.GetFolderFilesAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all saved Google Drive connections for the authenticated Account.
    /// </summary>
    [HttpGet("connections")]
    [ProducesResponseType(typeof(List<GoogleDriveConnectionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GoogleDriveConnectionDto>>> GetConnections(
        CancellationToken cancellationToken = default)
    {
        List<GoogleDriveConnectionDto> connections = await _driveConnectionService.GetUserConnectionsAsync(User.GetUserId(), cancellationToken);
        return Ok(connections);
    }

    /// <summary>
    /// Retrieves files for a specific connection from database cache (or syncs if stale or requested).
    /// </summary>
    [HttpGet("connections/{connectionId:int}")]
    [ProducesResponseType(typeof(GoogleDriveFolderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoogleDriveFolderResponseDto>> GetConnectionFiles(
        int connectionId,
        [FromQuery] bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        GoogleDriveFolderResponseDto result =
            await _driveConnectionService.GetConnectionFilesAsync(connectionId, User.GetUserId(), forceRefresh,
                cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Connects and persists a new or existing Google Drive folder connection.
    /// </summary>
    [HttpPost("connections")]
    [ProducesResponseType(typeof(GoogleDriveConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GoogleDriveConnectionDto>> Connect(
        [FromBody] ConnectGoogleDriveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.FolderUrl))
        {
            throw new ApiErrorException(ApiErrorKeys.GoogleDrive.FolderUrlRequired, "FolderUrl is required.",
                nameof(ConnectGoogleDriveRequestDto.FolderUrl));
        }

        GoogleDriveConnectionDto result = await _driveConnectionService.ConnectDriveAsync(User.GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Synchronizes a connected Google Drive folder with database cache incrementally.
    /// </summary>
    [HttpPost("connections/{connectionId:int}/sync")]
    [ProducesResponseType(typeof(GoogleDriveFolderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoogleDriveFolderResponseDto>> Sync(
        int connectionId,
        [FromBody] SyncGoogleDriveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        GoogleDriveFolderResponseDto result = await _driveConnectionService.SyncConnectionAsync(connectionId, User.GetUserId(),
            request?.ForceRefresh ?? true, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Re-authenticates or updates credentials for a connection.
    /// </summary>
    [HttpPost("connections/{connectionId:int}/reauth")]
    [ProducesResponseType(typeof(GoogleDriveConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoogleDriveConnectionDto>> Reauth(
        int connectionId,
        [FromBody] ReauthGoogleDriveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        GoogleDriveConnectionDto result = await _driveConnectionService.ReauthConnectionAsync(connectionId, User.GetUserId(),
            request?.ApiKey, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Removes a connection and its locally cached files from database.
    /// </summary>
    [HttpDelete("connections/{connectionId:int}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> Delete(
        int connectionId,
        CancellationToken cancellationToken = default)
    {
        bool deleted = await _driveConnectionService.DeleteConnectionAsync(connectionId, User.GetUserId(), cancellationToken);
        if (!deleted)
        {
            throw DriveConnectionService.ConnectionNotFound();
        }

        return Ok(true);
    }

    /// <summary>
    /// Reads and parses a spreadsheet from Google Drive into a monthly budget report with actual data.
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
            throw new ApiErrorException(ApiErrorKeys.GoogleDrive.FileRequired, "fileId or fileName is required.", "fileId");
        }

        MonthlyBudgetReportDto report = await _spreadsheetReader.GetSpreadsheetBudgetReportAsync(
            User.GetUserId(), fileId, fileName, connectionId, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Reads the transaction log (e.g. the "Transactions" sheet) from a spreadsheet on Google Drive.
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
            throw new ApiErrorException(ApiErrorKeys.GoogleDrive.FileIdRequired, "fileId is required.", "fileId");
        }

        SpreadsheetTransactionsDto transactions = await _spreadsheetReader.GetSpreadsheetTransactionsAsync(
            User.GetUserId(), fileId, fileName, connectionId, cancellationToken);
        return Ok(transactions);
    }
}
