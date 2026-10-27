using System.Diagnostics.CodeAnalysis;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.Features.NetWorth.Validators;

/// <summary>
/// Validates net worth projection request parameters. A failure carries its <see cref="ApiErrorKeys.NetWorth"/> key
/// (ADR 0017); the range keys are the Web form's own messages, so the bounds are written into the text.
/// </summary>
public static class NetWorthProjectionValidator
{
    /// <summary>
    /// Validates the net worth projection request parameters.
    /// Returns the first broken rule as an API error entry, or null if valid.
    /// </summary>
    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Over the ADR 0007 complexity ceiling before it was set; follow-up: net worth projection rule table.")]
    public static ApiErrorEntryDto? Validate(NetWorthProjectionRequestDto request)
    {
        if (request.StartingBalance < 0 || request.StartingBalance > 1_000_000_000m)
        {
            return Failure(ApiErrorKeys.NetWorth.StartingBalanceRange, nameof(request.StartingBalance),
                "Starting balance must be between 0 and 1 billion.");
        }

        if (request.AnnualSavings < 0 || request.AnnualSavings > 10_000_000m)
        {
            return Failure(ApiErrorKeys.NetWorth.AnnualSavingsRange, nameof(request.AnnualSavings),
                "Annual savings must be between 0 and 10 million.");
        }

        if (request.AnnualIncome < 0 || request.AnnualIncome > 100_000_000m)
        {
            return Failure(ApiErrorKeys.NetWorth.AnnualIncomeRange, nameof(request.AnnualIncome),
                "Annual income must be between 0 and 100 million.");
        }

        if (request.AnnualSpending < 0 || request.AnnualSpending > 10_000_000m)
        {
            return Failure(ApiErrorKeys.NetWorth.AnnualSpendingRange, nameof(request.AnnualSpending),
                "Annual spending must be between 0 and 10 million.");
        }

        if (request.AggressiveSavings < 0 || request.AggressiveSavings > 10_000_000m)
        {
            return Failure(ApiErrorKeys.NetWorth.AggressiveSavingsRange, nameof(request.AggressiveSavings),
                "Aggressive savings must be between 0 and 10 million.");
        }

        if (request.CurrentAge < 18 || request.CurrentAge > 100)
        {
            return Failure(ApiErrorKeys.NetWorth.CurrentAgeRange, nameof(request.CurrentAge),
                "Current age must be between 18 and 100.");
        }

        if (request.RetirementAge < 18 || request.RetirementAge > 120)
        {
            return Failure(ApiErrorKeys.NetWorth.RetirementAgeRange, nameof(request.RetirementAge),
                "Retirement age must be between 18 and 120.");
        }

        if (request.RetirementAge <= request.CurrentAge)
        {
            return Failure(ApiErrorKeys.NetWorth.RetirementAfterCurrentAge, nameof(request.RetirementAge),
                "Retirement age must be greater than current age.");
        }

        if (request.BaseRoi < 0 || request.BaseRoi > 50)
        {
            return Failure(ApiErrorKeys.NetWorth.BaseRoiRange, nameof(request.BaseRoi),
                "Base ROI must be between 0% and 50%.");
        }

        if (request.ConservativeRoi < 0 || request.ConservativeRoi > 50)
        {
            return Failure(ApiErrorKeys.NetWorth.ConservativeRoiRange, nameof(request.ConservativeRoi),
                "Conservative ROI must be between 0% and 50%.");
        }

        if (request.AggressiveRoi < 0 || request.AggressiveRoi > 50)
        {
            return Failure(ApiErrorKeys.NetWorth.AggressiveRoiRange, nameof(request.AggressiveRoi),
                "Aggressive ROI must be between 0% and 50%.");
        }

        return null;
    }

    private static ApiErrorEntryDto Failure(string key, string field, string message) =>
        new() { Key = key, Message = message, Field = field };
}
