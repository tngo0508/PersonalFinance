namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Result of checking whether an AI request is allowed under the current quota.
/// </summary>
public class QuotaCheckResult
{
    /// <summary>
    /// Whether the request is allowed.
    /// </summary>
    public bool Allowed { get; set; }

    /// <summary>
    /// Reason for denial, or null if allowed.
    /// </summary>
    public string? DenialReason { get; set; }

    /// <summary>
    /// True if the global monthly budget is approaching the warning threshold.
    /// </summary>
    public bool BudgetWarning { get; set; }
}

/// <summary>
/// Manages AI assistant token usage tracking and spend cap enforcement.
/// </summary>
public interface IAiQuotaService
{
    /// <summary>
    /// Checks whether the given user is allowed to make an AI request.
    /// </summary>
    Task<QuotaCheckResult> CheckQuotaAsync(string userId, string endpoint);

    /// <summary>
    /// Records token usage after a successful AI call.
    /// </summary>
    Task RecordUsageAsync(string userId, string endpoint, int promptTokens, int completionTokens,
        int cachedPromptTokens, decimal estimatedCostUsd);
}
