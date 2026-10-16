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
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestTimestamps = new();
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
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_requestTimestamps.TryGetValue(userId, out var queue))
            {
                queue = new Queue<DateTime>();
                _requestTimestamps[userId] = queue;
            }

            // Remove expired timestamps outside the current window
            while (queue.Count > 0 && now - queue.Peek() > _window) queue.Dequeue();

            // Check if the user has exceeded the rate limit
            if (queue.Count >= _maxRequests) return false;

            // Record this request
            queue.Enqueue(now);
            return true;
        }
    }
}