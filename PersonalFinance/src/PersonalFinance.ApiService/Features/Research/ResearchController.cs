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
    /// Gets the World snapshot: each country's stock-market capitalisation and market cap as % of GDP per year, from
    /// the World Bank. Answers 503 with the reason when the dataset has never loaded.
    /// </summary>
    [HttpGet("world")]
    [ProducesResponseType(typeof(WorldSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResearchUnavailableDto), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWorld(CancellationToken cancellationToken = default) =>
        SnapshotOrUnavailable(await _researchService.GetWorldAsync(cancellationToken), WorldNeverLoaded);

    /// <summary>200 with the snapshot, or 503 with <paramref name="reason"/> when the dataset has never loaded.</summary>
    private IActionResult SnapshotOrUnavailable<TSnapshot>(ResearchSnapshotResult<TSnapshot> result, string reason)
        where TSnapshot : class =>
        result.Snapshot is null
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, new ResearchUnavailableDto { Reason = reason })
            : Ok(result.Snapshot);
}
