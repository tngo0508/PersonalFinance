using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using CreateNotificationRequestDto = PersonalFinance.ApiService.Infrastructure.CreateNotificationRequestDto;
namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// Service implementation for managing user savings goals.
/// </summary>
internal sealed class SavingsGoalService : ISavingsGoalService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SavingsGoalService> _logger;
    private readonly INotificationService _notificationService;

    public SavingsGoalService(
        AppDbContext dbContext,
        ILogger<SavingsGoalService> logger,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<List<SavingsGoalResponseDto>> GetSavingsGoalsAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        List<SavingsGoalResponseDto> goals = await _dbContext.SavingsGoals
            .AsNoTracking()
            .NewestFirstFor(userId)
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
                ProgressPercent = g.TargetAmount > 0
                    ? (double)Math.Min(100m, (g.CurrentAmount / g.TargetAmount) * 100m)
                    : 0
            })
            .ToListAsync(cancellationToken);

        return goals;
    }

    public async Task<int> CreateSavingsGoalAsync(string userId, CreateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default)
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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} created savings goal {GoalId}", userId, goal.Id);
        }
        return goal.Id;
    }

    public async Task<bool> UpdateSavingsGoalAsync(string userId, int id, UpdateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        SavingsGoal? goal = await _dbContext.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);

        if (goal is null) return false;

        if (!string.IsNullOrEmpty(request.Name)) goal.Name = request.Name;
        if (request.TargetAmount.HasValue) goal.TargetAmount = request.TargetAmount.Value;
        decimal previousAmount = goal.CurrentAmount;
        if (request.CurrentAmount.HasValue) goal.CurrentAmount = request.CurrentAmount.Value;
        if (request.Description != null) goal.Description = request.Description;
        if (request.TargetDate.HasValue) goal.TargetDate = request.TargetDate.Value;
        goal.IsCompleted = request.IsCompleted;
        goal.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Check for milestone notifications
        await CheckSavingsMilestonesAsync(userId, goal.Id, goal.Name, previousAmount, goal.CurrentAmount,
            goal.TargetAmount, cancellationToken);

        return true;
    }

    public async Task<bool> DeleteSavingsGoalAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        SavingsGoal? goal = await _dbContext.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);

        if (goal is null) return false;

        goal.SoftDelete();
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} deleted savings goal {GoalId}", userId, id);
        }
        return true;
    }

    /// <summary>
    /// Checks if a savings goal has crossed any milestone thresholds (50%, 75%, 100%).
    /// Creates notifications only when crossing from below to above the threshold.
    /// </summary>
    async Task CheckSavingsMilestonesAsync(string userId, int goalId, string goalName,
        decimal previousAmount, decimal currentAmount, decimal targetAmount, CancellationToken cancellationToken)
    {
        try
        {
            if (targetAmount <= 0) return;

            int[] milestones = new[] { 50, 75, 100 };

            foreach (int milestone in milestones)
            {
                decimal threshold = targetAmount * milestone / 100m;

                // Only notify if we just crossed the threshold
                if (previousAmount < threshold && currentAmount >= threshold)
                {
                    await _notificationService.CreateNotificationAsync(userId, new CreateNotificationRequestDto
                    {
                        Type = "SavingsMilestone",
                        Title = $"{milestone}% savings milestone reached!",
                        Message =
                            $"Your goal '{goalName}' has reached {milestone}% of its target ({currentAmount:C} of {targetAmount:C}).",
                        SourceFeature = "SavingsGoals"
                    }, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check savings milestones for goal {GoalId}", goalId);
        }
    }
}
