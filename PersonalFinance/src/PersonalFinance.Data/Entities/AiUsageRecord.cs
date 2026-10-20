namespace PersonalFinance.Data.Entities;

/// <summary>
/// Records token usage for each AI assistant API call so the system can enforce spend limits.
/// </summary>
public class AiUsageRecord : AuditableEntity
{
    /// <summary>
    /// The endpoint that was called (e.g., "chat", "summarize").
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Number of prompt (input) tokens consumed.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Number of completion (output) tokens generated.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Number of prompt tokens served from cache (cheaper rate).
    /// </summary>
    public int CachedPromptTokens { get; set; }

    /// <summary>
    /// Estimated cost in USD based on the model's pricing.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; }
}
