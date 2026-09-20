using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Google Drive folder explorer feature.
/// Allows users to enter a Google Drive folder link or ID and lists files in an interactive DataTable.
/// </summary>
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
    /// Displays the Google Drive folder explorer view.
    /// If folderUrl is provided in the query string, automatically executes the explore operation.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] string? folderUrl,
        [FromQuery] string? apiKey,
        CancellationToken cancellationToken = default)
    {
        var model = new GoogleDriveViewModel();

        if (!string.IsNullOrWhiteSpace(folderUrl))
        {
            model.FolderUrl = folderUrl;
            model.ApiKey = apiKey;
            return await ExecuteExploreAsync(model, cancellationToken);
        }

        return View(model);
    }

    /// <summary>
    /// Handles the form submission to explore a Google Drive folder link.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        GoogleDriveViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        return await ExecuteExploreAsync(model, cancellationToken);
    }

    private async Task<IActionResult> ExecuteExploreAsync(
        GoogleDriveViewModel model,
        CancellationToken cancellationToken)
    {
        model.HasQueried = true;
        _logger.LogInformation("Invoking GoogleDriveApi to explore folder '{FolderUrl}'...", model.FolderUrl);

        try
        {
            var response = await _googleDriveApi.ExploreFolderAsync(
                new GoogleDriveRequestDto
                {
                    FolderUrl = model.FolderUrl,
                    ApiKey = model.ApiKey
                },
                cancellationToken);

            model.Response = response;

            if (!response.Success && !string.IsNullOrEmpty(response.ErrorMessage))
            {
                model.ErrorMessage = response.ErrorMessage;
            }
        }
        catch (Refit.ApiException apiEx)
        {
            _logger.LogError(apiEx, "ApiService returned HTTP {StatusCode} when querying Google Drive.", apiEx.StatusCode);
            model.ErrorMessage = $"Backend service returned error: {(int)apiEx.StatusCode} ({apiEx.StatusCode})";
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "Unable to reach ApiService backend.");
            model.ErrorMessage = "Unable to connect to the ApiService backend. Please verify that the API service is running.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Google Drive explore request was canceled by the client.");
            model.ErrorMessage = "The request was canceled.";
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Google Drive explore request timed out.");
            model.ErrorMessage = "The request timed out while communicating with the backend service. Please try again.";
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Google Drive explore request timed out.");
            model.ErrorMessage = "The request timed out while communicating with the backend service. Please try again.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during Google Drive explore request.");
            model.ErrorMessage = "An unexpected error occurred while communicating with the backend service.";
        }

        return View("Index", model);
    }
}
