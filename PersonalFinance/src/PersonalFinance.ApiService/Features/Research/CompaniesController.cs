using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// API controller for the Companies tab of Research: search suggestions, Company Profiles and their press releases from
/// SEC EDGAR, for information only. Read-only; nothing about the Account is stored.
/// </summary>
[Authorize]
[ApiController]
[Route("api/Research/companies")]
[Produces("application/json")]
public class CompaniesController : ControllerBase
{
    /// <summary>The 503 reason when SEC's ticker list has never been read and cannot be now.</summary>
    public const string TickerListUnavailable = "The company list has not loaded yet.";

    /// <summary>The 503 reason when SEC EDGAR cannot be reached and no profile of the ticker is kept.</summary>
    public const string ProfileUnavailable = "SEC EDGAR could not be reached.";

    /// <summary>The 429 message when the Account has made too many lookups.</summary>
    public const string TooManyLookups = "Too many lookups. Try again shortly.";

    /// <summary>The 400 message of a query that is empty or too long.</summary>
    public const string InvalidQuery = "Enter 1 to 40 characters.";

    /// <summary>The 400 message of a ticker that is not 1 to 10 letters, digits and dashes.</summary>
    public const string InvalidTicker = "Enter a ticker of up to 10 letters, digits, dots or dashes.";

    private readonly ICompanyProfileService _companies;

    public CompaniesController(ICompanyProfileService companies)
    {
        _companies = companies;
    }

    /// <summary>
    /// Gets up to eight companies matching <paramref name="q"/> by ticker or name, exact ticker first. Answers 400
    /// when the query is empty or longer than 40 characters, and 503 with the reason when SEC's ticker list cannot be
    /// read.
    /// </summary>
    [HttpGet("suggest")]
    [ProducesResponseType(typeof(List<CompanySuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Suggest([FromQuery] string? q, CancellationToken cancellationToken = default)
    {
        if (!CompanyLookupRules.TryNormalizeQuery(q, out string? query))
        {
            return BadRequest(new { errors = new[] { InvalidQuery } });
        }

        List<CompanySuggestionDto>? suggestions = await _companies.SuggestAsync(query, cancellationToken);
        return suggestions is null
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, new ResearchUnavailableDto { Reason = TickerListUnavailable })
            : Ok(suggestions);
    }

    /// <summary>
    /// Looks up a ticker: a Company Profile, a fund, or a ticker SEC does not list, each as 200. Answers 400 for a
    /// ticker that is not 1 to 10 letters, digits, dots or dashes, 429 when the Account has made too many lookups in
    /// the last minute, and 503 when SEC EDGAR cannot be reached and no profile of the ticker is kept.
    /// </summary>
    [HttpGet("{ticker}")]
    [ProducesResponseType(typeof(CompanyLookupResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(string ticker, CancellationToken cancellationToken = default)
    {
        if (!CompanyLookupRules.TryNormalizeTicker(ticker, out string? normalized))
        {
            return BadRequest(new { errors = new[] { InvalidTicker } });
        }

        return Answer(await _companies.LookUpAsync(User.GetUserId(), normalized, cancellationToken));
    }

    /// <summary>
    /// Gets a company's press releases by ticker: the announcements it attached to its 8-K filings in the last 12
    /// months, newest first, at most 10, or a fund or a ticker SEC does not list, each as 200. Answers 400 for a ticker
    /// that is not 1 to 10 letters, digits, dots or dashes, 404 when press releases are switched off
    /// (<see cref="ResearchSettings.PressReleasesEnabledKey"/>), 429 when the Account has made too many lookups in the
    /// last minute, and 503 when SEC EDGAR cannot be reached and no list of the ticker is kept.
    /// </summary>
    [HttpGet("{ticker}/releases")]
    [ProducesResponseType(typeof(CompanyPressReleasesResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Releases(string ticker, CancellationToken cancellationToken = default)
    {
        if (!CompanyLookupRules.TryNormalizeTicker(ticker, out string? normalized))
        {
            return BadRequest(new { errors = new[] { InvalidTicker } });
        }

        return Answer(await _companies.GetPressReleasesAsync(User.GetUserId(), normalized, cancellationToken));
    }

    /// <summary>
    /// The response to a request by ticker: 200 with the answer, 404 when switched off, 429 when the Account has made
    /// too many lookups, and 503 when SEC EDGAR cannot be reached and nothing is kept.
    /// </summary>
    private IActionResult Answer<TResult>(CompanyLookupOutcome<TResult> outcome) where TResult : class
    {
        if (outcome.SwitchedOff)
        {
            return NotFound();
        }

        if (outcome.RateLimited)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = TooManyLookups });
        }

        return outcome.Result is { } result
            ? Ok(result)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new ResearchUnavailableDto { Reason = ProfileUnavailable });
    }
}
