using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Net Worth projection and Financial Independence planning feature.
/// Delegates projection computation to the ApiService via the INetWorthApi Refit client.
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
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var model = new NetWorthViewModel();
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
}
