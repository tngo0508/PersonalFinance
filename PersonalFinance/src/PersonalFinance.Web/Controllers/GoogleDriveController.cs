using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Models.GoogleDrive;
using PersonalFinance.Web.Services;

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
    private readonly IStringLocalizer _localizer;
    private readonly ILogger<GoogleDriveController> _logger;
    private readonly IUserPreferenceService _preferences;

    public GoogleDriveController(
        IGoogleDriveApi googleDriveApi,
        ILogger<GoogleDriveController> logger,
        IStringLocalizer localizer,
        IUserPreferenceService preferences)
    {
        _googleDriveApi = googleDriveApi;
        _logger = logger;
        _localizer = localizer;
        _preferences = preferences;
    }

    /// <summary>
    /// Displays the Google Drive dashboard.
    /// Loads saved configurations from database, skipping setup when a connection exists.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int? connectionId,
        [FromQuery] string? folderUrl,
        [FromQuery] string? apiKey,
        [FromQuery] bool addNew = false,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        var model = new GoogleDriveViewModel
        {
            StatusMessage = TempData["StatusMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string
        };

        try
        {
            // The connect form asks for the spreadsheets' currency, the Reporting Currency by default (ADR 0016).
            model.CurrencyCode = await _preferences.GetReportingCurrencyAsync(userId, cancellationToken);

            // 1. Load all saved connections for this user from database
            List<GoogleDriveConnectionDto> connections = await _googleDriveApi.GetUserConnectionsAsync(cancellationToken);
            model.Connections = Localize(connections);

            // 2. Handle explicit "Add New Drive" request
            if (addNew || (model.Connections.Count == 0 && string.IsNullOrWhiteSpace(folderUrl)))
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

            // 4. Subsequent login or normal view: Select connection and load from database cache
            if (model.Connections.Count > 0)
            {
                int selectedId = connectionId.HasValue && model.Connections.Any(c => c.Id == connectionId.Value)
                    ? connectionId.Value
                    : model.Connections.First().Id;

                model.SelectedConnectionId = selectedId;

                // Load cached files from database (zero external API requests if fresh)
                GoogleDriveFolderResponseDto cachedResponse = GoogleDriveText.Localize(_localizer,
                    await _googleDriveApi.GetConnectionFilesAsync(selectedId, forceRefresh: false, cancellationToken));
                model.Response = cachedResponse;
                model.HasQueried = true;

                if (!cachedResponse.Success && !string.IsNullOrEmpty(cachedResponse.ErrorMessage))
                {
                    model.ErrorMessage = cachedResponse.ErrorMessage;
                }
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Loading Google Drive connections failed for user {UserId}", userId);
            model.ErrorMessage = ApiErrorMessages.ForUser(_localizer, ex, _localizer["GoogleDrive.Message.LoadFailed"]);
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
        string userId = User.GetUserId();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} requested manual sync for Google Drive connection {ConnectionId}", userId, id);
        }

        try
        {
            GoogleDriveFolderResponseDto syncResponse = GoogleDriveText.Localize(_localizer,
                await _googleDriveApi.SyncConnectionAsync(
                    id,
                    new SyncGoogleDriveRequestDto
                    {
                        ConnectionId = id,
                        ForceRefresh = true
                    },
                    cancellationToken));

            if (syncResponse.Success)
            {
                TempData["StatusMessage"] = _localizer["GoogleDrive.Message.Synced"].Value;
            }
            else
            {
                TempData["ErrorMessage"] = syncResponse.ErrorMessage ?? _localizer["GoogleDrive.Message.SyncWarnings"].Value;
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error synchronizing connection {ConnectionId}", id);
            TempData["ErrorMessage"] = ApiErrorMessages.ForUser(_localizer, ex, _localizer["GoogleDrive.Message.SyncFailed"]);
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
        string userId = User.GetUserId();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} re-authenticating Google Drive connection {ConnectionId}", userId, id);
        }

        try
        {
            GoogleDriveConnectionDto updated = GoogleDriveText.Localize(_localizer,
                await _googleDriveApi.ReauthConnectionAsync(
                    id,
                    new ReauthGoogleDriveRequestDto
                    {
                        ConnectionId = id,
                        ApiKey = apiKey
                    },
                    cancellationToken));

            if (updated.IsValid)
            {
                TempData["StatusMessage"] = _localizer["GoogleDrive.Message.Reauthed"].Value;
            }
            else
            {
                TempData["ErrorMessage"] = updated.ErrorMessage ?? _localizer["GoogleDrive.Message.ReauthUnverified"].Value;
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error re-authenticating connection {ConnectionId}", id);
            TempData["ErrorMessage"] = ApiErrorMessages.ForUser(_localizer, ex, _localizer["GoogleDrive.Message.ReauthFailed"]);
        }

        return RedirectToAction(nameof(Index), new { connectionId = id });
    }

    /// <summary>
    /// Removes a connected Drive and deletes all locally cached data from database.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} deleting Google Drive connection {ConnectionId}", userId, id);
        }

        try
        {
            await _googleDriveApi.DeleteConnectionAsync(id, cancellationToken);
            TempData["StatusMessage"] = _localizer["GoogleDrive.Message.Removed"].Value;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error deleting connection {ConnectionId}", id);
            TempData["ErrorMessage"] = ApiErrorMessages.ForUser(_localizer, ex, _localizer["GoogleDrive.Message.RemoveFailed"]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Returns the monthly budget report read from the spreadsheet on Google Drive. When the sheet cannot be read the
    /// report has no months and an <c>errorMessage</c>; figures are never made up.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> MonthlyBudgetReport(
        [FromQuery] string? fileName = null,
        [FromQuery] string? fileId = null,
        [FromQuery] int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return BadRequest(new { error = _localizer["GoogleDrive.Message.FileIdRequired"].Value });
        }

        MonthlyBudgetReportDto report = await _googleDriveApi.GetSpreadsheetBudgetReportAsync(fileId, fileName, connectionId, cancellationToken);
        return Json(GoogleDriveText.Localize(_localizer, report));
    }

    /// <summary>
    /// Returns the transaction log read from the spreadsheet's Transactions sheet.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> SpreadsheetTransactions(
        [FromQuery] string fileId,
        [FromQuery] string? fileName = null,
        [FromQuery] int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return BadRequest(new { error = _localizer["GoogleDrive.Message.FileIdRequired"].Value });
        }

        SpreadsheetTransactionsDto result = await _googleDriveApi.GetSpreadsheetTransactionsAsync(fileId, fileName, connectionId, cancellationToken);
        return Json(GoogleDriveText.Localize(_localizer, result));
    }

    private async Task<IActionResult> ConnectAndExploreAsync(
        GoogleDriveViewModel model,
        CancellationToken cancellationToken)
    {
        model.HasQueried = true;

        if (string.IsNullOrWhiteSpace(model.FolderUrl))
        {
            model.ErrorMessage = _localizer["GoogleDrive.Message.FolderUrlRequired"];
            model.ShowConnectForm = true;
            return View("Index", model);
        }

        if (model.CurrencyCode is not null && !SupportedCurrencies.IsSupported(model.CurrencyCode))
        {
            model.ErrorMessage = _localizer["GoogleDrive.Validation.CurrencyUnsupported"];
            model.ShowConnectForm = true;
            return View("Index", model);
        }

        try
        {
            GoogleDriveConnectionDto connection = await _googleDriveApi.ConnectDriveAsync(
                new ConnectGoogleDriveRequestDto
                {
                    FolderUrl = model.FolderUrl,
                    ConnectionName = model.ConnectionName,
                    ApiKey = model.ApiKey,
                    CurrencyCode = model.CurrencyCode
                },
                cancellationToken);

            TempData["StatusMessage"] = _localizer.Plural("GoogleDrive.Message.Connected", connection.CachedFilesCount,
                ("name", connection.Name));
            return RedirectToAction(nameof(Index), new { connectionId = connection.Id });
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Connecting a Google Drive folder failed.");
            model.ErrorMessage = ApiErrorMessages.ForUser(_localizer, ex, _localizer["GoogleDrive.Message.ConnectFailed"]);
            model.ShowConnectForm = true;
        }

        // Re-load existing connections for display on error; the connect error is already shown.
        try
        {
            model.Connections = Localize(await _googleDriveApi.GetUserConnectionsAsync(cancellationToken));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Reloading Google Drive connections after a failed connect also failed.");
        }

        return View("Index", model);
    }

    /// <summary>The connections with each error in the reader's language; empty when there are none.</summary>
    private List<GoogleDriveConnectionDto> Localize(List<GoogleDriveConnectionDto>? connections) =>
        (connections ?? []).Select(connection => GoogleDriveText.Localize(_localizer, connection)).ToList();
}
