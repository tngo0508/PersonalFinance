using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// API controller for the Journey (GLOSSARY.md): an Account's progress along the road, its Review cards and its
/// Dashboard preference. Unlike <see cref="LearningController"/>, every action needs the signed-in Account (see
/// XUserIdAuthenticationHandler); the Library stays public. Each action takes the caller's Language code; an unknown
/// or missing one means English.
/// </summary>
[Authorize]
[ApiController]
[Route("api/learning/journey")]
[Produces("application/json")]
public class JourneyController : ControllerBase
{
    private readonly IJourneyService _journey;

    public JourneyController(IJourneyService journey)
    {
        _journey = journey;
    }

    /// <summary>The Account's Journey: every Lesson's status and when the next one opens.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(JourneyStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<JourneyStateDto>> GetState([FromQuery] string? language,
        CancellationToken cancellationToken = default) =>
        await _journey.GetStateAsync(User.GetUserId(), language, cancellationToken);

    /// <summary>
    /// Finishes the open Lesson with this slug once its Check is answered, and returns the Journey. A finished Lesson
    /// is left as it is. An unknown slug gets 404; a locked Lesson 409 with an <see cref="ApiErrorDto"/>.
    /// </summary>
    [HttpPost("lessons/{slug}/finish")]
    [ProducesResponseType(typeof(JourneyStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JourneyStateDto>> Finish(string slug, [FromBody] FinishLessonRequest request,
        [FromQuery] string? language, CancellationToken cancellationToken = default)
    {
        JourneyStateDto? state = await _journey.FinishLessonAsync(User.GetUserId(), slug, request, language, cancellationToken);
        return state is null ? NotFound() : state;
    }

    /// <summary>Today's Review cards, up to three from the finished Lessons; none on Day 1 or after the Journey ends.</summary>
    [HttpGet("review")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ReviewDto>> GetReview([FromQuery] string? language,
        CancellationToken cancellationToken = default) =>
        await _journey.GetReviewAsync(User.GetUserId(), language, cancellationToken);

    /// <summary>Marks the card for this Term "Knew it" or "Not yet"; a Term not in a finished Lesson gets 404.</summary>
    [HttpPost("review/{slug}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkCard(string slug, [FromBody] MarkReviewCardRequest request,
        CancellationToken cancellationToken = default) =>
        await _journey.MarkReviewCardAsync(User.GetUserId(), slug, request, cancellationToken)
            ? NoContent()
            : NotFound();

    /// <summary>Shows or hides the quiet Atelier card on the Dashboard.</summary>
    [HttpPut("dashboard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetShowOnDashboard([FromBody] ShowOnDashboardRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _journey.SetShowOnDashboardAsync(User.GetUserId(), request.Show, cancellationToken);
        return NoContent();
    }
}
