using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Tests.AiAssistant;

/// <summary>
/// Behaviour of the AI quota service — token tracking, cost calculation, and limit enforcement.
/// </summary>
public class AiQuotaServiceTests
{
    private const string UserId = "quota-user";
    private const string OtherUserId = "other-user";

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Quota_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CheckQuotaAsync_UsesConfiguredLimitsAcrossServiceInstances()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PersistentQuota_{Guid.NewGuid():N}").Options;
        IConfigurationRoot config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenAI:DailyMessageLimit"] = "1",
            ["OpenAI:MonthlyBudgetUsd"] = "2",
            ["OpenAI:BudgetWarningThreshold"] = "0.5"
        }).Build();
        await using (var first = new AppDbContext(options))
        {
            await new AiQuotaService(first, config).RecordUsageAsync(UserId, "chat", 1, 1, 0, 1m);
        }
        await using var second = new AppDbContext(options);
        var quota = new AiQuotaService(second, config);
        Assert.False((await quota.CheckQuotaAsync(UserId, "summarize")).Allowed);
        Assert.True((await quota.CheckQuotaAsync(OtherUserId, "chat")).BudgetWarning);
        await quota.RecordUsageAsync(OtherUserId, "summarize", 1, 1, 0, 1m);
        Assert.False((await quota.CheckQuotaAsync("third-user", "chat")).Allowed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("anonymous")]
    public async Task CheckQuotaAsync_AnonymousRequestsAreDenied(string userId)
    {
        await using AppDbContext db = CreateContext();
        Assert.False((await new AiQuotaService(db).CheckQuotaAsync(userId, "chat")).Allowed);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenNoUsage_ReturnsAllowed()
    {
        // Arrange
        await using AppDbContext db = CreateContext();
        var service = new AiQuotaService(db);

        // Act
        QuotaCheckResult result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.True(result.Allowed);
        Assert.Null(result.DenialReason);
        Assert.False(result.BudgetWarning);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenUserExceedsDailyLimit_ReturnsDenied()
    {
        // Arrange
        await using AppDbContext db = CreateContext();
        var service = new AiQuotaService(db);
        DateTime today = DateTime.UtcNow;

        // Add 30 records for the user today (the daily limit)
        for (int i = 0; i < 30; i++)
        {
            db.AiUsageRecords.Add(new AiUsageRecord
            {
                UserId = UserId,
                Endpoint = "chat",
                PromptTokens = 100,
                CompletionTokens = 50,
                EstimatedCostUsd = 0.01m,
                CreatedAt = today,
            });
        }
        await db.SaveChangesAsync();

        // Act
        QuotaCheckResult result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.False(result.Allowed);
        Assert.Contains("daily limit", result.DenialReason);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenOtherUserExceedsLimit_CurrentUserAllowed()
    {
        // Arrange
        await using AppDbContext db = CreateContext();
        var service = new AiQuotaService(db);
        DateTime today = DateTime.UtcNow;

        // Add 30 records for the other user
        for (int i = 0; i < 30; i++)
        {
            db.AiUsageRecords.Add(new AiUsageRecord
            {
                UserId = OtherUserId,
                Endpoint = "chat",
                PromptTokens = 100,
                CompletionTokens = 50,
                EstimatedCostUsd = 0.01m,
                CreatedAt = today,
            });
        }
        await db.SaveChangesAsync();

        // Act
        QuotaCheckResult result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenMonthlyBudgetExceeded_ReturnsDenied()
    {
        // Arrange
        await using AppDbContext db = CreateContext();
        var service = new AiQuotaService(db);
        DateTime now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        // Add records totaling $10 (the monthly budget)
        for (int i = 0; i < 10; i++)
        {
            db.AiUsageRecords.Add(new AiUsageRecord
            {
                UserId = UserId,
                Endpoint = "chat",
                PromptTokens = 1000,
                CompletionTokens = 500,
                EstimatedCostUsd = 1.0m,
                CreatedAt = monthStart.AddDays(i),
            });
        }
        await db.SaveChangesAsync();

        // Act
        QuotaCheckResult result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.False(result.Allowed);
        Assert.Contains("monthly budget", result.DenialReason);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenBudgetNearWarningThreshold_ReturnsWarning()
    {
        // Arrange
        await using AppDbContext db = CreateContext();
        var service = new AiQuotaService(db);
        DateTime now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        // Add $9.00 worth of usage (90% of $10 budget)
        for (int i = 0; i < 9; i++)
        {
            db.AiUsageRecords.Add(new AiUsageRecord
            {
                UserId = UserId,
                Endpoint = "chat",
                PromptTokens = 1000,
                CompletionTokens = 500,
                EstimatedCostUsd = 1.0m,
                CreatedAt = monthStart.AddDays(i),
            });
        }
        await db.SaveChangesAsync();

        // Act
        QuotaCheckResult result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.True(result.Allowed);
        Assert.True(result.BudgetWarning);
    }

    [Fact]
    public async Task RecordUsageAsync_SavesRecordToDatabase()
    {
        // Arrange
        await using AppDbContext db = CreateContext();
        var service = new AiQuotaService(db);

        // Act
        await service.RecordUsageAsync(UserId, "chat", 1000, 500, 200, 0.01m);

        // Assert
        AiUsageRecord? record = await db.AiUsageRecords.FirstOrDefaultAsync(r => r.UserId == UserId);
        Assert.NotNull(record);
        Assert.Equal("chat", record.Endpoint);
        Assert.Equal(1000, record.PromptTokens);
        Assert.Equal(500, record.CompletionTokens);
        Assert.Equal(200, record.CachedPromptTokens);
        Assert.Equal(0.01m, record.EstimatedCostUsd);
    }

    [Fact]
    public void CalculateEstimatedCost_MatchesGpt4oMiniPricing()
    {
        // Arrange
        int promptTokens = 1000;
        int completionTokens = 500;
        int cachedPromptTokens = 200;

        // Act
        decimal cost = AiQuotaService.CalculateEstimatedCost(promptTokens, completionTokens, cachedPromptTokens);

        // Assert
        // Non-cached input: 800 * $0.15 / 1M = $0.00012
        // Cached input: 200 * $0.075 / 1M = $0.000015
        // Output: 500 * $0.60 / 1M = $0.0003
        // Total: $0.000435
        decimal expected = 0.000435m;
        Assert.Equal(expected, cost);
    }

    [Fact]
    public void CalculateEstimatedCost_ZeroTokens_ReturnsZero()
    {
        // Act
        decimal cost = AiQuotaService.CalculateEstimatedCost(0, 0, 0);

        // Assert
        Assert.Equal(0m, cost);
    }

    [Fact]
    public async Task CheckQuotaAsync_OldRecords_DoNotCount()
    {
        // Arrange
        await using AppDbContext db = CreateContext();
        var service = new AiQuotaService(db);
        DateTime yesterday = DateTime.UtcNow.AddDays(-1);
        DateTime lastMonth = DateTime.UtcNow.AddMonths(-1);

        // Add records from yesterday and last month
        for (int i = 0; i < 30; i++)
        {
            db.AiUsageRecords.Add(new AiUsageRecord
            {
                UserId = UserId,
                Endpoint = "chat",
                PromptTokens = 100,
                CompletionTokens = 50,
                EstimatedCostUsd = 0.01m,
                CreatedAt = yesterday,
            });
        }
        for (int i = 0; i < 20; i++)
        {
            db.AiUsageRecords.Add(new AiUsageRecord
            {
                UserId = UserId,
                Endpoint = "chat",
                PromptTokens = 100,
                CompletionTokens = 50,
                EstimatedCostUsd = 0.5m,
                CreatedAt = lastMonth,
            });
        }
        await db.SaveChangesAsync();

        // Act
        QuotaCheckResult result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.True(result.Allowed);
    }
}
