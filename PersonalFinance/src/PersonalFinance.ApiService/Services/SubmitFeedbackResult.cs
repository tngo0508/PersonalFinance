namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Outcome of sending Feedback: either it was stored, or the request was rejected with messages safe to show.
/// </summary>
public sealed class SubmitFeedbackResult
{
    /// <summary>
    /// The message shown when an Account has sent too much Feedback too quickly.
    /// </summary>
    public const string RateLimitedMessage =
        "You've sent a lot of feedback recently. Please try again later.";

    public bool Succeeded { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    /// <summary>
    /// True when the Feedback was rejected because the Account hit the submission limit.
    /// </summary>
    public bool RateLimited { get; private init; }

    public static SubmitFeedbackResult Success() => new() { Succeeded = true };

    public static SubmitFeedbackResult Invalid(IReadOnlyList<string> errors) => new() { Errors = errors };

    public static SubmitFeedbackResult TooManyRequests() =>
        new() { RateLimited = true, Errors = [RateLimitedMessage] };
}
