using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.AiAssistant;
using PersonalFinance.ApiService.Features.BudgetPlanner;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.ApiService.Features.SavingsGoals;
using PersonalFinance.ApiService.Features.Search;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.DTOs.Search;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// Regression tests for the "¤1,234.56" currency bug in backend text. A chiseled container starts the ApiService in
/// the invariant culture, so strings built by the Dashboard, Search and Savings Goal services contain "¤" unless
/// <see cref="AppCulture.ApplyDefault"/> runs at startup.
/// </summary>
[Collection("Culture")]
public sealed class ApiServiceCultureTests : IDisposable
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
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
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
        DashboardSummaryDto summary = await new DashboardService(db, TestBudgetDataProviderFactory.Create(db))
            .GetDashboardSummaryAsync(UserId);
        return Assert.Single(summary.RecentActivity, a => a.Type == "SavingsGoal").Description;
    }

    [Fact]
    public async Task DashboardRecentActivity_InInvariantCulture_StillShowsDollarAmounts()
    {
        StartInInvariantCulture();
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.Add(NewGoal());
        await db.SaveChangesAsync();

        string activity = await GetDashboardGoalActivityAsync(db);

        // The symbol comes from the currency (MoneyFormatter), never from the culture, so "¤" cannot return.
        Assert.Equal("Target: $2,500.50 | Saved: $1,234.56", activity);
    }

    [Fact]
    public async Task DashboardRecentActivity_AfterApplyDefault_ShowsDollarAmounts()
    {
        StartInInvariantCulture();
        AppCulture.ApplyDefault();
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.Add(NewGoal());
        await db.SaveChangesAsync();

        string activity = await GetDashboardGoalActivityAsync(db);

        Assert.Equal("Target: $2,500.50 | Saved: $1,234.56", activity);
        Assert.DoesNotContain(CurrencySymbolOfInvariantCulture, activity);
    }

    [Fact]
    public async Task Search_AfterApplyDefault_ShowsDollarAmountsForTransactionsAndGoals()
    {
        StartInInvariantCulture();
        AppCulture.ApplyDefault();
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(new SpreadsheetTransactionRecord
        {
            UserId = UserId,
            Date = new DateTime(2026, 10, 1),
            Amount = 1_234.56m,
            Description = "Culture groceries",
            Category = "Food",
            Type = "Expense"
        });
        db.SavingsGoals.Add(NewGoal());
        await db.SaveChangesAsync();
        var service = new SearchService(db, new BudgetDataSourceService(db), TimeProvider.System);

        SearchResultsDto transactions = await service.SearchAsync(UserId, "culture");
        SearchResultsDto goals = await service.SearchAsync(UserId, "emergency");

        Assert.Equal("Oct 1, 2026 - Food - $1,234.56 (Expense)", Assert.Single(transactions.Transactions).Description);
        Assert.Equal("Target: $2,500.50", Assert.Single(goals.SavingsGoals).Description);
    }

    [Fact]
    public async Task SavingsMilestoneNotification_AfterApplyDefault_ContainsDollarAmounts()
    {
        StartInInvariantCulture();
        AppCulture.ApplyDefault();
        await using AppDbContext db = CreateContext();
        var goal = new SavingsGoal { UserId = UserId, Name = "Vacation", TargetAmount = 1_000m, CurrentAmount = 0m };
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var notifications = new CapturingNotificationService();
        var service = new SavingsGoalService(db, NullLogger<SavingsGoalService>.Instance, notifications,
            TimeProvider.System);

        ContributionResult added = await service.AddContributionAsync(UserId, goal.Id,
            new SaveContributionRequestDto { Amount = 600m });

        Assert.Equal(ContributionOutcome.Saved, added.Outcome);
        CreateNotificationRequestDto notification = Assert.Single(notifications.Created);
        Assert.Equal("Your goal 'Vacation' has reached 50% of its target ($600.00 of $1,000.00).",
            notification.Message);
        Assert.DoesNotContain(CurrencySymbolOfInvariantCulture, notification.Message);
        // Stored as key + arguments too, so the Web renders it in the reader's language (localization spec §2.3).
        Assert.Equal("Notifications.SavingsMilestone.Title", notification.TitleKey);
        Assert.Equal("Notifications.SavingsMilestone.Message", notification.MessageKey);
        Assert.Equal(new Dictionary<string, string>
        {
            ["percent"] = "50",
            ["goalName"] = "Vacation",
            ["current"] = "$600.00",
            ["target"] = "$1,000.00"
        }, notification.Arguments);
    }

    /// <summary>Switches the current culture as if the host ran under a non-US locale.</summary>
    private static void StartInCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("th-TH")]
    public async Task SaveBudget_WithoutPeriod_StoresInvariantYearMonthKeyUnderAnyCulture(string cultureName)
    {
        StartInCulture(cultureName);
        await using AppDbContext db = CreateContext();
        var service = new BudgetService(db, NullLogger<BudgetService>.Instance, new CapturingNotificationService(),
            TestBudgetDataProviderFactory.Create(db));

        await service.SaveBudgetAsync(UserId, new SaveBudgetRequestDto
        {
            MonthlyIncome = 5_000m,
            Allocations = new BudgetAllocationsDto { Housing = 1_500m }
        });

        string expectedPeriod = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        BudgetAllocationResponseDto? saved = await service.GetBudgetAsync(UserId, expectedPeriod);
        Assert.NotNull(saved);
        Assert.Equal(5_000m, saved.MonthlyIncome);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("th-TH")]
    public async Task SaveBudget_WithPeriodKey_ParsesYearAndMonthUnderAnyCulture(string cultureName)
    {
        StartInCulture(cultureName);
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            ConnectionId = 1,
            Year = 2026,
            MonthNumber = 3,
            MonthName = "March",
            BudgetedExpenses = 1_000m,
            ActualExpenses = 1_100m,
            CategoryBreakdownJson = "[]"
        });
        await db.SaveChangesAsync();
        var notifications = new CapturingNotificationService();
        var service = new BudgetService(db, NullLogger<BudgetService>.Instance, notifications,
            TestBudgetDataProviderFactory.Create(db));

        await service.SaveBudgetAsync(UserId, new SaveBudgetRequestDto
        {
            Period = "2026-03",
            MonthlyIncome = 5_000m,
            Allocations = new BudgetAllocationsDto { Housing = 1_500m }
        });

        CreateNotificationRequestDto alert = Assert.Single(notifications.Created);
        Assert.Equal("Budget exceeded", alert.Title);
        Assert.EndsWith("for 2026-03.", alert.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AiAssistantTools_UnderGermanCulture_ReportFiguresInAppCulture()
    {
        StartInCulture("de-DE");
        await using AppDbContext db = CreateContext();
        db.NetWorthParameters.Add(new NetWorthParameters
        {
            UserId = UserId,
            AnnualIncome = 120_000m,
            AnnualSpending = 60_000m,
            AnnualSavings = 30_000m,
            StartingBalance = 50_000m,
            CurrentAge = 35,
            RetirementAge = 60
        });
        db.SavingsGoals.Add(new SavingsGoal
        {
            UserId = UserId,
            Name = "Emergency Fund",
            TargetAmount = 10_000m,
            CurrentAmount = 2_500m
        });
        await db.SaveChangesAsync();
        var outputs = new Dictionary<string, string?>();
        var chatClient = new ToolInvokingChatClient(outputs, "get_net_worth_details", "get_savings_goals");
        var service = new AiAssistantService(db, new AllowAllRateLimiter(), new AiQuotaService(db),
            NullLogger<AiAssistantService>.Instance, new ConfigurationBuilder().Build(),
            TestBudgetDataProviderFactory.Create(db), chatClient);

        await service.SendChatMessageAsync(UserId, new AiChatRequestDto { Message = "How am I doing?" });

        string? netWorth = outputs["get_net_worth_details"];
        Assert.Contains("Annual income: $120,000", netWorth);
        Assert.Contains("Annual spending: $60,000", netWorth);
        Assert.Contains("Starting/current net worth: $50,000", netWorth);
        Assert.Contains("- Emergency Fund: $2,500 of $10,000 (25.0% complete)", outputs["get_savings_goals"]);
    }

    [Fact]
    public void ApiServiceProgram_AppliesDefaultCultureRightAfterCreateBuilder()
    {
        string programPath = FindRepositoryFile("PersonalFinance", "src", "PersonalFinance.ApiService", "Program.cs");

        string[] lines = File.ReadAllLines(programPath);
        int createBuilder = Array.FindIndex(lines, l => l.Contains("WebApplication.CreateBuilder(args)"));
        int applyDefault = Array.FindIndex(lines, l => l.Contains("AppCulture.ApplyDefault()"));

        Assert.True(createBuilder >= 0, "ApiService Program.cs no longer calls WebApplication.CreateBuilder(args).");
        Assert.True(applyDefault > createBuilder,
            "ApiService Program.cs must call AppCulture.ApplyDefault() right after WebApplication.CreateBuilder(args).");
        Assert.All(lines.Skip(createBuilder + 1).Take(applyDefault - createBuilder - 1),
            l => Assert.True(string.IsNullOrWhiteSpace(l) || l.TrimStart().StartsWith("//", StringComparison.Ordinal),
                $"Unexpected statement between CreateBuilder and ApplyDefault: {l}"));
    }

    private static string FindRepositoryFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. relativeSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(relativeSegments)} above the test output.");
    }

    /// <summary>Fake model that calls the named tools once and records what they return.</summary>
    private sealed class ToolInvokingChatClient(Dictionary<string, string?> outputs, params string[] toolNames)
        : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            foreach (string name in toolNames)
            {
                AIFunction tool = options!.Tools!.OfType<AIFunction>().Single(t => t.Name == name);
                outputs[name] = (await tool.InvokeAsync(new AIFunctionArguments(), cancellationToken))?.ToString();
            }

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 }
            };
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class AllowAllRateLimiter : IRateLimiterService
    {
        public bool TryAllow(string userId) => true;
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
