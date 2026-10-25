using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.MoneyPriorities;

/// <summary>
/// API controller for Money Priorities. Every endpoint is scoped to the authenticated Account (see
/// XUserIdAuthenticationHandler); another Account's Priority Walk is never visible or deletable.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MoneyPrioritiesController : ControllerBase
{
    private readonly IMoneyPrioritiesService _service;
    private readonly IValidator<SubmitPriorityWalkRequest> _submitValidator;

    public MoneyPrioritiesController(IMoneyPrioritiesService service,
        IValidator<SubmitPriorityWalkRequest> submitValidator)
    {
        _service = service;
        _submitValidator = submitValidator;
    }

    /// <summary>
    /// Walks the Priority Tree from the answers and saves a Priority Walk, unless it is identical to the Account's
    /// latest one, which is returned instead. A rejected request gets 400 with its messages keyed by field.
    /// </summary>
    [HttpPost("walks")]
    [ProducesResponseType(typeof(PriorityWalkResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MoneyPrioritiesValidationErrorsDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Submit([FromBody] SubmitPriorityWalkRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _submitValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ToErrors(validation));
        }

        return Ok(await _service.SubmitAsync(User.GetUserId(), request, cancellationToken));
    }

    /// <summary>The Account's 10 newest Priority Walks, newest first.</summary>
    [HttpGet("walks")]
    [ProducesResponseType(typeof(List<PriorityWalkSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default) =>
        Ok(await _service.ListRecentAsync(User.GetUserId(), cancellationToken));

    /// <summary>One of the Account's Priority Walks with its figures recomputed; 404 when it has none with that id.</summary>
    [HttpGet("walks/{id:int}")]
    [ProducesResponseType(typeof(PriorityWalkResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken = default)
    {
        PriorityWalkResultDto? walk = await _service.GetAsync(User.GetUserId(), id, cancellationToken);
        return walk is null ? NotFound() : Ok(walk);
    }

    /// <summary>Soft-deletes one of the Account's Priority Walks; 404 when it has none with that id.</summary>
    [HttpDelete("walks/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default) =>
        await _service.DeleteAsync(User.GetUserId(), id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>
    /// Writes (or reuses) the Purpose Note of one of the Account's Priority Walks and saves it onto the Walk; 404 when
    /// it has none with that id. Every other outcome (no Purpose, not available, quota spent, refused) is a 200 whose
    /// state says which; the Walk is always kept.
    /// </summary>
    [HttpPost("walks/{id:int}/note")]
    [ProducesResponseType(typeof(PriorityWalkPurposeNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Note(int id, CancellationToken cancellationToken = default)
    {
        PriorityWalkPurposeNoteDto? note = await _service.WritePurposeNoteAsync(User.GetUserId(), id, cancellationToken);
        return note is null ? NotFound() : Ok(note);
    }

    /// <summary>The whole Priority Tree with nothing highlighted, shown before a submit.</summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(PriorityTreeDiagramDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Tree() => Ok(_service.GetTree());

    private static MoneyPrioritiesValidationErrorsDto ToErrors(ValidationResult validation) => new()
    {
        Errors = validation.Errors.Select(e => e.ErrorMessage).ToList(),
        FieldErrors = validation.Errors
            .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToList(), StringComparer.Ordinal)
    };
}
