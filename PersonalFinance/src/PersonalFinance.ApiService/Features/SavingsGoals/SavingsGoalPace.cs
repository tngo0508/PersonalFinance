using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// Works out a goal's Pace (spec #395, "Pace rules") from its Contribution log at today's date. Pure arithmetic: no
/// data access, so the goal list computes it for every goal from the Contributions it already loaded.
/// </summary>
internal static class SavingsGoalPace
{
    /// <summary>The window the recent rate is measured over.</summary>
    public const int RateWindowDays = 90;

    /// <summary>Under this age a goal is Just started and its rate is unknown.</summary>
    public const int JustStartedDays = 30;

    /// <summary>Days in an average month.</summary>
    public const decimal DaysPerMonth = 30.44m;

    /// <summary>The On track band either side of the ideal line, as a share of the target.</summary>
    public const decimal OnTrackBand = 0.05m;

    /// <summary>A projection further out than this is not shown.</summary>
    private const int MaxProjectionDays = 100 * 365;

    /// <summary>
    /// The goal's Pace. <paramref name="contributions"/> are the goal's saved Contributions, in any order.
    /// </summary>
    public static SavingsGoalPaceDto Calculate(decimal saved, decimal target, DateTime createdAt, DateTime? targetDate,
        IReadOnlyCollection<ContributionDto> contributions, DateOnly today)
    {
        DateOnly start = DateOnly.FromDateTime(createdAt);
        if (contributions.Count > 0)
        {
            DateOnly earliest = contributions.Min(c => c.Date);
            start = earliest < start ? earliest : start;
        }

        int ageDays = today.DayNumber - start.DayNumber;
        decimal remaining = Math.Max(0m, target - saved);
        bool reached = target > 0 && saved >= target;
        DateOnly? due = targetDate is { } date ? DateOnly.FromDateTime(date) : null;

        var pace = new SavingsGoalPaceDto { Status = Status(saved, target, start, ageDays, due, contributions, today) };

        if (!reached && due is { } dueDate && dueDate >= today)
        {
            decimal months = Math.Max(1m, (dueDate.DayNumber - today.DayNumber) / DaysPerMonth);
            pace.NeededPerMonth = decimal.Ceiling(remaining / months * 100m) / 100m;
        }

        (pace.RateState, decimal? perMonth) = RecentRate(ageDays, contributions, today);
        pace.RatePerMonth = perMonth is { } value ? decimal.Round(value, 2) : null;
        if (!reached && perMonth is { } rate)
        {
            decimal days = decimal.Ceiling(remaining / rate * DaysPerMonth);
            pace.ProjectedFinish = days <= MaxProjectionDays ? today.AddDays((int)days) : null;
        }

        return pace;
    }

    /// <summary>The net of the goal's Regular Contributions dated in today's calendar month.</summary>
    public static decimal SavedThisMonth(IEnumerable<ContributionDto> contributions, DateOnly today) =>
        contributions
            .Where(c => c.Kind == ContributionKind.Regular && c.Date.Year == today.Year && c.Date.Month == today.Month
                        && c.Date <= today)
            .Sum(c => c.Amount);

    private static PaceStatus Status(decimal saved, decimal target, DateOnly start, int ageDays, DateOnly? due,
        IReadOnlyCollection<ContributionDto> contributions, DateOnly today)
    {
        if (target > 0 && saved >= target) return PaceStatus.Reached;
        if (due is not { } dueDate) return PaceStatus.NoTargetDate;
        if (dueDate < today) return PaceStatus.PastDue;
        if (ageDays < JustStartedDays) return PaceStatus.JustStarted;

        // The ideal line runs from (start, starting amount) to (target date, target).
        decimal startingAmount = contributions.Where(c => c.Kind == ContributionKind.Opening).Sum(c => c.Amount);
        int span = dueDate.DayNumber - start.DayNumber;
        decimal ideal = span <= 0
            ? target
            : startingAmount + (target - startingAmount) * Math.Min(1m, (decimal)ageDays / span);
        decimal band = target * OnTrackBand;

        if (saved >= ideal + band) return PaceStatus.Ahead;
        return saved >= ideal - band ? PaceStatus.OnTrack : PaceStatus.Behind;
    }

    /// <summary>
    /// The net of Regular Contributions over the last 90 days (or the goal's age when younger), per month. The Opening
    /// amount and balance corrections are left out: they are not saving.
    /// </summary>
    private static (PaceRateState State, decimal? PerMonth) RecentRate(int ageDays,
        IEnumerable<ContributionDto> contributions, DateOnly today)
    {
        var regular = contributions.Where(c => c.Kind == ContributionKind.Regular).ToList();
        if (ageDays < JustStartedDays || regular.Count == 0) return (PaceRateState.Unknown, null);

        int windowDays = Math.Min(RateWindowDays, ageDays);
        DateOnly from = today.AddDays(-windowDays);
        var inWindow = regular.Where(c => c.Date > from && c.Date <= today).ToList();
        decimal net = inWindow.Sum(c => c.Amount);
        if (inWindow.Count == 0 || net <= 0) return (PaceRateState.Stalled, null);

        return (PaceRateState.Positive, net / (windowDays / DaysPerMonth));
    }
}
