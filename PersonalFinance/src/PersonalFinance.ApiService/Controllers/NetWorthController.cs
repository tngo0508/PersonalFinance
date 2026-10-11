using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// RESTful API controller for net worth projection calculations.
/// Accepts projection parameters and returns computed scenarios.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NetWorthController : ControllerBase
{
    /// <summary>
    /// Computes a net worth projection for the given parameters.
    /// </summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(NetWorthProjectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CalculateProjection([FromBody] NetWorthProjectionRequestDto request)
    {
        // Validate financial values
        if (request.StartingBalance < 0 || request.StartingBalance > 1_000_000_000m)
        {
            return BadRequest("Starting balance must be between 0 and 1 billion.");
        }

        if (request.AnnualSavings < 0 || request.AnnualSavings > 10_000_000m)
        {
            return BadRequest("Annual savings must be between 0 and 10 million.");
        }

        if (request.AnnualIncome < 0 || request.AnnualIncome > 100_000_000m)
        {
            return BadRequest("Annual income must be between 0 and 100 million.");
        }

        if (request.AnnualSpending < 0 || request.AnnualSpending > 10_000_000m)
        {
            return BadRequest("Annual spending must be between 0 and 10 million.");
        }

        if (request.AggressiveSavings < 0 || request.AggressiveSavings > 10_000_000m)
        {
            return BadRequest("Aggressive savings must be between 0 and 10 million.");
        }

        // Validate age values
        if (request.CurrentAge < 18 || request.CurrentAge > 100)
        {
            return BadRequest("Current age must be between 18 and 100.");
        }

        if (request.RetirementAge < 18 || request.RetirementAge > 120)
        {
            return BadRequest("Retirement age must be between 18 and 120.");
        }

        if (request.RetirementAge <= request.CurrentAge)
        {
            return BadRequest("Retirement age must be greater than current age.");
        }

        // Validate ROI values
        if (request.BaseRoi < 0 || request.BaseRoi > 50)
        {
            return BadRequest("Base ROI must be between 0% and 50%.");
        }

        if (request.ConservativeRoi < 0 || request.ConservativeRoi > 50)
        {
            return BadRequest("Conservative ROI must be between 0% and 50%.");
        }

        if (request.AggressiveRoi < 0 || request.AggressiveRoi > 50)
        {
            return BadRequest("Aggressive ROI must be between 0% and 50%.");
        }

        var projection = NetWorthProjectionService.ComputeProjection(request);
        return Ok(projection);
    }
}
