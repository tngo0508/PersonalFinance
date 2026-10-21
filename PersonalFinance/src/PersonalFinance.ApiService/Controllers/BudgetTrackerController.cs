using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// RESTful API controller for the Budget Tracker feature: monthly sheets, categories, transactions, and data source preference.
/// All operations are isolated and scoped to the user identified via the forwarded X-User-Id header.
/// </summary>
[Authorize]
[ApiController]
[Route("api/budget-tracker")]
[Produces("application/json")]
public class BudgetTrackerController : ControllerBase
{
    private readonly IMonthlyBudgetService _budgetService;
    private readonly IBudgetDataSourceService _dataSourceService;
    private readonly IValidator<InitializeMonthRequest> _initializeMonthValidator;
    private readonly IValidator<SaveCategoryRequest> _saveCategoryValidator;
    private readonly IValidator<SaveTransactionRequest> _saveTransactionValidator;
    private readonly IValidator<SetActualOverrideRequest> _setActualOverrideValidator;
    private readonly IValidator<SetBudgetDataSourceRequest> _setDataSourceValidator;
    private readonly IValidator<SetPlannedRequest> _setPlannedValidator;

    public BudgetTrackerController(
        IMonthlyBudgetService budgetService,
        IBudgetDataSourceService dataSourceService,
        IValidator<InitializeMonthRequest> initializeMonthValidator,
        IValidator<SetPlannedRequest> setPlannedValidator,
        IValidator<SetActualOverrideRequest> setActualOverrideValidator,
        IValidator<SaveCategoryRequest> saveCategoryValidator,
        IValidator<SaveTransactionRequest> saveTransactionValidator,
        IValidator<SetBudgetDataSourceRequest> setDataSourceValidator)
    {
        _budgetService = budgetService;
        _dataSourceService = dataSourceService;
        _initializeMonthValidator = initializeMonthValidator;
        _setPlannedValidator = setPlannedValidator;
        _setActualOverrideValidator = setActualOverrideValidator;
        _saveCategoryValidator = saveCategoryValidator;
        _saveTransactionValidator = saveTransactionValidator;
        _setDataSourceValidator = setDataSourceValidator;
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("User identity is missing.");
        }

        return userId;
    }

    /// <summary>
    /// Gets the monthly budget sheet for the specified year and month.
    /// </summary>
    [HttpGet("sheet")]
    [ProducesResponseType(typeof(MonthlyBudgetSheetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSheet([FromQuery] int year, [FromQuery] int month,
        CancellationToken ct = default)
    {
        if (year < 1900 || year > 2100 || month < 1 || month > 12)
        {
            return BadRequest(new { error = "Invalid year or month." });
        }

        var userId = GetUserId();
        var sheet = await _budgetService.GetSheetAsync(userId, year, month, ct);
        return Ok(sheet);
    }

    /// <summary>
    /// Initializes a monthly budget plan, optionally copying planned amounts from the nearest earlier month.
    /// </summary>
    [HttpPost("months")]
    [ProducesResponseType(typeof(MonthlyBudgetSheetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitializeMonth([FromBody] InitializeMonthRequest request,
        CancellationToken ct = default)
    {
        var validation = await _initializeMonthValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetUserId();
        var sheet = await _budgetService.InitializeMonthAsync(userId, request, ct);
        return Ok(sheet);
    }

    /// <summary>
    /// Sets the planned amount for a specific category in a month and returns the updated sheet.
    /// </summary>
    [HttpPut("months/{year:int}/{month:int}/lines/{categoryId:int}/planned")]
    [ProducesResponseType(typeof(MonthlyBudgetSheetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPlanned(int year, int month, int categoryId,
        [FromBody] SetPlannedRequest request, CancellationToken ct = default)
    {
        if (year < 1900 || year > 2100 || month < 1 || month > 12)
        {
            return BadRequest(new { error = "Invalid year or month." });
        }

        var validation = await _setPlannedValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetUserId();
        try
        {
            var sheet = await _budgetService.SetPlannedAsync(userId, year, month, categoryId, request, ct);
            return Ok(sheet);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Sets or clears a manual Actual override for a category in a month and returns the updated sheet.
    /// </summary>
    [HttpPut("months/{year:int}/{month:int}/lines/{categoryId:int}/actual-override")]
    [ProducesResponseType(typeof(MonthlyBudgetSheetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActualOverride(int year, int month, int categoryId,
        [FromBody] SetActualOverrideRequest request, CancellationToken ct = default)
    {
        if (year < 1900 || year > 2100 || month < 1 || month > 12)
        {
            return BadRequest(new { error = "Invalid year or month." });
        }

        var validation = await _setActualOverrideValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetUserId();
        try
        {
            var sheet = await _budgetService.SetActualOverrideAsync(userId, year, month, categoryId, request, ct);
            return Ok(sheet);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all categories for the authenticated user.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<BudgetCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories([FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var categories = await _budgetService.GetCategoriesAsync(userId, includeArchived, ct);
        return Ok(categories);
    }

    /// <summary>
    /// Creates a new budget category.
    /// </summary>
    [HttpPost("categories")]
    [ProducesResponseType(typeof(BudgetCategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory([FromBody] SaveCategoryRequest request,
        CancellationToken ct = default)
    {
        var validation = await _saveCategoryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetUserId();
        try
        {
            var created = await _budgetService.CreateCategoryAsync(userId, request, ct);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (DuplicateCategoryException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing budget category.
    /// </summary>
    [HttpPut("categories/{id:int}")]
    [ProducesResponseType(typeof(BudgetCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] SaveCategoryRequest request,
        CancellationToken ct = default)
    {
        var validation = await _saveCategoryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetUserId();
        try
        {
            var updated = await _budgetService.UpdateCategoryAsync(userId, id, request, ct);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (DuplicateCategoryException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Archives (soft-deletes) a budget category.
    /// </summary>
    [HttpDelete("categories/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        try
        {
            await _budgetService.DeleteCategoryAsync(userId, id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new Budget Tracker transaction.
    /// </summary>
    [HttpPost("transactions")]
    [ProducesResponseType(typeof(BudgetTransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTransaction([FromBody] SaveTransactionRequest request,
        CancellationToken ct = default)
    {
        var validation = await _saveTransactionValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetUserId();
        try
        {
            var created = await _budgetService.CreateTransactionAsync(userId, request, ct);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing Budget Tracker transaction.
    /// </summary>
    [HttpPut("transactions/{id:int}")]
    [ProducesResponseType(typeof(BudgetTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTransaction(int id, [FromBody] SaveTransactionRequest request,
        CancellationToken ct = default)
    {
        var validation = await _saveTransactionValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetUserId();
        try
        {
            var updated = await _budgetService.UpdateTransactionAsync(userId, id, request, ct);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes (soft-deletes) a Budget Tracker transaction.
    /// </summary>
    [HttpDelete("transactions/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTransaction(int id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        try
        {
            await _budgetService.DeleteTransactionAsync(userId, id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the user's active budget data source preference.
    /// </summary>
    [HttpGet("data-source")]
    [ProducesResponseType(typeof(BudgetDataSourceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDataSource(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var preference = await _dataSourceService.GetDataSourcePreferenceAsync(userId, ct);
        return Ok(preference);
    }

    /// <summary>
    /// Sets the user's active budget data source preference.
    /// </summary>
    [HttpPut("data-source")]
    [ProducesResponseType(typeof(BudgetDataSourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetDataSource([FromBody] SetBudgetDataSourceRequest request,
        CancellationToken ct = default)
    {
        var validation = await _setDataSourceValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetUserId();
        var preference = await _dataSourceService.SetDataSourcePreferenceAsync(userId, request, ct);
        return Ok(preference);
    }
}
