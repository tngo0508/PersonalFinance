using System.Globalization;
using System.Text.RegularExpressions;

namespace PersonalFinance.ApiService.Features.Watchlist.Adapters;

/// <summary>
/// Reads an RSS <c>pubDate</c>: an RFC 822 date such as <c>Tue, 20 Oct 2026 08:00:00 -0400</c> or
/// <c>... EST</c>, independent of the server culture, falling back to any invariant date and time (ISO 8601 feeds).
/// </summary>
internal static partial class Rfc822Date
{
    private static readonly string[] DateTimeFormats =
        ["d MMM yyyy HH:mm:ss", "d MMM yyyy HH:mm", "d MMM yy HH:mm:ss", "d MMM yy HH:mm"];

    // The zone names RFC 822 section 5 defines; military single-letter zones other than Z are left out.
    private static readonly Dictionary<string, TimeSpan> NamedZones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UT"] = TimeSpan.Zero,
        ["GMT"] = TimeSpan.Zero,
        ["Z"] = TimeSpan.Zero,
        ["EST"] = TimeSpan.FromHours(-5),
        ["EDT"] = TimeSpan.FromHours(-4),
        ["CST"] = TimeSpan.FromHours(-6),
        ["CDT"] = TimeSpan.FromHours(-5),
        ["MST"] = TimeSpan.FromHours(-7),
        ["MDT"] = TimeSpan.FromHours(-6),
        ["PST"] = TimeSpan.FromHours(-8),
        ["PDT"] = TimeSpan.FromHours(-7),
    };

    /// <summary>
    /// Parses <paramref name="value"/> to a UTC <see cref="DateTime"/>; false when it is not a date.
    /// </summary>
    public static bool TryParseUtc(string value, out DateTime utc)
    {
        Match match = Rfc822Pattern().Match(value);
        if (match.Success
            && DateTime.TryParseExact(match.Groups["datetime"].Value, DateTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowInnerWhite, out DateTime local)
            && TryParseZone(match.Groups["zone"].Value, out TimeSpan offset))
        {
            utc = new DateTimeOffset(local, offset).UtcDateTime;
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
        {
            utc = parsed.UtcDateTime;
            return true;
        }

        utc = default;
        return false;
    }

    /// <summary>
    /// Reads <c>+hhmm</c>/<c>-hhmm</c> or a zone name; a missing zone is UTC.
    /// </summary>
    private static bool TryParseZone(string zone, out TimeSpan offset)
    {
        if (zone.Length == 0)
        {
            offset = TimeSpan.Zero;
            return true;
        }

        if (zone[0] is '+' or '-')
        {
            int hours = int.Parse(zone.AsSpan(1, 2), CultureInfo.InvariantCulture);
            int minutes = int.Parse(zone.AsSpan(3, 2), CultureInfo.InvariantCulture);
            offset = new TimeSpan(hours, minutes, 0) * (zone[0] == '-' ? -1 : 1);
            return true;
        }

        return NamedZones.TryGetValue(zone, out offset);
    }

    // Optional day name, then "d MMM yy[yy] HH:mm[:ss]", then an optional numeric offset or zone name.
    [GeneratedRegex(@"^\s*(?:[A-Za-z]{3},\s*)?(?<datetime>\d{1,2}\s+[A-Za-z]{3}\s+\d{2,4}\s+\d{1,2}:\d{2}(?::\d{2})?)\s*(?<zone>[+-]\d{4}|[A-Za-z]{1,3})?\s*$")]
    private static partial Regex Rfc822Pattern();
}
