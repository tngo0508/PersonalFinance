using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.AiAssistant.Commands;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.News;

namespace PersonalFinance.ApiService.AiAssistant;

/// <summary>
/// API controller for the AI assistant chat feature.
/// All endpoints are scoped to the current user identified via the X-User-Id header forwarded by the Web proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AiAssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    public AiAssistantController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the current user's ID from the X-User-Id header forwarded by the Web proxy.
    /// </summary>
    private string GetUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userId))
        {
            var value = userId.First();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return "anonymous";
    }

    /// <summary>
    /// Sends a chat message to the AI assistant and returns the response.
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        var userId = GetUserId();
        var response = await _mediator.Send(new ChatCommand { UserId = userId, Request = request }, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    ///     Summarizes a news article using the AI assistant.
    /// </summary>
    [HttpPost("summarize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Summarize([FromBody] NewsArticleSummaryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Title)) return BadRequest(new { error = "Title is required." });

        var userId = GetUserId();

        try
        {
            var response = await _mediator.Send(new SummarizeArticleCommand { UserId = userId, Request = request }, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Rate limit exceeded. Please try again later." });
        }
    }
}