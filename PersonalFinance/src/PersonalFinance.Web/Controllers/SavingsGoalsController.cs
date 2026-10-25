using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Savings Goals feature.
/// Allows users to create, update, and delete savings goals.
/// All data persistence is delegated to the ApiService via the ISavingsGoalsApi Refit client.
/// </summary>
[Authorize]
public class SavingsGoalsController : Controller
{
    private readonly ISavingsGoalsApi _savingsGoalsApi;
    private readonly ILogger<SavingsGoalsController> _logger;
    private readonly IStringLocalizer _localizer;

    public SavingsGoalsController(ISavingsGoalsApi savingsGoalsApi, ILogger<SavingsGoalsController> logger,
        IStringLocalizer localizer)
    {
        _savingsGoalsApi = savingsGoalsApi;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Displays the Savings Goals page with all goals and an add goal form.
    /// When the ApiService is unavailable, shows the Unavailable State instead of an empty goal list.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        return IndexWithGoalsAsync(cancellationToken);
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
        // An amount the binder rejected (written the other culture's way) arrives as 0: never save it.
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(name) || targetAmount <= 0)
        {
            ModelState.AddModelError("", _localizer["SavingsGoals.Message.InvalidGoal"]);
            return await IndexWithGoalsAsync(cancellationToken);
        }

        var request = new CreateSavingsGoalRequestDto
        {
            Name = name.Trim(),
            TargetAmount = targetAmount,
            CurrentAmount = currentAmount,
            Description = description?.Trim(),
            // <input type="date"> always posts ISO yyyy-MM-dd, whatever the browser or host locale.
            TargetDate = !string.IsNullOrEmpty(targetDate)
                ? DateTime.ParseExact(targetDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null
        };

        try
        {
            await _savingsGoalsApi.CreateSavingsGoalAsync(request, cancellationToken);
            TempData["Success"] = _localizer["SavingsGoals.Message.Created"].Value;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Creating a savings goal failed.");
            ModelState.AddModelError("", ApiErrorMessages.ForUser(ex, _localizer["SavingsGoals.Message.CreateFailed"]));
            return await IndexWithGoalsAsync(cancellationToken);
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
        // A rejected amount arrives as 0 and would wipe the saved amount; show the binder's (localized) message instead.
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrEmpty(message))
                ?? _localizer["SavingsGoals.Message.UpdateFailed"].Value;
            return RedirectToAction(nameof(Index));
        }

        var request = new UpdateSavingsGoalRequestDto
        {
            CurrentAmount = currentAmount,
            IsCompleted = isCompleted
        };

        try
        {
            await _savingsGoalsApi.UpdateSavingsGoalAsync(id, request, cancellationToken);
            TempData["Success"] = _localizer["SavingsGoals.Message.Updated"].Value;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // ModelState does not survive the redirect, so the message travels in TempData.
            _logger.LogError(ex, "Updating savings goal {GoalId} failed.", id);
            TempData["Error"] = ApiErrorMessages.ForUser(ex, _localizer["SavingsGoals.Message.UpdateFailed"]);
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
            await _savingsGoalsApi.DeleteSavingsGoalAsync(id, cancellationToken);
            TempData["Success"] = _localizer["SavingsGoals.Message.Deleted"].Value;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Deleting savings goal {GoalId} failed.", id);
            TempData["Error"] = ApiErrorMessages.ForUser(ex, _localizer["SavingsGoals.Message.DeleteFailed"]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Renders the Index view with the user's goals, or the Unavailable State when they cannot be loaded.
    /// </summary>
    private async Task<IActionResult> IndexWithGoalsAsync(CancellationToken cancellationToken)
    {
        try
        {
            ViewData["Goals"] = await _savingsGoalsApi.GetSavingsGoalsAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load savings goals; rendering the Unavailable State instead.");
            return this.UnavailableView(_localizer["SavingsGoals.Title"], _localizer["SavingsGoals.Message.Unavailable"],
                "SavingsGoals");
        }

        return View(nameof(Index));
    }
}
