using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.CurrencyConverter;
using PersonalFinance.Web.Services;
using Refit;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Currency Converter: a plain GET form, so the URL holds the inputs. Before the first submit it
/// shows the form with defaults from the Account's Reporting Currency; on a submit it asks the
/// <see cref="ICurrencyConverterApi"/> Refit client for the conversion. The ApiService does every calculation.
/// </summary>
[Authorize]
public class CurrencyConverterController : Controller
{
    private readonly ICurrencyConverterApi _converterApi;
    private readonly IUserPreferenceService _preferences;
    private readonly IStringLocalizer _localizer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CurrencyConverterController> _logger;

    public CurrencyConverterController(ICurrencyConverterApi converterApi, IUserPreferenceService preferences,
        IStringLocalizer localizer, TimeProvider timeProvider, ILogger<CurrencyConverterController> logger)
    {
        _converterApi = converterApi;
        _preferences = preferences;
        _localizer = localizer;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Displays the converter and, when the form was submitted, its result. A rejected field shows its message next to
    /// it; a failed call shows a general message above the form, never exception details.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(decimal? amount, string? from, string? to, DateOnly? date,
        string? variant = null, CancellationToken cancellationToken = default)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        string reporting = userId is null
            ? SupportedCurrencies.DefaultCode
            : await _preferences.GetReportingCurrencyAsync(userId, cancellationToken);
        bool submitted = amount is not null || from is not null || to is not null || date is not null
                         || !ModelState.IsValid;
        var model = new CurrencyConverterViewModel
        {
            Amount = amount,
            From = from ?? DefaultFrom(reporting),
            To = to ?? reporting,
            Date = date,
            MaxDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime)
        };

        if (!submitted || !ModelState.IsValid)
        {
            return View(ViewName(variant, reporting), model);
        }

        try
        {
            model.Result = await _converterApi.ConvertAsync(amount, model.From, model.To, date, cancellationToken);
        }
        catch (ApiException ex) when ((int)ex.StatusCode < 500)
        {
            AddFieldErrors(ex);
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Currency conversion failed.");
            ModelState.AddModelError(string.Empty,
                ApiErrorMessages.ForUser(_localizer, ex, _localizer["CurrencyConverter.Error.Failed"]));
        }

        return View(ViewName(variant, reporting), model);
    }

    // PROTOTYPE (#437): throwaway page variants on ?variant=A|B|C|D with stub rates, Development only. Do not merge
    // to main. Variant A renders the shipped page unchanged inside the switcher.
    private string ViewName(string? variant, string reporting)
    {
        if (variant is null || !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
        {
            return "Index";
        }

        ViewData["Variant"] = variant.ToUpperInvariant();
        ViewData["ReportingCurrency"] = reporting;
        return "CurrencyConverterPrototype";
    }

    /// <summary>The From default: US dollars, or đồng when the Reporting Currency is US dollars.</summary>
    private static string DefaultFrom(string reporting) =>
        string.Equals(reporting, SupportedCurrencies.Usd.Code, StringComparison.Ordinal)
            ? SupportedCurrencies.Vnd.Code
            : SupportedCurrencies.Usd.Code;

    /// <summary>The ApiService's keyed rejections, each under its field (or above the form).</summary>
    private void AddFieldErrors(ApiException ex)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> fields = ApiErrorMessages.Fields(_localizer, ex);
        if (fields.Count == 0)
        {
            ModelState.AddModelError(string.Empty, ApiErrorMessages.FromApiException(_localizer, ex));
            return;
        }

        foreach ((string field, IReadOnlyList<string> messages) in fields)
        {
            foreach (string message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }
    }
}
