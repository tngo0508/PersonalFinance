using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.FinancialUniverse;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC controller for the Financial Universe page (spec #617). Reads the current month's graph through
/// <see cref="IFinancialUniverseApi"/>, labels it in the request's Language and renders the list view; the
/// ApiService derives every node and amount.
/// </summary>
[Authorize]
public class FinancialUniverseController : Controller
{
    private readonly IFinancialUniverseApi _financialUniverseApi;
    private readonly ILogger<FinancialUniverseController> _logger;
    private readonly IStringLocalizer _localizer;

    public FinancialUniverseController(IFinancialUniverseApi financialUniverseApi,
        ILogger<FinancialUniverseController> logger, IStringLocalizer localizer)
    {
        _financialUniverseApi = financialUniverseApi;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// The current month's Financial Universe as a grouped list, with the graph in a JSON island for the renderer.
    /// Shows the Unavailable State when the graph cannot be loaded, never an empty page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        try
        {
            FinancialUniverseGraphDto graph = await _financialUniverseApi.GetGraphAsync(cancellationToken: cancellationToken);
            return View(FinancialUniverseText.Build(graph, _localizer, CultureInfo.CurrentCulture));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the Financial Universe; rendering the Unavailable State instead.");
            return this.UnavailableView(_localizer["FinancialUniverse.Title"],
                _localizer["FinancialUniverse.Message.Unavailable"], "FinancialUniverse");
        }
    }
}
