using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Constants;
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
    private readonly ILogger<NetworthController> _logger;

    public NetworthController(INetWorthApi netWorthApi, ILogger<NetworthController> logger)
    {
        _netWorthApi = netWorthApi;
        _logger = logger;
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

            // Only apply saved values if they represent real data (not a blank DTO with all zeros)
            if (saved != null && saved.CurrentAge > 0 && saved.RetirementAge > 0)
            {
                model.ApplySaved(saved);
            }
        }
        catch
        {
            // If the API is unavailable, fall back to default values
        }

        model.Projection = await _netWorthApi.CalculateProjectionAsync(model.ToProjectionRequest(), cancellationToken);
        return View(model);
    }

    /// <summary>
    /// Returns JSON projection data for AJAX-based chart updates. Parameters missing from the query string
    /// keep their <see cref="NetWorthDefaults"/> value.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Calculate([FromQuery] NetWorthViewModel parameters,
        CancellationToken cancellationToken = default)
    {
        var projection = await _netWorthApi.CalculateProjectionAsync(parameters.ToProjectionRequest(), cancellationToken);
        return Json(projection);
    }

    /// <summary>
    /// Saves the current net worth projection parameters via the ApiService.
    /// Validates the model, delegates to the API, and redirects back to Index with a success message.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveParameters(NetWorthViewModel model)
    {
        var request = model.ToProjectionRequest();

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
        catch (Exception ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Saving Net Worth parameters failed.");
            ModelState.AddModelError("", "Failed to save parameters. Please try again.");
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
