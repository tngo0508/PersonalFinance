using PersonalFinance.ApiService.Features.Learning.Domain;

namespace PersonalFinance.Tests.Learning;

/// <summary>
/// When the next Lesson opens: the local midnight after a finish, in the zone the browser sent, with clock changes
/// and unknown zones handled so the next Lesson never opens early and never gets stuck.
/// </summary>
public class JourneyClockTests
{
    [Fact]
    public void NextLocalMidnight_IsTheNextMidnightOfTheZone_AsUtc()
    {
        // 22:30 UTC on 1 November is 05:30 on 2 November in Ho Chi Minh City (UTC+7): the next midnight there is
        // 3 November 00:00, which is 2 November 17:00 UTC.
        DateTime opens = JourneyClock.NextLocalMidnightUtc(
            new DateTime(2026, 11, 1, 22, 30, 0, DateTimeKind.Utc), JourneyClock.Zone("Asia/Ho_Chi_Minh"));

        Assert.Equal(new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc), opens);
    }

    [Fact]
    public void NextLocalMidnight_WhenTheClocksGoBackThatNight_UsesTheNewOffset()
    {
        // New York leaves daylight time on 1 November 2026 at 02:00. A finish at 01:30 EDT (05:30 UTC) is followed
        // by midnight on 2 November in standard time (UTC-5): 05:00 UTC, 23.5 hours later, not 24.
        DateTime opens = JourneyClock.NextLocalMidnightUtc(
            new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc), JourneyClock.Zone("America/New_York"));

        Assert.Equal(new DateTime(2026, 11, 2, 5, 0, 0, DateTimeKind.Utc), opens);
    }

    [Fact]
    public void NextLocalMidnight_AfterTheClocksWentForward_UsesDaylightTime()
    {
        // New York entered daylight time on 8 March 2026 at 02:00. A finish at 08:00 EDT that day is followed by
        // midnight on 9 March in daylight time (UTC-4): 04:00 UTC.
        DateTime opens = JourneyClock.NextLocalMidnightUtc(
            new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc), JourneyClock.Zone("America/New_York"));

        Assert.Equal(new DateTime(2026, 3, 9, 4, 0, 0, DateTimeKind.Utc), opens);
    }

    [Fact]
    public void NextLocalMidnight_WhenMidnightItselfIsSkipped_OpensAtTheFirstMinuteThatExists()
    {
        // Santiago's clocks jump from 00:00 to 01:00 on 6 September 2026, so that midnight never happens. The Lesson
        // opens at 01:00 (UTC-3), which is 04:00 UTC, rather than throwing or waiting another day.
        DateTime opens = JourneyClock.NextLocalMidnightUtc(
            new DateTime(2026, 9, 5, 15, 0, 0, DateTimeKind.Utc), JourneyClock.Zone("America/Santiago"));

        Assert.Equal(new DateTime(2026, 9, 6, 4, 0, 0, DateTimeKind.Utc), opens);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("Not a zone at all, just a very long string that no platform could possibly know about")]
    public void ResolveZoneId_UnknownOrMissing_IsUtc(string? sent)
    {
        Assert.Equal("UTC", JourneyClock.ResolveZoneId(sent));
    }

    [Theory]
    [InlineData("Asia/Ho_Chi_Minh")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    public void ResolveZoneId_KnownIanaZone_IsKeptAsSent(string sent)
    {
        Assert.Equal(sent, JourneyClock.ResolveZoneId($" {sent} "));
    }

    [Fact]
    public void Zone_UnknownStoredId_IsUtc()
    {
        Assert.Equal(TimeZoneInfo.Utc, JourneyClock.Zone("Mars/Olympus_Mons"));
    }
}
