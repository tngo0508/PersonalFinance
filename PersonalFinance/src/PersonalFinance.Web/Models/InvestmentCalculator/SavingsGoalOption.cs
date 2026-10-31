using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.Web.Models.InvestmentCalculator;

/// <summary>
/// One of the Account's active Savings Goals, offered by the Reach a goal tile as a starting goal (#593): its target in
/// the calculator's Reporting Currency and, for a dated goal, the years to its target date. Read-only; the calculator
/// never writes to Savings Goals.
/// </summary>
/// <param name="Id">The Savings Goal's id.</param>
/// <param name="Name">The goal's name.</param>
/// <param name="Target">The target in the Reporting Currency, rounded up to a whole amount.</param>
/// <param name="Years">Whole years from today to the target date, rounded up and kept within the calculator's years
/// (1 to 50); null for a goal with no target date.</param>
/// <param name="ConvertedFrom">The goal's own currency when its target was converted at the latest rate; null when
/// the goal is in the Reporting Currency.</param>
public sealed record SavingsGoalOption(int Id, string Name, decimal Target, int? Years, string? ConvertedFrom)
{
    /// <summary>The calculator's shortest plan, in years (investment-math.js LIMITS.years).</summary>
    public const int MinYears = 1;

    /// <summary>The calculator's longest plan, in years.</summary>
    public const int MaxYears = 50;

    private const double DaysPerYear = 365.25;

    /// <summary>
    /// The goals the picker offers, in the API's order: every goal not yet reached whose target can be shown in
    /// <paramref name="currency"/>. A goal in another currency uses the API's converted target; one with no rate to
    /// convert at is left out rather than shown in the wrong currency.
    /// </summary>
    public static IReadOnlyList<SavingsGoalOption> From(IEnumerable<SavingsGoalDto> goals, string currency, DateOnly today)
    {
        List<SavingsGoalOption> options = [];
        foreach (SavingsGoalDto goal in goals)
        {
            if (goal.IsCompleted || TargetIn(goal, currency) is not { } target || target <= 0)
            {
                continue;
            }

            bool converted = !string.Equals(goal.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase);
            options.Add(new SavingsGoalOption(goal.Id, goal.Name, Math.Ceiling(target), YearsTo(goal.TargetDate, today),
                converted ? goal.CurrencyCode : null));
        }

        return options;
    }

    private static decimal? TargetIn(SavingsGoalDto goal, string currency)
    {
        if (string.Equals(goal.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
        {
            return goal.TargetAmount;
        }

        return string.Equals(goal.ReportingCurrency, currency, StringComparison.OrdinalIgnoreCase)
            ? goal.ReportingTargetAmount
            : null;
    }

    private static int? YearsTo(DateTime? targetDate, DateOnly today)
    {
        if (targetDate is not { } date)
        {
            return null;
        }

        int days = DateOnly.FromDateTime(date).DayNumber - today.DayNumber;
        int years = (int)Math.Ceiling(Math.Max(0, days) / DaysPerYear);
        return Math.Clamp(years, MinYears, MaxYears);
    }
}
