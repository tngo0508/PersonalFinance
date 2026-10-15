using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// Proxy controller that forwards AI Assistant chat requests to the backend API service
/// via the IAiAssistantApi Refit client.
/// </summary>
[Authorize]
[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/[controller]")]
[Produces("application/json")]
public class AiAssistantController : ControllerBase
{
    private readonly IAiAssistantApi _aiAssistantApi;
    private readonly ILogger<AiAssistantController> _logger;

    public AiAssistantController(
        IAiAssistantApi aiAssistantApi,
        ILogger<AiAssistantController> logger)
    {
        _aiAssistantApi = aiAssistantApi;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.Identity?.Name
               ?? "default-user";
    }

    /// <summary>
    /// Sends a chat message to the AI assistant and returns the response.
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        try
        {
            var response = await _aiAssistantApi.SendChatMessageAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending chat message to AI assistant for user {UserId}", GetCurrentUserId());
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Failed to get response from AI assistant." });
        }
    }

    /// <summary>
    ///     Summarizes a news article using the AI assistant.
    /// </summary>
    [HttpPost("summarize")]
    public async Task<IActionResult> Summarize([FromBody] NewsArticleSummaryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request?.Title)) return BadRequest(new { error = "Title is required." });

        try
        {
            var response = await _aiAssistantApi.SummarizeArticleAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing news article for user {UserId}", GetCurrentUserId());
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to generate summary." });
        }
    }
}