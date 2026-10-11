using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Models;
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
    private readonly IGoogleDriveAuthorizationService _driveAuthorizationService;
    private readonly ILogger<GoogleDriveController> _logger;

    public GoogleDriveController(
        IGoogleDriveApi googleDriveApi,
        IGoogleDriveAuthorizationService driveAuthorizationService,
        ILogger<GoogleDriveController> logger)
    {
        _googleDriveApi = googleDriveApi;
        _driveAuthorizationService = driveAuthorizationService;
        _logger = logger;
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
        var userId = GetCurrentUserId();
        var model = new GoogleDriveViewModel
        {
            StatusMessage = TempData["StatusMessage"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string
        };

        try
        {
            // 1. Load all saved connections for this user from database
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

            // 4. Subsequent login or normal view: Select connection and load from database cache
            if (model.Connections.Any())
            {
                var selectedId = connectionId.HasValue && model.Connections.Any(c => c.Id == connectionId.Value)
                    ? connectionId.Value
                    : model.Connections.First().Id;

                model.SelectedConnectionId = selectedId;

                // Load cached files from database (zero external API requests if fresh)
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
                TempData["StatusMessage"] = "Google Drive files synchronized successfully with database cache.";
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
    /// Removes a connected Drive and deletes all locally cached data from database.
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

    /// <summary>
    /// Returns the transaction log read from the spreadsheet's Transactions sheet.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SpreadsheetTransactions(
        [FromQuery] string fileId,
        [FromQuery] string? fileName = null,
        [FromQuery] int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return BadRequest(new { error = "File ID is required." });
        }

        var userId = GetCurrentUserId();

        try
        {
            var result = await _googleDriveApi.GetSpreadsheetTransactionsAsync(fileId, fileName, connectionId, userId, cancellationToken);
            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read transactions from spreadsheet '{FileId}'.", fileId);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "The spreadsheet could not be read. Please try again." });
        }
    }

    /// <summary>
    /// Downloads the raw Monthly Budget Excel template (.xlsx) file directly to the user's browser.
    /// </summary>
    [HttpGet]
    [Route("api/GoogleDrive/templates/monthly-budget/download")]
    [Route("[controller]/DownloadMonthlyBudgetTemplate")]
    public IActionResult DownloadMonthlyBudgetTemplate()
    {
        var bytes = GoogleDriveHelper.GetMonthlyBudgetTemplateBytes();
        if (bytes == null || bytes.Length == 0)
        {
            return NotFound("Monthly budget template asset not found.");
        }

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Monthly budget.xlsx");
    }

    /// <summary>
    /// Starts the Google OAuth consent flow that grants the app write access (drive.file scope) to the user's Google Drive.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AuthorizeDrive([FromQuery] int? connectionId)
    {
        if (!await _driveAuthorizationService.IsConfiguredAsync())
        {
            TempData["ErrorMessage"] = "Google sign-in is not configured for this app, so Google Drive write access can't be authorized.";
            return RedirectToAction(nameof(Index), new { connectionId });
        }

        var properties = new GoogleChallengeProperties
        {
            RedirectUri = Url.Action(nameof(AuthorizeDriveCallback), new { connectionId }),
            Scope = new List<string> { "openid", "profile", "email", GoogleDriveAuthorizationService.DriveFileScope },
            AccessType = "offline",
            Prompt = "consent",
            IncludeGrantedScopes = true,
            LoginHint = User.FindFirstValue(ClaimTypes.Email)
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Completes the Google Drive consent flow and stores the granted tokens for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AuthorizeDriveCallback([FromQuery] int? connectionId, CancellationToken cancellationToken = default)
    {
        // The Google handler signs the OAuth result into Identity's temporary external cookie, not the app session
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var tokens = result.Succeeded ? result.Properties?.GetTokens() : null;
        if (tokens == null || !tokens.Any())
        {
            TempData["ErrorMessage"] = "Google Drive authorization was cancelled or failed. Please try again.";
            return RedirectToAction(nameof(Index), new { connectionId });
        }

        try
        {
            await _driveAuthorizationService.StoreTokensAsync(GetCurrentUserId(), tokens, cancellationToken);
            TempData["StatusMessage"] = "Google Drive access authorized. You can now create budget spreadsheets in your Drive.";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to store Google Drive tokens.");
            TempData["ErrorMessage"] = "Google Drive authorization failed. Please try again.";
        }

        return RedirectToAction(nameof(Index), new { connectionId });
    }

    /// <summary>
    /// Creates a new Monthly Budget template spreadsheet in the user's connected Google Drive folder.
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Route("api/GoogleDrive/templates/monthly-budget")]
    [Route("[controller]/CreateMonthlyBudget")]
    public async Task<IActionResult> CreateMonthlyBudget(
        [FromBody] CreateMonthlyBudgetTemplateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.ConnectionId <= 0)
        {
            return Json(new CreateMonthlyBudgetTemplateResponseDto
            {
                Success = false,
                ErrorMessage = "Valid ConnectionId is required."
            });
        }

        var userId = GetCurrentUserId();
        request.UserId = userId;

        try
        {
            // The Drive access token always comes from the server-side token store, never from the browser
            request.AccessToken = await _driveAuthorizationService.GetValidAccessTokenAsync(userId, cancellationToken);

            var result = await _googleDriveApi.CreateMonthlyBudgetTemplateAsync(request, cancellationToken);
            if (result.RequiresAuthorization)
            {
                if (request.AccessToken != null)
                {
                    // Google rejected the stored token (revoked or missing scope); force a fresh consent
                    await _driveAuthorizationService.ClearTokensAsync(userId, cancellationToken);
                }

                if (await _driveAuthorizationService.IsConfiguredAsync())
                {
                    result.AuthorizationUrl = Url.Action(nameof(AuthorizeDrive), new { connectionId = request.ConnectionId });
                }
                else
                {
                    result.ErrorMessage = "Google sign-in is not configured for this app, so Google Drive write access can't be authorized. Download the template as an Excel (.xlsx) file instead.";
                }
            }

            return Json(result);
        }
        catch (Refit.ApiException apiEx)
        {
            _logger.LogError(apiEx, "ApiService error HTTP {StatusCode} creating Monthly Budget template.", apiEx.StatusCode);

            string? errorMessage = null;
            if (!string.IsNullOrWhiteSpace(apiEx.Content))
            {
                try
                {
                    using var doc = JsonDocument.Parse(apiEx.Content);
                    if (doc.RootElement.TryGetProperty("errorMessage", out var errProp) && !string.IsNullOrWhiteSpace(errProp.GetString()))
                    {
                        errorMessage = errProp.GetString();
                    }
                    else if (doc.RootElement.TryGetProperty("detail", out var detailProp) && !string.IsNullOrWhiteSpace(detailProp.GetString()))
                    {
                        errorMessage = detailProp.GetString();
                    }
                    else if (doc.RootElement.TryGetProperty("title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.GetString()))
                    {
                        errorMessage = titleProp.GetString();
                    }
                }
                catch
                {
                    errorMessage = apiEx.Content.Trim();
                }
            }

            return Json(new CreateMonthlyBudgetTemplateResponseDto
            {
                Success = false,
                ErrorMessage = !string.IsNullOrWhiteSpace(errorMessage)
                    ? errorMessage
                    : $"Backend service error (HTTP {(int)apiEx.StatusCode} {apiEx.StatusCode})"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating Monthly Budget template.");
            return Json(new CreateMonthlyBudgetTemplateResponseDto
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred while creating the budget spreadsheet."
            });
        }
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

            TempData["StatusMessage"] = $"Connected to '{connection.Name}' and cached {connection.CachedFilesCount} items.";
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
