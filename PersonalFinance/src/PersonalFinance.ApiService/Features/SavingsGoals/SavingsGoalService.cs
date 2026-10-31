using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;
using CreateNotificationRequestDto = PersonalFinance.ApiService.Infrastructure.CreateNotificationRequestDto;
namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// Service implementation for managing user savings goals and their Contribution log. A goal's saved amount
/// (<see cref="SavingsGoal.CurrentAmount"/>) is the total of its Contributions, rewritten in the same save as every
/// Contribution change, and Reached (<see cref="SavingsGoal.IsCompleted"/>) is derived from it.
/// </summary>
internal sealed class SavingsGoalService : ISavingsGoalService
{
    private static readonly int[] Milestones = [50, 75, 100];

    private readonly AppDbContext _dbContext;
    private readonly ILogger<SavingsGoalService> _logger;
    private readonly INotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrencyConverter _currencyConverter;

    public SavingsGoalService(
        AppDbContext dbContext,
        ILogger<SavingsGoalService> logger,
        INotificationService notificationService,
        TimeProvider timeProvider,
        ICurrencyConverter currencyConverter)
    {
        _dbContext = dbContext;
        _logger = logger;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
        _currencyConverter = currencyConverter;
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
                CurrencyCode = g.CurrencyCode,
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

        // Every goal's history in one query, never one per goal.
        var goalIds = goals.Select(g => g.Id).ToList();
        ILookup<int, ContributionDto> contributions = (await NewestFirst(_dbContext.Contributions.AsNoTracking()
                .Where(c => c.UserId == userId && goalIds.Contains(c.SavingsGoalId)))
            .ToListAsync(cancellationToken))
            .ToLookup(c => c.SavingsGoalId);
        DateOnly today = ContributionRules.Today(_timeProvider);
        foreach (SavingsGoalResponseDto goal in goals)
        {
            goal.Contributions = contributions[goal.Id].ToList();
            goal.SavedThisMonth = SavingsGoalPace.SavedThisMonth(goal.Contributions, today);
            goal.Pace = SavingsGoalPace.Calculate(goal.CurrentAmount, goal.TargetAmount, goal.CreatedAtUtc,
                goal.TargetDate, goal.Contributions, today);
            goal.History = SavingsGoalPace.History(goal.Pace.Start, goal.Contributions, today);
        }

        // Each goal keeps its own currency; the page's totals need every amount in the Reporting Currency, converted
        // once here at the latest rate (ADR 0016). A goal already in it needs no rate and gets no caption.
        string reportingCurrency = await _currencyConverter.GetReportingCurrencyAsync(userId, cancellationToken);
        ReportingConversion conversion = await _currencyConverter.PrepareAsync(reportingCurrency,
            goals.Select(g => new MoneyAt(g.CurrencyCode, null)), cancellationToken);
        foreach (SavingsGoalResponseDto goal in goals)
        {
            goal.ReportingCurrency = reportingCurrency;
            goal.ReportingTargetAmount = conversion.Convert(goal.TargetAmount, goal.CurrencyCode, null);
            goal.ReportingCurrentAmount = conversion.Convert(goal.CurrentAmount, goal.CurrencyCode, null);
            goal.ReportingSavedThisMonth = conversion.Convert(goal.SavedThisMonth, goal.CurrencyCode, null);
        }

        foreach (SavingsGoalResponseDto goal in goals.Where(g => !conversion.IsReporting(g.CurrencyCode)))
        {
            goal.Conversion = conversion.ToDto();
        }

        return goals;
    }

    public async Task<int> CreateSavingsGoalAsync(string userId, CreateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        DateTime now = UtcNow();
        SupportedCurrency currency =
            SupportedCurrencies.Find(CurrencyCodeRules.Resolve(request.CurrencyCode)) ?? SupportedCurrencies.Default;
        decimal startingAmount = currency.Round(request.CurrentAmount ?? 0);
        var goal = new SavingsGoal
        {
            UserId = userId,
            CurrencyCode = currency.Code,
            Name = request.Name,
            TargetAmount = currency.Round(request.TargetAmount),
            CurrentAmount = startingAmount,
            Description = request.Description,
            TargetDate = request.TargetDate,
            CreatedAt = now
        };
        goal.IsCompleted = IsReached(goal);
        // A goal created part-way sends no Notification for the milestones it starts past.
        goal.HighestMilestoneNotified = Milestones.Where(m => HasReached(goal, m)).DefaultIfEmpty(0).Max();

        _dbContext.SavingsGoals.Add(goal);
        if (startingAmount != 0)
        {
            _dbContext.Contributions.Add(new Contribution
            {
                UserId = userId,
                SavingsGoal = goal,
                Amount = startingAmount,
                Date = DateOnly.FromDateTime(now),
                Kind = ContributionKind.Opening,
                CreatedAt = now
            });
        }

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
        SavingsGoal? goal = await FindGoalAsync(userId, id, cancellationToken);

        if (goal is null) return false;

        if (!string.IsNullOrEmpty(request.Name)) goal.Name = request.Name;
        if (request.TargetAmount.HasValue)
        {
            goal.TargetAmount = SupportedCurrencies.Round(goal.CurrencyCode, request.TargetAmount.Value);
        }
        if (request.Description != null) goal.Description = request.Description;
        if (request.TargetDate.HasValue) goal.TargetDate = request.TargetDate.Value;

        // A lower target can reach the goal, or a milestone, without any Contribution.
        await SaveWithTotalAsync(goal, goal.CurrentAmount, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSavingsGoalAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        SavingsGoal? goal = await FindGoalAsync(userId, id, cancellationToken);

        if (goal is null) return false;

        goal.SoftDelete();
        foreach (Contribution contribution in await _dbContext.Contributions
                     .Where(c => c.UserId == userId && c.SavingsGoalId == id)
                     .ToListAsync(cancellationToken))
        {
            contribution.SoftDelete();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} deleted savings goal {GoalId}", userId, id);
        }
        return true;
    }

    public async Task<List<ContributionDto>?> GetContributionsAsync(string userId, int goalId,
        CancellationToken cancellationToken = default)
    {
        bool goalExists = await _dbContext.SavingsGoals.AsNoTracking()
            .AnyAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);
        if (!goalExists) return null;

        return await NewestFirst(_dbContext.Contributions.AsNoTracking()
                .Where(c => c.UserId == userId && c.SavingsGoalId == goalId))
            .ToListAsync(cancellationToken);
    }

    public async Task<ContributionResult> AddContributionAsync(string userId, int goalId,
        SaveContributionRequestDto request, CancellationToken cancellationToken = default)
    {
        SavingsGoal? goal = await FindGoalAsync(userId, goalId, cancellationToken);
        if (goal is null) return new ContributionResult(ContributionOutcome.NotFound);

        decimal amount = SupportedCurrencies.Round(goal.CurrencyCode, request.Amount);
        decimal total = await LogTotalAsync(userId, goalId, cancellationToken) + amount;
        if (total < 0) return new ContributionResult(ContributionOutcome.BelowZero);

        var contribution = new Contribution
        {
            UserId = userId,
            SavingsGoalId = goalId,
            Amount = amount,
            Date = request.Date ?? ContributionRules.Today(_timeProvider),
            Note = NoteOrNull(request.Note),
            Kind = ContributionKind.Regular,
            CreatedAt = UtcNow()
        };
        _dbContext.Contributions.Add(contribution);

        await SaveWithTotalAsync(goal, total, cancellationToken);
        return new ContributionResult(ContributionOutcome.Saved, contribution.Id);
    }

    public async Task<ContributionResult> UpdateContributionAsync(string userId, int goalId, int contributionId,
        SaveContributionRequestDto request, CancellationToken cancellationToken = default)
    {
        SavingsGoal? goal = await FindGoalAsync(userId, goalId, cancellationToken);
        Contribution? contribution = goal is null
            ? null
            : await FindContributionAsync(userId, goalId, contributionId, cancellationToken);
        if (goal is null || contribution is null) return new ContributionResult(ContributionOutcome.NotFound);
        if (contribution.Kind == ContributionKind.Adjustment)
        {
            return new ContributionResult(ContributionOutcome.NotEditable);
        }

        decimal amount = SupportedCurrencies.Round(goal.CurrencyCode, request.Amount);
        decimal total = await LogTotalAsync(userId, goalId, cancellationToken) - contribution.Amount + amount;
        if (total < 0) return new ContributionResult(ContributionOutcome.BelowZero);

        contribution.Amount = amount;
        contribution.Date = request.Date ?? contribution.Date;
        contribution.Note = NoteOrNull(request.Note);
        contribution.UpdatedAt = UtcNow();

        await SaveWithTotalAsync(goal, total, cancellationToken);
        return new ContributionResult(ContributionOutcome.Saved, contribution.Id);
    }

    public async Task<ContributionResult> DeleteContributionAsync(string userId, int goalId, int contributionId,
        CancellationToken cancellationToken = default)
    {
        SavingsGoal? goal = await FindGoalAsync(userId, goalId, cancellationToken);
        Contribution? contribution = goal is null
            ? null
            : await FindContributionAsync(userId, goalId, contributionId, cancellationToken);
        if (goal is null || contribution is null) return new ContributionResult(ContributionOutcome.NotFound);

        decimal total = await LogTotalAsync(userId, goalId, cancellationToken) - contribution.Amount;
        if (total < 0) return new ContributionResult(ContributionOutcome.BelowZero);

        contribution.SoftDelete();

        await SaveWithTotalAsync(goal, total, cancellationToken);
        return new ContributionResult(ContributionOutcome.Saved, contribution.Id);
    }

    public async Task<ContributionResult> CorrectBalanceAsync(string userId, int goalId,
        CorrectBalanceRequestDto request, CancellationToken cancellationToken = default)
    {
        SavingsGoal? goal = await FindGoalAsync(userId, goalId, cancellationToken);
        if (goal is null) return new ContributionResult(ContributionOutcome.NotFound);

        decimal balance = SupportedCurrencies.Round(goal.CurrencyCode, request.Balance);
        decimal difference = balance - await LogTotalAsync(userId, goalId, cancellationToken);
        Contribution? adjustment = null;
        if (difference != 0)
        {
            adjustment = new Contribution
            {
                UserId = userId,
                SavingsGoalId = goalId,
                Amount = difference,
                Date = ContributionRules.Today(_timeProvider),
                Kind = ContributionKind.Adjustment,
                CreatedAt = UtcNow()
            };
            _dbContext.Contributions.Add(adjustment);
        }

        await SaveWithTotalAsync(goal, balance, cancellationToken);
        return new ContributionResult(ContributionOutcome.Saved, adjustment?.Id);
    }

    /// <summary>Contributions newest first: date descending, then id, so same-day entries keep a stable order.</summary>
    private static IQueryable<ContributionDto> NewestFirst(IQueryable<Contribution> contributions) =>
        contributions
            .OrderByDescending(c => c.Date).ThenByDescending(c => c.Id)
            .Select(c => new ContributionDto
            {
                Id = c.Id,
                SavingsGoalId = c.SavingsGoalId,
                Amount = c.Amount,
                Date = c.Date,
                Note = c.Note,
                Kind = c.Kind
            });

    private Task<SavingsGoal?> FindGoalAsync(string userId, int goalId, CancellationToken cancellationToken) =>
        _dbContext.SavingsGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);

    private Task<Contribution?> FindContributionAsync(string userId, int goalId, int contributionId,
        CancellationToken cancellationToken) =>
        _dbContext.Contributions.FirstOrDefaultAsync(
            c => c.Id == contributionId && c.SavingsGoalId == goalId && c.UserId == userId, cancellationToken);

    /// <summary>The total of the goal's saved Contributions: the log, not the cached figure, is the source of truth.</summary>
    private Task<decimal> LogTotalAsync(string userId, int goalId, CancellationToken cancellationToken) =>
        _dbContext.Contributions
            .Where(c => c.UserId == userId && c.SavingsGoalId == goalId)
            .SumAsync(c => c.Amount, cancellationToken);

    /// <summary>
    /// Writes the saved amount, Reached and the milestone high-water mark in the same save as the change, then sends
    /// a Notification for each milestone newly passed.
    /// </summary>
    private async Task SaveWithTotalAsync(SavingsGoal goal, decimal total, CancellationToken cancellationToken)
    {
        goal.CurrentAmount = total;
        goal.IsCompleted = IsReached(goal);
        goal.UpdatedAt = UtcNow();
        var passed = Milestones.Where(m => m > goal.HighestMilestoneNotified && HasReached(goal, m)).ToList();
        if (passed.Count > 0)
        {
            goal.HighestMilestoneNotified = passed.Max();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (int milestone in passed)
        {
            await NotifyMilestoneAsync(goal, milestone, cancellationToken);
        }
    }

    private static bool IsReached(SavingsGoal goal) => goal.TargetAmount > 0 && goal.CurrentAmount >= goal.TargetAmount;

    private static bool HasReached(SavingsGoal goal, int milestone) =>
        goal.TargetAmount > 0 && goal.CurrentAmount >= goal.TargetAmount * milestone / 100m;

    private static string? NoteOrNull(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Sends the milestone Notification. It is already recorded as sent, so a failure here never repeats it later.
    /// </summary>
    async Task NotifyMilestoneAsync(SavingsGoal goal, int milestone, CancellationToken cancellationToken)
    {
        try
        {
            string current = MoneyFormatter.Format(goal.CurrentAmount, goal.CurrencyCode);
            string target = MoneyFormatter.Format(goal.TargetAmount, goal.CurrencyCode);
            await _notificationService.CreateNotificationAsync(goal.UserId, new CreateNotificationRequestDto
            {
                Type = "SavingsMilestone",
                Title = $"{milestone}% savings milestone reached!",
                Message =
                    $"Your goal '{goal.Name}' has reached {milestone}% of its target ({current} of {target}).",
                TitleKey = "Notifications.SavingsMilestone.Title",
                MessageKey = "Notifications.SavingsMilestone.Message",
                Arguments = new Dictionary<string, string>
                {
                    ["percent"] = milestone.ToString(CultureInfo.InvariantCulture),
                    ["goalName"] = goal.Name,
                    ["current"] = current,
                    ["target"] = target
                },
                SourceFeature = "SavingsGoals"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send a savings milestone for goal {GoalId}", goal.Id);
        }
    }
}
