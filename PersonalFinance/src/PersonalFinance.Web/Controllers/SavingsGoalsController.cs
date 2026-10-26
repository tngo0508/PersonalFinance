using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Web.Infrastructure;
using ApiException = Refit.ApiException;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Savings Goals feature.
/// Allows users to create and delete savings goals, and to record, edit and delete their Contributions.
/// All data persistence is delegated to the ApiService via the ISavingsGoalsApi Refit client.
/// </summary>
[Authorize]
public class SavingsGoalsController : Controller
{
    /// <summary>The longest Contribution note the ApiService keeps.</summary>
    public const int MaxNoteLength = 200;

    /// <summary>No Contribution is dated before this.</summary>
    public static readonly DateOnly EarliestContributionDate = new(2000, 1, 1);

    private readonly ISavingsGoalsApi _savingsGoalsApi;
    private readonly ILogger<SavingsGoalsController> _logger;
    private readonly IStringLocalizer _localizer;
    private readonly TimeProvider _timeProvider;

    public SavingsGoalsController(ISavingsGoalsApi savingsGoalsApi, ILogger<SavingsGoalsController> logger,
        IStringLocalizer localizer, TimeProvider timeProvider)
    {
        _savingsGoalsApi = savingsGoalsApi;
        _logger = logger;
        _localizer = localizer;
        _timeProvider = timeProvider;
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
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(name) || targetAmount <= 0 || currentAmount < 0)
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
    /// Records a deposit or withdrawal on a goal. The amount is always positive; <paramref name="direction"/> gives
    /// its sign. An empty date means today.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddContribution(
        [FromForm] int goalId,
        [FromForm] decimal amount,
        [FromForm] string? direction,
        [FromForm] string? date,
        [FromForm] string? note,
        CancellationToken cancellationToken = default)
    {
        if (ContributionRequestOrError(amount, direction, date, note) is not { } request)
        {
            return RedirectToAction(nameof(Index));
        }

        return await SaveContributionAsync(
            () => _savingsGoalsApi.AddContributionAsync(goalId, request, cancellationToken),
            "SavingsGoals.Message.ContributionAdded", goalId, cancellationToken);
    }

    /// <summary>
    /// Edits a Contribution's amount, direction, date or note.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditContribution(
        [FromForm] int goalId,
        [FromForm] int contributionId,
        [FromForm] decimal amount,
        [FromForm] string? direction,
        [FromForm] string? date,
        [FromForm] string? note,
        CancellationToken cancellationToken = default)
    {
        if (ContributionRequestOrError(amount, direction, date, note) is not { } request)
        {
            return RedirectToAction(nameof(Index));
        }

        return await SaveContributionAsync(
            () => _savingsGoalsApi.UpdateContributionAsync(goalId, contributionId, request, cancellationToken),
            "SavingsGoals.Message.ContributionUpdated", goalId, cancellationToken);
    }

    /// <summary>
    /// Deletes a Contribution.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContribution(
        [FromForm] int goalId,
        [FromForm] int contributionId,
        CancellationToken cancellationToken = default)
    {
        return await SaveContributionAsync(
            () => _savingsGoalsApi.DeleteContributionAsync(goalId, contributionId, cancellationToken),
            "SavingsGoals.Message.ContributionDeleted", goalId, cancellationToken);
    }

    /// <summary>
    /// Corrects a goal's saved amount to the figure the user's bank shows; the ApiService records the difference
    /// as an Adjustment.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CorrectBalance(
        [FromForm] int goalId,
        [FromForm] decimal balance,
        CancellationToken cancellationToken = default)
    {
        // A rejected amount arrives as 0 and would empty the goal; show the binder's (localized) message instead.
        if (!ModelState.IsValid || balance < 0)
        {
            TempData["Error"] = FirstModelError() ?? _localizer["SavingsGoals.Message.InvalidBalance"].Value;
            return RedirectToAction(nameof(Index));
        }

        try
        {
            SaveContributionResponseDto response = await _savingsGoalsApi.CorrectBalanceAsync(goalId,
                new CorrectBalanceRequestDto { Balance = balance }, cancellationToken);
            TempData["Success"] = _localizer[response.Id is null
                ? "SavingsGoals.Message.BalanceUnchanged"
                : "SavingsGoals.Message.BalanceCorrected"].Value;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Correcting the balance of savings goal {GoalId} failed.", goalId);
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
    /// Builds the request for a Contribution form, or puts the reason it can't be saved, in the page language, in
    /// TempData (ModelState does not survive the redirect) and returns null.
    /// </summary>
    private SaveContributionRequestDto? ContributionRequestOrError(decimal amount, string? direction, string? date,
        string? note)
    {
        // A rejected amount arrives as 0; show the binder's (localized) message rather than "amount required".
        string? error = FirstModelError();
        DateOnly? parsedDate = null;
        if (error is null && amount <= 0)
        {
            error = _localizer["SavingsGoals.Message.AmountRequired"].Value;
        }
        else if (error is null && !string.IsNullOrEmpty(date))
        {
            // <input type="date"> always posts ISO yyyy-MM-dd, whatever the browser or host locale.
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out DateOnly value))
            {
                error = _localizer["SavingsGoals.Message.InvalidDate"].Value;
            }
            else if (value > DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime))
            {
                error = _localizer["SavingsGoals.Message.FutureDate"].Value;
            }
            else if (value < EarliestContributionDate)
            {
                error = _localizer["SavingsGoals.Message.DateTooEarly"].Value;
            }
            else
            {
                parsedDate = value;
            }
        }

        if (error is null && note?.Trim().Length > MaxNoteLength)
        {
            error = _localizer["SavingsGoals.Message.NoteTooLong"].Value;
        }

        if (error is not null)
        {
            TempData["Error"] = error;
            return null;
        }

        return new SaveContributionRequestDto
        {
            Amount = string.Equals(direction, "withdrawal", StringComparison.Ordinal) ? -amount : amount,
            Date = parsedDate,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
    }

    /// <summary>
    /// Sends a Contribution change and reports the outcome. A 409 means it would leave the saved amount below zero,
    /// said in the page language.
    /// </summary>
    private async Task<IActionResult> SaveContributionAsync(Func<Task> send, string successKey, int goalId,
        CancellationToken cancellationToken)
    {
        try
        {
            await send();
            TempData["Success"] = _localizer[successKey].Value;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            TempData["Error"] = _localizer["SavingsGoals.Message.BelowZero"].Value;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Saving a Contribution to savings goal {GoalId} failed.", goalId);
            TempData["Error"] = ApiErrorMessages.ForUser(ex, _localizer["SavingsGoals.Message.UpdateFailed"]);
        }

        return RedirectToAction(nameof(Index));
    }

    private string? FirstModelError() =>
        ModelState.IsValid
            ? null
            : ModelState.Values.SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrEmpty(message));

    /// <summary>
    /// Renders the Index view with the user's goals, or the Unavailable State when they cannot be loaded.
    /// </summary>
    private async Task<IActionResult> IndexWithGoalsAsync(CancellationToken cancellationToken)
    {
        try
        {
            ViewData["Goals"] = await _savingsGoalsApi.GetSavingsGoalsAsync(cancellationToken);
            ViewData["Today"] = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
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
