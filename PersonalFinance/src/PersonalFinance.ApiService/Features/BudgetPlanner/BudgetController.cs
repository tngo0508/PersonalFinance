using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.BudgetPlanner.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.BudgetPlanner;

/// <summary>
/// API controller for budget allocation endpoints.
/// All endpoints are scoped to the current user identified by the authenticated Account (see XUserIdAuthenticationHandler).
/// A rejected save is an <see cref="ApiErrorDto"/> carrying <see cref="ApiErrorKeys.BudgetPlanner"/> keys (ADR 0017).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _budgetService;
    private readonly IValidator<SaveBudgetRequestDto> _validator;

    public BudgetController(IBudgetService budgetService, IValidator<SaveBudgetRequestDto> validator)
    {
        _budgetService = budgetService;
        _validator = validator;
    }

    /// <summary>
    /// Gets the current user's budget allocation (defaults to current period).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBudgetCurrent(CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        BudgetAllocationResponseDto? budget = await _budgetService.GetBudgetAsync(userId, "current", cancellationToken);

        if (budget is null)
        {
            return Ok(new { allocations = (object?)null, monthlyIncome = 0m });
        }

        return Ok(new
        {
            currencyCode = budget.CurrencyCode,
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
        string userId = User.GetUserId();

        BudgetAllocationResponseDto? budget = await _budgetService.GetBudgetAsync(userId, period, cancellationToken);

        if (budget is null)
        {
            return Ok(new { allocations = (object?)null, monthlyIncome = 0m });
        }

        return Ok(new
        {
            currencyCode = budget.CurrencyCode,
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
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveBudget([FromBody] SaveBudgetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToApiError());
        }

        string userId = User.GetUserId();
        await _budgetService.SaveBudgetAsync(userId, request, cancellationToken);
        return Ok(new { success = true });
    }
}
