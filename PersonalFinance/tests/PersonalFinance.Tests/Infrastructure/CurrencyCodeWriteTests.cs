using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.BudgetPlanner;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Features.MoneyPriorities;
using PersonalFinance.ApiService.Features.SavingsGoals;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.MoneyPriorities;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// What a money write stores (ADR 0016, localization spec §2.5): the requested currency, or the row's own when the
/// request names none (US dollars for a new row), with each amount rounded to that currency's minor units, halves away
/// from zero. A Google Drive connection takes the Account's Reporting Currency by default.
/// </summary>
public class CurrencyCodeWriteTests
{
    private const string UserId = "currency-user";

    [Fact]
    public async Task Transaction_InVnd_IsStoredInWholeDong_AndAnUpdateWithoutACodeKeepsVnd()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        int categoryId = await AddCategoryAsync(db);

        BudgetTransactionDto created = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 1), Amount = 125_000.5m, Description = "Phở", CategoryId = categoryId,
            CurrencyCode = "VND"
        });
        await service.UpdateTransactionAsync(UserId, created.Id, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 2), Amount = 99_999.49m, Description = "Phở", CategoryId = categoryId
        });

        BudgetTransaction stored = await db.BudgetTransactions.SingleAsync();
        Assert.Equal("VND", stored.CurrencyCode);
        Assert.Equal(99_999m, stored.Amount);
    }

    [Fact]
    public async Task Transaction_WithoutACode_IsUsd_AsBeforeAmountsCarriedACurrency()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        int categoryId = await AddCategoryAsync(db);

        await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 1), Amount = 12.34m, Description = "Lunch", CategoryId = categoryId
        });

        BudgetTransaction stored = await db.BudgetTransactions.SingleAsync();
        Assert.Equal("USD", stored.CurrencyCode);
        Assert.Equal(12.34m, stored.Amount);
    }

    [Fact]
    public async Task BudgetLine_TakesTheRequestedCurrency_AndACopiedMonthKeepsIt()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        int categoryId = await AddCategoryAsync(db);

        await service.SetPlannedAsync(UserId, 2026, 9, categoryId,
            new SetPlannedRequest { Amount = 2_500_000.5m, CurrencyCode = "VND" });
        await service.SetActualOverrideAsync(UserId, 2026, 9, categoryId, new SetActualOverrideRequest { Amount = 1_000.5m });
        await service.InitializeMonthAsync(UserId, new InitializeMonthRequest { Year = 2026, Month = 10, CopyFromPrevious = true });

        List<MonthlyBudgetLine> lines = await db.MonthlyBudgetLines.OrderBy(l => l.Id).ToListAsync();
        Assert.All(lines, line => Assert.Equal("VND", line.CurrencyCode));
        Assert.Equal(2_500_001m, lines[0].PlannedAmount);
        Assert.Equal(1_001m, lines[0].ActualOverride);
        Assert.Equal(2_500_001m, lines[1].PlannedAmount);
    }

    [Fact]
    public async Task SavingsGoal_InVnd_RoundsItsTargetOpeningAndContributions_AndAnUpdateKeepsItsCurrency()
    {
        await using AppDbContext db = CreateContext();
        var service = new SavingsGoalService(db, NullLogger<SavingsGoalService>.Instance, new NoNotifications(),
            TimeProvider.System, TestCurrencyConverter.Create(db));

        int id = await service.CreateSavingsGoalAsync(UserId, new CreateSavingsGoalRequestDto
        {
            Name = "Tết", TargetAmount = 10_000_000.5m, CurrentAmount = 500_000.5m, CurrencyCode = "VND"
        });
        await service.AddContributionAsync(UserId, id, new SaveContributionRequestDto { Amount = 0.5m });
        await service.UpdateSavingsGoalAsync(UserId, id, new UpdateSavingsGoalRequestDto { TargetAmount = 12_000_000.4m });

        SavingsGoal goal = await db.SavingsGoals.SingleAsync();
        Assert.Equal("VND", goal.CurrencyCode);
        Assert.Equal(12_000_000m, goal.TargetAmount);
        Assert.Equal(500_002m, goal.CurrentAmount);
        Assert.Equal([1m, 500_001m], await db.Contributions.OrderBy(c => c.Amount).Select(c => c.Amount).ToListAsync());
    }

    [Fact]
    public async Task BudgetAllocation_TakesTheRequestedCurrency_AndASaveWithoutACodeKeepsIt()
    {
        await using AppDbContext db = CreateContext();
        var service = new BudgetService(db, NullLogger<BudgetService>.Instance, new NoNotifications(),
            TestBudgetDataProviderFactory.Create(db));

        await service.SaveBudgetAsync(UserId, new SaveBudgetRequestDto
        {
            Period = "2026-10", MonthlyIncome = 30_000_000.5m, CurrencyCode = "VND",
            Allocations = new BudgetAllocationsDto { Housing = 9_000_000.5m }
        });
        await service.SaveBudgetAsync(UserId, new SaveBudgetRequestDto
        {
            Period = "2026-10", MonthlyIncome = 31_000_000m, Allocations = new BudgetAllocationsDto { Housing = 9_500_000.5m }
        });

        UserBudgetAllocation stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal("VND", stored.CurrencyCode);
        Assert.Equal(31_000_000m, stored.MonthlyIncome);
        Assert.Equal(9_500_001m, stored.HousingAmount);
    }

    [Fact]
    public void NetWorthParameters_TakeTheRequestedCurrency_AndRoundOnlyTheAmounts()
    {
        var saved = new NetWorthParameters();

        saved.Apply(new NetWorthProjectionRequestDto
        {
            CurrencyCode = "VND", StartingBalance = 100_000_000.5m, AnnualSavings = 0.5m, BaseRoi = 7.25m
        });
        saved.Apply(new NetWorthProjectionRequestDto { StartingBalance = 1.5m, BaseRoi = 6.75m });

        Assert.Equal("VND", saved.CurrencyCode);
        Assert.Equal(2m, saved.StartingBalance);
        Assert.Equal(6.75m, saved.BaseRoi);
    }

    [Fact]
    public async Task PriorityWalk_InAnotherCurrency_IsANewWalk_NotTheSameAnswers()
    {
        await using AppDbContext db = CreateContext();
        MoneyPrioritiesService service = MoneyPrioritiesTestServices.Service(db);
        var usd = new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 22m, DebtBalance = 5000m };
        var vnd = new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 22m, DebtBalance = 5000.5m, CurrencyCode = "VND"
        };

        PriorityWalkResultDto first = await service.SubmitAsync(UserId, usd);
        PriorityWalkResultDto second = await service.SubmitAsync(UserId, vnd);
        PriorityWalkResultDto again = await service.SubmitAsync(UserId, vnd);

        Assert.NotEqual(first.WalkId, second.WalkId);
        Assert.Equal(second.WalkId, again.WalkId);
        PriorityWalk stored = await db.PriorityWalks.SingleAsync(w => w.Id == second.WalkId);
        Assert.Equal("VND", stored.CurrencyCode);
        Assert.Equal(5001m, stored.DebtBalance);
    }

    [Fact]
    public async Task DriveConnection_WithoutACode_TakesTheReportingCurrency_AndAReconnectKeepsIt()
    {
        await using AppDbContext db = CreateContext();
        db.UserPreferences.Add(new UserPreference { UserId = UserId, Language = "vi", ReportingCurrency = "VND" });
        await db.SaveChangesAsync();
        DriveConnectionService service = DriveService(db);
        var request = new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "https://drive.google.com/drive/folders/1FakeFolderIdForTests0000000000ab", ApiKey = "AIzaSyTestKey"
        };

        await service.ConnectDriveAsync(UserId, request);
        await service.ConnectDriveAsync(UserId, request);

        Assert.Equal("VND", (await db.GoogleDriveConnections.SingleAsync()).CurrencyCode);
    }

    [Fact]
    public async Task DriveConnection_WithACode_TakesIt_OverTheReportingCurrency()
    {
        await using AppDbContext db = CreateContext();
        DriveConnectionService service = DriveService(db);

        await service.ConnectDriveAsync(UserId, new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "https://drive.google.com/drive/folders/1FakeFolderIdForTests0000000000ab", ApiKey = "AIzaSyTestKey",
            CurrencyCode = "VND"
        });

        Assert.Equal("VND", (await db.GoogleDriveConnections.SingleAsync()).CurrencyCode);
    }

    [Fact]
    public async Task DriveConnection_ForAnAccountWithNoPreference_IsUsd()
    {
        await using AppDbContext db = CreateContext();

        await DriveService(db).ConnectDriveAsync(UserId, new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "https://drive.google.com/drive/folders/1FakeFolderIdForTests0000000000ab", ApiKey = "AIzaSyTestKey"
        });

        Assert.Equal("USD", (await db.GoogleDriveConnections.SingleAsync()).CurrencyCode);
    }

    private static DriveConnectionService DriveService(AppDbContext db)
    {
        var handler = new DelegatingTestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "files": [] }""", System.Text.Encoding.UTF8, "application/json")
        }));
        return new DriveConnectionService(new HttpClient(handler), new ConfigurationBuilder().Build(), db,
            NullLogger<DriveConnectionService>.Instance);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CurrencyWrites_{Guid.NewGuid():N}")
            .Options);

    private static async Task<int> AddCategoryAsync(AppDbContext db)
    {
        var category = new BudgetCategory { UserId = UserId, Name = "Food", Kind = BudgetCategoryKind.Expense };
        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private sealed class NoNotifications : INotificationService
    {
        public Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<List<NotificationDto>> GetNotificationsAsync(string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<NotificationDto>());

        public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> MarkAsReadAsync(string userId, int notificationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
