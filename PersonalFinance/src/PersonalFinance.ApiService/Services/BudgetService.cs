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
    private readonly AppDbContext _dbContext;
    private readonly ILogger<BudgetService> _logger;
    private readonly INotificationService _notificationService;

    public BudgetService(
        AppDbContext dbContext,
        ILogger<BudgetService> logger,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _notificationService = notificationService;
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
        budget.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} saved budget for period {Period}", userId, period);

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
            var year = int.Parse(period.Substring(0, 4));
            var month = int.Parse(period.Substring(5, 2));

            var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.Year == year && s.MonthNumber == month)
                .ToListAsync(cancellationToken);

            if (!snapshots.Any())
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

            var totalBudgetedExpenses = snapshots.Sum(s => s.BudgetedExpenses);
            var totalActualExpenses = snapshots.Sum(s => s.ActualExpenses);

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