using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.SavingsGoals.Services;

/// <summary>
/// Service implementation for managing user savings goals.
/// </summary>
public class SavingsGoalService : ISavingsGoalService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SavingsGoalService> _logger;

    public SavingsGoalService(
        AppDbContext dbContext,
        ILogger<SavingsGoalService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<SavingsGoalResponseDto>> GetSavingsGoalsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var goals = await _dbContext.SavingsGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new SavingsGoalResponseDto
            {
                Id = g.Id,
                Name = g.Name,
                TargetAmount = g.TargetAmount,
                CurrentAmount = g.CurrentAmount,
                Description = g.Description,
                TargetDate = g.TargetDate,
                IsCompleted = g.IsCompleted,
                CreatedAtUtc = g.CreatedAt,
                UpdatedAtUtc = g.UpdatedAt ?? g.CreatedAt,
                ProgressPercent = g.TargetAmount > 0 ? (double)Math.Min(100m, (g.CurrentAmount / g.TargetAmount) * 100m) : 0
            })
            .ToListAsync(cancellationToken);

        return goals;
    }

    public async Task<int> CreateSavingsGoalAsync(string userId, CreateSavingsGoalRequestDto request, CancellationToken cancellationToken = default)
    {
        var goal = new SavingsGoal
        {
            UserId = userId,
            Name = request.Name,
            TargetAmount = request.TargetAmount,
            CurrentAmount = request.CurrentAmount ?? 0,
            Description = request.Description,
            TargetDate = request.TargetDate
        };

        _dbContext.SavingsGoals.Add(goal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} created savings goal {GoalId}", userId, goal.Id);
        return goal.Id;
    }

    public async Task<bool> UpdateSavingsGoalAsync(string userId, int id, UpdateSavingsGoalRequestDto request, CancellationToken cancellationToken = default)
    {
        var goal = await _dbContext.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);

        if (goal is null) return false;

        if (!string.IsNullOrEmpty(request.Name)) goal.Name = request.Name;
        if (request.TargetAmount.HasValue) goal.TargetAmount = request.TargetAmount.Value;
        if (request.CurrentAmount.HasValue) goal.CurrentAmount = request.CurrentAmount.Value;
        if (request.Description != null) goal.Description = request.Description;
        if (request.TargetDate.HasValue) goal.TargetDate = request.TargetDate.Value;
        goal.IsCompleted = request.IsCompleted;
        goal.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSavingsGoalAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var goal = await _dbContext.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);

        if (goal is null) return false;

        goal.SoftDelete();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} deleted savings goal {GoalId}", userId, id);
        return true;
    }
}
