using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Net Worth projection and Financial Independence planning feature.
/// Acts as a thin presentation layer — all business logic and data persistence are delegated
/// to the ApiService via the INetWorthApi Refit client.
/// </summary>
[Authorize]
public class NetworthController : Controller
{
    private readonly INetWorthApi _netWorthApi;

    public NetworthController(INetWorthApi netWorthApi)
    {
        _netWorthApi = netWorthApi;
    }

    /// <summary>
    /// Displays the Net Worth projection dashboard with scenario comparison and charts.
    /// Loads saved parameters from the ApiService if available, otherwise uses defaults.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var model = new NetWorthViewModel();

        // Try to load saved parameters from the API service
        try
        {
            var saved = await _netWorthApi.LoadParametersAsync(cancellationToken);

            if (saved != null)
            {
                model.StartingBalance = saved.StartingBalance;
                model.AnnualSavings = saved.AnnualSavings;
                model.AnnualIncome = saved.AnnualIncome;
                model.AnnualSpending = saved.AnnualSpending;
                model.CurrentAge = saved.CurrentAge;
                model.RetirementAge = saved.RetirementAge;
                model.BaseRoi = saved.BaseRoi;
                model.ConservativeRoi = saved.ConservativeRoi;
                model.AggressiveRoi = saved.AggressiveRoi;
                model.AggressiveSavings = saved.AggressiveSavings;
            }
        }
        catch
        {
            // If the API is unavailable, fall back to default values
        }

        var request = BuildRequest(model);
        model.Projection = await _netWorthApi.CalculateProjectionAsync(request, cancellationToken);
        return View(model);
    }

    /// <summary>
    /// Returns JSON projection data for AJAX-based chart updates.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Calculate(
        decimal startingBalance = 195033.14m,
        decimal annualSavings = 32000m,
        decimal annualIncome = 108409.60m,
        decimal annualSpending = 50000m,
        int currentAge = 33,
        int retirementAge = 65,
        decimal baseRoi = 7.0m,
        decimal conservativeRoi = 6.0m,
        decimal aggressiveRoi = 10.0m,
        decimal aggressiveSavings = 45000m,
        CancellationToken cancellationToken = default)
    {
        var request = new NetWorthProjectionRequestDto
        {
            StartingBalance = startingBalance,
            AnnualSavings = annualSavings,
            AnnualIncome = annualIncome,
            AnnualSpending = annualSpending,
            CurrentAge = currentAge,
            RetirementAge = retirementAge,
            BaseRoi = baseRoi,
            ConservativeRoi = conservativeRoi,
            AggressiveRoi = aggressiveRoi,
            AggressiveSavings = aggressiveSavings
        };

        var projection = await _netWorthApi.CalculateProjectionAsync(request, cancellationToken);
        return Json(projection);
    }

    /// <summary>
    /// Builds a projection request DTO from a view model.
    /// </summary>
    private static NetWorthProjectionRequestDto BuildRequest(NetWorthViewModel model)
    {
        return new NetWorthProjectionRequestDto
        {
            StartingBalance = model.StartingBalance,
            AnnualSavings = model.AnnualSavings,
            AnnualIncome = model.AnnualIncome,
            AnnualSpending = model.AnnualSpending,
            CurrentAge = model.CurrentAge,
            RetirementAge = model.RetirementAge,
            BaseRoi = model.BaseRoi,
            ConservativeRoi = model.ConservativeRoi,
            AggressiveRoi = model.AggressiveRoi,
            AggressiveSavings = model.AggressiveSavings
        };
    }

    /// <summary>
    /// Saves the current net worth projection parameters via the ApiService.
    /// Validates the model, delegates to the API, and redirects back to Index with a success message.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveParameters(NetWorthViewModel model)
    {
        var request = BuildRequest(model);

        if (!ModelState.IsValid)
        {
            model.Projection = await _netWorthApi.CalculateProjectionAsync(request, CancellationToken.None);
            return View("Index", model);
        }

        try
        {
            await _netWorthApi.SaveParametersAsync(request, HttpContext.RequestAborted);
            TempData["SaveSuccess"] = "Parameters saved to cloud.";
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Failed to save parameters: {ex.Message}");
            model.Projection = await _netWorthApi.CalculateProjectionAsync(request, CancellationToken.None);
            return View("Index", model);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes the saved net worth projection parameters via the ApiService.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteParameters()
    {
        try
        {
            await _netWorthApi.DeleteParametersAsync(HttpContext.RequestAborted);
        }
        catch
        {
            // Silently ignore — parameters may not exist
        }

        return RedirectToAction(nameof(Index));
    }
}
