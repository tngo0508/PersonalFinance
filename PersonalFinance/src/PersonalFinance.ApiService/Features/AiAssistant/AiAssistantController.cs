using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.Learning;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.News;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.AiAssistant;

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
    private readonly ILearningLibraryService _learning;

    public AiAssistantController(IAiAssistantService aiAssistantService, IAiScopeFilter scopeFilter,
        IAiQuotaService quotaService, ILearningLibraryService learning)
    {
        _aiAssistantService = aiAssistantService;
        _scopeFilter = scopeFilter;
        _quotaService = quotaService;
        _learning = learning;
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
            throw new ApiErrorException(ApiErrorKeys.AiAssistant.MessageRequired, "Message is required.",
                nameof(AiChatRequestDto.Message));
        }

        // Enforce message length limit
        if (request.Message.Length > AiAssistantMessages.MaxMessageLength)
        {
            throw new ApiErrorException(ApiErrorKeys.AiAssistant.MessageTooLong,
                $"Message must be under {AiAssistantMessages.MaxMessageLength} characters.",
                nameof(AiChatRequestDto.Message), ErrorArgs.Number("max", AiAssistantMessages.MaxMessageLength));
        }

        // Check scope before calling the LLM; a question about a Published Term Entry is finance by construction
        bool aboutTerm = !string.IsNullOrWhiteSpace(request.TermSlug)
                         && _learning.FindTerm(request.TermSlug, null) is not null;
        AiChatResponseDto? cannedResponse = _scopeFilter.Evaluate(request.Message, aboutTerm);
        if (cannedResponse != null)
        {
            return Ok(cannedResponse);
        }

        string userId = User.GetUserId();

        // Check quota before calling the LLM
        QuotaCheckResult quotaResult = await _quotaService.CheckQuotaAsync(userId, "chat");
        if (!quotaResult.Allowed)
        {
            throw quotaResult.ToRejection(StatusCodes.Status429TooManyRequests);
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
        if (string.IsNullOrWhiteSpace(request?.Title))
        {
            throw new ApiErrorException(ApiErrorKeys.AiAssistant.TitleRequired, "Title is required.",
                nameof(NewsArticleSummaryRequestDto.Title));
        }

        string userId = User.GetUserId();

        QuotaCheckResult quotaResult = await _quotaService.CheckQuotaAsync(userId, "summarize");
        if (!quotaResult.Allowed)
        {
            throw quotaResult.ToRejection(StatusCodes.Status429TooManyRequests);
        }

        NewsArticleSummaryResponseDto response = await _aiAssistantService.SummarizeArticleAsync(userId, request, cancellationToken);
        response.Disclaimer = AiAssistantMessages.Disclaimer;
        response.BudgetWarning = quotaResult.BudgetWarning ? AiAssistantMessages.BudgetWarning : null;
        return Ok(response);
    }
}
