using PersonalFinance.Shared.DTOs;

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
    Task<AiChatResponseDto> SendChatMessageAsync(string userId, AiChatRequestDto request, CancellationToken cancellationToken = default);
}
