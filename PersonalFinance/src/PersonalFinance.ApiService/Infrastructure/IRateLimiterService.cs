
namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
///     Service for rate limiting API requests per user.
/// </summary>
public interface IRateLimiterService
{
    /// <summary>
    ///     Attempts to allow a request for the given user ID.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>True if the request is allowed within the rate limit; otherwise, false.</returns>
    bool TryAllow(string userId);

    /// <summary>
    ///     Like <see cref="TryAllow" />, but returns the reserved slot so a request that fails afterwards can give it
    ///     back with <see cref="Release" />.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="slot">The reserved slot; meaningful only when the method returns true.</param>
    /// <returns>True if the request is allowed within the rate limit; otherwise, false.</returns>
    bool TryReserve(string userId, out DateTime slot);

    /// <summary>
    ///     Gives back a slot taken by <see cref="TryReserve" />, so a request that failed does not count toward the
    ///     limit.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="slot">The slot <see cref="TryReserve" /> returned.</param>
    void Release(string userId, DateTime slot);
}
