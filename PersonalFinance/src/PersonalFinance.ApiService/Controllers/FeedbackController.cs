using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for Feedback. Sending Feedback is scoped to the authenticated Account
/// (see XUserIdAuthenticationHandler).
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
}
