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
    public async Task<IActionResult> GetSectors(CancellationToken cancellationToken = default)
    {
        ResearchSnapshotResult<SectorsSnapshotDto> result = await _researchService.GetSectorsAsync(cancellationToken);
        if (result.Snapshot is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ResearchUnavailableDto { Reason = SectorsNeverLoaded });
        }

        return Ok(result.Snapshot);
    }
}
