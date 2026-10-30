using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.Learning.Domain;

/// <summary>
/// When the next Lesson of the Journey (GLOSSARY.md) opens: the local midnight after the previous Lesson's finish, in
/// the time zone the browser sent with it. Pure: no clock, no database.
/// </summary>
internal static class JourneyClock
{
    /// <summary>The zone stored when the browser sent none or an unknown one.</summary>
    public const string Utc = BrowserTimeZone.Utc;

    /// <summary>The longest zone id stored; a longer one is treated as unknown.</summary>
    public const int ZoneIdMaxLength = BrowserTimeZone.ZoneIdMaxLength;

    /// <summary>The IANA id to store for the zone the browser sent (<see cref="BrowserTimeZone.Resolve"/>).</summary>
    public static string ResolveZoneId(string? zoneId) => BrowserTimeZone.Resolve(zoneId);

    /// <summary>The zone a stored id names (<see cref="BrowserTimeZone.Zone"/>).</summary>
    public static TimeZoneInfo Zone(string zoneId) => BrowserTimeZone.Zone(zoneId);

    /// <summary>
    /// The first local midnight in <paramref name="zone"/> after <paramref name="finishedAtUtc"/>, as a UTC instant.
    /// When a clock change skips midnight, the first valid minute after it counts; when midnight happens twice, the
    /// later (standard-time) one does, so the next Lesson never opens early.
    /// </summary>
    public static DateTime NextLocalMidnightUtc(DateTime finishedAtUtc, TimeZoneInfo zone)
    {
        DateTime utc = DateTime.SpecifyKind(finishedAtUtc, DateTimeKind.Utc);
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        DateTime midnight = DateTime.SpecifyKind(local.Date.AddDays(1), DateTimeKind.Unspecified);
        // Clock changes are an hour, or half an hour in a few zones: step in quarters until the time exists.
        for (int step = 0; step < 8 && zone.IsInvalidTime(midnight); step++)
        {
            midnight = midnight.AddMinutes(15);
        }

        return TimeZoneInfo.ConvertTimeToUtc(midnight, zone);
    }
}
