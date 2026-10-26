namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// The limits a Contribution must stay within, shared by the Feature's validators and its service.
/// </summary>
internal static class ContributionRules
{
    /// <summary>The largest amount a <c>decimal(18,2)</c> column holds.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>The longest note a Contribution keeps.</summary>
    public const int MaxNoteLength = 200;

    /// <summary>No Contribution is dated before this.</summary>
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);

    /// <summary>
    /// Today's date. Accounts have no time zone yet, so it is the UTC date (the spec's fallback).
    /// </summary>
    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
