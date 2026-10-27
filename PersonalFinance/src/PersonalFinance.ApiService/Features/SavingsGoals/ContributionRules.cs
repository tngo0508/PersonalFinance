using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// The limits a goal and its Contributions must stay within, shared by the Feature's validators and its service, and
/// the ready-made API error argument that names the largest amount (ADR 0017).
/// </summary>
internal static class ContributionRules
{
    /// <summary>The largest amount a <c>decimal(18,2)</c> column holds.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>The longest note a Contribution keeps.</summary>
    public const int MaxNoteLength = 200;

    /// <summary>The longest goal name.</summary>
    public const int MaxNameLength = 200;

    /// <summary>The longest goal description.</summary>
    public const int MaxDescriptionLength = 1000;

    /// <summary>No Contribution is dated before this.</summary>
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);

    /// <summary>The <c>max</c> amount argument of every "can be at most" message.</summary>
    public static readonly IReadOnlyDictionary<string, ApiErrorArgDto> MaxAmountArgs =
        ErrorArgs.Money("max", MaxAmount, SupportedCurrencies.Default.Code);

    /// <summary>
    /// Today's date. Accounts have no time zone yet, so it is the UTC date (the spec's fallback).
    /// </summary>
    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
