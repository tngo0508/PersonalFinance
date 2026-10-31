using System.Globalization;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.Web.Models.SavingsGoals;

/// <summary>
/// The geometry of a card's saved-vs-ideal chart (spec #395, slice 3): a stepped line of the saved amount from the
/// goal's start to today, a dashed ideal line from (start, starting amount) to (target date, target) for dated goals, a
/// thin target line and a marker at today. Coordinates are in the SVG's own <see cref="Width"/> by
/// <see cref="Height"/> box, formatted invariant. The chart is decorative: the pace sentence carries the same facts.
/// </summary>
public sealed class SavingsGoalChartViewModel
{
    public const int Width = 280;
    public const int Height = 72;
    private const double Pad = 4;

    /// <summary>The saved line, as an SVG polyline's points.</summary>
    public required string SavedPoints { get; init; }

    /// <summary>The ideal line's ends; null for an undated goal or one whose date is not after its start.</summary>
    public (string X1, string Y1, string X2, string Y2)? Ideal { get; init; }

    public required string TargetY { get; init; }
    public required string TodayX { get; init; }
    public required string TodayY { get; init; }

    /// <summary>The chart for a goal, or null when the response carries no history (an older ApiService).</summary>
    public static SavingsGoalChartViewModel? From(SavingsGoalDto goal, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(goal);
        if (goal.History.Count == 0) return null;

        List<SavingsGoalHistoryPointDto> history = goal.History.OrderBy(p => p.Date).ToList();
        DateOnly from = history[0].Date;
        DateOnly? due = goal.TargetDate is { } date ? DateOnly.FromDateTime(date) : null;
        DateOnly to = due is { } dueDate && dueDate > today ? dueDate : today;
        int span = Math.Max(1, to.DayNumber - from.DayNumber);
        decimal top = Math.Max(1m, Math.Max(goal.TargetAmount, history.Max(p => p.Saved))) * 1.05m;

        double X(DateOnly day) => Pad + (Width - 2 * Pad) * Math.Clamp(day.DayNumber - from.DayNumber, 0, span) / span;
        double Y(decimal amount) => Height - Pad - (Height - 2 * Pad) * (double)(Math.Max(0m, amount) / top);

        decimal saved = history[^1].Saved;
        return new SavingsGoalChartViewModel
        {
            SavedPoints = SteppedPoints(history, today, X, Y),
            Ideal = IdealLine(goal, from, due, X, Y),
            TargetY = F(Y(goal.TargetAmount)),
            TodayX = F(X(today)),
            TodayY = F(Y(saved))
        };
    }

    /// <summary>Each point holds its level until the next date, then steps; the last level runs on to today.</summary>
    private static string SteppedPoints(List<SavingsGoalHistoryPointDto> history, DateOnly today,
        Func<DateOnly, double> x, Func<decimal, double> y)
    {
        var points = new List<string> { Point(x(history[0].Date), y(history[0].Saved)) };
        for (int i = 1; i < history.Count; i++)
        {
            points.Add(Point(x(history[i].Date), y(history[i - 1].Saved)));
            points.Add(Point(x(history[i].Date), y(history[i].Saved)));
        }

        points.Add(Point(x(today), y(history[^1].Saved)));
        return string.Join(' ', points);
    }

    /// <summary>
    /// The ideal line from the goal's start to its date. When the history was cut to its last 24 months, the line
    /// starts at the chart's left edge at the height the ideal line has there.
    /// </summary>
    private static (string, string, string, string)? IdealLine(SavingsGoalDto goal, DateOnly from, DateOnly? due,
        Func<DateOnly, double> x, Func<decimal, double> y)
    {
        if (goal.Pace is not { } pace || due is not { } dueDate || dueDate <= pace.Start) return null;

        DateOnly left = pace.Start < from ? from : pace.Start;
        decimal share = (decimal)(left.DayNumber - pace.Start.DayNumber) / (dueDate.DayNumber - pace.Start.DayNumber);
        decimal leftAmount = pace.StartingAmount + (goal.TargetAmount - pace.StartingAmount) * Math.Min(1m, share);
        return (F(x(left)), F(y(leftAmount)), F(x(dueDate)), F(y(goal.TargetAmount)));
    }

    private static string Point(double x, double y) => $"{F(x)},{F(y)}";

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
