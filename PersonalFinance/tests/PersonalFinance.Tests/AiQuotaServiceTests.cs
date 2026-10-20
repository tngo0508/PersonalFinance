using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Tests;

/// <summary>
/// Behaviour of the AI quota service — token tracking, cost calculation, and limit enforcement.
/// </summary>
public class AiQuotaServiceTests
{
    private const string UserId = "quota-user";
    private const string OtherUserId = "other-user";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Quota_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenNoUsage_ReturnsAllowed()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new AiQuotaService(db);

        // Act
        var result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.True(result.Allowed);
        Assert.Null(result.DenialReason);
        Assert.False(result.BudgetWarning);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenUserExceedsDailyLimit_ReturnsDenied()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new AiQuotaService(db);
        var today = DateTime.UtcNow;

        // Add 30 records for the user today (the daily limit)
        for (var i = 0; i < 30; i++)
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
        var result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.False(result.Allowed);
        Assert.Contains("daily limit", result.DenialReason);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenOtherUserExceedsLimit_CurrentUserAllowed()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new AiQuotaService(db);
        var today = DateTime.UtcNow;

        // Add 30 records for the other user
        for (var i = 0; i < 30; i++)
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
        var result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenMonthlyBudgetExceeded_ReturnsDenied()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new AiQuotaService(db);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        // Add records totaling $10 (the monthly budget)
        for (var i = 0; i < 10; i++)
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
        var result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.False(result.Allowed);
        Assert.Contains("monthly budget", result.DenialReason);
    }

    [Fact]
    public async Task CheckQuotaAsync_WhenBudgetNearWarningThreshold_ReturnsWarning()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new AiQuotaService(db);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        // Add $9.00 worth of usage (90% of $10 budget)
        for (var i = 0; i < 9; i++)
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
        var result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.True(result.Allowed);
        Assert.True(result.BudgetWarning);
    }

    [Fact]
    public async Task RecordUsageAsync_SavesRecordToDatabase()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new AiQuotaService(db);

        // Act
        await service.RecordUsageAsync(UserId, "chat", 1000, 500, 200, 0.01m);

        // Assert
        var record = await db.AiUsageRecords.FirstOrDefaultAsync(r => r.UserId == UserId);
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
        var promptTokens = 1000;
        var completionTokens = 500;
        var cachedPromptTokens = 200;

        // Act
        var cost = AiQuotaService.CalculateEstimatedCost(promptTokens, completionTokens, cachedPromptTokens);

        // Assert
        // Non-cached input: 800 * $0.15 / 1M = $0.00012
        // Cached input: 200 * $0.075 / 1M = $0.000015
        // Output: 500 * $0.60 / 1M = $0.0003
        // Total: $0.000435
        var expected = 0.000435m;
        Assert.Equal(expected, cost);
    }

    [Fact]
    public void CalculateEstimatedCost_ZeroTokens_ReturnsZero()
    {
        // Act
        var cost = AiQuotaService.CalculateEstimatedCost(0, 0, 0);

        // Assert
        Assert.Equal(0m, cost);
    }

    [Fact]
    public async Task CheckQuotaAsync_OldRecords_DoNotCount()
    {
        // Arrange
        await using var db = CreateContext();
        var service = new AiQuotaService(db);
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var lastMonth = DateTime.UtcNow.AddMonths(-1);

        // Add records from yesterday and last month
        for (var i = 0; i < 30; i++)
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
        for (var i = 0; i < 20; i++)
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
        var result = await service.CheckQuotaAsync(UserId, "chat");

        // Assert
        Assert.True(result.Allowed);
    }
}
