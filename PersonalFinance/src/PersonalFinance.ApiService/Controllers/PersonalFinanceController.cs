using Microsoft.AspNetCore.Mvc;
using MediatR;
using PersonalFinance.ApiService.Features.Budget;
using PersonalFinance.ApiService.Features.SavingsGoals;
using PersonalFinance.ApiService.Features.Watchlist;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for personal finance features: budget planner, savings goals, and article watchlist.
/// All endpoints are scoped to the current user identified via the X-User-Id header forwarded by the Web proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PersonalFinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public PersonalFinanceController(IMediator mediator)
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

    // =========================================================================
    // Budget Allocation Endpoints
    // =========================================================================

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

    // =========================================================================
    // Savings Goal Endpoints
    // =========================================================================

    /// <summary>
    /// Gets all savings goals for the current user.
    /// </summary>
    [HttpGet("savings-goals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSavingsGoals(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var goals = await _mediator.Send(new GetSavingsGoalsQuery { UserId = userId }, cancellationToken);
        return Ok(goals);
    }

    /// <summary>
    /// Creates a new savings goal for the current user.
    /// </summary>
    [HttpPost("savings-goals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateSavingsGoal([FromBody] CreateSavingsGoalRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var id = await _mediator.Send(new CreateSavingsGoalCommand { UserId = userId, Request = request }, cancellationToken);
        return Ok(new { success = true, id });
    }

    /// <summary>
    /// Updates an existing savings goal.
    /// </summary>
    [HttpPut("savings-goals/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSavingsGoal(int id, [FromBody] UpdateSavingsGoalRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var updated = await _mediator.Send(new UpdateSavingsGoalCommand { UserId = userId, Id = id, Request = request }, cancellationToken);
        if (!updated) return NotFound();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Deletes a savings goal.
    /// </summary>
    [HttpDelete("savings-goals/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSavingsGoal(int id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var deleted = await _mediator.Send(new DeleteSavingsGoalCommand { UserId = userId, Id = id }, cancellationToken);
        if (!deleted) return NotFound();
        return Ok(new { success = true });
    }

    // =========================================================================
    // Saved Articles (Watchlist) Endpoints
    // =========================================================================

    /// <summary>
    /// Gets all saved articles for the current user.
    /// </summary>
    [HttpGet("watchlist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWatchlist(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var articles = await _mediator.Send(new GetWatchlistQuery { UserId = userId }, cancellationToken);
        return Ok(articles);
    }

    /// <summary>
    /// Saves an article to the user's watchlist.
    /// </summary>
    [HttpPost("watchlist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveArticle([FromBody] SaveArticleRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _mediator.Send(new SaveArticleCommand { UserId = userId, Request = request }, cancellationToken);
        return Ok(new { success = result.Success, alreadySaved = result.AlreadySaved, id = result.Id });
    }

    /// <summary>
    /// Removes an article from the user's watchlist.
    /// </summary>
    [HttpDelete("watchlist/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveArticle(int id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var removed = await _mediator.Send(new RemoveArticleCommand { UserId = userId, Id = id }, cancellationToken);
        if (!removed) return NotFound();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Checks whether the current user has saved a specific article URL.
    /// </summary>
    [HttpGet("watchlist/check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckArticleSaved([FromQuery] string url, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var isSaved = await _mediator.Send(new CheckArticleSavedQuery { UserId = userId, Url = url }, cancellationToken);
        return Ok(new { isSaved });
    }
}
