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
        // Validate financial values
        if (startingBalance < 0 || startingBalance > 1_000_000_000m)
        {
            return BadRequest("Starting balance must be between 0 and 1 billion.");
        }

        if (annualSavings < 0 || annualSavings > 10_000_000m)
        {
            return BadRequest("Annual savings must be between 0 and 10 million.");
        }

        if (annualIncome < 0 || annualIncome > 100_000_000m)
        {
            return BadRequest("Annual income must be between 0 and 100 million.");
        }

        if (annualSpending < 0 || annualSpending > 10_000_000m)
        {
            return BadRequest("Annual spending must be between 0 and 10 million.");
        }

        if (aggressiveSavings < 0 || aggressiveSavings > 10_000_000m)
        {
            return BadRequest("Aggressive savings must be between 0 and 10 million.");
        }

        // Validate age values
        if (currentAge < 18 || currentAge > 100)
        {
            return BadRequest("Current age must be between 18 and 100.");
        }

        if (retirementAge < 18 || retirementAge > 120)
        {
            return BadRequest("Retirement age must be between 18 and 120.");
        }

        if (retirementAge <= currentAge)
        {
            return BadRequest("Retirement age must be greater than current age.");
        }

        // Validate ROI values
        if (baseRoi < 0 || baseRoi > 50)
        {
            return BadRequest("Base ROI must be between 0% and 50%.");
        }

        if (conservativeRoi < 0 || conservativeRoi > 50)
        {
            return BadRequest("Conservative ROI must be between 0% and 50%.");
        }

        if (aggressiveRoi < 0 || aggressiveRoi > 50)
        {
            return BadRequest("Aggressive ROI must be between 0% and 50%.");
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
