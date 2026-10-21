using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Web.Models;
using ApiException = Refit.ApiException;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Budget Tracker feature.
/// Handles page rendering and acts as a secure, anti-forgery-protected gateway to the Refit API client.
/// </summary>
[Authorize]
public class BudgetTrackerController : Controller
{
    private readonly IBudgetTrackerApi _budgetTrackerApi;
    private readonly ILogger<BudgetTrackerController> _logger;

    public BudgetTrackerController(IBudgetTrackerApi budgetTrackerApi, ILogger<BudgetTrackerController> logger)
    {
        _budgetTrackerApi = budgetTrackerApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Budget Tracker page for a specified year and month.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var selectedYear = year ?? now.Year;
        var selectedMonth = month ?? now.Month;

        if (selectedMonth < 1 || selectedMonth > 12)
        {
            selectedMonth = now.Month;
        }

        var model = new BudgetTrackerViewModel
        {
            Year = selectedYear,
            Month = selectedMonth
        };

        try
        {
            model.Sheet = await _budgetTrackerApi.GetSheetAsync(selectedYear, selectedMonth, cancellationToken);
            model.Categories = await _budgetTrackerApi.GetCategoriesAsync(false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load initial budget tracker sheet for {Year}-{Month}", selectedYear,
                selectedMonth);
            model.ErrorMessage = "Unable to load budget tracker data. Please try again.";
        }

        return View(model);
    }

    /// <summary>
    /// Gets the monthly budget sheet JSON.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSheet(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sheet = await _budgetTrackerApi.GetSheetAsync(year, month, cancellationToken);
            return Json(sheet);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error fetching budget sheet for {Year}-{Month}", year, month);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching budget sheet for {Year}-{Month}", year, month);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while loading the budget sheet." });
        }
    }

    /// <summary>
    /// Initializes a month plan (copying from previous or blank).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InitializeMonth(
        [FromBody] InitializeMonthRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid month initialization request." });
        }

        try
        {
            var sheet = await _budgetTrackerApi.InitializeMonthAsync(request, cancellationToken);
            return Json(sheet);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error initializing month {Year}-{Month}", request.Year, request.Month);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initializing month {Year}-{Month}", request.Year, request.Month);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to initialize month." });
        }
    }

    /// <summary>
    /// Sets the planned amount for a category in a month.
    /// </summary>
    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPlanned(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int categoryId,
        [FromBody] SetPlannedRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid planned amount request." });
        }

        try
        {
            var sheet = await _budgetTrackerApi.SetPlannedAsync(year, month, categoryId, request, cancellationToken);
            return Json(sheet);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error setting planned amount for {Year}-{Month} Category {CategoryId}", year,
                month, categoryId);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error setting planned amount for {Year}-{Month} Category {CategoryId}",
                year, month, categoryId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to update planned amount." });
        }
    }

    /// <summary>
    /// Sets or clears a manual Actual override for a category in a month.
    /// </summary>
    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActualOverride(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int categoryId,
        [FromBody] SetActualOverrideRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid actual override request." });
        }

        try
        {
            var sheet = await _budgetTrackerApi.SetActualOverrideAsync(year, month, categoryId, request,
                cancellationToken);
            return Json(sheet);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error setting actual override for {Year}-{Month} Category {CategoryId}", year,
                month, categoryId);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error setting actual override for {Year}-{Month} Category {CategoryId}",
                year, month, categoryId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to update actual override." });
        }
    }

    /// <summary>
    /// Gets all active or all categories for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var categories = await _budgetTrackerApi.GetCategoriesAsync(includeArchived, cancellationToken);
            return Json(categories);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error fetching categories");
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching categories");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to load categories." });
        }
    }

    /// <summary>
    /// Creates a new custom category.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(
        [FromBody] SaveCategoryRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid category request." });
        }

        try
        {
            var category = await _budgetTrackerApi.CreateCategoryAsync(request, cancellationToken);
            return Json(category);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error creating category {Name}", request.Name);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating category {Name}", request.Name);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to create category." });
        }
    }

    /// <summary>
    /// Updates a category name or sort order.
    /// </summary>
    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategory(
        [FromQuery] int id,
        [FromBody] SaveCategoryRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid category update request." });
        }

        try
        {
            var category = await _budgetTrackerApi.UpdateCategoryAsync(id, request, cancellationToken);
            return Json(category);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error updating category {Id}", id);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating category {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to update category." });
        }
    }

    /// <summary>
    /// Deletes (archives) a category.
    /// </summary>
    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(
        [FromQuery] int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _budgetTrackerApi.DeleteCategoryAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error deleting category {Id}", id);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting category {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to delete category." });
        }
    }

    /// <summary>
    /// Creates a transaction.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTransaction(
        [FromBody] SaveTransactionRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid transaction request." });
        }

        try
        {
            var transaction = await _budgetTrackerApi.CreateTransactionAsync(request, cancellationToken);
            return Json(transaction);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error creating transaction");
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating transaction");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to create transaction." });
        }
    }

    /// <summary>
    /// Updates an existing transaction.
    /// </summary>
    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTransaction(
        [FromQuery] int id,
        [FromBody] SaveTransactionRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid transaction update request." });
        }

        try
        {
            var transaction = await _budgetTrackerApi.UpdateTransactionAsync(id, request, cancellationToken);
            return Json(transaction);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error updating transaction {Id}", id);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating transaction {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to update transaction." });
        }
    }

    /// <summary>
    /// Deletes a transaction.
    /// </summary>
    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransaction(
        [FromQuery] int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _budgetTrackerApi.DeleteTransactionAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error deleting transaction {Id}", id);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting transaction {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to delete transaction." });
        }
    }

    /// <summary>
    /// Gets the user's current budget data source preference.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDataSource(CancellationToken cancellationToken = default)
    {
        try
        {
            var preference = await _budgetTrackerApi.GetDataSourceAsync(cancellationToken);
            return Json(preference);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error fetching budget data source preference");
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching budget data source preference");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while loading data source preference." });
        }
    }

    /// <summary>
    /// Sets the user's budget data source preference.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDataSource(
        [FromBody] SetBudgetDataSourceRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid data source preference request." });
        }

        try
        {
            var updated = await _budgetTrackerApi.SetDataSourceAsync(request, cancellationToken);
            return Json(updated);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API error setting budget data source preference to {Source}", request.Source);
            return StatusCode((int)ex.StatusCode, new { error = GetApiErrorMessage(ex) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error setting budget data source preference");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to update data source preference." });
        }
    }

    /// <summary>
    /// Extracts a user-friendly error message from an ApiException payload.
    /// Avoids exposing raw HTML, non-JSON backend payloads, or 5xx server internals.
    /// </summary>
    public static string GetApiErrorMessage(ApiException ex)
    {
        if ((int)ex.StatusCode >= 500)
        {
            return "A server error occurred. Please try again.";
        }

        if (!string.IsNullOrWhiteSpace(ex.Content))
        {
            try
            {
                using var doc = JsonDocument.Parse(ex.Content);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    // 1. Direct single error property
                    if (root.TryGetProperty("error", out var errorProp) && errorProp.GetString() is { } errorMsg &&
                        !string.IsNullOrWhiteSpace(errorMsg))
                    {
                        return errorMsg;
                    }

                    // 2. Errors collection (array of messages or object dictionary of arrays)
                    if (root.TryGetProperty("errors", out var errorsProp))
                    {
                        var errorList = new List<string>();

                        if (errorsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in errorsProp.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } str &&
                                    !string.IsNullOrWhiteSpace(str))
                                {
                                    errorList.Add(str);
                                }
                                else if (item.ValueKind == JsonValueKind.Object)
                                {
                                    if (item.TryGetProperty("errorMessage", out var em) &&
                                        em.GetString() is { } emStr && !string.IsNullOrWhiteSpace(emStr))
                                    {
                                        errorList.Add(emStr);
                                    }
                                    else if (item.TryGetProperty("message", out var m) && m.GetString() is { } mStr &&
                                             !string.IsNullOrWhiteSpace(mStr))
                                    {
                                        errorList.Add(mStr);
                                    }
                                }
                            }
                        }
                        else if (errorsProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in errorsProp.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in prop.Value.EnumerateArray())
                                    {
                                        if (item.GetString() is { } msg && !string.IsNullOrWhiteSpace(msg))
                                        {
                                            errorList.Add(msg);
                                        }
                                    }
                                }
                                else if (prop.Value.ValueKind == JsonValueKind.String &&
                                         prop.Value.GetString() is { } singleMsg &&
                                         !string.IsNullOrWhiteSpace(singleMsg))
                                {
                                    errorList.Add(singleMsg);
                                }
                            }
                        }

                        if (errorList.Count > 0)
                        {
                            return string.Join("; ", errorList);
                        }
                    }

                    // 3. Detail property
                    if (root.TryGetProperty("detail", out var detailProp) && detailProp.GetString() is { } detailMsg &&
                        !string.IsNullOrWhiteSpace(detailMsg))
                    {
                        return detailMsg;
                    }

                    // 4. Message property
                    if (root.TryGetProperty("message", out var messageProp) &&
                        messageProp.GetString() is { } messageMsg && !string.IsNullOrWhiteSpace(messageMsg))
                    {
                        return messageMsg;
                    }

                    // 5. Title property
                    if (root.TryGetProperty("title", out var titleProp) && titleProp.GetString() is { } titleMsg &&
                        !string.IsNullOrWhiteSpace(titleMsg))
                    {
                        return titleMsg;
                    }
                }
            }
            catch
            {
                return "An error occurred while processing the request.";
            }
        }

        return ex.StatusCode switch
        {
            HttpStatusCode.NotFound => "The requested resource was not found.",
            HttpStatusCode.Conflict => "A conflict occurred while processing the request.",
            HttpStatusCode.BadRequest => "The request was invalid.",
            _ => "An error occurred while processing the request."
        };
    }
}
