using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// API controller for personal finance features: budget planner, savings goals, and article watchlist.
/// All endpoints require authentication and are scoped to the current user.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PersonalFinanceController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<PersonalFinanceController> _logger;

    public PersonalFinanceController(
        AppDbContext dbContext,
        UserManager<IdentityUser> userManager,
        ILogger<PersonalFinanceController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Returns the current user's ID, or null if not found.
    /// </summary>
    private async Task<string?> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id;
    }

    // =========================================================================
    // Budget Allocation Endpoints
    // =========================================================================

    /// <summary>
    /// Gets the current user's budget allocation for the specified period.
    /// </summary>
    [HttpGet("budget/{period}")]
    public async Task<IActionResult> GetBudget(string period)
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var budget = await _dbContext.UserBudgetAllocations
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Period == period);

        if (budget is null)
        {
            return Ok(new { allocations = (object?)null, monthlyIncome = 0m });
        }

        return Ok(new
        {
            allocations = new
            {
                housing = budget.HousingPercent,
                food = budget.FoodPercent,
                transportation = budget.TransportationPercent,
                utilities = budget.UtilitiesPercent,
                insurance = budget.InsurancePercent,
                healthcare = budget.HealthcarePercent,
                savings = budget.SavingsPercent,
                debtRepayment = budget.DebtRepaymentPercent,
                entertainment = budget.EntertainmentPercent,
                miscellaneous = budget.MiscellaneousPercent
            },
            monthlyIncome = budget.MonthlyIncome
        });
    }

    /// <summary>
    /// Saves or updates the current user's budget allocation for a period.
    /// </summary>
    [HttpPost("budget")]
    public async Task<IActionResult> SaveBudget([FromBody] SaveBudgetRequest request)
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var period = request.Period ?? DateTime.UtcNow.ToString("yyyy-MM");

        var budget = await _dbContext.UserBudgetAllocations
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Period == period);

        if (budget is null)
        {
            budget = new UserBudgetAllocation
            {
                UserId = userId,
                Period = period
            };
            _dbContext.UserBudgetAllocations.Add(budget);
        }

        budget.MonthlyIncome = request.MonthlyIncome;
        budget.HousingPercent = request.Allocations.Housing;
        budget.FoodPercent = request.Allocations.Food;
        budget.TransportationPercent = request.Allocations.Transportation;
        budget.UtilitiesPercent = request.Allocations.Utilities;
        budget.InsurancePercent = request.Allocations.Insurance;
        budget.HealthcarePercent = request.Allocations.Healthcare;
        budget.SavingsPercent = request.Allocations.Savings;
        budget.DebtRepaymentPercent = request.Allocations.DebtRepayment;
        budget.EntertainmentPercent = request.Allocations.Entertainment;
        budget.MiscellaneousPercent = request.Allocations.Miscellaneous;
        budget.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation("User {UserId} saved budget for period {Period}", userId, period);

        return Ok(new { success = true });
    }

    // =========================================================================
    // Savings Goal Endpoints
    // =========================================================================

    /// <summary>
    /// Gets all savings goals for the current user.
    /// </summary>
    [HttpGet("savings-goals")]
    public async Task<IActionResult> GetSavingsGoals()
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var goals = await _dbContext.SavingsGoals
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.UpdatedAtUtc)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.TargetAmount,
                g.CurrentAmount,
                g.Description,
                g.TargetDate,
                g.IsCompleted,
                g.CreatedAtUtc,
                g.UpdatedAtUtc,
                ProgressPercent = g.TargetAmount > 0 ? Math.Min(100, (g.CurrentAmount / g.TargetAmount) * 100) : 0
            })
            .ToListAsync();

        return Ok(goals);
    }

    /// <summary>
    /// Creates a new savings goal for the current user.
    /// </summary>
    [HttpPost("savings-goals")]
    public async Task<IActionResult> CreateSavingsGoal([FromBody] CreateSavingsGoalRequest request)
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var goal = new SavingsGoal
        {
            UserId = userId,
            Name = request.Name,
            TargetAmount = request.TargetAmount,
            CurrentAmount = request.CurrentAmount ?? 0,
            Description = request.Description,
            TargetDate = request.TargetDate
        };

        _dbContext.SavingsGoals.Add(goal);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        return Ok(new { success = true, id = goal.Id });
    }

    /// <summary>
    /// Updates an existing savings goal.
    /// </summary>
    [HttpPut("savings-goals/{id}")]
    public async Task<IActionResult> UpdateSavingsGoal(int id, [FromBody] UpdateSavingsGoalRequest request)
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var goal = await _dbContext.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

        if (goal is null) return NotFound();

        if (!string.IsNullOrEmpty(request.Name)) goal.Name = request.Name;
        if (request.TargetAmount.HasValue) goal.TargetAmount = request.TargetAmount.Value;
        if (request.CurrentAmount.HasValue) goal.CurrentAmount = request.CurrentAmount.Value;
        if (request.Description != null) goal.Description = request.Description;
        if (request.TargetDate.HasValue) goal.TargetDate = request.TargetDate.Value;
        goal.IsCompleted = request.IsCompleted;
        goal.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(CancellationToken.None);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Deletes a savings goal.
    /// </summary>
    [HttpDelete("savings-goals/{id}")]
    public async Task<IActionResult> DeleteSavingsGoal(int id)
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var goal = await _dbContext.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

        if (goal is null) return NotFound();

        _dbContext.SavingsGoals.Remove(goal);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        return Ok(new { success = true });
    }

    // =========================================================================
    // Saved Articles (Watchlist) Endpoints
    // =========================================================================

    /// <summary>
    /// Gets all saved articles for the current user.
    /// </summary>
    [HttpGet("watchlist")]
    public async Task<IActionResult> GetWatchlist()
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var articles = await _dbContext.SavedArticles
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.SavedAtUtc)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Url,
                a.Source,
                a.Category,
                a.Notes,
                a.ImageUrl,
                a.SavedAtUtc
            })
            .ToListAsync();

        return Ok(articles);
    }

    /// <summary>
    /// Saves an article to the user's watchlist.
    /// </summary>
    [HttpPost("watchlist")]
    public async Task<IActionResult> SaveArticle([FromBody] SaveArticleRequest request)
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var existing = await _dbContext.SavedArticles
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Url == request.Url);

        if (existing is not null)
        {
            existing.Notes = request.Notes ?? existing.Notes;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            return Ok(new { success = true, alreadySaved = true, id = existing.Id });
        }

        var article = new SavedArticle
        {
            UserId = userId,
            Title = request.Title,
            Url = request.Url,
            Source = request.Source,
            Category = request.Category,
            Notes = request.Notes,
            ImageUrl = request.ImageUrl
        };

        _dbContext.SavedArticles.Add(article);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        return Ok(new { success = true, alreadySaved = false, id = article.Id });
    }

    /// <summary>
    /// Removes an article from the user's watchlist.
    /// </summary>
    [HttpDelete("watchlist/{id}")]
    public async Task<IActionResult> RemoveArticle(int id)
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var article = await _dbContext.SavedArticles
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

        if (article is null) return NotFound();

        _dbContext.SavedArticles.Remove(article);
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Checks whether the current user has saved a specific article URL.
    /// </summary>
    [HttpGet("watchlist/check")]
    public async Task<IActionResult> CheckArticleSaved([FromQuery] string url)
    {
        var userId = await GetUserIdAsync();
        if (userId is null) return Unauthorized();

        var exists = await _dbContext.SavedArticles
            .AnyAsync(a => a.UserId == userId && a.Url == url);

        return Ok(new { isSaved = exists });
    }

    // =========================================================================
    // Request DTOs
    // =========================================================================

    public class SaveBudgetRequest
    {
        public string? Period { get; set; }
        public decimal MonthlyIncome { get; set; }
        public BudgetAllocations Allocations { get; set; } = new();
    }

    public class BudgetAllocations
    {
        public decimal Housing { get; set; }
        public decimal Food { get; set; }
        public decimal Transportation { get; set; }
        public decimal Utilities { get; set; }
        public decimal Insurance { get; set; }
        public decimal Healthcare { get; set; }
        public decimal Savings { get; set; }
        public decimal DebtRepayment { get; set; }
        public decimal Entertainment { get; set; }
        public decimal Miscellaneous { get; set; }
    }

    public class CreateSavingsGoalRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal? CurrentAmount { get; set; }
        public string? Description { get; set; }
        public DateTime? TargetDate { get; set; }
    }

    public class UpdateSavingsGoalRequest
    {
        public string? Name { get; set; }
        public decimal? TargetAmount { get; set; }
        public decimal? CurrentAmount { get; set; }
        public string? Description { get; set; }
        public DateTime? TargetDate { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class SaveArticleRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? ImageUrl { get; set; }
    }
}
