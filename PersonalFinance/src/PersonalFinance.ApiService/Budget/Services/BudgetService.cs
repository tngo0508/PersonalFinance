using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Budget.Services;

/// <summary>
/// Service implementation for managing user budget allocations.
/// </summary>
public class BudgetService : IBudgetService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<BudgetService> _logger;

    public BudgetService(
        AppDbContext dbContext,
        ILogger<BudgetService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<BudgetAllocationResponseDto?> GetBudgetAsync(string userId, string period, CancellationToken cancellationToken = default)
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

    public async Task SaveBudgetAsync(string userId, SaveBudgetRequestDto request, CancellationToken cancellationToken = default)
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
    }
}
