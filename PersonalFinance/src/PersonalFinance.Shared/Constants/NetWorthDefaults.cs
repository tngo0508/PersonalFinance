using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.Shared.Constants;

/// <summary>
/// Net Worth parameters shown before an Account saves its own. The Net Worth page, its Reset fallback
/// (rendered into the page for <c>networth-presets.js</c>) and the projection endpoint all read these values.
/// </summary>
public static class NetWorthDefaults
{
    public const decimal StartingBalance = 195_033.14m;
    public const decimal AnnualSavings = 32_000m;
    public const decimal AnnualIncome = 108_409.60m;
    public const decimal AnnualSpending = 50_000m;
    public const int CurrentAge = 33;
    public const int RetirementAge = 65;
    public const decimal BaseRoi = 7.0m;
    public const decimal ConservativeRoi = 6.0m;
    public const decimal AggressiveRoi = 10.0m;
    public const decimal AggressiveSavings = 45_000m;

    /// <summary>A projection request filled with the default parameters.</summary>
    public static NetWorthProjectionRequestDto CreateRequest()
    {
        return new NetWorthProjectionRequestDto
        {
            StartingBalance = StartingBalance,
            AnnualSavings = AnnualSavings,
            AnnualIncome = AnnualIncome,
            AnnualSpending = AnnualSpending,
            CurrentAge = CurrentAge,
            RetirementAge = RetirementAge,
            BaseRoi = BaseRoi,
            ConservativeRoi = ConservativeRoi,
            AggressiveRoi = AggressiveRoi,
            AggressiveSavings = AggressiveSavings
        };
    }
}
