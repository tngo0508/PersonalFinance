using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// The one hand-written mapping between the saved Net Worth parameters and their DTOs (ADR 0001: no AutoMapper).
/// A new parameter is added here, not in each reader.
/// </summary>
public static class NetWorthParametersMapping
{
    /// <summary>The saved parameters as input for <see cref="NetWorthProjectionService.ComputeProjection"/>.</summary>
    public static NetWorthProjectionRequestDto ToProjectionRequest(this NetWorthParameters parameters)
    {
        return new NetWorthProjectionRequestDto
        {
            CurrencyCode = parameters.CurrencyCode,
            StartingBalance = parameters.StartingBalance,
            AnnualSavings = parameters.AnnualSavings,
            AnnualIncome = parameters.AnnualIncome,
            AnnualSpending = parameters.AnnualSpending,
            CurrentAge = parameters.CurrentAge,
            RetirementAge = parameters.RetirementAge,
            BaseRoi = parameters.BaseRoi,
            ConservativeRoi = parameters.ConservativeRoi,
            AggressiveRoi = parameters.AggressiveRoi,
            AggressiveSavings = parameters.AggressiveSavings
        };
    }

    /// <summary>The saved parameters as returned to Web.</summary>
    public static NetWorthParametersDto ToDto(this NetWorthParameters parameters)
    {
        return new NetWorthParametersDto
        {
            CurrencyCode = parameters.CurrencyCode,
            StartingBalance = parameters.StartingBalance,
            AnnualSavings = parameters.AnnualSavings,
            AnnualIncome = parameters.AnnualIncome,
            AnnualSpending = parameters.AnnualSpending,
            CurrentAge = parameters.CurrentAge,
            RetirementAge = parameters.RetirementAge,
            BaseRoi = parameters.BaseRoi,
            ConservativeRoi = parameters.ConservativeRoi,
            AggressiveRoi = parameters.AggressiveRoi,
            AggressiveSavings = parameters.AggressiveSavings,
            UpdatedAtUtc = parameters.UpdatedAt ?? parameters.CreatedAt
        };
    }

    /// <summary>
    /// Copies the submitted values onto the saved parameters, in the requested currency (or the saved one) and with each
    /// amount rounded to that currency's minor units.
    /// </summary>
    public static void Apply(this NetWorthParameters parameters, NetWorthProjectionRequestDto values)
    {
        parameters.CurrencyCode = CurrencyCodeRules.Resolve(values.CurrencyCode, parameters.CurrencyCode);
        SupportedCurrency currency = SupportedCurrencies.Find(parameters.CurrencyCode) ?? SupportedCurrencies.Default;
        parameters.StartingBalance = currency.Round(values.StartingBalance);
        parameters.AnnualSavings = currency.Round(values.AnnualSavings);
        parameters.AnnualIncome = currency.Round(values.AnnualIncome);
        parameters.AnnualSpending = currency.Round(values.AnnualSpending);
        parameters.CurrentAge = values.CurrentAge;
        parameters.RetirementAge = values.RetirementAge;
        parameters.BaseRoi = values.BaseRoi;
        parameters.ConservativeRoi = values.ConservativeRoi;
        parameters.AggressiveRoi = values.AggressiveRoi;
        parameters.AggressiveSavings = currency.Round(values.AggressiveSavings);
    }
}
