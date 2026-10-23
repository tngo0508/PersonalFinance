using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.Feedback;

/// <summary>
/// Settings of the Feedback rate limiter, registered in <see cref="FeedbackFeature.AddFeedbackFeature"/>.
/// </summary>
internal static class FeedbackRateLimit
{
    /// <summary>
    /// DI key of the Feedback rate limiter, kept separate from the shared <see cref="IRateLimiterService"/> used by
    /// other features so their limits are unchanged.
    /// </summary>
    public const string Key = "Feedback";

    /// <summary>
    /// Most Feedback entries an Account may send within <see cref="Window"/>.
    /// </summary>
    public const int MaxSubmissionsPerWindow = 5;

    /// <summary>
    /// The sliding window for <see cref="MaxSubmissionsPerWindow"/>.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);
}
