namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Outcome of sending validated Feedback: either it was stored, or the Account hit the submission limit.
/// </summary>
public sealed class SubmitFeedbackResult
{
    public bool Succeeded { get; private init; }

    /// <summary>
    /// True when the Feedback was rejected because the Account hit the submission limit.
    /// </summary>
    public bool RateLimited { get; private init; }

    public static SubmitFeedbackResult Success() => new() { Succeeded = true };

    public static SubmitFeedbackResult TooManyRequests() => new() { RateLimited = true };
}
