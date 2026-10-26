using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Networth;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Net Worth projection and Financial Independence planning feature.
/// Acts as a thin presentation layer — all business logic and data persistence are delegated
/// to the ApiService via the INetWorthApi Refit client.
/// </summary>
[Authorize]
public class NetworthController : Controller
{
    /// <summary>The camelCase shape the page's scripts read, matching the app's MVC JSON settings.</summary>
    private static readonly JsonSerializerOptions CalculateJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly INetWorthApi _netWorthApi;
    private readonly ILogger<NetworthController> _logger;
    private readonly IStringLocalizer _localizer;

    public NetworthController(INetWorthApi netWorthApi, ILogger<NetworthController> logger, IStringLocalizer localizer)
    {
        _netWorthApi = netWorthApi;
        _logger = logger;
        _localizer = localizer;
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
    /// keep their <see cref="NetWorthDefaults"/> value. The FI Breakdown cards ride alongside the projection's
    /// own properties (as <c>breakdown</c>), so the page's scripts keep reading the projection at the top level.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Calculate([FromQuery] NetWorthViewModel parameters,
        CancellationToken cancellationToken = default)
    {
        NetWorthProjectionDto projection = await _netWorthApi.CalculateProjectionAsync(parameters.ToProjectionRequest(), cancellationToken);
        JsonObject result = JsonSerializer.SerializeToNode(projection, CalculateJsonOptions)!.AsObject();
        result["breakdown"] = JsonSerializer.SerializeToNode(FiBreakdownBuilder.Build(projection, _localizer), CalculateJsonOptions);
        return Json(result);
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
            TempData["SaveSuccess"] = _localizer["NetWorth.Message.Saved"].Value;
        }
        catch (Exception ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Saving Net Worth parameters failed.");
            ModelState.AddModelError("", ApiErrorMessages.ForUser(ex, _localizer["NetWorth.Message.SaveFailed"]));
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
            TempData["SaveError"] = ApiErrorMessages.ForUser(ex, _localizer["NetWorth.Message.ClearFailed"]);
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
        this.UnavailableView(_localizer["NetWorth.Title"], _localizer["NetWorth.Message.Unavailable"], "Networth");
}
