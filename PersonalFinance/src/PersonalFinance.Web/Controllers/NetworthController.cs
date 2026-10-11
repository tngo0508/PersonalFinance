using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Web.Models;

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
        model.Projection = ComputeProjection(model);
        return View(model);
    }

    /// <summary>
    /// Handles form submission to recalculate projections with updated parameters.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(NetWorthViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.Projection = ComputeProjection(model);
        model.StatusMessage = "Projection updated successfully.";
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

        var projection = ComputeProjection(model);
        return Json(projection);
    }

    private static NetWorthProjectionDto ComputeProjection(NetWorthViewModel model)
    {
        var currentYear = DateTime.Now.Year;
        var projection = new NetWorthProjectionDto
        {
            CurrentYear = currentYear,
            CurrentAge = model.CurrentAge,
            AnnualIncome = model.AnnualIncome,
            AnnualSpending = model.AnnualSpending,
            FiTarget = model.AnnualSpending * 25m // 4% rule
        };

        // Base scenario
        projection.Base = BuildScenario(
            "Base", model.StartingBalance, model.AnnualSavings, model.CurrentAge,
            model.RetirementAge, model.BaseRoi / 100m, currentYear);

        // Conservative scenario
        projection.Conservative = BuildScenario(
            "Conservative", model.StartingBalance, model.AnnualSavings, model.CurrentAge,
            model.RetirementAge, model.ConservativeRoi / 100m, currentYear);

        // Aggressive scenario
        projection.Aggressive = BuildScenario(
            "Aggressive", model.StartingBalance, model.AggressiveSavings, model.CurrentAge,
            model.RetirementAge, model.AggressiveRoi / 100m, currentYear);

        return projection;
    }

    private static NetWorthScenarioDto BuildScenario(
        string name,
        decimal startingBalance,
        decimal annualSavings,
        int startAge,
        int retirementAge,
        decimal roiRate,
        int startYear)
    {
        var scenario = new NetWorthScenarioDto
        {
            Name = name,
            RoiPercent = roiRate * 100m,
            AnnualSavings = annualSavings,
            StartingBalance = startingBalance,
            StartingAge = startAge
        };

        decimal balance = startingBalance;
        decimal totalContributions = startingBalance;
        int year = startYear;
        int age = startAge;

        while (age <= retirementAge)
        {
            var roi = balance * roiRate;
            var endingBalance = balance + annualSavings + roi;

            scenario.Years.Add(new NetWorthYearDto
            {
                Year = year,
                Age = age,
                StartingBalance = balance,
                Savings = annualSavings,
                Roi = roi,
                EndingBalance = endingBalance
            });

            totalContributions += annualSavings;
            balance = endingBalance;
            year++;
            age++;
        }

        scenario.FinalNetWorth = balance;
        scenario.TotalContributions = totalContributions;
        scenario.TotalInterestEarned = balance - totalContributions;

        return scenario;
    }
}
