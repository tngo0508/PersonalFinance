using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Web.Infrastructure;
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
    /// Loads the saved parameters from the ApiService; a user with no saved parameters gets the defaults.
    /// When the ApiService is unavailable, shows the Unavailable State instead of the defaults, so the user
    /// cannot save the defaults over their real parameters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var model = new NetWorthViewModel();

        try
        {
            NetWorthParametersDto? saved = await _netWorthApi.LoadParametersAsync(cancellationToken);

            // Only apply saved values if they represent real data (not a blank DTO with all zeros)
            if (saved != null && saved.CurrentAge > 0 && saved.RetirementAge > 0)
            {
                model.ApplySaved(saved);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load Net Worth parameters; rendering the Unavailable State instead.");
            return Unavailable();
        }

        return await IndexWithProjectionAsync(model, cancellationToken);
    }

    /// <summary>
    /// Returns JSON projection data for AJAX-based chart updates. Parameters missing from the query string
    /// keep their <see cref="NetWorthDefaults"/> value.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Calculate([FromQuery] NetWorthViewModel parameters,
        CancellationToken cancellationToken = default)
    {
        NetWorthProjectionDto projection = await _netWorthApi.CalculateProjectionAsync(parameters.ToProjectionRequest(), cancellationToken);
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
        NetWorthProjectionRequestDto request = model.ToProjectionRequest();

        if (!ModelState.IsValid)
        {
            return await IndexWithProjectionAsync(model, CancellationToken.None);
        }

        try
        {
            await _netWorthApi.SaveParametersAsync(request, HttpContext.RequestAborted);
            TempData["SaveSuccess"] = "Parameters saved to cloud.";
        }
        catch (Exception ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Saving Net Worth parameters failed.");
            ModelState.AddModelError("", ApiErrorMessages.ForUser(ex, "Failed to save parameters. Please try again."));
            return await IndexWithProjectionAsync(model, CancellationToken.None);
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
            // ApiService answers success when there is nothing to delete, so any failure here is real.
            await _netWorthApi.DeleteParametersAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Deleting Net Worth parameters failed.");
            TempData["SaveError"] = ApiErrorMessages.ForUser(ex, "Failed to clear your saved parameters. Please try again.");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Renders the Index view with a fresh projection, or the Unavailable State when the projection fails.
    /// </summary>
    private async Task<IActionResult> IndexWithProjectionAsync(NetWorthViewModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            model.Projection = await _netWorthApi.CalculateProjectionAsync(model.ToProjectionRequest(), cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to calculate the Net Worth projection; rendering the Unavailable State instead.");
            return Unavailable();
        }

        return View(nameof(Index), model);
    }

    private ViewResult Unavailable() =>
        this.UnavailableView("Net Worth Projection", "We couldn't load your net worth projection", "Networth");
}
