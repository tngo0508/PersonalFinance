using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using FluentValidation;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for budget allocation endpoints.
/// All endpoints are scoped to the current user identified via the X-User-Id header forwarded by the Web proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _budgetService;
    private readonly SaveBudgetRequestValidator _validator;

    public BudgetController(IBudgetService budgetService, SaveBudgetRequestValidator validator)
    {
        _budgetService = budgetService;
        _validator = validator;
    }

    /// <summary>
    /// Returns the current user's ID from the X-User-Id header forwarded by the Web proxy.
    /// </summary>
    private string GetUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userId))
        {
            var value = userId.First();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return "anonymous";
    }

    /// <summary>
    /// Gets the current user's budget allocation (defaults to current period).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBudgetCurrent(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var budget = await _budgetService.GetBudgetAsync(userId, "current", cancellationToken);

        if (budget is null)
        {
            return Ok(new { allocations = (object?)null, monthlyIncome = 0m });
        }

        return Ok(new
        {
            allocations = new
            {
                housing = budget.Allocations!.Housing,
                food = budget.Allocations.Food,
                transportation = budget.Allocations.Transportation,
                utilities = budget.Allocations.Utilities,
                insurance = budget.Allocations.Insurance,
                healthcare = budget.Allocations.Healthcare,
                savings = budget.Allocations.Savings,
                debtRepayment = budget.Allocations.DebtRepayment,
                entertainment = budget.Allocations.Entertainment,
                miscellaneous = budget.Allocations.Miscellaneous
            },
            monthlyIncome = budget.MonthlyIncome
        });
    }

    /// <summary>
    /// Gets the current user's budget allocation for the specified period.
    /// </summary>
    [HttpGet("budget/{period}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBudget(string period, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();

        var budget = await _budgetService.GetBudgetAsync(userId, period, cancellationToken);

        if (budget is null)
        {
            return Ok(new { allocations = (object?)null, monthlyIncome = 0m });
        }

        return Ok(new
        {
            allocations = new
            {
                housing = budget.Allocations!.Housing,
                food = budget.Allocations.Food,
                transportation = budget.Allocations.Transportation,
                utilities = budget.Allocations.Utilities,
                insurance = budget.Allocations.Insurance,
                healthcare = budget.Allocations.Healthcare,
                savings = budget.Allocations.Savings,
                debtRepayment = budget.Allocations.DebtRepayment,
                entertainment = budget.Allocations.Entertainment,
                miscellaneous = budget.Allocations.Miscellaneous
            },
            monthlyIncome = budget.MonthlyIncome
        });
    }

    /// <summary>
    /// Saves or updates the current user's budget allocation for a period.
    /// </summary>
    [HttpPost("budget")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveBudget([FromBody] SaveBudgetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors.Select(e => new { e.ErrorCode, e.ErrorMessage }));
        }

        var userId = GetUserId();
        await _budgetService.SaveBudgetAsync(userId, request, cancellationToken);
        return Ok(new { success = true });
    }
}