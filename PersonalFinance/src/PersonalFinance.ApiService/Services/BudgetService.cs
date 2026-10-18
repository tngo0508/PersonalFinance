using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service implementation for managing user budget allocations.
/// </summary>
public class BudgetService : IBudgetService
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
        var budget = await _dbContext.UserBudgetAllocations
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Period == period, cancellationToken);

        if (budget is null)
        {
            return null;
        }

        return new BudgetAllocationResponseDto
        {
            Allocations = new BudgetAllocationsDto
            {
                Housing = budget.HousingPercent,
                Food = budget.FoodPercent,
                Transportation = budget.TransportationPercent,
                Utilities = budget.UtilitiesPercent,
                Insurance = budget.InsurancePercent,
                Healthcare = budget.HealthcarePercent,
                Savings = budget.SavingsPercent,
                DebtRepayment = budget.DebtRepaymentPercent,
                Entertainment = budget.EntertainmentPercent,
                Miscellaneous = budget.MiscellaneousPercent
            },
            MonthlyIncome = budget.MonthlyIncome
        };
    }

    public async Task SaveBudgetAsync(string userId, SaveBudgetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var period = request.Period ?? DateTime.UtcNow.ToString("yyyy-MM");

        var budget = await _dbContext.UserBudgetAllocations
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
        budget.HousingPercent = request.Allocations.Housing;
        budget.FoodPercent = request.Allocations.Food;
        budget.TransportationPercent = request.Allocations.Transportation;
        budget.UtilitiesPercent = request.Allocations.Utilities;
        budget.InsurancePercent = request.Allocations.Insurance;
        budget.HealthcarePercent = request.Allocations.Healthcare;
        budget.SavingsPercent = request.Allocations.Savings;
        budget.DebtRepaymentPercent = request.Allocations.DebtRepayment;
        budget.EntertainmentPercent = request.Allocations.Entertainment;
        budget.MiscellaneousPercent = request.Allocations.Miscellaneous;
        budget.HousingAmount = ToAmount(request.MonthlyIncome, request.Allocations.Housing);
        budget.FoodAmount = ToAmount(request.MonthlyIncome, request.Allocations.Food);
        budget.TransportationAmount = ToAmount(request.MonthlyIncome, request.Allocations.Transportation);
        budget.UtilitiesAmount = ToAmount(request.MonthlyIncome, request.Allocations.Utilities);
        budget.InsuranceAmount = ToAmount(request.MonthlyIncome, request.Allocations.Insurance);
        budget.HealthcareAmount = ToAmount(request.MonthlyIncome, request.Allocations.Healthcare);
        budget.SavingsAmount = ToAmount(request.MonthlyIncome, request.Allocations.Savings);
        budget.DebtRepaymentAmount = ToAmount(request.MonthlyIncome, request.Allocations.DebtRepayment);
        budget.EntertainmentAmount = ToAmount(request.MonthlyIncome, request.Allocations.Entertainment);
        budget.MiscellaneousAmount = ToAmount(request.MonthlyIncome, request.Allocations.Miscellaneous);
        budget.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} saved budget for period {Period}", userId, period);

        // Check for budget alerts after saving
        await CheckBudgetAlertsAsync(userId, period, cancellationToken);
    }

    /// <summary>
    /// Converts an unrounded request percentage into a whole-cent dollar amount of the monthly income.
    /// The amount is computed from the full-precision percentage, before the stored percentage column rounds it.
    /// </summary>
    private static decimal ToAmount(decimal monthlyIncome, decimal percent)
    {
        return Math.Round(monthlyIncome * percent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Checks if overall budget spending is approaching or exceeding the allocated amount.
    /// Creates notifications at 80% and 100% thresholds based on synced spreadsheet data.
    /// </summary>
    async Task CheckBudgetAlertsAsync(string userId, string period, CancellationToken cancellationToken)
    {
        try
        {
            var year = int.Parse(period.Substring(0, 4));
            var month = int.Parse(period.Substring(5, 2));

            var monthData = await _budgetDataProvider.GetMonthAsync(userId, year, month, cancellationToken);
            if (monthData == null)
            {
                return;
            }

            var budget = await _dbContext.UserBudgetAllocations
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.UserId == userId && b.Period == period, cancellationToken);

            if (budget == null)
            {
                return;
            }

            var totalBudgetedExpenses = monthData.BudgetedExpenses;
            var totalActualExpenses = monthData.ActualExpenses;

            if (totalBudgetedExpenses <= 0)
            {
                return;
            }

            var percentUsed = (totalActualExpenses / totalBudgetedExpenses) * 100;

            if (percentUsed >= 100)
            {
                await _notificationService.CreateNotificationAsync(userId, new CreateNotificationRequestDto
                {
                    Type = "BudgetAlert",
                    Title = "Budget exceeded",
                    Message =
                        $"Your total spending has reached {percentUsed:F1}% of your budgeted amount for {period}.",
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
                    SourceFeature = "Budget"
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check budget alerts for user {UserId}", userId);
        }
    }
}