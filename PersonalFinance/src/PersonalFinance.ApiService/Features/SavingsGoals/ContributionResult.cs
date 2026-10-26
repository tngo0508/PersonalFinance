namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// The result of adding, editing or deleting a Contribution, or of a balance correction.
/// </summary>
/// <param name="Outcome">Whether it was saved, and why not.</param>
/// <param name="ContributionId">The Contribution saved; null when nothing was recorded.</param>
public sealed record ContributionResult(ContributionOutcome Outcome, int? ContributionId = null);
