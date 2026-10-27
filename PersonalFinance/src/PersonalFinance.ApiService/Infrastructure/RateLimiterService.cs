using System.Collections.Concurrent;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
///     In-memory rate limiter using a sliding window algorithm.
///     Limits each user to a configurable number of requests within a time window.
///     Registered as a Singleton to share state across requests.
/// </summary>
public class RateLimiterService : IRateLimiterService
{
    private readonly object _lock = new();
    private readonly int _maxRequests;
    private readonly ConcurrentDictionary<string, LinkedList<DateTime>> _requestTimestamps = new();
    private readonly TimeSpan _window;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RateLimiterService" /> class.
    /// </summary>
    /// <param name="maxRequests">Maximum number of requests allowed within the window. Default is 10.</param>
    /// <param name="window">Time window for rate limiting. Default is 60 seconds.</param>
    public RateLimiterService(int maxRequests = 10, TimeSpan? window = null)
    {
        _maxRequests = maxRequests;
        _window = window ?? TimeSpan.FromSeconds(60);
    }

    /// <summary>
    ///     Attempts to allow a request for the given user ID.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>True if the request is allowed within the rate limit; otherwise, false.</returns>
    public bool TryAllow(string userId)
    {
        DateTime now = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_requestTimestamps.TryGetValue(userId, out LinkedList<DateTime>? timestamps))
            {
                timestamps = new LinkedList<DateTime>();
                _requestTimestamps[userId] = timestamps;
            }

            // Remove expired timestamps outside the current window
            while (timestamps.First is not null && now - timestamps.First.Value > _window) timestamps.RemoveFirst();

            // Check if the user has exceeded the rate limit
            if (timestamps.Count >= _maxRequests) return false;

            // Record this request
            timestamps.AddLast(now);
            return true;
        }
    }

    /// <summary>
    ///     Gives back the slot of the user's newest allowed request. With concurrent requests the newest slot may
    ///     belong to another request; the count is the same either way, and the older slot that stays expires sooner.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    public void Release(string userId)
    {
        lock (_lock)
        {
            if (_requestTimestamps.TryGetValue(userId, out LinkedList<DateTime>? timestamps) && timestamps.Count > 0)
                timestamps.RemoveLast();
        }
    }
}
