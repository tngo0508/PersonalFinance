using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Net Worth projection and Financial Independence planning feature.
/// Delegates projection computation to the ApiService via the INetWorthApi Refit client.
/// Provides save/load/delete endpoints for persisting user parameters to Azure SQL.
/// </summary>
[Authorize]
public class NetworthController : Controller
{
    private readonly INetWorthApi _netWorthApi;
    private readonly AppDbContext _dbContext;

    public NetworthController(INetWorthApi netWorthApi, AppDbContext dbContext)
    {
        _netWorthApi = netWorthApi;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Gets the current authenticated user's ID from claims.
    /// </summary>
    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.Identity?.Name
               ?? "default-user";
    }

    /// <summary>
    /// Displays the Net Worth projection dashboard with scenario comparison and charts.
    /// Loads saved parameters from Azure SQL if available, otherwise uses defaults.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var model = new NetWorthViewModel();

        // Try to load saved parameters from the database
        var userId = GetCurrentUserId();
        var saved = await _dbContext.NetWorthParameters
            .FirstOrDefaultAsync(e => e.UserId == userId);

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
    /// Saves the current net worth projection parameters for the authenticated user to Azure SQL.
    /// Creates or updates the user's single parameters record, then redirects back to Index.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveParameters(NetWorthViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var request = BuildRequest(model);
            model.Projection = await _netWorthApi.CalculateProjectionAsync(request, CancellationToken.None);
            return View("Index", model);
        }

        var userId = GetCurrentUserId();

        var existing = await _dbContext.NetWorthParameters
            .FirstOrDefaultAsync(e => e.UserId == userId);

        if (existing != null)
        {
            existing.StartingBalance = model.StartingBalance;
            existing.AnnualSavings = model.AnnualSavings;
            existing.AnnualIncome = model.AnnualIncome;
            existing.AnnualSpending = model.AnnualSpending;
            existing.CurrentAge = model.CurrentAge;
            existing.RetirementAge = model.RetirementAge;
            existing.BaseRoi = model.BaseRoi;
            existing.ConservativeRoi = model.ConservativeRoi;
            existing.AggressiveRoi = model.AggressiveRoi;
            existing.AggressiveSavings = model.AggressiveSavings;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            await _dbContext.NetWorthParameters.AddAsync(new NetWorthParameters
            {
                UserId = userId,
                StartingBalance = model.StartingBalance,
                AnnualSavings = model.AnnualSavings,
                AnnualIncome = model.AnnualIncome,
                AnnualSpending = model.AnnualSpending,
                CurrentAge = model.CurrentAge,
                RetirementAge = model.RetirementAge,
                BaseRoi = model.BaseRoi,
                ConservativeRoi = model.ConservativeRoi,
                AggressiveRoi = model.AggressiveRoi,
                AggressiveSavings = model.AggressiveSavings,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        TempData["SaveSuccess"] = "Parameters saved to cloud.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Loads the saved net worth projection parameters for the authenticated user.
    /// Returns 204 No Content if no saved parameters exist.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> LoadParameters()
    {
        var userId = GetCurrentUserId();

        var parameters = await _dbContext.NetWorthParameters
            .FirstOrDefaultAsync(e => e.UserId == userId);

        if (parameters == null)
        {
            return NoContent();
        }

        var dto = new NetWorthParametersDto
        {
            StartingBalance = parameters.StartingBalance,
            AnnualSavings = parameters.AnnualSavings,
            AnnualIncome = parameters.AnnualIncome,
            AnnualSpending = parameters.AnnualSpending,
            CurrentAge = parameters.CurrentAge,
            RetirementAge = parameters.RetirementAge,
            BaseRoi = parameters.BaseRoi,
            ConservativeRoi = parameters.ConservativeRoi,
            AggressiveRoi = parameters.AggressiveRoi,
            AggressiveSavings = parameters.AggressiveSavings,
            UpdatedAtUtc = parameters.UpdatedAtUtc
        };

        return Json(dto);
    }

    /// <summary>
    /// Deletes the saved net worth projection parameters for the authenticated user.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteParameters()
    {
        var userId = GetCurrentUserId();

        var parameters = await _dbContext.NetWorthParameters
            .FirstOrDefaultAsync(e => e.UserId == userId);

        if (parameters != null)
        {
            _dbContext.NetWorthParameters.Remove(parameters);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}
