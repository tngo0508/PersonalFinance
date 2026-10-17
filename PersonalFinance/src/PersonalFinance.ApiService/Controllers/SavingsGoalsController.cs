using PersonalFinance.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for savings goals endpoints.
/// All endpoints are scoped to the current user identified via the X-User-Id header forwarded by the Web proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SavingsGoalsController : ControllerBase
{
    private readonly ISavingsGoalService _savingsGoalService;

    public SavingsGoalsController(ISavingsGoalService savingsGoalService)
    {
        _savingsGoalService = savingsGoalService;
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
    /// Gets all savings goals for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSavingsGoals(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var goals = await _savingsGoalService.GetSavingsGoalsAsync(userId, cancellationToken);
        return Ok(goals);
    }

    /// <summary>
    /// Creates a new savings goal for the current user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateSavingsGoal([FromBody] CreateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var id = await _savingsGoalService.CreateSavingsGoalAsync(userId, request, cancellationToken);
        return Ok(new { success = true, id });
    }

    /// <summary>
    /// Updates an existing savings goal.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSavingsGoal(int id, [FromBody] UpdateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var updated = await _savingsGoalService.UpdateSavingsGoalAsync(userId, id, request, cancellationToken);
        if (!updated) return NotFound();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Deletes a savings goal.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSavingsGoal(int id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var deleted = await _savingsGoalService.DeleteSavingsGoalAsync(userId, id, cancellationToken);
        if (!deleted) return NotFound();
        return Ok(new { success = true });
    }
}