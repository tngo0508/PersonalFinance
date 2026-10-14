using Refit;
using PersonalFinance.Shared.DTOs;

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
    Task<AiChatResponseDto> SendChatMessageAsync([Body] AiChatRequestDto request, CancellationToken cancellationToken = default);
}
