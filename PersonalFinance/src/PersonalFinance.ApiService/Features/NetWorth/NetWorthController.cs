using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.NetWorth.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.NetWorth;

/// <summary>
/// RESTful API controller for net worth projection calculations and parameter persistence.
/// Delegates projection computation to <see cref="NetWorthProjectionService"/> and
/// parameter CRUD to <see cref="INetWorthParametersService"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NetWorthController : ControllerBase
{
    private readonly INetWorthParametersService _parametersService;

    public NetWorthController(INetWorthParametersService parametersService)
    {
        _parametersService = parametersService;
    }

    /// <summary>
    /// Computes a net worth projection for the given parameters.
    /// </summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(NetWorthProjectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CalculateProjection([FromBody] NetWorthProjectionRequestDto request)
    {
        ApiErrorEntryDto? error = NetWorthProjectionValidator.Validate(request);
        if (error != null)
        {
            return BadRequest(new ApiErrorDto { Errors = [error] });
        }

        NetWorthProjectionDto projection = NetWorthProjectionService.ComputeProjection(request);
        return Ok(projection);
    }

    /// <summary>
    /// Saves or updates the net worth projection parameters for the authenticated user.
    /// </summary>
    [HttpPost("parameters")]
    [ProducesResponseType(typeof(NetWorthParametersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveParameters([FromBody] NetWorthProjectionRequestDto request)
    {
        ApiErrorEntryDto? error = NetWorthProjectionValidator.Validate(request);
        if (error != null)
        {
            return BadRequest(new ApiErrorDto { Errors = [error] });
        }

        string userId = User.GetUserId();
        NetWorthParametersDto result = await _parametersService.SaveAsync(userId, request, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Loads the saved net worth projection parameters for the authenticated user.
    /// </summary>
    [HttpGet("parameters")]
    [ProducesResponseType(typeof(NetWorthParametersDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadParameters()
    {
        string userId = User.GetUserId();
        NetWorthParametersDto? parameters = await _parametersService.LoadAsync(userId, HttpContext.RequestAborted);

        if (parameters == null)
        {
            return Ok(new NetWorthParametersDto());
        }

        return Ok(parameters);
    }

    /// <summary>
    /// Deletes the saved net worth projection parameters for the authenticated user.
    /// </summary>
    [HttpDelete("parameters")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteParameters()
    {
        string userId = User.GetUserId();
        await _parametersService.DeleteAsync(userId, HttpContext.RequestAborted);
        return NoContent();
    }

}
