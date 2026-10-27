using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.News;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.AiAssistant;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// Proxy controller that forwards AI Assistant chat requests to the backend API service
/// via the IAiAssistantApi Refit client. ApiService errors (including 429 usage limits) are returned as
/// <c>{ "error": "..." }</c> with the same status code by <see cref="JsonApiErrorsAttribute"/>.
/// </summary>
[Authorize]
[JsonApiErrors]
[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
[Produces("application/json")]
public class AiAssistantController : ControllerBase
{
    private readonly IAiAssistantApi _aiAssistantApi;
    private readonly IStringLocalizer _localizer;

    public AiAssistantController(IAiAssistantApi aiAssistantApi, IStringLocalizer localizer)
    {
        _aiAssistantApi = aiAssistantApi;
        _localizer = localizer;
    }

    /// <summary>
    /// Sends a chat message to the AI assistant and returns the response.
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new { error = _localizer["AiAssistant.Validation.MessageRequired"].Value });
        }

        AiChatResponseDto response = await _aiAssistantApi.SendChatMessageAsync(request);
        return Ok(AiAssistantText.Localize(_localizer, response));
    }

    /// <summary>
    ///     Summarizes a news article using the AI assistant.
    /// </summary>
    [HttpPost("summarize")]
    public async Task<IActionResult> Summarize([FromBody] NewsArticleSummaryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request?.Title))
        {
            return BadRequest(new { error = _localizer["AiAssistant.Validation.TitleRequired"].Value });
        }

        NewsArticleSummaryResponseDto response = await _aiAssistantApi.SummarizeArticleAsync(request);
        return Ok(AiAssistantText.Localize(_localizer, response));
    }
}
