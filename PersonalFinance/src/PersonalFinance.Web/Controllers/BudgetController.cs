using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Models;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Shared.Constants;

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

    public BudgetController(IBudgetPlannerApi budgetPlannerApi, ILogger<BudgetController> logger)
    {
        _budgetPlannerApi = budgetPlannerApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Budget Planner page with form, pie chart, and allocation summary.
    /// Loads saved budget data from the ApiService if available, otherwise uses defaults.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var model = new BudgetViewModel();

        // Try to load saved budget from the API service
        try
        {
            var saved = await _budgetPlannerApi.GetBudgetAsync("current", cancellationToken);

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
        catch
        {
            // If the API is unavailable, fall back to default values
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
            ModelState.AddModelError("", "Monthly income must be greater than 0.");
            return View("Index", model);
        }

        var request = BuildRequest(model);

        try
        {
            await _budgetPlannerApi.SaveBudgetAsync(request, HttpContext.RequestAborted);
            if (model.TotalAllocated > model.MonthlyIncome)
            {
                TempData["SaveWarning"] =
                    $"Budget saved. Note: Total allocations (${model.TotalAllocated:F2}) exceed your monthly income of ${model.MonthlyIncome:F2}.";
            }
            else
            {
                TempData["SaveSuccess"] = "Budget saved successfully.";
            }
        }
        catch (Exception ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Saving the budget failed.");
            ModelState.AddModelError("", ApiErrorMessages.ForUser(ex, "Failed to save budget. Please try again."));
            return View("Index", model);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Returns JSON budget data for AJAX-based chart updates.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBudgetData(CancellationToken cancellationToken = default)
    {
        try
        {
            var saved = await _budgetPlannerApi.GetBudgetAsync("current", cancellationToken);

            if (saved == null || saved.Allocations == null)
            {
                return Json(new
                {
                    monthlyIncome = 0m,
                    allocations = new decimal[0],
                    labels = new string[0]
                });
            }

            var categories = BudgetPlannerCategories.AllocationAmounts(saved.Allocations);

            return Json(new
            {
                monthlyIncome = saved.MonthlyIncome,
                allocations = categories.Select(c => c.Amount),
                labels = categories.Select(c => c.Name)
            });
        }
        catch
        {
            return Json(new
            {
                monthlyIncome = 0m,
                allocations = new decimal[0],
                labels = new string[0]
            });
        }
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
