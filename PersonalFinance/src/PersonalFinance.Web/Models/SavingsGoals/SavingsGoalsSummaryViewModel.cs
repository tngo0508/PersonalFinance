using PersonalFinance.Shared.DTOs.Currency;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.SavingsGoals;

/// <summary>
/// The four tiles above the Savings Goals grid, worked out from the goal list: no endpoint of its own (spec #395).
/// </summary>
public class SavingsGoalsSummaryViewModel
{
    /// <summary>The saved amount across goals not yet reached.</summary>
    public decimal TotalSavedActive { get; init; }

    /// <summary>The net of every goal's Regular Contributions this calendar month.</summary>
    public decimal SavedThisMonth { get; init; }

    /// <summary>
    /// How the goals in other currencies were converted into the Reporting Currency (ADR 0016), for the caption under
    /// the tiles; null when none needed it. The two amounts above sum each goal's reporting amount (its own when no
    /// rate exists).
    /// </summary>
    public ConversionDto? Conversion { get; init; }

    /// <summary>Dated goals with an Ahead or On track verdict.</summary>
    public int OnTrackCount { get; init; }

    /// <summary>Dated goals with a verdict: Ahead, On track, Behind or Past due (not Reached or Just started).</summary>
    public int DatedCount { get; init; }

    /// <summary>The nearest target date, today or later, of a goal not yet reached; null when there is none.</summary>
    public DateOnly? NextTargetDate { get; init; }

    /// <summary>The name of the goal due on <see cref="NextTargetDate"/>.</summary>
    public string? NextTargetGoalName { get; init; }

    /// <summary>Builds the tiles from the goals the page shows.</summary>
    public static SavingsGoalsSummaryViewModel From(IReadOnlyCollection<SavingsGoalDto> goals, DateOnly today)
    {
        var verdicts = goals
            .Where(g => g.Pace?.Status is PaceStatus.Ahead or PaceStatus.OnTrack or PaceStatus.Behind
                or PaceStatus.PastDue)
            .ToList();
        SavingsGoalDto? next = goals
            .Where(g => !g.IsCompleted && g.TargetDate.HasValue
                        && DateOnly.FromDateTime(g.TargetDate.Value) >= today)
            .OrderBy(g => g.TargetDate)
            .ThenBy(g => g.Id)
            .FirstOrDefault();

        return new SavingsGoalsSummaryViewModel
        {
            TotalSavedActive = goals.Where(g => !g.IsCompleted).Sum(g => g.ReportingCurrentAmount ?? g.CurrentAmount),
            SavedThisMonth = goals.Sum(g => g.ReportingSavedThisMonth ?? g.SavedThisMonth),
            Conversion = ConversionDto.Combine(goals.Select(g => g.Conversion)),
            OnTrackCount = verdicts.Count(g => g.Pace!.Status is PaceStatus.Ahead or PaceStatus.OnTrack),
            DatedCount = verdicts.Count,
            NextTargetDate = next is null ? null : DateOnly.FromDateTime(next.TargetDate!.Value),
            NextTargetGoalName = next?.Name
        };
    }
}
