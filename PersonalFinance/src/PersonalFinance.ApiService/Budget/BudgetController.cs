using Microsoft.AspNetCore.Mvc;
using MediatR;
using PersonalFinance.ApiService.Budget;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Budget;

/// <summary>
/// API controller for budget allocation endpoints.
/// All endpoints are scoped to the current user identified via the X-User-Id header forwarded by the Web proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BudgetController : ControllerBase
{
    private readonly IMediator _mediator;

    public BudgetController(IMediator mediator)
    {
        _mediator = mediator;
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
    /// Gets the current user's budget allocation for the specified period.
    /// </summary>
    [HttpGet("budget/{period}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBudget(string period, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();

        var budget = await _mediator.Send(new GetBudgetQuery { UserId = userId, Period = period }, cancellationToken);

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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveBudget([FromBody] SaveBudgetRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _mediator.Send(new SaveBudgetCommand { UserId = userId, Request = request }, cancellationToken);
        return Ok(new { success = true });
    }
}
