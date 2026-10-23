using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

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
}
