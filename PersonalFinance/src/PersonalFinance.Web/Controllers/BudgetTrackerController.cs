using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Budget Tracker feature.
/// Handles page rendering and acts as a secure, anti-forgery-protected gateway to the Refit API client.
/// JSON actions let <see cref="JsonApiErrorsAttribute"/> turn ApiService failures into error responses.
/// </summary>
[Authorize]
[JsonApiErrors]
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
        var sheet = await _budgetTrackerApi.GetSheetAsync(year, month, cancellationToken);
        return Json(sheet);
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

        var sheet = await _budgetTrackerApi.InitializeMonthAsync(request, cancellationToken);
        return Json(sheet);
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

        var sheet = await _budgetTrackerApi.SetPlannedAsync(year, month, categoryId, request, cancellationToken);
        return Json(sheet);
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

        var sheet = await _budgetTrackerApi.SetActualOverrideAsync(year, month, categoryId, request,
            cancellationToken);
        return Json(sheet);
    }

    /// <summary>
    /// Gets all active or all categories for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var categories = await _budgetTrackerApi.GetCategoriesAsync(includeArchived, cancellationToken);
        return Json(categories);
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

        var category = await _budgetTrackerApi.CreateCategoryAsync(request, cancellationToken);
        return Json(category);
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

        var category = await _budgetTrackerApi.UpdateCategoryAsync(id, request, cancellationToken);
        return Json(category);
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
        await _budgetTrackerApi.DeleteCategoryAsync(id, cancellationToken);
        return NoContent();
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

        var transaction = await _budgetTrackerApi.CreateTransactionAsync(request, cancellationToken);
        return Json(transaction);
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

        var transaction = await _budgetTrackerApi.UpdateTransactionAsync(id, request, cancellationToken);
        return Json(transaction);
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
        await _budgetTrackerApi.DeleteTransactionAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Gets the user's current budget data source preference.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDataSource(CancellationToken cancellationToken = default)
    {
        var preference = await _budgetTrackerApi.GetDataSourceAsync(cancellationToken);
        return Json(preference);
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

        var updated = await _budgetTrackerApi.SetDataSourceAsync(request, cancellationToken);
        return Json(updated);
    }
}
