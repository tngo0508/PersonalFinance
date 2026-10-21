using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.News;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service for interacting with the AI assistant (OpenAI).
/// </summary>
public interface IAiAssistantService
{
    /// <summary>
    /// Sends a chat message to the AI assistant and returns the response.
    /// </summary>
    /// <param name="userId">The user's ID for personalization.</param>
    /// <param name="request">The chat request containing the message and optional conversation history.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI assistant's response.</returns>
    Task<AiChatResponseDto> SendChatMessageAsync(string userId, AiChatRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Summarizes a news article using the AI assistant.
    /// </summary>
    /// <param name="userId">The user's ID for rate limiting.</param>
    /// <param name="request">The summary request containing the article title and description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI-generated summary response.</returns>
    Task<NewsArticleSummaryResponseDto> SummarizeArticleAsync(string userId, NewsArticleSummaryRequestDto request,
        CancellationToken cancellationToken = default);
}
