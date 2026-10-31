using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// API controller for the Research page's public market datasets. Read-only: the datasets are built by the Research
/// refresh job, never by a request.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ResearchController : ControllerBase
{
    /// <summary>The 503 reason of a Sectors dataset that has never loaded.</summary>
    public const string SectorsNeverLoaded = "The Sectors data has not loaded yet.";

    /// <summary>The 503 reason of a funds dataset that has never loaded.</summary>
    public const string FundsNeverLoaded = "The funds data has not loaded yet.";

    /// <summary>The 503 reason of a fund overlap dataset that has never loaded.</summary>
    public const string FundOverlapNeverLoaded = "The fund overlap data has not loaded yet.";

    /// <summary>The 503 reason of a fund returns dataset that has never loaded.</summary>
    public const string FundReturnsNeverLoaded = "The fund returns data has not loaded yet.";

    /// <summary>The 503 reason of a macro dataset that has never loaded.</summary>
    public const string MacroNeverLoaded = "The inflation and interest rate data has not loaded yet.";

    /// <summary>The 503 reason of a fund facts dataset that has never loaded.</summary>
    public const string FundFactsNeverLoaded = "The fund facts data has not loaded yet.";

    /// <summary>The 503 reason of a fund exposure dataset that has never loaded.</summary>
    public const string FundExposureNeverLoaded = "The fund exposure data has not loaded yet.";

    /// <summary>The 503 reason of a fund industries dataset that has never loaded.</summary>
    public const string FundIndustriesNeverLoaded = "The fund industries data has not loaded yet.";

    /// <summary>The 503 reason of a World dataset that has never loaded.</summary>
    public const string WorldNeverLoaded = "The World data has not loaded yet.";

    private readonly IResearchService _researchService;

    public ResearchController(IResearchService researchService)
    {
        _researchService = researchService;
    }

    /// <summary>
    /// Gets the Sectors snapshot: the largest SEC filers by Public Float per year. Answers 503 with the reason when
    /// the dataset has never loaded.
    /// </summary>
    [HttpGet("sectors")]
    [ProducesResponseType(typeof(SectorsSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSectors(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetSectorsAsync(cancellationToken), SectorsNeverLoaded);

    /// <summary>
    /// Gets the funds snapshot: what each Tracked Fund held each quarter, from their Form N-PORT reports. Answers
    /// 503 with the reason when the dataset has never loaded.
    /// </summary>
    [HttpGet("funds")]
    [ProducesResponseType(typeof(FundsSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFunds(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetFundsAsync(cancellationToken), FundsNeverLoaded);

    /// <summary>
    /// Gets the fund overlap snapshot: every pair of Tracked Funds' overlap by weight and largest shared holdings, from
    /// each fund's latest Form N-PORT report. Answers 503 with the reason when the dataset has never loaded.
    /// </summary>
    [HttpGet("funds/overlap")]
    [ProducesResponseType(typeof(FundOverlapSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFundOverlap(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetFundOverlapAsync(cancellationToken), FundOverlapNeverLoaded);

    /// <summary>
    /// Gets the fund returns snapshot: each Tracked Fund's monthly NAV total returns from its Form N-PORT reports, with
    /// per-window returns, volatility, Sharpe ratio, worst fall and pairwise correlation. Answers 503 with the reason
    /// when the dataset has never loaded.
    /// </summary>
    [HttpGet("funds/returns")]
    [ProducesResponseType(typeof(FundReturnsSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFundReturns(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetFundReturnsAsync(cancellationToken), FundReturnsNeverLoaded);

    /// <summary>
    /// Gets the macro snapshot: monthly CPI-U from the Bureau of Labor Statistics, and the effective federal funds rate
    /// and 3-month Treasury bill rate from the Fed Board's H.15 release. Answers 503 with the reason when the dataset
    /// has never loaded.
    /// </summary>
    [HttpGet("macro")]
    [ProducesResponseType(typeof(MacroSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMacro(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetMacroAsync(cancellationToken), MacroNeverLoaded);

    /// <summary>
    /// Gets the fund facts snapshot: each Tracked Fund's net assets (from Form N-PORT) and expense ratio (from its
    /// shareholder report's XBRL). Answers 503 with the reason when the dataset has never loaded.
    /// </summary>
    [HttpGet("funds/facts")]
    [ProducesResponseType(typeof(FundFactsSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFundFacts(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetFundFactsAsync(cancellationToken), FundFactsNeverLoaded);

    /// <summary>
    /// Gets the fund exposure snapshot: each Tracked Fund's share of net assets by issuer country and by currency, from
    /// every holding of its latest Form N-PORT report. Answers 503 with the reason when the dataset has never loaded.
    /// </summary>
    [HttpGet("funds/exposure")]
    [ProducesResponseType(typeof(FundExposureSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFundExposure(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetFundExposureAsync(cancellationToken), FundExposureNeverLoaded);

    /// <summary>
    /// Gets the fund industries snapshot: each Tracked Fund's share of net assets by SEC industry office and SIC industry
    /// with each industry's largest companies, then the stocks SEC does not classify by issuer country, bonds by issuer
    /// type, and cash, from every holding of its latest Form N-PORT report. Answers 503 with the reason when the
    /// dataset has never loaded.
    /// </summary>
    [HttpGet("funds/industries")]
    [ProducesResponseType(typeof(FundIndustriesSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFundIndustries(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetFundIndustriesAsync(cancellationToken), FundIndustriesNeverLoaded);

    /// <summary>
    /// Gets the World snapshot: each country's stock-market capitalisation and market cap as % of GDP per year, from
    /// the World Bank. Answers 503 with the reason when the dataset has never loaded.
    /// </summary>
    [HttpGet("world")]
    [ProducesResponseType(typeof(WorldSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWorld(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetWorldAsync(cancellationToken), WorldNeverLoaded);

    /// <summary>
    /// Gets each stored dataset's SourceAsOf and BuiltAt, without the datasets themselves. Always answers 200: the
    /// list is empty when nothing has loaded.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ResearchStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken = default) =>
        Ok(await _researchService.GetStatusAsync(cancellationToken));

    /// <summary>200 with the snapshot, or 503 with <paramref name="reason"/> when the dataset has never loaded.</summary>
    private IActionResult SnapshotOrUnavailable<TSnapshot>(ResearchSnapshotResult<TSnapshot> result, string reason)
        where TSnapshot : class =>
        result.Snapshot is null
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, new ResearchUnavailableDto { Reason = reason })
            : Ok(result.Snapshot);
}
