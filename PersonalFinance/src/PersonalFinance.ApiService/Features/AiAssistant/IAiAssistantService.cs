using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.News;

namespace PersonalFinance.ApiService.Features.AiAssistant;

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

    /// <summary>
    /// Writes a Money Priorities Purpose Note: plain text of about 120 words at most explaining how the given priority
    /// serves the user's Purpose. Only the facts in <paramref name="request"/> reach the model. The Purpose gets the
    /// prompt-injection check (not the keyword scope filter); the call counts against the shared AI quota as endpoint
    /// <c>money-priorities-note</c>. Never throws for a missing key, a spent quota or a failed call: the
    /// <see cref="PurposeNoteResponseDto.State"/> says which.
    /// </summary>
    /// <param name="userId">The Account the note is for, for the quota.</param>
    /// <param name="request">The Walk's Purpose, answers, path labels, leaf and figures.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PurposeNoteResponseDto> WritePurposeNoteAsync(string userId, PurposeNoteRequestDto request,
        CancellationToken cancellationToken = default);
}
