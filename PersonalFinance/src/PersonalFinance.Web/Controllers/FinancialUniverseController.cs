using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.FinancialUniverse;
using ApiException = Refit.ApiException;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC controller for the Financial Universe page (spec #617). Reads one month's graph through
/// <see cref="IFinancialUniverseApi"/>, labels it in the request's Language and renders the list view, the graph's
/// JSON island and the detail panel's lines; the ApiService derives every node and amount. The month comes from the
/// query, as <c>?year=&amp;month=</c> or as the picker's <c>?period=yyyy-MM</c>; without one, the current month.
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
    /// The Financial Universe for <paramref name="year"/> and <paramref name="month"/>, or for <paramref name="period"/>
    /// (<c>2026-10</c>, the month picker's value, which wins when both are given), or for the current month. A month
    /// the ApiService rejects, or a picker value that is not a month, redirects to the current month. Shows the
    /// Unavailable State when the graph cannot be loaded, never an empty page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(int? year = null, int? month = null, string? period = null,
        CancellationToken cancellationToken = default)
    {
        if (period is not null)
        {
            if (!FinancialUniverseText.TryParsePeriod(period, out int periodYear, out int periodMonth))
            {
                return RedirectToAction(nameof(Index));
            }

            (year, month) = (periodYear, periodMonth);
        }

        try
        {
            FinancialUniverseGraphDto graph = await _financialUniverseApi.GetGraphAsync(year, month, cancellationToken);
            return View(FinancialUniverseText.Build(graph, _localizer, CultureInfo.CurrentCulture));
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && (year is not null || month is not null))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("The Financial Universe month {Year}-{Month} was rejected; showing the current month instead.",
                    year, month);
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the Financial Universe; rendering the Unavailable State instead.");
            return this.UnavailableView(_localizer["FinancialUniverse.Title"],
                _localizer["FinancialUniverse.Message.Unavailable"], "FinancialUniverse");
        }
    }
}
