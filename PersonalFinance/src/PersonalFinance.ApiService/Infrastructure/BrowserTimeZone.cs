namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// The IANA time zone a browser sends (<c>Intl.DateTimeFormat().resolvedOptions().timeZone</c>), checked before it is
/// stored. Shared by the Features that count local days: the Journey's Lessons and the Guild Hall's Check-ins.
/// </summary>
public static class BrowserTimeZone
{
    /// <summary>The zone stored when the browser sent none or an unknown one.</summary>
    public const string Utc = "UTC";

    /// <summary>The longest zone id stored; a longer one is treated as unknown.</summary>
    public const int ZoneIdMaxLength = 64;

    /// <summary>
    /// The IANA id to store for the zone the browser sent: the id itself when the platform knows it (a Windows id is
    /// converted), otherwise <see cref="Utc"/>.
    /// </summary>
    public static string Resolve(string? zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId) || zoneId.Trim().Length > ZoneIdMaxLength)
        {
            return Utc;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zoneId.Trim(), out TimeZoneInfo? zone))
        {
            return Utc;
        }

        if (zone.HasIanaId)
        {
            return zone.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out string? iana) ? iana : Utc;
    }

    /// <summary>The zone a stored id names; <see cref="TimeZoneInfo.Utc"/> when the platform no longer knows it.</summary>
    public static TimeZoneInfo Zone(string zoneId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out TimeZoneInfo? zone) ? zone : TimeZoneInfo.Utc;

    /// <summary>The local date in <paramref name="zone"/> at <paramref name="utcNow"/>.</summary>
    public static DateOnly Today(DateTimeOffset utcNow, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);
}
