using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Feedback;

/// <summary>
/// API controller for Feedback. Sending Feedback is scoped to the authenticated Account
/// (see XUserIdAuthenticationHandler); reading the Feedback inbox requires the Admin Policy.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FeedbackController : ControllerBase
{
    private readonly IValidator<ChangeFeedbackStatusRequest> _changeStatusValidator;
    private readonly IFeedbackService _feedbackService;
    private readonly IValidator<SubmitFeedbackRequest> _submitValidator;

    public FeedbackController(IFeedbackService feedbackService,
        IValidator<SubmitFeedbackRequest> submitValidator,
        IValidator<ChangeFeedbackStatusRequest> changeStatusValidator)
    {
        _feedbackService = feedbackService;
        _submitValidator = submitValidator;
        _changeStatusValidator = changeStatusValidator;
    }

    /// <summary>
    /// Sends Feedback as the current Account. It is stored with status New. An invalid request gets 400 and does
    /// not count toward the limit; an Account that has sent too much Feedback within the last hour gets 429 and
    /// nothing is stored.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit([FromBody] SubmitFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _submitValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        SubmitFeedbackResult result = await _feedbackService.SubmitAsync(User.GetUserId(), request, cancellationToken);
        if (result.RateLimited)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = FeedbackMessages.RateLimited });
        }

        return NoContent();
    }

    /// <summary>
    /// Lists Feedback for the Admin inbox, newest first. With <paramref name="status"/>, only Feedback with
    /// that Feedback Status; without one, Archived Feedback is hidden.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(List<FeedbackAdminItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] FeedbackStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (status is not null && !Enum.IsDefined(status.Value))
        {
            return BadRequest(new { errors = new[] { FeedbackMessages.UnknownStatus } });
        }

        return Ok(await _feedbackService.ListAsync(status, cancellationToken));
    }

    /// <summary>
    /// Sets the Feedback Status of one piece of Feedback. Unknown (or soft-deleted) Feedback returns 404.
    /// </summary>
    [HttpPut("{id:int}/status")]
    [Authorize(Policy = AppPolicies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeFeedbackStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _changeStatusValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        return await _feedbackService.ChangeStatusAsync(id, request.Status, cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
