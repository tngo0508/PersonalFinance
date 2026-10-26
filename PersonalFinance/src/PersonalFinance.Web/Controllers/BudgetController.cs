using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Models.Budget;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Budget Planner feature.
/// Allows users to set monthly income and allocate percentages across 10 spending categories.
/// All data persistence is delegated to the ApiService via the IBudgetPlannerApi Refit client.
/// </summary>
[Authorize]
public class BudgetController : Controller
{
    private readonly IBudgetPlannerApi _budgetPlannerApi;
    private readonly ILogger<BudgetController> _logger;
    private readonly IStringLocalizer _localizer;

    public BudgetController(IBudgetPlannerApi budgetPlannerApi, ILogger<BudgetController> logger,
        IStringLocalizer localizer)
    {
        _budgetPlannerApi = budgetPlannerApi;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Displays the Budget Planner page with form, pie chart, and allocation summary.
    /// Loads the saved budget from the ApiService; a user with no saved budget gets the defaults.
    /// When the ApiService is unavailable, shows the Unavailable State instead of the defaults, so the user
    /// cannot save the defaults over their real budget.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var model = new BudgetViewModel();

        try
        {
            BudgetResponseDto saved = await _budgetPlannerApi.GetBudgetAsync("current", cancellationToken);

            if (saved != null && saved.Allocations != null)
            {
                model.MonthlyIncome = saved.MonthlyIncome;
                model.Housing = saved.Allocations.Housing;
                model.Food = saved.Allocations.Food;
                model.Transportation = saved.Allocations.Transportation;
                model.Utilities = saved.Allocations.Utilities;
                model.Insurance = saved.Allocations.Insurance;
                model.Healthcare = saved.Allocations.Healthcare;
                model.Savings = saved.Allocations.Savings;
                model.DebtRepayment = saved.Allocations.DebtRepayment;
                model.Entertainment = saved.Allocations.Entertainment;
                model.Miscellaneous = saved.Allocations.Miscellaneous;
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the budget; rendering the Unavailable State instead.");
            return this.UnavailableView(_localizer["BudgetPlanner.Title"], _localizer["BudgetPlanner.Message.Unavailable"],
                "Budget");
        }

        return View(model);
    }

    /// <summary>
    /// Saves the current budget allocation via the ApiService.
    /// Validates income and delegates to the API, displaying a warning if allocations exceed income.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(BudgetViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        // Validate monthly income
        if (model.MonthlyIncome <= 0)
        {
            ModelState.AddModelError("", _localizer["BudgetPlanner.Message.IncomeRequired"]);
            return View("Index", model);
        }

        SaveBudgetRequestDto request = BuildRequest(model);

        try
        {
            await _budgetPlannerApi.SaveBudgetAsync(request, HttpContext.RequestAborted);
            if (model.TotalAllocated > model.MonthlyIncome)
            {
                TempData["SaveWarning"] = _localizer.Named("BudgetPlanner.Message.SavedOverIncome",
                    ("total", MoneyFormatter.Format(model.TotalAllocated, SupportedCurrencies.Default.Code)),
                    ("income", MoneyFormatter.Format(model.MonthlyIncome, SupportedCurrencies.Default.Code)));
            }
            else
            {
                TempData["SaveSuccess"] = _localizer["BudgetPlanner.Message.Saved"].Value;
            }
        }
        catch (Exception ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Saving the budget failed.");
            ModelState.AddModelError("", ApiErrorMessages.ForUser(_localizer, ex, _localizer["BudgetPlanner.Message.SaveFailed"]));
            return View("Index", model);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Returns JSON budget data for AJAX-based chart updates. A failure answers with an error
    /// (see <see cref="JsonApiErrorsAttribute"/>), never with an empty budget.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> GetBudgetData(CancellationToken cancellationToken = default)
    {
        BudgetResponseDto saved = await _budgetPlannerApi.GetBudgetAsync("current", cancellationToken);

        if (saved == null || saved.Allocations == null)
        {
            return Json(new
            {
                monthlyIncome = 0m,
                allocations = Array.Empty<decimal>(),
                labels = Array.Empty<string>()
            });
        }

        IReadOnlyList<(string Name, decimal Amount)> categories = BudgetPlannerCategories.AllocationAmounts(saved.Allocations);

        return Json(new
        {
            monthlyIncome = saved.MonthlyIncome,
            allocations = categories.Select(c => c.Amount),
            labels = categories.Select(c => c.Name)
        });
    }

    /// <summary>
    /// Builds a SaveBudgetRequestDto from a view model.
    /// </summary>
    private static SaveBudgetRequestDto BuildRequest(BudgetViewModel model)
    {
        return new SaveBudgetRequestDto
        {
            Period = "current",
            MonthlyIncome = model.MonthlyIncome,
            Allocations = new BudgetAllocationsDto
            {
                Housing = model.Housing,
                Food = model.Food,
                Transportation = model.Transportation,
                Utilities = model.Utilities,
                Insurance = model.Insurance,
                Healthcare = model.Healthcare,
                Savings = model.Savings,
                DebtRepayment = model.DebtRepayment,
                Entertainment = model.Entertainment,
                Miscellaneous = model.Miscellaneous
            }
        };
    }
}
