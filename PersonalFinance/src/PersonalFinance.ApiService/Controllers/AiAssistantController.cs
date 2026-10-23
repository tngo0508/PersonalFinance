using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.News;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for the AI assistant chat feature.
/// All endpoints are scoped to the current user identified by the authenticated Account (see XUserIdAuthenticationHandler).
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class AiAssistantController : ControllerBase
{
    private readonly IAiAssistantService _aiAssistantService;
    private readonly IAiScopeFilter _scopeFilter;
    private readonly IAiQuotaService _quotaService;

    public AiAssistantController(IAiAssistantService aiAssistantService, IAiScopeFilter scopeFilter,
        IAiQuotaService quotaService)
    {
        _aiAssistantService = aiAssistantService;
        _scopeFilter = scopeFilter;
        _quotaService = quotaService;
    }

    /// <summary>
    /// Sends a chat message to the AI assistant and returns the response.
    /// Off-topic messages are refused immediately without calling the LLM.
    /// Rate limited by per-user daily quota and global monthly budget.
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        // Enforce message length limit
        if (request.Message.Length > AiAssistantMessages.MaxMessageLength)
        {
            return BadRequest(new { error = $"Message must be under {AiAssistantMessages.MaxMessageLength} characters." });
        }

        // Check scope before calling the LLM
        AiChatResponseDto? cannedResponse = _scopeFilter.Evaluate(request.Message);
        if (cannedResponse != null)
        {
            return Ok(cannedResponse);
        }

        string userId = User.GetUserId();

        // Check quota before calling the LLM
        QuotaCheckResult quotaResult = await _quotaService.CheckQuotaAsync(userId, "chat");
        if (!quotaResult.Allowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = quotaResult.DenialReason });
        }

        AiChatResponseDto response = await _aiAssistantService.SendChatMessageAsync(userId, request, cancellationToken);

        // Add disclaimer to AI-generated responses
        response.Disclaimer = AiAssistantMessages.Disclaimer;
        response.BudgetWarning = quotaResult.BudgetWarning ? AiAssistantMessages.BudgetWarning : null;
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

        string userId = User.GetUserId();

        QuotaCheckResult quotaResult = await _quotaService.CheckQuotaAsync(userId, "summarize");
        if (!quotaResult.Allowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = quotaResult.DenialReason });
        }

        try
        {
            NewsArticleSummaryResponseDto response = await _aiAssistantService.SummarizeArticleAsync(userId, request, cancellationToken);
            response.Disclaimer = AiAssistantMessages.Disclaimer;
            response.BudgetWarning = quotaResult.BudgetWarning ? AiAssistantMessages.BudgetWarning : null;
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Rate limit exceeded. Please try again later." });
        }
    }
}
