using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Models;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Net Worth projection and Financial Independence planning feature.
/// </summary>
[Authorize]
public class NetworthController : Controller
{
    /// <summary>
    /// Displays the Net Worth projection dashboard with scenario comparison and charts.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        var model = new NetWorthViewModel();
        model.Projection = NetWorthProjectionService.ComputeProjection(model);
        return View(model);
    }

    /// <summary>
    /// Returns JSON projection data for AJAX-based chart updates.
    /// </summary>
    [HttpGet]
    public IActionResult Calculate(
        decimal startingBalance = 195033.14m,
        decimal annualSavings = 32000m,
        decimal annualIncome = 108409.60m,
        decimal annualSpending = 50000m,
        int currentAge = 33,
        int retirementAge = 65,
        decimal baseRoi = 7.0m,
        decimal conservativeRoi = 6.0m,
        decimal aggressiveRoi = 10.0m,
        decimal aggressiveSavings = 45000m)
    {
        // Validate input ranges
        if (startingBalance < 0 || annualSavings < 0 || annualIncome < 0 || annualSpending < 0)
        {
            return BadRequest("Financial values cannot be negative.");
        }

        if (currentAge < 18 || currentAge > 100)
        {
            return BadRequest("Current age must be between 18 and 100.");
        }

        if (retirementAge <= currentAge)
        {
            return BadRequest("Retirement age must be greater than current age.");
        }

        if (baseRoi < 0 || baseRoi > 50 || conservativeRoi < 0 || conservativeRoi > 50 || aggressiveRoi < 0 || aggressiveRoi > 50)
        {
            return BadRequest("ROI values must be between 0% and 50%.");
        }

        var model = new NetWorthViewModel
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

        var projection = NetWorthProjectionService.ComputeProjection(model);
        return Json(projection);
    }
}
