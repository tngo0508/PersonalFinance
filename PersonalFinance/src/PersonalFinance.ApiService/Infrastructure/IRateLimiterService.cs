
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
    ///     Gives back the slot of the user's newest allowed request, for a request that failed after
    ///     <see cref="TryAllow" /> reserved it, so the failure does not count toward the limit.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    void Release(string userId);
}
