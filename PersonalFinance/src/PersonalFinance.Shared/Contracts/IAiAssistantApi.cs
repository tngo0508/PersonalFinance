using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.News;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the AI assistant chat feature.
/// </summary>
public interface IAiAssistantApi
{
    /// <summary>
    /// Sends a chat message to the AI assistant and returns the response.
    /// </summary>
    [Post("/api/AiAssistant/chat")]
    Task<AiChatResponseDto> SendChatMessageAsync([Body] AiChatRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Summarizes a news article using the AI assistant.
    /// </summary>
    [Post("/api/AiAssistant/summarize")]
    Task<NewsArticleSummaryResponseDto> SummarizeArticleAsync([Body] NewsArticleSummaryRequestDto request,
        CancellationToken cancellationToken = default);
}
