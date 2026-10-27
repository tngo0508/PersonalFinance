using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Features.AiAssistant;

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
    /// The <c>ApiErrorKeys.AiAssistant</c> key of the denial, or null if allowed (ADR 0017).
    /// </summary>
    public string? DenialKey { get; set; }

    /// <summary>
    /// The named arguments of <see cref="DenialKey"/>, or null when it has none.
    /// </summary>
    public IReadOnlyDictionary<string, ApiErrorArgDto>? DenialArgs { get; set; }

    /// <summary>
    /// The denial as the rejection the API returns, with the given status code.
    /// </summary>
    public ApiErrorException ToRejection(int statusCode) =>
        new(DenialKey ?? ApiErrorKeys.AiAssistant.DailyLimitReached, DenialReason ?? string.Empty,
            args: DenialArgs, statusCode: statusCode);

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
