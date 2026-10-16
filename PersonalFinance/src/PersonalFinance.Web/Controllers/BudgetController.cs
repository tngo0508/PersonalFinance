using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Budget Planner feature.
/// Allows users to set monthly income and allocate percentages across 10 spending categories.
/// All data persistence is delegated to the ApiService via the IPersonalFinanceApi Refit client.
/// </summary>
[Authorize]
public class BudgetController : Controller
{
    private readonly IPersonalFinanceApi _personalFinanceApi;

    public BudgetController(IPersonalFinanceApi personalFinanceApi)
    {
        _personalFinanceApi = personalFinanceApi;
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
            var saved = await _personalFinanceApi.GetBudgetAsync("current", cancellationToken);

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
    /// Validates that allocations sum to 100% and delegates to the API.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(BudgetViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        // Validate that allocations sum to 100%
        if (model.TotalAllocation <= 0 || model.TotalAllocation > 100m)
        {
            ModelState.AddModelError("",
                $"Budget allocations must add up to exactly 100%. Current total: {model.TotalAllocation:F1}%.");
            return View("Index", model);
        }

        var request = BuildRequest(model);

        try
        {
            await _personalFinanceApi.SaveBudgetAsync(request, HttpContext.RequestAborted);
            TempData["SaveSuccess"] = "Budget saved successfully.";
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Failed to save budget: {ex.Message}");
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
            var saved = await _personalFinanceApi.GetBudgetAsync("current", cancellationToken);

            if (saved == null || saved.Allocations == null)
            {
                return Json(new
                {
                    monthlyIncome = 0m,
                    allocations = new decimal[0],
                    labels = new string[0]
                });
            }

            var alloc = saved.Allocations;
            var labels = new[]
            {
                "Housing", "Food", "Transportation", "Utilities", "Insurance",
                "Healthcare", "Savings", "Debt Repayment", "Entertainment", "Miscellaneous"
            };
            var values = new[]
            {
                alloc.Housing, alloc.Food, alloc.Transportation, alloc.Utilities,
                alloc.Insurance, alloc.Healthcare, alloc.Savings, alloc.DebtRepayment,
                alloc.Entertainment, alloc.Miscellaneous
            };

            return Json(new
            {
                monthlyIncome = saved.MonthlyIncome,
                allocations = values,
                labels = labels
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