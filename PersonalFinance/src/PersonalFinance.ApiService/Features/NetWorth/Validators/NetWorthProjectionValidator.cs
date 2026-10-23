using System.Diagnostics.CodeAnalysis;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.Features.NetWorth.Validators;

/// <summary>
/// Validates net worth projection request parameters.
/// </summary>
public static class NetWorthProjectionValidator
{
    /// <summary>
    /// Validates the net worth projection request parameters.
    /// Returns an error message if validation fails, null if valid.
    /// </summary>
    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Over the ADR 0007 complexity ceiling before it was set; follow-up: net worth projection rule table.")]
    public static string? Validate(NetWorthProjectionRequestDto request)
    {
        if (request.StartingBalance < 0 || request.StartingBalance > 1_000_000_000m)
        {
            return "Starting balance must be between 0 and 1 billion.";
        }

        if (request.AnnualSavings < 0 || request.AnnualSavings > 10_000_000m)
        {
            return "Annual savings must be between 0 and 10 million.";
        }

        if (request.AnnualIncome < 0 || request.AnnualIncome > 100_000_000m)
        {
            return "Annual income must be between 0 and 100 million.";
        }

        if (request.AnnualSpending < 0 || request.AnnualSpending > 10_000_000m)
        {
            return "Annual spending must be between 0 and 10 million.";
        }

        if (request.AggressiveSavings < 0 || request.AggressiveSavings > 10_000_000m)
        {
            return "Aggressive savings must be between 0 and 10 million.";
        }

        if (request.CurrentAge < 18 || request.CurrentAge > 100)
        {
            return "Current age must be between 18 and 100.";
        }

        if (request.RetirementAge < 18 || request.RetirementAge > 120)
        {
            return "Retirement age must be between 18 and 120.";
        }

        if (request.RetirementAge <= request.CurrentAge)
        {
            return "Retirement age must be greater than current age.";
        }

        if (request.BaseRoi < 0 || request.BaseRoi > 50)
        {
            return "Base ROI must be between 0% and 50%.";
        }

        if (request.ConservativeRoi < 0 || request.ConservativeRoi > 50)
        {
            return "Conservative ROI must be between 0% and 50%.";
        }

        if (request.AggressiveRoi < 0 || request.AggressiveRoi > 50)
        {
            return "Aggressive ROI must be between 0% and 50%.";
        }

        return null;
    }
}
