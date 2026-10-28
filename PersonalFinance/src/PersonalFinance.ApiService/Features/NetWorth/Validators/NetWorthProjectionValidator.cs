using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.NetWorth.Validators;

/// <summary>
/// Validates net worth projection request parameters. A failure carries its <see cref="ApiErrorKeys.NetWorth"/> key
/// (ADR 0017). The amounts are checked against <see cref="NetWorthLimits"/> scaled to the request's currency
/// (<see cref="MoneyBounds"/>, #444), and their failures carry the bounds as money arguments; the age and rate keys
/// are the Web form's own messages, so those bounds are written into the text.
/// </summary>
public static class NetWorthProjectionValidator
{
    /// <summary>
    /// Validates the net worth projection request parameters.
    /// Returns the first broken rule as an API error entry, or null if valid.
    /// </summary>
    public static ApiErrorEntryDto? Validate(NetWorthProjectionRequestDto request)
    {
        if (request.CurrencyCode is not null && !SupportedCurrencies.IsSupported(request.CurrencyCode))
        {
            return Failure(ApiErrorKeys.NetWorth.CurrencyUnsupported, nameof(request.CurrencyCode),
                CurrencyCodeRules.UnsupportedMessage);
        }

        string currency = SupportedCurrencies.CodeOrDefault(request.CurrencyCode);
        return AmountFailure(request.StartingBalance, NetWorthLimits.StartingBalanceMax, currency,
                   ApiErrorKeys.NetWorth.StartingBalanceRange, nameof(request.StartingBalance), "Starting balance")
               ?? AmountFailure(request.AnnualSavings, NetWorthLimits.AnnualSavingsMax, currency,
                   ApiErrorKeys.NetWorth.AnnualSavingsRange, nameof(request.AnnualSavings), "Annual savings")
               ?? AmountFailure(request.AnnualIncome, NetWorthLimits.AnnualIncomeMax, currency,
                   ApiErrorKeys.NetWorth.AnnualIncomeRange, nameof(request.AnnualIncome), "Annual income")
               ?? AmountFailure(request.AnnualSpending, NetWorthLimits.AnnualSpendingMax, currency,
                   ApiErrorKeys.NetWorth.AnnualSpendingRange, nameof(request.AnnualSpending), "Annual spending")
               ?? AmountFailure(request.AggressiveSavings, NetWorthLimits.AggressiveSavingsMax, currency,
                   ApiErrorKeys.NetWorth.AggressiveSavingsRange, nameof(request.AggressiveSavings), "Aggressive savings")
               ?? AgeOrRateFailure(request);
    }

    /// <summary>
    /// Null when <paramref name="amount"/> is from 0 to <paramref name="dollarMax"/> in <paramref name="currency"/>;
    /// otherwise the failure, with <c>{min}</c> and <c>{max}</c> as money arguments in that currency.
    /// </summary>
    private static ApiErrorEntryDto? AmountFailure(decimal amount, long dollarMax, string currency, string key,
        string field, string label)
    {
        decimal max = MoneyBounds.Max(dollarMax, currency);
        if (amount >= 0 && amount <= max)
        {
            return null;
        }

        ApiErrorEntryDto failure = Failure(key, field,
            MoneyBounds.Describe(label + " must be between {min} and {max}.", dollarMax, currency));
        failure.Args = new Dictionary<string, ApiErrorArgDto>(ErrorArgs.MoneyRange(0m, max, currency));
        return failure;
    }

    private static ApiErrorEntryDto? AgeOrRateFailure(NetWorthProjectionRequestDto request)
    {
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

        if (request.InflationPercent < 0 || request.InflationPercent > 20)
        {
            return Failure(ApiErrorKeys.NetWorth.InflationRange, nameof(request.InflationPercent),
                "Inflation must be between 0% and 20%.");
        }

        return null;
    }

    private static ApiErrorEntryDto Failure(string key, string field, string message) =>
        new() { Key = key, Message = message, Field = field };
}
