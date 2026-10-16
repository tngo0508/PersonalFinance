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

                // Map the dynamic allocations object to view model properties
                var alloc = saved.Allocations;
                if (alloc is not null)
                {
                    var ps = alloc.GetType().GetProperties();
                    foreach (var p in ps)
                    {
                        if (p.PropertyType == typeof(decimal))
                        {
                            var rawValue = p.GetValue(alloc);
                            if (rawValue is decimal value)
                            {
                                var prop = model.GetType().GetProperty(p.Name);
                                prop?.SetValue(model, value);
                            }
                        }
                    }
                }
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
            var labels = new List<string>();
            var values = new List<decimal>();

            if (alloc is not null)
            {
                var ps = alloc.GetType().GetProperties();
                foreach (var p in ps)
                {
                    if (p.PropertyType == typeof(decimal))
                    {
                        labels.Add(FriendlyCategoryName(p.Name));
                        if (p.GetValue(alloc) is decimal decValue)
                        {
                            values.Add(decValue);
                        }
                    }
                }
            }

            return Json(new
            {
                monthlyIncome = saved.MonthlyIncome,
                allocations = values.ToArray(),
                labels = labels.ToArray()
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

    /// <summary>
    /// Returns a user-friendly name for a budget category.
    /// </summary>
    private static string FriendlyCategoryName(string propertyName)
    {
        return propertyName switch
        {
            "Housing" => "Housing",
            "Food" => "Food",
            "Transportation" => "Transportation",
            "Utilities" => "Utilities",
            "Insurance" => "Insurance",
            "Healthcare" => "Healthcare",
            "Savings" => "Savings",
            "DebtRepayment" => "Debt Repayment",
            "Entertainment" => "Entertainment",
            "Miscellaneous" => "Miscellaneous",
            _ => propertyName
        };
    }
}