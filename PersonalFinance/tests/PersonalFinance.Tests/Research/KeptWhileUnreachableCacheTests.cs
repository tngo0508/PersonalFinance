using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The cache behind Company Profiles and their press releases: a value is fresh for a day, then kept for a week to
/// serve while SEC is unreachable, then gone.
/// </summary>
public class KeptWhileUnreachableCacheTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AValue_IsFreshForADay_ThenKeptForAWeek_ThenGone()
    {
        var time = new FixedTimeProvider(Now);
        using var cache = new KeptWhileUnreachableCache<CompanyProfileDto>(time);
        var profile = new CompanyProfileDto { Ticker = "SWI" };
        cache.Set("SWI", profile);

        time.Now = Now + TimeSpan.FromHours(23);
        Assert.Equal((profile, true), cache.Get("SWI"));

        time.Now = Now + TimeSpan.FromDays(1);
        Assert.Equal((profile, false), cache.Get("SWI"));

        time.Now = Now + TimeSpan.FromDays(7);
        Assert.Null(cache.Get("SWI"));
    }

    [Fact]
    public void ATickerNeverKept_IsNull()
    {
        using var cache = new KeptWhileUnreachableCache<List<CompanyPressReleaseDto>>(new FixedTimeProvider(Now));

        Assert.Null(cache.Get("SWI"));
    }
}
