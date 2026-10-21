using PersonalFinance.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using PersonalFinance.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.Controllers;

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
        var error = NetWorthProjectionValidator.Validate(request);
        if (error != null)
        {
            return BadRequest(error);
        }

        var projection = NetWorthProjectionService.ComputeProjection(request);
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
        var error = NetWorthProjectionValidator.Validate(request);
        if (error != null)
        {
            return BadRequest(error);
        }

        var userId = User.GetUserId();
        var result = await _parametersService.SaveAsync(userId, request, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Loads the saved net worth projection parameters for the authenticated user.
    /// </summary>
    [HttpGet("parameters")]
    [ProducesResponseType(typeof(NetWorthParametersDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadParameters()
    {
        var userId = User.GetUserId();
        var parameters = await _parametersService.LoadAsync(userId, HttpContext.RequestAborted);

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
        var userId = User.GetUserId();
        await _parametersService.DeleteAsync(userId, HttpContext.RequestAborted);
        return NoContent();
    }

}
