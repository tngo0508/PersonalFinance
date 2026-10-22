namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Outcome of sending Feedback: either it was stored, or the request was rejected with messages safe to show.
/// </summary>
public sealed class SubmitFeedbackResult
{
    public bool Succeeded { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static SubmitFeedbackResult Success() => new() { Succeeded = true };

    public static SubmitFeedbackResult Invalid(IReadOnlyList<string> errors) => new() { Errors = errors };
}
