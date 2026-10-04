using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Google Drive management and folder explorer feature.
/// Supports multi-drive connections, automatic loading of cached data on subsequent logins,
/// incremental synchronization, manual refresh, removal, and credential re-authentication.
/// </summary>
[Authorize]
public class GoogleDriveController : Controller
{
    private readonly IGoogleDriveApi _googleDriveApi;
    private readonly ILogger<GoogleDriveController> _logger;

    public GoogleDriveController(
        IGoogleDriveApi googleDriveApi,
        ILogger<GoogleDriveController> logger)
    {
        _googleDriveApi = googleDriveApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Google Drive dashboard.
    /// Loads saved configurations from SQLite, skipping setup when a connection exists.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int? connectionId,
        [FromQuery] string? folderUrl,
        [FromQuery] string? apiKey,
        [FromQuery] bool addNew = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var model = new GoogleDriveViewModel
        {
            StatusMessage = TempData["StatusMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string
        };

        try
        {
            // 1. Load all saved connections for this user from SQLite
            var connections = await _googleDriveApi.GetUserConnectionsAsync(userId, cancellationToken);
            model.Connections = connections ?? new List<GoogleDriveConnectionDto>();

            // 2. Handle explicit "Add New Drive" request
            if (addNew || (!model.Connections.Any() && string.IsNullOrWhiteSpace(folderUrl)))
            {
                model.ShowConnectForm = true;
                return View(model);
            }

            // 3. Handle direct explore link via query string (if provided)
            if (!string.IsNullOrWhiteSpace(folderUrl))
            {
                model.FolderUrl = folderUrl;
                model.ApiKey = apiKey;
                return await ConnectAndExploreAsync(model, cancellationToken);
            }

            // 4. Subsequent login or normal view: Select connection and load from SQLite cache
            if (model.Connections.Any())
            {
                var selectedId = connectionId.HasValue && model.Connections.Any(c => c.Id == connectionId.Value)
                    ? connectionId.Value
                    : model.Connections.First().Id;

                model.SelectedConnectionId = selectedId;

                // Load cached files from SQLite (zero external API requests if fresh)
                var cachedResponse = await _googleDriveApi.GetConnectionFilesAsync(selectedId, userId, forceRefresh: false, cancellationToken);
                model.Response = cachedResponse;
                model.HasQueried = true;

                if (!cachedResponse.Success && !string.IsNullOrEmpty(cachedResponse.ErrorMessage))
                {
                    model.ErrorMessage = cachedResponse.ErrorMessage;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Google Drive connections for user {UserId}", userId);
            model.ErrorMessage = "An error occurred while loading your Google Drive connections.";
        }

        return View(model);
    }

    /// <summary>
    /// Handles form submission to connect a new Google Drive folder.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        GoogleDriveViewModel model,
        CancellationToken cancellationToken = default)
    {
        return await ConnectAndExploreAsync(model, cancellationToken);
    }

    /// <summary>
    /// Manually refreshes / synchronizes files for a connected Drive from Google API.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} requested manual sync for Google Drive connection {ConnectionId}", userId, id);

        try
        {
            var syncResponse = await _googleDriveApi.SyncConnectionAsync(
                id,
                new SyncGoogleDriveRequestDto
                {
                    ConnectionId = id,
                    UserId = userId,
                    ForceRefresh = true
                },
                cancellationToken);

            if (syncResponse.Success)
            {
                TempData["StatusMessage"] = "Google Drive files synchronized successfully with local SQLite cache.";
            }
            else
            {
                TempData["ErrorMessage"] = syncResponse.ErrorMessage ?? "Synchronization completed with warnings.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing connection {ConnectionId}", id);
            TempData["ErrorMessage"] = "Failed to synchronize Google Drive files. Please check your credentials.";
        }

        return RedirectToAction(nameof(Index), new { connectionId = id });
    }

    /// <summary>
    /// Re-authenticates or updates API credentials for a connection.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reauth(
        int id,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} re-authenticating Google Drive connection {ConnectionId}", userId, id);

        try
        {
            var updated = await _googleDriveApi.ReauthConnectionAsync(
                id,
                new ReauthGoogleDriveRequestDto
                {
                    ConnectionId = id,
                    UserId = userId,
                    ApiKey = apiKey
                },
                cancellationToken);

            if (updated.IsValid)
            {
                TempData["StatusMessage"] = "Credentials updated and Google Drive re-synchronized successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = updated.ErrorMessage ?? "Credentials could not be verified.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-authenticating connection {ConnectionId}", id);
            TempData["ErrorMessage"] = "Failed to update Google Drive credentials. Please try again.";
        }

        return RedirectToAction(nameof(Index), new { connectionId = id });
    }

    /// <summary>
    /// Removes a connected Drive and deletes all locally cached data from SQLite.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} deleting Google Drive connection {ConnectionId}", userId, id);

        try
        {
            await _googleDriveApi.DeleteConnectionAsync(id, userId, cancellationToken);
            TempData["StatusMessage"] = "Google Drive connection and cached data removed successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting connection {ConnectionId}", id);
            TempData["ErrorMessage"] = "Failed to delete Google Drive connection.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Generates a monthly budget summary report and chart data by reading the spreadsheet from Google Drive.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> MonthlyBudgetReport(
        [FromQuery] string fileName,
        [FromQuery] string? fileId = null,
        [FromQuery] int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(fileId))
        {
            return BadRequest(new { error = "File name or file ID is required." });
        }

        var userId = GetCurrentUserId();

        try
        {
            if (!string.IsNullOrWhiteSpace(fileId))
            {
                var report = await _googleDriveApi.GetSpreadsheetBudgetReportAsync(fileId, fileName, connectionId, userId, cancellationToken);
                if (report != null)
                {
                    return Json(report);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read spreadsheet from Google Drive for fileId '{FileId}', using fallback summary.", fileId);
        }

        var fallbackReport = GoogleDriveHelper.GenerateMonthlyBudgetReport(fileName, fileId);
        return Json(fallbackReport);
    }

    private async Task<IActionResult> ConnectAndExploreAsync(
        GoogleDriveViewModel model,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        model.HasQueried = true;

        if (string.IsNullOrWhiteSpace(model.FolderUrl))
        {
            model.ErrorMessage = "Please enter a valid Google Drive folder URL or Folder ID.";
            model.ShowConnectForm = true;
            return View("Index", model);
        }

        try
        {
            var connection = await _googleDriveApi.ConnectDriveAsync(
                new ConnectGoogleDriveRequestDto
                {
                    UserId = userId,
                    FolderUrl = model.FolderUrl,
                    ConnectionName = model.ConnectionName,
                    ApiKey = model.ApiKey
                },
                cancellationToken);

            TempData["StatusMessage"] = $"Connected to '{connection.Name}' and cached {connection.CachedFilesCount} items in SQLite.";
            return RedirectToAction(nameof(Index), new { connectionId = connection.Id });
        }
        catch (Refit.ApiException apiEx)
        {
            _logger.LogError(apiEx, "ApiService returned HTTP {StatusCode} when connecting Google Drive.", apiEx.StatusCode);
            model.ErrorMessage = $"Backend service returned error: {(int)apiEx.StatusCode} ({apiEx.StatusCode})";
            model.ShowConnectForm = true;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "Unable to reach ApiService backend.");
            model.ErrorMessage = "Unable to connect to the ApiService backend. Please verify that the API service is running.";
            model.ShowConnectForm = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during Google Drive connect request.");
            model.ErrorMessage = "An unexpected error occurred while connecting Google Drive.";
            model.ShowConnectForm = true;
        }

        // Re-load existing connections for display on error
        try
        {
            model.Connections = await _googleDriveApi.GetUserConnectionsAsync(userId, cancellationToken) ?? new List<GoogleDriveConnectionDto>();
        }
        catch
        {
            // Ignore secondary error
        }

        return View("Index", model);
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.Identity?.Name
               ?? "default-user";
    }
}
