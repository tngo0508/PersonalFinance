using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Research;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Research page: public market data, for information only. Reads each dataset from
/// ApiService through <see cref="IResearchApi"/>; a dataset that has never loaded (503) or cannot be read shows that
/// tab's Unavailable State.
/// </summary>
[Authorize]
public sealed class ResearchController : Controller
{
    /// <summary>The 400 message of a company search or lookup whose input was rejected.</summary>
    public const string InvalidCompanyInput = "Enter a ticker or company name.";

    private readonly IResearchApi _researchApi;
    private readonly ILogger<ResearchController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchController"/> class.
    /// </summary>
    /// <param name="researchApi">ApiService client for the Research datasets.</param>
    /// <param name="logger">Logger for this controller.</param>
    public ResearchController(IResearchApi researchApi, ILogger<ResearchController> logger)
    {
        _researchApi = researchApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Research page, opening on the Sectors tab at the latest year, or on <paramref name="tab"/> when it
    /// names a tab. On the Companies tab, <paramref name="ticker"/> is the company to look up; an unknown tab or a
    /// rejected ticker is ignored.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? tab = null, [FromQuery] string? ticker = null,
        CancellationToken cancellationToken = default)
    {
        SectorsSnapshotDto? sectors = await ReadAsync(_researchApi.GetSectorsAsync, "Sectors", cancellationToken);
        EtfsSnapshotDto? etfs = await ReadAsync(_researchApi.GetEtfsAsync, "ETFs", cancellationToken);
        WorldSnapshotDto? world = await ReadAsync(_researchApi.GetWorldAsync, "World", cancellationToken);
        return View(new ResearchViewModel(sectors, etfs, world, tab, ticker));
    }

    /// <summary>
    /// Suggestions for the Companies tab's search box, as JSON, from ApiService. A rejected query is 400; an ApiService
    /// failure (such as 503 when the company list cannot be read) keeps its status code with a safe message.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> Suggest([FromQuery] string? q, CancellationToken cancellationToken = default) =>
        CompanyLookupRules.TryNormalizeQuery(q, out string? query)
            ? Json(await _researchApi.SuggestCompaniesAsync(query, cancellationToken))
            : BadRequest(new { error = InvalidCompanyInput });

    /// <summary>
    /// The Company Profile lookup of <paramref name="ticker"/>, as JSON, from ApiService. A rejected ticker is 400; an
    /// ApiService failure (429 too many lookups, 503 SEC EDGAR unreachable) keeps its status code with a safe message.
    /// </summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> Company([FromQuery] string? ticker, CancellationToken cancellationToken = default) =>
        CompanyLookupRules.TryNormalizeTicker(ticker, out string? normalized)
            ? Json(await _researchApi.GetCompanyAsync(normalized, cancellationToken))
            : BadRequest(new { error = InvalidCompanyInput });

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
            : BadRequest(new { error = InvalidCompanyInput });

    /// <summary>
    /// Reads one dataset; null when it has never loaded (503) or ApiService cannot be reached, so only that tab shows
    /// the Unavailable State.
    /// </summary>
    private async Task<TSnapshot?> ReadAsync<TSnapshot>(Func<CancellationToken, Task<TSnapshot>> read, string dataset,
        CancellationToken cancellationToken) where TSnapshot : class
    {
        try
        {
            return await read(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not load the Research {Dataset} data", dataset);
            return null;
        }
    }
}
