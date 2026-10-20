using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Enforces per-user daily message limits and a global monthly budget ceiling for the AI assistant.
/// Uses the database for persistent tracking so limits survive restarts.
/// </summary>
public class AiQuotaService : IAiQuotaService
{
    /// <summary>
    /// gpt-4o-mini pricing per 1M tokens.
    /// </summary>
    private const decimal InputTokenPricePerMillion = 0.15m;

    /// <summary>
    /// gpt-4o-mini cached input pricing per 1M tokens.
    /// </summary>
    private const decimal CachedInputTokenPricePerMillion = 0.075m;

    /// <summary>
    /// gpt-4o-mini output pricing per 1M tokens.
    /// </summary>
    private const decimal OutputTokenPricePerMillion = 0.60m;

    /// <summary>
    /// Maximum messages per user per day.
    /// </summary>
    private readonly int MaxDailyMessagesPerUser;

    /// <summary>
    /// Global monthly budget ceiling in USD.
    /// </summary>
    private readonly decimal MonthlyBudgetCeilingUsd;

    /// <summary>
    /// Warning threshold as a fraction of the monthly budget (90%).
    /// </summary>
    private readonly decimal WarningThreshold;

    private readonly AppDbContext _dbContext;

    public AiQuotaService(AppDbContext dbContext, IConfiguration? configuration = null)
    {
        _dbContext = dbContext;
        MaxDailyMessagesPerUser = Math.Max(0, configuration?.GetValue<int?>("OpenAI:DailyMessageLimit") ?? 30);
        MonthlyBudgetCeilingUsd = Math.Max(0m, configuration?.GetValue<decimal?>("OpenAI:MonthlyBudgetUsd") ?? 10m);
        WarningThreshold = Math.Clamp(configuration?.GetValue<decimal?>("OpenAI:BudgetWarningThreshold") ?? 0.9m, 0m, 1m);
    }

    /// <summary>
    /// Checks whether the given user is allowed to make an AI request.
    /// </summary>
    public async Task<QuotaCheckResult> CheckQuotaAsync(string userId, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId == "anonymous")
        {
            return new QuotaCheckResult { Allowed = false, DenialReason = "Please sign in to use the AI assistant." };
        }
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        // Check per-user daily limit
        var userDailyCount = await _dbContext.AiUsageRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.CreatedAt >= todayStart)
            .CountAsync();

        if (userDailyCount >= MaxDailyMessagesPerUser)
        {
            return new QuotaCheckResult
            {
                Allowed = false,
                DenialReason = $"You have reached the daily limit of {MaxDailyMessagesPerUser} messages. Please try again tomorrow.",
            };
        }

        // Check global monthly budget
        var monthlyTotalCost = await _dbContext.AiUsageRecords
            .AsNoTracking()
            .Where(r => r.CreatedAt >= monthStart)
            .SumAsync(r => r.EstimatedCostUsd);

        if (monthlyTotalCost >= MonthlyBudgetCeilingUsd)
        {
            return new QuotaCheckResult
            {
                Allowed = false,
                DenialReason = "The AI assistant has reached its monthly budget limit. It will be available again next month.",
            };
        }

        var budgetWarning = monthlyTotalCost >= MonthlyBudgetCeilingUsd * WarningThreshold;

        return new QuotaCheckResult
        {
            Allowed = true,
            BudgetWarning = budgetWarning,
        };
    }

    /// <summary>
    /// Records token usage after a successful AI call.
    /// </summary>
    public async Task RecordUsageAsync(string userId, string endpoint, int promptTokens,
        int completionTokens, int cachedPromptTokens, decimal estimatedCostUsd)
    {
        var record = new AiUsageRecord
        {
            UserId = userId,
            Endpoint = endpoint,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            CachedPromptTokens = cachedPromptTokens,
            EstimatedCostUsd = estimatedCostUsd,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.AiUsageRecords.Add(record);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Calculates the estimated cost for a given token usage using gpt-4o-mini pricing.
    /// </summary>
    public static decimal CalculateEstimatedCost(int promptTokens, int completionTokens, int cachedPromptTokens)
    {
        // Non-cached input tokens
        promptTokens = Math.Max(0, promptTokens);
        completionTokens = Math.Max(0, completionTokens);
        cachedPromptTokens = Math.Clamp(cachedPromptTokens, 0, promptTokens);
        var nonCachedInputTokens = promptTokens - cachedPromptTokens;

        var inputCost = nonCachedInputTokens * InputTokenPricePerMillion / 1_000_000m;
        var cachedInputCost = cachedPromptTokens * CachedInputTokenPricePerMillion / 1_000_000m;
        var outputCost = completionTokens * OutputTokenPricePerMillion / 1_000_000m;

        return inputCost + cachedInputCost + outputCost;
    }
}
