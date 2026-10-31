using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.InvestmentScenarios;

/// <summary>
/// API controller for the Account's Investment Scenarios (#592). Every endpoint is scoped to the authenticated Account
/// (see XUserIdAuthenticationHandler); another Account's scenario answers 404.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InvestmentScenariosController : ControllerBase
{
    private readonly IInvestmentScenarioService _scenarios;
    private readonly IValidator<SaveInvestmentScenarioRequestDto> _saveValidator;
    private readonly IValidator<DuplicateInvestmentScenarioRequestDto> _duplicateValidator;

    public InvestmentScenariosController(IInvestmentScenarioService scenarios,
        IValidator<SaveInvestmentScenarioRequestDto> saveValidator,
        IValidator<DuplicateInvestmentScenarioRequestDto> duplicateValidator)
    {
        _scenarios = scenarios;
        _saveValidator = saveValidator;
        _duplicateValidator = duplicateValidator;
    }

    /// <summary>The Account's scenarios, last edited first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<InvestmentScenarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default) =>
        Ok(await _scenarios.ListAsync(User.GetUserId(), cancellationToken));

    /// <summary>One scenario.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InvestmentScenarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken = default)
    {
        InvestmentScenarioDto? scenario = await _scenarios.GetAsync(User.GetUserId(), id, cancellationToken);
        return scenario is null ? NotFound() : Ok(scenario);
    }

    /// <summary>Saves a new scenario; 409 when the Account already keeps the most allowed.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(InvestmentScenarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] SaveInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _saveValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        return Ok(await _scenarios.CreateAsync(User.GetUserId(), request, cancellationToken));
    }

    /// <summary>Replaces a scenario's name, note and inputs.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(InvestmentScenarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _saveValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        InvestmentScenarioDto? updated = await _scenarios.UpdateAsync(User.GetUserId(), id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Copies a scenario under a new name; 409 when the Account already keeps the most allowed.</summary>
    [HttpPost("{id:int}/duplicate")]
    [ProducesResponseType(typeof(InvestmentScenarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Duplicate(int id, [FromBody] DuplicateInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _duplicateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        InvestmentScenarioDto? copy = await _scenarios.DuplicateAsync(User.GetUserId(), id, request.Name, cancellationToken);
        return copy is null ? NotFound() : Ok(copy);
    }

    /// <summary>Deletes a scenario (soft delete).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default) =>
        await _scenarios.DeleteAsync(User.GetUserId(), id, cancellationToken) ? NoContent() : NotFound();
}
