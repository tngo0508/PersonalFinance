using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// RESTful API controller for Google Drive folder exploration, connection management,
/// SQLite caching, incremental synchronization, and re-authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GoogleDriveController : ControllerBase
{
    private readonly IGoogleDriveService _googleDriveService;
    private readonly ILogger<GoogleDriveController> _logger;

    public GoogleDriveController(
        IGoogleDriveService googleDriveService,
        ILogger<GoogleDriveController> logger)
    {
        _googleDriveService = googleDriveService;
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
            return BadRequest(new GoogleDriveFolderResponseDto
            {
                Success = false,
                ErrorMessage = "Please provide a valid Google Drive folder URL or Folder ID."
            });
        }

        _logger.LogInformation("Processing Google Drive explore request for input '{Url}'", request.FolderUrl);

        var result = await _googleDriveService.GetFolderFilesAsync(request, cancellationToken);
        if (!result.Success && string.IsNullOrEmpty(result.FolderId))
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves all saved Google Drive connections for an authenticated user.
    /// </summary>
    [HttpGet("connections")]
    [ProducesResponseType(typeof(List<GoogleDriveConnectionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GoogleDriveConnectionDto>>> GetConnections(
        [FromQuery] string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Ok(new List<GoogleDriveConnectionDto>());
        }

        var connections = await _googleDriveService.GetUserConnectionsAsync(userId, cancellationToken);
        return Ok(connections);
    }

    /// <summary>
    /// Retrieves files for a specific connection from SQLite cache (or syncs if stale or requested).
    /// </summary>
    [HttpGet("connections/{connectionId:int}")]
    [ProducesResponseType(typeof(GoogleDriveFolderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoogleDriveFolderResponseDto>> GetConnectionFiles(
        int connectionId,
        [FromQuery] string userId,
        [FromQuery] bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest("User identifier is required.");
        }

        var result = await _googleDriveService.GetConnectionFilesAsync(connectionId, userId, forceRefresh, cancellationToken);
        if (!result.Success && result.ErrorMessage == "Google Drive connection not found.")
        {
            return NotFound(result);
        }

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
        if (string.IsNullOrWhiteSpace(request?.UserId) || string.IsNullOrWhiteSpace(request?.FolderUrl))
        {
            return BadRequest("UserId and FolderUrl are required.");
        }

        try
        {
            var result = await _googleDriveService.ConnectDriveAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException argEx)
        {
            return BadRequest(argEx.Message);
        }
    }

    /// <summary>
    /// Synchronizes a connected Google Drive folder with SQLite cache incrementally.
    /// </summary>
    [HttpPost("connections/{connectionId:int}/sync")]
    [ProducesResponseType(typeof(GoogleDriveFolderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoogleDriveFolderResponseDto>> Sync(
        int connectionId,
        [FromBody] SyncGoogleDriveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.UserId))
        {
            return BadRequest("UserId is required.");
        }

        var result = await _googleDriveService.SyncConnectionAsync(connectionId, request.UserId, request.ForceRefresh, cancellationToken);
        if (!result.Success && result.ErrorMessage == "Google Drive connection not found.")
        {
            return NotFound(result);
        }

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
        if (string.IsNullOrWhiteSpace(request?.UserId))
        {
            return BadRequest("UserId is required.");
        }

        try
        {
            var result = await _googleDriveService.ReauthConnectionAsync(connectionId, request.UserId, request.ApiKey, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Connection not found.");
        }
    }

    /// <summary>
    /// Removes a connection and its locally cached files from SQLite.
    /// </summary>
    [HttpDelete("connections/{connectionId:int}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> Delete(
        int connectionId,
        [FromQuery] string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest("UserId is required.");
        }

        var deleted = await _googleDriveService.DeleteConnectionAsync(connectionId, userId, cancellationToken);
        if (!deleted)
        {
            return NotFound(false);
        }

        return Ok(true);
    }
}
