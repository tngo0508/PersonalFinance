using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using CreateNotificationRequestDto = PersonalFinance.ApiService.Infrastructure.CreateNotificationRequestDto;

namespace PersonalFinance.ApiService.Features.BudgetPlanner;

/// <summary>
/// Service implementation for managing user budget allocations.
/// </summary>
internal sealed class BudgetService : IBudgetService
{
    private readonly IBudgetDataProvider _budgetDataProvider;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<BudgetService> _logger;
    private readonly INotificationService _notificationService;

    public BudgetService(
        AppDbContext dbContext,
        ILogger<BudgetService> logger,
        INotificationService notificationService,
        IBudgetDataProvider budgetDataProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _notificationService = notificationService;
        _budgetDataProvider = budgetDataProvider;
    }

    public async Task<BudgetAllocationResponseDto?> GetBudgetAsync(string userId, string period,
        CancellationToken cancellationToken = default)
    {
        UserBudgetAllocation? budget = await _dbContext.UserBudgetAllocations
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Period == period, cancellationToken);

        if (budget is null)
        {
            return null;
        }

        return new BudgetAllocationResponseDto
        {
            Allocations = budget.ToAllocationsDto(),
            MonthlyIncome = budget.MonthlyIncome
        };
    }

    public async Task SaveBudgetAsync(string userId, SaveBudgetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string period = request.Period ?? DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        UserBudgetAllocation? budget = await _dbContext.UserBudgetAllocations
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Period == period, cancellationToken);

        if (budget is null)
        {
            budget = new UserBudgetAllocation
            {
                UserId = userId,
                Period = period
            };
            _dbContext.UserBudgetAllocations.Add(budget);
        }

        budget.MonthlyIncome = request.MonthlyIncome;
        budget.HousingAmount = request.Allocations.Housing;
        budget.FoodAmount = request.Allocations.Food;
        budget.TransportationAmount = request.Allocations.Transportation;
        budget.UtilitiesAmount = request.Allocations.Utilities;
        budget.InsuranceAmount = request.Allocations.Insurance;
        budget.HealthcareAmount = request.Allocations.Healthcare;
        budget.SavingsAmount = request.Allocations.Savings;
        budget.DebtRepaymentAmount = request.Allocations.DebtRepayment;
        budget.EntertainmentAmount = request.Allocations.Entertainment;
        budget.MiscellaneousAmount = request.Allocations.Miscellaneous;
        budget.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} saved budget for period {Period}", userId, period);
        }

        // Check for budget alerts after saving
        await CheckBudgetAlertsAsync(userId, period, cancellationToken);
    }

    /// <summary>
    /// Checks if overall budget spending is approaching or exceeding the allocated amount.
    /// Creates notifications at 80% and 100% thresholds based on synced spreadsheet data.
    /// </summary>
    async Task CheckBudgetAlertsAsync(string userId, string period, CancellationToken cancellationToken)
    {
        try
        {
            int year = int.Parse(period.AsSpan(0, 4), CultureInfo.InvariantCulture);
            int month = int.Parse(period.AsSpan(5, 2), CultureInfo.InvariantCulture);

            BudgetMonthData? monthData = await _budgetDataProvider.GetMonthAsync(userId, year, month, cancellationToken);
            if (monthData == null)
            {
                return;
            }

            UserBudgetAllocation? budget = await _dbContext.UserBudgetAllocations
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.UserId == userId && b.Period == period, cancellationToken);

            if (budget == null)
            {
                return;
            }

            decimal totalBudgetedExpenses = monthData.BudgetedExpenses;
            decimal totalActualExpenses = monthData.ActualExpenses;

            if (totalBudgetedExpenses <= 0)
            {
                return;
            }

            decimal percentUsed = (totalActualExpenses / totalBudgetedExpenses) * 100;

            if (percentUsed >= 100)
            {
                await _notificationService.CreateNotificationAsync(userId, new CreateNotificationRequestDto
                {
                    Type = "BudgetAlert",
                    Title = "Budget exceeded",
                    Message =
                        $"Your total spending has reached {percentUsed:F1}% of your budgeted amount for {period}.",
                    TitleKey = "Notifications.BudgetExceeded.Title",
                    MessageKey = "Notifications.BudgetAlert.Message",
                    Arguments = BudgetAlertArguments(percentUsed, period),
                    SourceFeature = "Budget"
                }, cancellationToken);
            }
            else if (percentUsed >= 80)
            {
                await _notificationService.CreateNotificationAsync(userId, new CreateNotificationRequestDto
                {
                    Type = "BudgetAlert",
                    Title = "Budget warning",
                    Message =
                        $"Your total spending has reached {percentUsed:F1}% of your budgeted amount for {period}.",
                    TitleKey = "Notifications.BudgetWarning.Title",
                    MessageKey = "Notifications.BudgetAlert.Message",
                    Arguments = BudgetAlertArguments(percentUsed, period),
                    SourceFeature = "Budget"
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check budget alerts for user {UserId}", userId);
        }
    }

    /// <summary>The named arguments of the budget alert Notifications, formatted as their English text is.</summary>
    private static Dictionary<string, string> BudgetAlertArguments(decimal percentUsed, string period) => new()
    {
        ["percent"] = percentUsed.ToString("F1", CultureInfo.CurrentCulture),
        ["period"] = period
    };
}
