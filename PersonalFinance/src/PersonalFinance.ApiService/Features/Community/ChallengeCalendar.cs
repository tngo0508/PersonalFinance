using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// The periods of a Challenge (GLOSSARY.md), counted in local days: a Daily Challenge's period is the day itself, and a
/// Weekly one's is the seven days from its start date, then the next seven, and so on. Pure: no clock, no database.
/// </summary>
internal static class ChallengeCalendar
{
    /// <summary>
    /// The first day of the period <paramref name="today"/> falls in, or null when the Challenge is not running that
    /// day (it has not started, or it has ended).
    /// </summary>
    public static DateOnly? CurrentPeriod(CommunityChallengePeriod period, DateOnly startDate, DateOnly endDate,
        DateOnly today)
    {
        if (today < startDate || today > endDate)
        {
            return null;
        }

        return period == CommunityChallengePeriod.Weekly
            ? startDate.AddDays((today.DayNumber - startDate.DayNumber) / 7 * 7)
            : today;
    }

    /// <summary>The first day of the period before the one starting on <paramref name="periodStart"/>.</summary>
    public static DateOnly Previous(CommunityChallengePeriod period, DateOnly periodStart) =>
        periodStart.AddDays(period == CommunityChallengePeriod.Weekly ? -7 : -1);

    /// <summary>
    /// How many periods in a row the Account kept, ending with the current one when it has checked in for it, or with
    /// the one before when it has not yet (the streak is still alive until the current period ends).
    /// </summary>
    public static int Streak(CommunityChallengePeriod period, DateOnly startDate, DateOnly currentPeriod,
        IReadOnlySet<DateOnly> checkIns)
    {
        DateOnly cursor = checkIns.Contains(currentPeriod) ? currentPeriod : Previous(period, currentPeriod);
        int streak = 0;
        while (cursor >= startDate && checkIns.Contains(cursor))
        {
            streak++;
            cursor = Previous(period, cursor);
        }

        return streak;
    }
}
