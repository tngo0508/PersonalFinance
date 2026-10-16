using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Savings Goals feature.
/// Allows users to create, update, and delete savings goals.
/// All data persistence is delegated to the ApiService via the IPersonalFinanceApi Refit client.
/// </summary>
[Authorize]
public class SavingsGoalsController : Controller
{
    private readonly IPersonalFinanceApi _personalFinanceApi;

    public SavingsGoalsController(IPersonalFinanceApi personalFinanceApi)
    {
        _personalFinanceApi = personalFinanceApi;
    }

    /// <summary>
    /// Displays the Savings Goals page with all goals and an add goal form.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        try
        {
            var goals = await _personalFinanceApi.GetSavingsGoalsAsync(cancellationToken);
            ViewData["Goals"] = goals;
        }
        catch
        {
            ViewData["Goals"] = new List<SavingsGoalDto>();
        }

        return View();
    }

    /// <summary>
    /// Creates a new savings goal via the ApiService.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] string name,
        [FromForm] decimal targetAmount,
        [FromForm] decimal currentAmount,
        [FromForm] string? description,
        [FromForm] string? targetDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || targetAmount <= 0)
        {
            ModelState.AddModelError("", "Please enter a goal name and a valid target amount.");
            var goals = await _personalFinanceApi.GetSavingsGoalsAsync(cancellationToken);
            ViewData["Goals"] = goals;
            return View("Index");
        }

        var request = new CreateSavingsGoalRequestDto
        {
            Name = name.Trim(),
            TargetAmount = targetAmount,
            CurrentAmount = currentAmount,
            Description = description?.Trim(),
            TargetDate = !string.IsNullOrEmpty(targetDate) ? (DateTime?)DateTime.Parse(targetDate) : null
        };

        try
        {
            await _personalFinanceApi.CreateSavingsGoalAsync(request, cancellationToken);
            TempData["Success"] = "Savings goal created!";
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Failed to create goal: {ex.Message}");
            var goals = await _personalFinanceApi.GetSavingsGoalsAsync(cancellationToken);
            ViewData["Goals"] = goals;
            return View("Index");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Updates an existing savings goal.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        [FromForm] int id,
        [FromForm] decimal currentAmount,
        [FromForm] bool isCompleted,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateSavingsGoalRequestDto
        {
            CurrentAmount = currentAmount,
            IsCompleted = isCompleted
        };

        try
        {
            await _personalFinanceApi.UpdateSavingsGoalAsync(id, request, cancellationToken);
            TempData["Success"] = "Goal updated!";
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Failed to update goal: {ex.Message}");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a savings goal.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        [FromForm] int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _personalFinanceApi.DeleteSavingsGoalAsync(id, cancellationToken);
            TempData["Success"] = "Goal deleted.";
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Failed to delete goal: {ex.Message}");
        }

        return RedirectToAction(nameof(Index));
    }
}