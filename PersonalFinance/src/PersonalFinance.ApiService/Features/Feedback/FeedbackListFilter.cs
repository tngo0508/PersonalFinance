using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.Feedback;

/// <summary>
/// The query of the Admin Feedback inbox (<c>GET /api/feedback</c>), so its validator has a request to check.
/// </summary>
/// <param name="Status">Only Feedback with this Feedback Status; <c>null</c> hides Archived Feedback.</param>
public sealed record FeedbackListFilter(FeedbackStatus? Status);
