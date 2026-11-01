using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Research;
using PersonalFinance.Web.Services;
using ApiException = Refit.ApiException;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Research page: public market data, for information only. Reads each dataset from
/// ApiService through <see cref="IResearchApi"/>; a dataset that has never loaded (503) or cannot be read shows that
/// tab's Unavailable State.
/// </summary>
[Authorize]
public sealed class ResearchController : Controller
{
    private readonly IResearchApi _researchApi;
    private readonly IResearchSnapshotCache _snapshotCache;
    private readonly ILogger<ResearchController> _logger;
    private readonly IStringLocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchController"/> class.
    /// </summary>
    /// <param name="researchApi">ApiService client for the Research datasets.</param>
    /// <param name="snapshotCache">Keeps the datasets read from ApiService, so the page does not fetch them each view.</param>
    /// <param name="logger">Logger for this controller.</param>
    /// <param name="localizer">The page's text, in the request's Language.</param>
    public ResearchController(IResearchApi researchApi, IResearchSnapshotCache snapshotCache,
        ILogger<ResearchController> logger, IStringLocalizer localizer)
    {
        _researchApi = researchApi;
        _snapshotCache = snapshotCache;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>The 400 body of a company search or lookup whose input was rejected, in the request's Language.</summary>
    private BadRequestObjectResult InvalidCompanyInput() =>
        BadRequest(new { error = _localizer["Research.Message.InvalidCompanyInput"].Value });

    /// <summary>
    /// The Research Home page at <c>/Research</c>: the search box, starting points, the Fund Basket and when each dataset
    /// is as of. It reads only the datasets' status, through the snapshot cache, never a dataset itself; when the status
    /// cannot be read the page still renders, with every dataset not loaded. An old tab address
    /// (<c>?tab=sectors</c>, ...) redirects (302) to the page that replaced the tab, keeping a well-formed
    /// <paramref name="basket"/> (see <see cref="OldTabRedirect"/>); an unknown <paramref name="tab"/> shows Home.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? tab = null, [FromQuery] string? ticker = null,
        [FromQuery] string? basket = null, CancellationToken cancellationToken = default)
    {
        RedirectToActionResult? redirect = OldTabRedirect(tab, ticker, basket);
        if (redirect is not null)
        {
            return redirect;
        }

        ResearchStatusDto? status = await ReadAsync(_researchApi.GetStatusAsync, ResearchCacheKeys.Status, "status",
            cancellationToken);
        return View(new ResearchHomeViewModel(status));
    }

    /// <summary>
    /// Where an address of the old tabbed Research page now goes: <c>sectors</c> to Sectors, <c>funds</c> and
    /// <c>etfs</c> to the Basket page, <c>performance</c> to the Basket page's growth section, <c>risk</c> to the Funds
    /// page's risk view, <c>world</c> to World, and <c>companies</c> to the Company page with its
    /// <paramref name="ticker"/> (dropped when rejected). Null for no tab or an unknown one, which show Home.
    /// </summary>
    private RedirectToActionResult? OldTabRedirect(string? tab, string? ticker, string? basket)
    {
        string? trimmedBasket = basket?.Trim();
        string? keptBasket = ResearchAddress.IsWellFormedBasket(trimmedBasket) ? trimmedBasket : null;
        string? companyTicker = CompanyLookupRules.TryNormalizeTicker(ticker, out string? normalized) ? normalized : null;
        return tab?.Trim().ToLowerInvariant() switch
        {
            "sectors" => RedirectToAction(nameof(Sectors), new { basket = keptBasket }),
            "funds" or "etfs" => RedirectToAction(nameof(Basket), new { basket = keptBasket }),
            "performance" => RedirectToAction(nameof(Basket), "Research", new { basket = keptBasket }, "basket-growth"),
            "risk" => RedirectToAction(nameof(Funds), new { view = ResearchFundsPageViewModel.RiskView, basket = keptBasket }),
            "world" => RedirectToAction(nameof(World), new { basket = keptBasket }),
            "companies" => RedirectToAction(nameof(Company), new { ticker = companyTicker, basket = keptBasket }),
            _ => null,
        };
    }

    /// <summary>
    /// The Research Sectors page: the treemap of the largest SEC filers by Public float, and the Fund Basket's view of
    /// it. It reads the Sectors data and the funds data (which the basket view needs) through the snapshot cache; each
    /// that cannot be read shows only its own section's Unavailable State. <paramref name="focus"/>
    /// (<c>Office</c> or <c>Office/Industry</c>) opens the chart zoomed into that group, and <paramref name="view"/>
    /// <c>basket</c> opens the basket view; a malformed value of either is ignored.
    /// A well-formed <c>basket</c> is kept on the Unavailable State's Retry.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Sectors([FromQuery] string? focus = null, [FromQuery] string? view = null,
        [FromQuery] string? basket = null, CancellationToken cancellationToken = default)
    {
        Task<SectorsSnapshotDto?> sectors = ReadAsync(_researchApi.GetSectorsAsync, ResearchCacheKeys.Sectors, "Sectors", cancellationToken);
        Task<FundsSnapshotDto?> funds = ReadAsync(_researchApi.GetFundsAsync, ResearchCacheKeys.Funds, "funds", cancellationToken);
        await Task.WhenAll(sectors, funds);
        return View(new ResearchSectorsPageViewModel(_localizer, await sectors, await funds, focus, view, basket));
    }

    /// <summary>
    /// The Research World page: the country map with its colour scale and country detail, and the Fund Basket's view by
    /// issuer country. It reads the World data, the funds data and the fund exposure data through the snapshot cache;
    /// each that cannot be read shows only its own section's Unavailable State, so the basket view still works without
    /// the World data and the map without the other two. <paramref name="country"/> opens the map with that country
    /// selected, and <paramref name="view"/> <c>basket</c> opens the basket view; a malformed value of either is
    /// ignored. A well-formed <c>basket</c> is kept on the Unavailable State's Retry.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> World([FromQuery] string? country = null, [FromQuery] string? view = null,
        [FromQuery] string? basket = null, CancellationToken cancellationToken = default)
    {
        Task<WorldSnapshotDto?> world = ReadAsync(_researchApi.GetWorldAsync, ResearchCacheKeys.World, "World", cancellationToken);
        Task<FundsSnapshotDto?> funds = ReadAsync(_researchApi.GetFundsAsync, ResearchCacheKeys.Funds, "funds", cancellationToken);
        Task<FundExposureSnapshotDto?> exposure = ReadAsync(_researchApi.GetFundExposureAsync, ResearchCacheKeys.FundExposure, "fund exposure", cancellationToken);
        await Task.WhenAll(world, funds, exposure);
        return View(new ResearchWorldPageViewModel(_localizer, await world, await funds, await exposure, country, view,
            basket));
    }

    /// <summary>
    /// The Research Funds page: every Tracked Fund compared in a sortable table, the risk and correlation views and the
    /// holdings list. It reads the funds, fund returns and fund facts data through the snapshot cache; each that cannot
    /// be read leaves only the views that need it unavailable. <paramref name="view"/> (table, risk, correlation,
    /// holdings), <paramref name="window"/> (1, 3 or 5 years), <paramref name="sort"/> and <paramref name="quarter"/>
    /// come from the address; an unknown value falls back to the table, 3 years, return and the latest quarter.
    /// A well-formed <c>basket</c> is kept on the Unavailable State's Retry.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Funds([FromQuery] string? view = null, [FromQuery] int? window = null,
        [FromQuery] string? sort = null, [FromQuery] string? quarter = null, [FromQuery] string? basket = null,
        CancellationToken cancellationToken = default)
    {
        Task<FundsSnapshotDto?> funds = ReadAsync(_researchApi.GetFundsAsync, ResearchCacheKeys.Funds, "funds", cancellationToken);
        Task<FundReturnsSnapshotDto?> returns = ReadAsync(_researchApi.GetFundReturnsAsync, ResearchCacheKeys.FundReturns, "fund returns", cancellationToken);
        Task<FundFactsSnapshotDto?> facts = ReadAsync(_researchApi.GetFundFactsAsync, ResearchCacheKeys.FundFacts, "fund facts", cancellationToken);
        await Task.WhenAll(funds, returns, facts);
        return View(new ResearchFundsPageViewModel(_localizer, await funds, await returns, await facts, view, window, sort,
            quarter, basket));
    }

    /// <summary>
    /// The Research Basket page: the Fund Basket's overview, then its growth, risk, overlap, correlation, sectors and
    /// countries, with the basket editor, starting mixes and "Copy link". It reads the funds, fund overlap, fund returns,
    /// macro, fund facts, fund exposure and Sectors data through the snapshot cache; each that cannot be read shows
    /// only the Unavailable State of the sections that need it. With no Tracked Fund in <paramref name="basket"/> the
    /// page opens on a prompt with starting mixes; a well-formed <paramref name="basket"/> is kept on every Retry.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Basket([FromQuery] string? basket = null, CancellationToken cancellationToken = default)
    {
        Task<FundsSnapshotDto?> funds = ReadAsync(_researchApi.GetFundsAsync, ResearchCacheKeys.Funds, "funds", cancellationToken);
        Task<FundOverlapSnapshotDto?> overlap = ReadAsync(_researchApi.GetFundOverlapAsync, ResearchCacheKeys.FundOverlap, "fund overlap", cancellationToken);
        Task<FundReturnsSnapshotDto?> returns = ReadAsync(_researchApi.GetFundReturnsAsync, ResearchCacheKeys.FundReturns, "fund returns", cancellationToken);
        Task<MacroSnapshotDto?> macro = ReadAsync(_researchApi.GetMacroAsync, ResearchCacheKeys.Macro, "macro", cancellationToken);
        Task<FundFactsSnapshotDto?> facts = ReadAsync(_researchApi.GetFundFactsAsync, ResearchCacheKeys.FundFacts, "fund facts", cancellationToken);
        Task<FundExposureSnapshotDto?> exposure = ReadAsync(_researchApi.GetFundExposureAsync, ResearchCacheKeys.FundExposure, "fund exposure", cancellationToken);
        Task<SectorsSnapshotDto?> sectors = ReadAsync(_researchApi.GetSectorsAsync, ResearchCacheKeys.Sectors, "Sectors", cancellationToken);
        await Task.WhenAll(funds, overlap, returns, macro, facts, exposure, sectors);
        return View(new ResearchBasketPageViewModel(_localizer, await funds, await overlap, await returns, await macro,
            await facts, await exposure, await sectors, basket));
    }

    /// <summary>
    /// The Research Fund page for one Tracked Fund at <c>/Research/Fund/{ticker}</c>, whatever the ticker's case: its
    /// header, growth, risk, similar funds, holdings and where its money goes. It reads the funds, fund returns, macro,
    /// fund facts, fund overlap and fund exposure data through the snapshot cache; each that cannot be read leaves only
    /// the sections that need it unavailable. A ticker <see cref="CompanyLookupRules.TryNormalizeTicker"/> rejects, or
    /// one that is not a Tracked Fund, gets the "not found" page with status 404; so does any ticker when the funds data
    /// cannot be read, with its Unavailable State.
    /// </summary>
    [HttpGet("Research/Fund/{ticker?}")]
    public async Task<IActionResult> Fund(string? ticker, [FromQuery] string? basket = null, CancellationToken cancellationToken = default)
    {
        if (!CompanyLookupRules.TryNormalizeTicker(ticker, out string? normalized))
        {
            return ResearchNotFound(new ResearchNotFoundViewModel());
        }

        Task<FundsSnapshotDto?> funds = ReadAsync(_researchApi.GetFundsAsync, ResearchCacheKeys.Funds, "funds", cancellationToken);
        Task<FundReturnsSnapshotDto?> returns = ReadAsync(_researchApi.GetFundReturnsAsync, ResearchCacheKeys.FundReturns, "fund returns", cancellationToken);
        Task<MacroSnapshotDto?> macro = ReadAsync(_researchApi.GetMacroAsync, ResearchCacheKeys.Macro, "macro", cancellationToken);
        Task<FundFactsSnapshotDto?> facts = ReadAsync(_researchApi.GetFundFactsAsync, ResearchCacheKeys.FundFacts, "fund facts", cancellationToken);
        Task<FundOverlapSnapshotDto?> overlap = ReadAsync(_researchApi.GetFundOverlapAsync, ResearchCacheKeys.FundOverlap, "fund overlap", cancellationToken);
        Task<FundExposureSnapshotDto?> exposure = ReadAsync(_researchApi.GetFundExposureAsync, ResearchCacheKeys.FundExposure, "fund exposure", cancellationToken);
        await Task.WhenAll(funds, returns, macro, facts, overlap, exposure);

        FundsSnapshotDto? fundsSnapshot = await funds;
        if (fundsSnapshot is null)
        {
            Dictionary<string, string> retryRoute = new(StringComparer.Ordinal) { ["ticker"] = normalized };
            return ResearchNotFound(new ResearchNotFoundViewModel
            {
                Ticker = normalized,
                FundsUnavailableState = new UnavailableStateViewModel
                {
                    PageTitle = _localizer["Research.Title"],
                    Heading = _localizer["Research.Unavailable.Funds"],
                    RetryController = "Research",
                    RetryAction = nameof(Fund),
                    RetryRouteValues = retryRoute,
                },
            });
        }

        TrackedFundDto? fund = fundsSnapshot.Funds.FirstOrDefault(f => string.Equals(f.Ticker, normalized, StringComparison.OrdinalIgnoreCase));
        if (fund is null)
        {
            return ResearchNotFound(new ResearchNotFoundViewModel { Ticker = normalized });
        }

        return View(new ResearchFundPageViewModel(_localizer, fund, fundsSnapshot, await returns, await macro, await facts,
            await overlap, await exposure, basket));
    }

    /// <summary>The Research "not found" page, with status 404.</summary>
    private ViewResult ResearchNotFound(ResearchNotFoundViewModel model)
    {
        ViewResult view = View("NotFound", model);
        view.StatusCode = StatusCodes.Status404NotFound;
        return view;
    }

    /// <summary>
    /// The fund industries snapshot, as JSON, from ApiService: the Sectors basket view fetches it the first time
    /// it is shown, so the page itself does not carry it (some 600 KB). An ApiService failure (503 when it has never
    /// loaded) keeps its status code with a safe message.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> FundIndustries(CancellationToken cancellationToken = default) =>
        Json(await _researchApi.GetFundIndustriesAsync(cancellationToken));

    /// <summary>
    /// The search index of the page's search box, as JSON, from ApiService: it loads once, so the page does not carry
    /// the lists. Read through the snapshot cache; an ApiService failure keeps its status code with a safe message and
    /// is never cached.
    /// An index with an empty part (a dataset not loaded yet) is served but not kept either.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> SearchIndex(CancellationToken cancellationToken = default) =>
        Json(await _snapshotCache.GetOrReadAsync(ResearchCacheKeys.SearchIndex, _researchApi.GetSearchIndexAsync,
            cancellationToken, keep: IsComplete));

    /// <summary>
    /// Whether every part of <paramref name="index"/> has entries. ApiService answers 200 with an empty part for a
    /// dataset that has never loaded, so an index with one is not kept: the next request reads it again.
    /// </summary>
    private static bool IsComplete(ResearchSearchIndexDto index) =>
        index.Funds.Count > 0 && index.Offices.Count > 0 && index.Industries.Count > 0 && index.Countries.Count > 0;

    /// <summary>
    /// Suggestions for the Research company lookup's search box, as JSON, from ApiService. A rejected query is 400; an
    /// ApiService
    /// failure (such as 503 when the company list cannot be read) keeps its status code with a safe message.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> Suggest([FromQuery] string? q, CancellationToken cancellationToken = default) =>
        CompanyLookupRules.TryNormalizeQuery(q, out string? query)
            ? Json(await _researchApi.SuggestCompaniesAsync(query, cancellationToken))
            : InvalidCompanyInput();

    /// <summary>
    /// The Research Company page at <c>/Research/Company/{ticker?}</c>: the company lookup on its own page. Without a
    /// ticker it opens on an empty search, which <paramref name="q"/> (when <see cref="CompanyLookupRules.TryNormalizeQuery"/>
    /// accepts it) pre-fills with its suggestions open; a rejected <paramref name="q"/> is ignored. A ticker it rejects
    /// gets the "not found" page with status 404. A Tracked Fund's ticker redirects to that fund's page, keeping
    /// <paramref name="basket"/>: only then is a snapshot read, the funds data through the cache; when it cannot be read
    /// the lookup shows here as for any other ticker. The profile itself is fetched by the page from
    /// <see cref="CompanyProfile"/>.
    /// </summary>
    [HttpGet("Research/Company/{ticker?}")]
    public async Task<IActionResult> Company(string? ticker, [FromQuery] string? q = null, [FromQuery] string? basket = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return View(new ResearchCompanyPageViewModel(null,
                CompanyLookupRules.TryNormalizeQuery(q, out string? query) ? query : null));
        }

        if (!CompanyLookupRules.TryNormalizeTicker(ticker, out string? normalized))
        {
            return ResearchNotFound(new ResearchNotFoundViewModel { IsCompany = true });
        }

        FundsSnapshotDto? funds = await ReadAsync(_researchApi.GetFundsAsync, ResearchCacheKeys.Funds, "funds", cancellationToken);
        if (funds?.Funds.Any(f => string.Equals(f.Ticker, normalized, StringComparison.OrdinalIgnoreCase)) == true)
        {
            string? trimmedBasket = basket?.Trim();
            return RedirectToAction(nameof(Fund),
                new { ticker = normalized, basket = ResearchAddress.IsWellFormedBasket(trimmedBasket) ? trimmedBasket : null });
        }

        return View(new ResearchCompanyPageViewModel(normalized, null));
    }

    /// <summary>
    /// The Company Profile lookup of <paramref name="ticker"/>, as JSON, from ApiService. A rejected ticker is 400; an
    /// ApiService failure (429 too many lookups, 503 SEC EDGAR unreachable) keeps its status code with a safe message.
    /// It was the <c>Company</c> action until the Company page took that address.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> CompanyProfile([FromQuery] string? ticker, CancellationToken cancellationToken = default) =>
        CompanyLookupRules.TryNormalizeTicker(ticker, out string? normalized)
            ? Json(await _researchApi.GetCompanyAsync(normalized, cancellationToken))
            : InvalidCompanyInput();

    /// <summary>
    /// The press releases of <paramref name="ticker"/>, as JSON, from ApiService. A rejected ticker is 400; an
    /// ApiService failure (404 switched off, 429 too many lookups, 503 SEC EDGAR unreachable) keeps its status code
    /// with a safe message.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> Releases([FromQuery] string? ticker,
        CancellationToken cancellationToken = default) =>
        CompanyLookupRules.TryNormalizeTicker(ticker, out string? normalized)
            ? Json(await _researchApi.GetCompanyPressReleasesAsync(normalized, cancellationToken))
            : InvalidCompanyInput();

    /// <summary>
    /// Reads one dataset; null when it has never loaded (503) or ApiService cannot be reached, so only that tab shows
    /// the Unavailable State. <paramref name="cacheKey"/> keys the snapshot cache; <paramref name="dataset"/> is only the
    /// label in the log.
    /// </summary>
    private async Task<TSnapshot?> ReadAsync<TSnapshot>(Func<CancellationToken, Task<TSnapshot>> read, string cacheKey,
        string dataset, CancellationToken cancellationToken) where TSnapshot : class
    {
        try
        {
            return await _snapshotCache.GetOrReadAsync(cacheKey, read, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            // The dataset has never loaded (no refresh has built it yet): expected, so one line and no stack trace.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("The Research {Dataset} data has not loaded yet", dataset);
            }
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not load the Research {Dataset} data", dataset);
            return null;
        }
    }
}
