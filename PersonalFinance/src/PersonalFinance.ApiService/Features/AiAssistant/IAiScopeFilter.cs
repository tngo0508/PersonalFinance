using PersonalFinance.Shared.DTOs.AiAssistant;

namespace PersonalFinance.ApiService.Features.AiAssistant;

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
    /// <param name="aboutTerm">
    /// The message asks about a Published Term Entry (Ask the AI), which is finance by construction: it needs no finance
    /// keyword of its own, but the injection, greeting and off-topic checks still apply.
    /// </param>
    /// <returns>A refusal or greeting response, or null if the message is in scope.</returns>
    AiChatResponseDto? Evaluate(string message, bool aboutTerm = false);
}
