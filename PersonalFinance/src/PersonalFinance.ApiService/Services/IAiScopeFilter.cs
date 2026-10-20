using PersonalFinance.Shared.DTOs.AiAssistant;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Classifies user messages for the AI assistant to determine whether they are within scope.
/// </summary>
public interface IAiScopeFilter
{
    /// <summary>
    /// Evaluates the user message and returns a canned response if the message is off-topic or a greeting.
    /// Returns null when the message is finance-related and should proceed to the LLM.
    /// </summary>
    /// <param name="message">The user's input message.</param>
    /// <returns>A refusal or greeting response, or null if the message is in scope.</returns>
    AiChatResponseDto? Evaluate(string message);
}
