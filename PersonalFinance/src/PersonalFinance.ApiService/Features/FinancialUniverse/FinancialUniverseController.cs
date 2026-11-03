using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.FinancialUniverse.Validators;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.FinancialUniverse;

/// <summary>
/// The Financial Universe endpoint (spec #617): one read of the Account's graph for a month. The month is validated
/// here and the projection delegated to <see cref="IFinancialUniverseService"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/financial-universe")]
[Produces("application/json")]
public class FinancialUniverseController : ControllerBase
{
    private readonly IFinancialUniverseService _financialUniverse;
    private readonly TimeProvider _timeProvider;

    public FinancialUniverseController(IFinancialUniverseService financialUniverse, TimeProvider timeProvider)
    {
        _financialUniverse = financialUniverse;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// The graph for <paramref name="year"/> and <paramref name="month"/>; a missing one takes the current month's value.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(FinancialUniverseGraphDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGraph([FromQuery] int? year, [FromQuery] int? month,
        CancellationToken cancellationToken = default)
    {
        (int Year, int Month)? period = FinancialUniverseQueryValidator.Resolve(year, month, _timeProvider.GetUtcNow(),
            out ApiErrorEntryDto? error);
        if (period is null)
        {
            return BadRequest(new ApiErrorDto { Errors = [error!] });
        }

        FinancialUniverseGraphDto graph = await _financialUniverse.GetGraphAsync(User.GetUserId(), period.Value.Year,
            period.Value.Month, cancellationToken);
        return Ok(graph);
    }
}
