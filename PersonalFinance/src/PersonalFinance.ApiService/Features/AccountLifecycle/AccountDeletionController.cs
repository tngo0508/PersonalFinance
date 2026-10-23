using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AccountDeletion;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.AccountLifecycle;

/// <summary>
/// API controller for Deletion Requests (ADR 0005). Making one is scoped to the authenticated Account (see
/// XUserIdAuthenticationHandler); cancelling one needs no Account, because the Account is locked out by then, and is
/// authorized by the single-use token from the confirmation email instead.
/// </summary>
[Authorize]
[ApiController]
[Route("api/account-deletion")]
[Produces("application/json")]
public class AccountDeletionController : ControllerBase
{
    private readonly IAccountDeletionService _accountDeletionService;
    private readonly IValidator<CancelDeletionRequest> _cancelValidator;

    public AccountDeletionController(IAccountDeletionService accountDeletionService,
        IValidator<CancelDeletionRequest> cancelValidator)
    {
        _accountDeletionService = accountDeletionService;
        _cancelValidator = cancelValidator;
    }

    /// <summary>
    /// Makes a Deletion Request for the current Account and returns the cancel token for the confirmation email.
    /// The last Admin, or an Account with a request already pending, gets 409; an unknown Account gets 404.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DeletionRequestAcceptedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        DeletionRequestResult result = await _accountDeletionService.RequestAsync(User.GetUserId(), cancellationToken);
        return result.Outcome switch
        {
            DeletionRequestOutcome.Accepted => Ok(new DeletionRequestAcceptedDto
            {
                CancelToken = result.CancelToken!,
                PurgeAfterUtc = result.PurgeAfter!.Value
            }),
            DeletionRequestOutcome.LastAdmin => Conflict(new { error = AccountDeletionMessages.LastAdmin }),
            DeletionRequestOutcome.AlreadyPending => Conflict(new { error = AccountDeletionMessages.AlreadyPending }),
            _ => NotFound()
        };
    }

    /// <summary>
    /// Cancels the pending Deletion Request that <paramref name="request"/>'s token belongs to. A malformed token
    /// gets 400; an unknown, used or expired one gets 404.
    /// </summary>
    [HttpPost("cancel")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel([FromBody] CancelDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _cancelValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage).Distinct() });
        }

        return await _accountDeletionService.CancelAsync(request.Token, cancellationToken)
            ? NoContent()
            : NotFound(new { error = AccountDeletionMessages.InvalidCancelLink });
    }
}
