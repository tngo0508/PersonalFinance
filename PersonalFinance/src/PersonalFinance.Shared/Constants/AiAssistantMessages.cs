namespace PersonalFinance.Shared.Constants;

/// <summary>
/// Static canned messages for the AI assistant's scope filter.
/// These responses require zero LLM token cost because they are returned deterministically.
/// </summary>
public static class AiAssistantMessages
{
    public const string BudgetWarning = "The AI assistant is approaching its monthly budget limit.";

    /// <summary>
    /// Maximum allowed length for a single user message to prevent abuse.
    /// </summary>
    public const int MaxMessageLength = 2000;

    /// <summary>
    /// Standard non-fiduciary disclaimer appended to AI-generated financial guidance.
    /// </summary>
    public const string Disclaimer =
        "This information is for educational and planning purposes only and does not constitute professional financial advice. "
        + "Please consult a qualified financial advisor before making important financial decisions.";

    /// <summary>
    /// Refusal message for off-topic queries that are clearly not personal finance.
    /// </summary>
    public const string OffTopicRefusal =
        "I can help with personal finance questions — budgeting, spending analysis, savings goals, debt reduction, "
        + "retirement planning, and more. What would you like to know?";

    /// <summary>
    /// Short acknowledgement for small talk greetings, guiding the user toward financial topics.
    /// </summary>
    public const string GreetingAcknowledgement =
        "Hello! I'm here to help with your personal finances. What can I do for you today?";
}
