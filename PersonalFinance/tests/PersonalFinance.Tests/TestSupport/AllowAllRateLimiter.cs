using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>A rate limiter that lets every request through, for tests about something other than the limit.</summary>
public sealed class AllowAllRateLimiter : IRateLimiterService
{
    public bool TryAllow(string userId) => true;

    public bool TryReserve(string userId, out DateTime slot)
    {
        slot = default;
        return true;
    }

    public void Release(string userId, DateTime slot)
    {
    }
}
