using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// Regression tests for the "¤1,234.56" currency bug in backend text. A chiseled container starts the ApiService in
/// the invariant culture, so strings built by the Dashboard, Search and Savings Goal services contain "¤" unless
/// <see cref="AppCulture.ApplyDefault"/> runs at startup.
/// </summary>
[Collection("Culture")]
public class ApiServiceCultureTests : IDisposable
{
    private const string UserId = "culture-user";
    private const string CurrencySymbolOfInvariantCulture = "¤";

    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultUiCulture;
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ApiCulture_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Simulates the chiseled container: no locale configured, so .NET runs in the invariant culture.</summary>
    private static void StartInInvariantCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    private static SavingsGoal NewGoal() => new()
    {
        UserId = UserId,
        Name = "Emergency Fund",
        TargetAmount = 2_500.50m,
        CurrentAmount = 1_234.56m
    };

    private static async Task<string> GetDashboardGoalActivityAsync(AppDbContext db)
    {
        var summary = await new DashboardService(db, TestBudgetDataProviderFactory.Create(db))
            .GetDashboardSummaryAsync(UserId);
        return Assert.Single(summary.RecentActivity, a => a.Type == "SavingsGoal").Description;
    }

    [Fact]
    public async Task DashboardRecentActivity_InInvariantCulture_UsesInvariantCurrencySymbol()
    {
        StartInInvariantCulture();
        await using var db = CreateContext();
        db.SavingsGoals.Add(NewGoal());
        await db.SaveChangesAsync();

        var activity = await GetDashboardGoalActivityAsync(db);

        Assert.Equal("Target: ¤2,500.50 | Saved: ¤1,234.56", activity);
    }

    [Fact]
    public async Task DashboardRecentActivity_AfterApplyDefault_ShowsDollarAmounts()
    {
        StartInInvariantCulture();
        AppCulture.ApplyDefault();
        await using var db = CreateContext();
        db.SavingsGoals.Add(NewGoal());
        await db.SaveChangesAsync();

        var activity = await GetDashboardGoalActivityAsync(db);

        Assert.Equal("Target: $2,500.50 | Saved: $1,234.56", activity);
        Assert.DoesNotContain(CurrencySymbolOfInvariantCulture, activity);
    }

    [Fact]
    public async Task Search_AfterApplyDefault_ShowsDollarAmountsForTransactionsBudgetsAndGoals()
    {
        StartInInvariantCulture();
        AppCulture.ApplyDefault();
        await using var db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(new SpreadsheetTransactionRecord
        {
            UserId = UserId,
            Date = new DateTime(2026, 10, 1),
            Amount = 1_234.56m,
            Description = "Culture groceries",
            Category = "Food",
            Type = "Expense"
        });
        db.UserBudgetAllocations.Add(new UserBudgetAllocation
        {
            UserId = UserId,
            Period = "2026-10",
            MonthlyIncome = 5_000m
        });
        db.SavingsGoals.Add(NewGoal());
        await db.SaveChangesAsync();
        var service = new SearchService(db, new BudgetDataSourceService(db));

        var transactions = await service.SearchAsync(UserId, "culture");
        var budgets = await service.SearchAsync(UserId, "2026-10");
        var goals = await service.SearchAsync(UserId, "emergency");

        Assert.Equal("Oct 1, 2026 - Food - $1,234.56 (Expense)",Assert.Single(transactions.Transactions).Description);
        Assert.Equal("Monthly income: $5,000.00", Assert.Single(budgets.BudgetItems).Description);
        Assert.Equal("Target: $2,500.50", Assert.Single(goals.SavingsGoals).Description);
    }

    [Fact]
    public async Task SavingsMilestoneNotification_AfterApplyDefault_ContainsDollarAmounts()
    {
        StartInInvariantCulture();
        AppCulture.ApplyDefault();
        await using var db = CreateContext();
        var goal = new SavingsGoal { UserId = UserId, Name = "Vacation", TargetAmount = 1_000m, CurrentAmount = 0m };
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var notifications = new CapturingNotificationService();
        var service = new SavingsGoalService(db, NullLogger<SavingsGoalService>.Instance, notifications);

        var updated = await service.UpdateSavingsGoalAsync(UserId, goal.Id,
            new UpdateSavingsGoalRequestDto { CurrentAmount = 600m });

        Assert.True(updated);
        var notification = Assert.Single(notifications.Created);
        Assert.Equal("Your goal 'Vacation' has reached 50% of its target ($600.00 of $1,000.00).",
            notification.Message);
        Assert.DoesNotContain(CurrencySymbolOfInvariantCulture, notification.Message);
    }

    [Fact]
    public void ApiServiceProgram_AppliesDefaultCultureRightAfterCreateBuilder()
    {
        var programPath = FindRepositoryFile("PersonalFinance", "src", "PersonalFinance.ApiService", "Program.cs");

        var lines = File.ReadAllLines(programPath);
        var createBuilder = Array.FindIndex(lines, l => l.Contains("WebApplication.CreateBuilder(args)"));
        var applyDefault = Array.FindIndex(lines, l => l.Contains("AppCulture.ApplyDefault()"));

        Assert.True(createBuilder >= 0, "ApiService Program.cs no longer calls WebApplication.CreateBuilder(args).");
        Assert.True(applyDefault > createBuilder,
            "ApiService Program.cs must call AppCulture.ApplyDefault() right after WebApplication.CreateBuilder(args).");
        Assert.All(lines.Skip(createBuilder + 1).Take(applyDefault - createBuilder - 1),
            l => Assert.True(string.IsNullOrWhiteSpace(l) || l.TrimStart().StartsWith("//"),
                $"Unexpected statement between CreateBuilder and ApplyDefault: {l}"));
    }

    private static string FindRepositoryFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(relativeSegments)} above the test output.");
    }

    private sealed class CapturingNotificationService : INotificationService
    {
        public List<CreateNotificationRequestDto> Created { get; } = new();

        public Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Created.Add(request);
            return Task.FromResult(Created.Count);
        }

        public Task<List<NotificationDto>> GetNotificationsAsync(string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<NotificationDto>());

        public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> MarkAsReadAsync(string userId, int notificationId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
