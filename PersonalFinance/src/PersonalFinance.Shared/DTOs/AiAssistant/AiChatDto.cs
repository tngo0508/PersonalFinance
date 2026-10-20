namespace PersonalFinance.Shared.DTOs.AiAssistant;

/// <summary>
/// Request DTO for sending a chat message to the AI assistant.
/// </summary>
public class AiChatRequestDto
{
    /// <summary>
    /// The user's message to send to the AI assistant.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional conversation history for context (previous messages).
    /// </summary>
    public List<ChatMessageDto>? ConversationHistory { get; set; }
}

/// <summary>
/// Response DTO for the AI assistant's reply.
/// </summary>
public class AiChatResponseDto
{
    /// <summary>
    /// The AI assistant's response message.
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// The model used for the response (e.g., "gpt-4o-mini").
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// True when the response is a canned refusal or acknowledgement and did not call the LLM.
    /// </summary>
    public bool IsRefusal { get; set; }

    /// <summary>
    /// Non-fiduciary disclaimer shown with AI-generated financial guidance, or null for canned responses.
    /// </summary>
    public string? Disclaimer { get; set; }

    /// <summary>
    /// Timestamp of the response in UTC.
    /// </summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a single chat message in the conversation history.
/// </summary>
public class ChatMessageDto
{
    /// <summary>
    /// The role of the message sender ("user" or "assistant").
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// The content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
