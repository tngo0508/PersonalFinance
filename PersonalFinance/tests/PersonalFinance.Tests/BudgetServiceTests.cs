using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using BudgetAllocationsDto = PersonalFinance.Shared.Contracts.BudgetAllocationsDto;
using SaveBudgetRequestDto = PersonalFinance.Shared.Contracts.SaveBudgetRequestDto;

namespace PersonalFinance.Tests;

/// <summary>
/// Behaviour of the Budget service's save and read paths, exercised against an in-memory database.
/// </summary>
public class BudgetServiceTests
{
    private const string UserId = "budget-user";
    private const string Period = "2026-10";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Budget_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static BudgetService CreateService(AppDbContext db)
    {
        return new BudgetService(
            db,
            NullLogger<BudgetService>.Instance,
            new StubNotificationService(),
            TestBudgetDataProviderFactory.Create(db));
    }

    private static SaveBudgetRequestDto CreateRequest(decimal income, BudgetAllocationsDto allocations,
        string period = Period)
    {
        return new SaveBudgetRequestDto { Period = period, MonthlyIncome = income, Allocations = allocations };
    }

    [Fact]
    public async Task Save_WhenHousingPercentIsFullPrecisionOf700Over3700_StoresExactly700Amount()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await service.SaveBudgetAsync(UserId, CreateRequest(3_700m,
            new BudgetAllocationsDto { Housing = 700m * 100m / 3_700m }));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(700.00m, stored.HousingAmount);
    }

    [Fact]
    public async Task Save_StoresRoundedAmountForEveryCategory()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var allocations = new BudgetAllocationsDto
        {
            Housing = 30.005m,
            Food = 12.5m,
            Transportation = 10.1234m,
            Utilities = 5m,
            Insurance = 7.77m,
            Healthcare = 3.333m,
            Savings = 20m,
            DebtRepayment = 4.4444m,
            Entertainment = 2.2222m,
            Miscellaneous = 1.1111m
        };

        await service.SaveBudgetAsync(UserId, CreateRequest(1_234.57m, allocations));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.Housing), stored.HousingAmount);
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.Food), stored.FoodAmount);
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.Transportation), stored.TransportationAmount);
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.Utilities), stored.UtilitiesAmount);
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.Insurance), stored.InsuranceAmount);
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.Healthcare), stored.HealthcareAmount);
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.Savings), stored.SavingsAmount);
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.DebtRepayment), stored.DebtRepaymentAmount);
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.Entertainment), stored.EntertainmentAmount);
        Assert.Equal(ExpectedAmount(1_234.57m, allocations.Miscellaneous), stored.MiscellaneousAmount);
    }

    [Fact]
    public async Task Save_RoundsMidpointAmountsAwayFromZero()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // 0.0005% of 1000 is exactly 0.005, which rounds away from zero to 0.01.
        await service.SaveBudgetAsync(UserId, CreateRequest(1_000m, new BudgetAllocationsDto { Housing = 0.0005m }));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(0.01m, stored.HousingAmount);
    }

    [Fact]
    public async Task Save_WhenIncomeIsZero_StoresZeroAmounts()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await service.SaveBudgetAsync(UserId, CreateRequest(0m,
            new BudgetAllocationsDto { Housing = 50m, Food = 25m, Miscellaneous = 25m }));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(0m, stored.HousingAmount);
        Assert.Equal(0m, stored.FoodAmount);
        Assert.Equal(0m, stored.MiscellaneousAmount);
    }

    [Fact]
    public async Task Get_AfterSave_ReturnsTheSamePercentagesAndIncomeAsSaved()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var allocations = new BudgetAllocationsDto
        {
            Housing = 30m,
            Food = 15.5m,
            Transportation = 10m,
            Utilities = 5m,
            Insurance = 5m,
            Healthcare = 4.5m,
            Savings = 20m,
            DebtRepayment = 5m,
            Entertainment = 3m,
            Miscellaneous = 2m
        };
        await service.SaveBudgetAsync(UserId, CreateRequest(3_700m, allocations));

        var response = await service.GetBudgetAsync(UserId, Period);

        Assert.NotNull(response);
        Assert.Equal(3_700m, response.MonthlyIncome);
        var returned = Assert.IsType<BudgetAllocationsDto>(response.Allocations);
        Assert.Equal(30m, returned.Housing);
        Assert.Equal(15.5m, returned.Food);
        Assert.Equal(10m, returned.Transportation);
        Assert.Equal(5m, returned.Utilities);
        Assert.Equal(5m, returned.Insurance);
        Assert.Equal(4.5m, returned.Healthcare);
        Assert.Equal(20m, returned.Savings);
        Assert.Equal(5m, returned.DebtRepayment);
        Assert.Equal(3m, returned.Entertainment);
        Assert.Equal(2m, returned.Miscellaneous);
    }

    [Fact]
    public async Task Save_WhenPeriodAlreadySaved_UpdatesTheAmountsInsteadOfAddingARow()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        await service.SaveBudgetAsync(UserId, CreateRequest(3_700m, new BudgetAllocationsDto { Housing = 20m }));

        await service.SaveBudgetAsync(UserId, CreateRequest(4_000m, new BudgetAllocationsDto { Housing = 25m }));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(4_000m, stored.MonthlyIncome);
        Assert.Equal(1_000m, stored.HousingAmount);
        Assert.Equal(25m, stored.HousingPercent);
    }

    private static decimal ExpectedAmount(decimal income, decimal percent)
    {
        return Math.Round(income * percent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private sealed class StubNotificationService : INotificationService
    {
        public Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

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
