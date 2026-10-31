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
    /// Displays the Research page, opening on the Sectors tab at the latest year, or on <paramref name="tab"/> when it
    /// names a tab. On the Companies tab, <paramref name="ticker"/> is the company to look up; an unknown tab or a
    /// rejected ticker is ignored.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? tab = null, [FromQuery] string? ticker = null,
        CancellationToken cancellationToken = default)
    {
        // The datasets are read side by side, so the page waits for the slowest one rather than for all in turn.
        Task<SectorsSnapshotDto?> sectors = ReadAsync(_researchApi.GetSectorsAsync, ResearchCacheKeys.Sectors, "Sectors", cancellationToken);
        Task<FundsSnapshotDto?> funds = ReadAsync(_researchApi.GetFundsAsync, ResearchCacheKeys.Funds, "funds", cancellationToken);
        Task<WorldSnapshotDto?> world = ReadAsync(_researchApi.GetWorldAsync, ResearchCacheKeys.World, "World", cancellationToken);
        Task<FundOverlapSnapshotDto?> overlap = ReadAsync(_researchApi.GetFundOverlapAsync, ResearchCacheKeys.FundOverlap, "fund overlap", cancellationToken);
        Task<FundReturnsSnapshotDto?> returns = ReadAsync(_researchApi.GetFundReturnsAsync, ResearchCacheKeys.FundReturns, "fund returns", cancellationToken);
        Task<MacroSnapshotDto?> macro = ReadAsync(_researchApi.GetMacroAsync, ResearchCacheKeys.Macro, "macro", cancellationToken);
        Task<FundFactsSnapshotDto?> facts = ReadAsync(_researchApi.GetFundFactsAsync, ResearchCacheKeys.FundFacts, "fund facts", cancellationToken);
        Task<FundExposureSnapshotDto?> exposure = ReadAsync(_researchApi.GetFundExposureAsync, ResearchCacheKeys.FundExposure, "fund exposure", cancellationToken);
        await Task.WhenAll(sectors, funds, world, overlap, returns, macro, facts, exposure);
        return View(new ResearchViewModel(_localizer, await sectors, await funds, await world, tab, ticker, await overlap,
            await returns, await macro, await facts, await exposure));
    }

    /// <summary>
    /// The Research Home page: the search box, starting points, the Fund Basket and when each dataset is as of. It reads
    /// only the datasets' status, through the snapshot cache, never a dataset itself; when the status cannot be read the
    /// page still renders, with every dataset not loaded.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Home(CancellationToken cancellationToken = default)
    {
        ResearchStatusDto? status = await ReadAsync(_researchApi.GetStatusAsync, ResearchCacheKeys.Status, "status",
            cancellationToken);
        return View(new ResearchHomeViewModel(status));
    }

    /// <summary>
    /// The Research Sectors page: the treemap of the largest SEC filers by Public float, and the Fund Basket's view of
    /// it. It reads the Sectors data and the funds data (which the basket view needs) through the snapshot cache; each
    /// that cannot be read shows only its own section's Unavailable State. <paramref name="focus"/>
    /// (<c>Office</c> or <c>Office/Industry</c>) opens the chart zoomed into that group, and <paramref name="view"/>
    /// <c>basket</c> opens the basket view; a malformed value of either is ignored.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Sectors([FromQuery] string? focus = null, [FromQuery] string? view = null,
        CancellationToken cancellationToken = default)
    {
        Task<SectorsSnapshotDto?> sectors = ReadAsync(_researchApi.GetSectorsAsync, ResearchCacheKeys.Sectors, "Sectors", cancellationToken);
        Task<FundsSnapshotDto?> funds = ReadAsync(_researchApi.GetFundsAsync, ResearchCacheKeys.Funds, "funds", cancellationToken);
        await Task.WhenAll(sectors, funds);
        return View(new ResearchSectorsPageViewModel(_localizer, await sectors, await funds, focus, view));
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
    /// Suggestions for the Companies tab's search box, as JSON, from ApiService. A rejected query is 400; an ApiService
    /// failure (such as 503 when the company list cannot be read) keeps its status code with a safe message.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> Suggest([FromQuery] string? q, CancellationToken cancellationToken = default) =>
        CompanyLookupRules.TryNormalizeQuery(q, out string? query)
            ? Json(await _researchApi.SuggestCompaniesAsync(query, cancellationToken))
            : InvalidCompanyInput();

    /// <summary>
    /// The Company Profile lookup of <paramref name="ticker"/>, as JSON, from ApiService. A rejected ticker is 400; an
    /// ApiService failure (429 too many lookups, 503 SEC EDGAR unreachable) keeps its status code with a safe message.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> Company([FromQuery] string? ticker, CancellationToken cancellationToken = default) =>
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
