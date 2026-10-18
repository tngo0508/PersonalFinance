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
    public async Task Save_WhenHousingAmountIs700_StoresExactly700Amount()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await service.SaveBudgetAsync(UserId, CreateRequest(3_700m,
            new BudgetAllocationsDto { Housing = 700m }));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(700.00m, stored.HousingAmount);
    }

    [Fact]
    public async Task Save_StoresExactAmountForEveryCategory()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var allocations = new BudgetAllocationsDto
        {
            Housing = 370.37m,
            Food = 154.32m,
            Transportation = 125.00m,
            Utilities = 61.73m,
            Insurance = 95.92m,
            Healthcare = 41.15m,
            Savings = 246.91m,
            DebtRepayment = 54.87m,
            Entertainment = 27.44m,
            Miscellaneous = 13.72m
        };

        await service.SaveBudgetAsync(UserId, CreateRequest(1_234.57m, allocations));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(370.37m, stored.HousingAmount);
        Assert.Equal(154.32m, stored.FoodAmount);
        Assert.Equal(125.00m, stored.TransportationAmount);
        Assert.Equal(61.73m, stored.UtilitiesAmount);
        Assert.Equal(95.92m, stored.InsuranceAmount);
        Assert.Equal(41.15m, stored.HealthcareAmount);
        Assert.Equal(246.91m, stored.SavingsAmount);
        Assert.Equal(54.87m, stored.DebtRepaymentAmount);
        Assert.Equal(27.44m, stored.EntertainmentAmount);
        Assert.Equal(13.72m, stored.MiscellaneousAmount);
    }

    [Fact]
    public async Task Save_StoresSmallCentAmountsExactly()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await service.SaveBudgetAsync(UserId, CreateRequest(1_000m, new BudgetAllocationsDto { Housing = 0.01m }));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(0.01m, stored.HousingAmount);
    }

    [Fact]
    public async Task Save_WhenIncomeIsZero_StoresZeroAmounts()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await service.SaveBudgetAsync(UserId, CreateRequest(0m,
            new BudgetAllocationsDto { Housing = 0m, Food = 0m, Miscellaneous = 0m }));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(0m, stored.HousingAmount);
        Assert.Equal(0m, stored.FoodAmount);
        Assert.Equal(0m, stored.MiscellaneousAmount);
    }

    [Fact]
    public async Task Get_AfterSave_ReturnsTheSameAmountsAndIncomeAsSaved()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var allocations = new BudgetAllocationsDto
        {
            Housing = 1110m,
            Food = 573.50m,
            Transportation = 370m,
            Utilities = 185m,
            Insurance = 185m,
            Healthcare = 166.50m,
            Savings = 740m,
            DebtRepayment = 185m,
            Entertainment = 111m,
            Miscellaneous = 74m
        };
        await service.SaveBudgetAsync(UserId, CreateRequest(3_700m, allocations));

        var response = await service.GetBudgetAsync(UserId, Period);

        Assert.NotNull(response);
        Assert.Equal(3_700m, response.MonthlyIncome);
        var returned = Assert.IsType<BudgetAllocationsDto>(response.Allocations);
        Assert.Equal(1110m, returned.Housing);
        Assert.Equal(573.50m, returned.Food);
        Assert.Equal(370m, returned.Transportation);
        Assert.Equal(185m, returned.Utilities);
        Assert.Equal(185m, returned.Insurance);
        Assert.Equal(166.50m, returned.Healthcare);
        Assert.Equal(740m, returned.Savings);
        Assert.Equal(185m, returned.DebtRepayment);
        Assert.Equal(111m, returned.Entertainment);
        Assert.Equal(74m, returned.Miscellaneous);
    }

    [Fact]
    public async Task Save_WhenPeriodAlreadySaved_UpdatesTheAmountsInsteadOfAddingARow()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        await service.SaveBudgetAsync(UserId, CreateRequest(3_700m, new BudgetAllocationsDto { Housing = 740m }));

        await service.SaveBudgetAsync(UserId, CreateRequest(4_000m, new BudgetAllocationsDto { Housing = 1_000m }));

        var stored = await db.UserBudgetAllocations.SingleAsync();
        Assert.Equal(4_000m, stored.MonthlyIncome);
        Assert.Equal(1_000m, stored.HousingAmount);
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
