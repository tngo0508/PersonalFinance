using PersonalFinance.Data.Entities;
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

    /// <summary>Copies the submitted values onto the saved parameters.</summary>
    public static void Apply(this NetWorthParameters parameters, NetWorthProjectionRequestDto values)
    {
        parameters.StartingBalance = values.StartingBalance;
        parameters.AnnualSavings = values.AnnualSavings;
        parameters.AnnualIncome = values.AnnualIncome;
        parameters.AnnualSpending = values.AnnualSpending;
        parameters.CurrentAge = values.CurrentAge;
        parameters.RetirementAge = values.RetirementAge;
        parameters.BaseRoi = values.BaseRoi;
        parameters.ConservativeRoi = values.ConservativeRoi;
        parameters.AggressiveRoi = values.AggressiveRoi;
        parameters.AggressiveSavings = values.AggressiveSavings;
    }
}
