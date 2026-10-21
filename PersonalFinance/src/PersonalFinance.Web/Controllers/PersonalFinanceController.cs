using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// Proxy controller that forwards Personal Finance API requests (savings goals, watchlist, budget)
/// to the backend API service via the IPersonalFinanceApi Refit client.
/// </summary>
[Authorize]
[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/[controller]")]
[Produces("application/json")]
public class PersonalFinanceController : ControllerBase
{
    private readonly IPersonalFinanceApi _personalFinanceApi;
    private readonly ILogger<PersonalFinanceController> _logger;

    public PersonalFinanceController(
        IPersonalFinanceApi personalFinanceApi,
        ILogger<PersonalFinanceController> logger)
    {
        _personalFinanceApi = personalFinanceApi;
        _logger = logger;
    }

    // =========================================================================
    // Savings Goals
    // =========================================================================

    /// <summary>
    /// Gets all savings goals for the current user.
    /// </summary>
    [HttpGet("savings-goals")]
    public async Task<IActionResult> GetSavingsGoals()
    {
        try
        {
            var goals = await _personalFinanceApi.GetSavingsGoalsAsync();
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting savings goals for user {UserId}", User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to load savings goals." });
        }
    }

    /// <summary>
    /// Creates a new savings goal for the current user.
    /// </summary>
    [HttpPost("savings-goals")]
    public async Task<IActionResult> CreateSavingsGoal([FromBody] CreateSavingsGoalRequestDto request)
    {
        try
        {
            var result = await _personalFinanceApi.CreateSavingsGoalAsync(request);
            return Ok(new { success = result.Success, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating savings goal for user {UserId}", User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to create goal." });
        }
    }

    /// <summary>
    /// Updates an existing savings goal.
    /// </summary>
    [HttpPut("savings-goals/{id}")]
    public async Task<IActionResult> UpdateSavingsGoal(int id, [FromBody] UpdateSavingsGoalRequestDto request)
    {
        try
        {
            var result = await _personalFinanceApi.UpdateSavingsGoalAsync(id, request);
            return Ok(new { success = result.Success });
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating savings goal {GoalId} for user {UserId}", id, User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to update goal." });
        }
    }

    /// <summary>
    /// Deletes a savings goal.
    /// </summary>
    [HttpDelete("savings-goals/{id}")]
    public async Task<IActionResult> DeleteSavingsGoal(int id)
    {
        try
        {
            var result = await _personalFinanceApi.DeleteSavingsGoalAsync(id);
            return Ok(new { success = result.Success });
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting savings goal {GoalId} for user {UserId}", id, User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to delete goal." });
        }
    }

    // =========================================================================
    // Watchlist
    // =========================================================================

    /// <summary>
    /// Gets all saved articles for the current user.
    /// </summary>
    [HttpGet("watchlist")]
    public async Task<IActionResult> GetWatchlist()
    {
        try
        {
            var articles = await _personalFinanceApi.GetWatchlistAsync();
            return Ok(articles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting watchlist for user {UserId}", User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to load watchlist." });
        }
    }

    /// <summary>
    /// Saves an article to the user's watchlist.
    /// </summary>
    [HttpPost("watchlist")]
    public async Task<IActionResult> SaveArticle([FromBody] SaveArticleRequestDto request)
    {
        try
        {
            var result = await _personalFinanceApi.SaveArticleAsync(request);
            return Ok(new { success = result.Success, alreadySaved = result.AlreadySaved, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving article for user {UserId}", User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to save article." });
        }
    }

    /// <summary>
    /// Removes an article from the user's watchlist.
    /// </summary>
    [HttpDelete("watchlist/{id}")]
    public async Task<IActionResult> RemoveArticle(int id)
    {
        try
        {
            var result = await _personalFinanceApi.RemoveArticleAsync(id);
            return Ok(new { success = result.Success });
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing article {ArticleId} for user {UserId}", id, User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to remove article." });
        }
    }

    /// <summary>
    /// Checks whether the current user has saved a specific article URL.
    /// </summary>
    [HttpGet("watchlist/check")]
    public async Task<IActionResult> CheckArticleSaved([FromQuery] string url)
    {
        try
        {
            var result = await _personalFinanceApi.CheckArticleSavedAsync(url);
            return Ok(new { isSaved = result.IsSaved });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking article saved status for user {UserId}", User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to check article status." });
        }
    }

    // =========================================================================
    // Budget
    // =========================================================================

    /// <summary>
    /// Gets the current user's budget allocation for the specified period.
    /// </summary>
    [HttpGet("budget/{period}")]
    public async Task<IActionResult> GetBudget(string period)
    {
        try
        {
            var result = await _personalFinanceApi.GetBudgetAsync(period);
            return Ok(new { allocations = result.Allocations, monthlyIncome = result.MonthlyIncome });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting budget for period {Period} and user {UserId}", period, User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to load budget." });
        }
    }

    /// <summary>
    /// Saves or updates the current user's budget allocation for a period.
    /// </summary>
    [HttpPost("budget")]
    public async Task<IActionResult> SaveBudget([FromBody] SaveBudgetRequestDto request)
    {
        try
        {
            var result = await _personalFinanceApi.SaveBudgetAsync(request);
            return Ok(new { success = result.Success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving budget for user {UserId}", User.GetUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to save budget." });
        }
    }
}
