using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for the AI assistant chat feature.
/// All endpoints are scoped to the current user identified via the X-User-Id header forwarded by the Web proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AiAssistantController : ControllerBase
{
    private readonly IAiAssistantService _aiAssistantService;

    public AiAssistantController(IAiAssistantService aiAssistantService)
    {
        _aiAssistantService = aiAssistantService;
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
    public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        var userId = GetUserId();
        var response = await _aiAssistantService.SendChatMessageAsync(userId, request, cancellationToken);

        return Ok(response);
    }
}
