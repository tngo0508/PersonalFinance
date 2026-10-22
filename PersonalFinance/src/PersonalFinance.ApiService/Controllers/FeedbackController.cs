using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Controllers;

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
    private readonly IFeedbackService _feedbackService;

    public FeedbackController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    /// <summary>
    /// Sends Feedback as the current Account. It is stored with status New.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Submit([FromBody] SubmitFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        var result = await _feedbackService.SubmitAsync(userId, request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return NoContent();
    }

    /// <summary>
    /// Lists all Feedback for the Admin inbox, newest first.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(List<FeedbackAdminItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default)
    {
        return Ok(await _feedbackService.ListAsync(cancellationToken));
    }
}
