using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Web.Models.Networth;

/// <summary>
/// View model for the Net Worth projection page.
/// </summary>
public class NetWorthViewModel
{
    /// <summary>
    /// User-configurable starting balance.
    /// </summary>
    [Display(Name = "Starting Balance")]
    [Range(0, 1_000_000_000, ErrorMessage = "Starting balance must be between 0 and 1 billion.")]
    public decimal StartingBalance { get; set; } = NetWorthDefaults.StartingBalance;

    /// <summary>
    /// User-configurable annual savings amount.
    /// </summary>
    [Display(Name = "Annual Savings")]
    [Range(0, 10_000_000, ErrorMessage = "Annual savings must be between 0 and 10 million.")]
    public decimal AnnualSavings { get; set; } = NetWorthDefaults.AnnualSavings;

    /// <summary>
    /// User-configurable annual spending.
    /// </summary>
    [Display(Name = "Annual Spending")]
    [Range(0, 10_000_000, ErrorMessage = "Annual spending must be between 0 and 10 million.")]
    public decimal AnnualSpending { get; set; } = NetWorthDefaults.AnnualSpending;

    /// <summary>
    /// User-configurable annual income.
    /// </summary>
    [Display(Name = "Annual Income")]
    [Range(0, 100_000_000, ErrorMessage = "Annual income must be between 0 and 100 million.")]
    public decimal AnnualIncome { get; set; } = NetWorthDefaults.AnnualIncome;

    /// <summary>
    /// User-configurable current age.
    /// </summary>
    [Display(Name = "Current Age")]
    [Range(18, 100, ErrorMessage = "Current age must be between 18 and 100.")]
    public int CurrentAge { get; set; } = NetWorthDefaults.CurrentAge;

    /// <summary>
    /// Target retirement age.
    /// </summary>
    [Display(Name = "Retirement Age")]
    [Range(18, 120, ErrorMessage = "Retirement age must be between 18 and 120.")]
    public int RetirementAge { get; set; } = NetWorthDefaults.RetirementAge;

    /// <summary>
    /// Base scenario ROI percentage.
    /// </summary>
    [Display(Name = "Base ROI %")]
    [Range(0, 50, ErrorMessage = "Base ROI must be between 0% and 50%.")]
    public decimal BaseRoi { get; set; } = NetWorthDefaults.BaseRoi;

    /// <summary>
    /// Conservative scenario ROI percentage.
    /// </summary>
    [Display(Name = "Conservative ROI %")]
    [Range(0, 50, ErrorMessage = "Conservative ROI must be between 0% and 50%.")]
    public decimal ConservativeRoi { get; set; } = NetWorthDefaults.ConservativeRoi;

    /// <summary>
    /// Aggressive scenario ROI percentage.
    /// </summary>
    [Display(Name = "Aggressive ROI %")]
    [Range(0, 50, ErrorMessage = "Aggressive ROI must be between 0% and 50%.")]
    public decimal AggressiveRoi { get; set; } = NetWorthDefaults.AggressiveRoi;

    /// <summary>
    /// Aggressive scenario annual savings (higher savings rate).
    /// </summary>
    [Display(Name = "Aggressive Savings")]
    [Range(0, 10_000_000, ErrorMessage = "Aggressive savings must be between 0 and 10 million.")]
    public decimal AggressiveSavings { get; set; } = NetWorthDefaults.AggressiveSavings;

    /// <summary>
    /// Computed projection data for all scenarios.
    /// </summary>
    public NetWorthProjectionDto? Projection { get; set; }

    /// <summary>
    /// Selected scenario for the detail table view.
    /// </summary>
    [RegularExpression("^(Base|Conservative|Aggressive)$", ErrorMessage = "Invalid scenario selected.")]
    public string SelectedScenario { get; set; } = "Base";

    /// <summary>
    /// User-facing success/status message.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// User-facing error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Savings Rate shown before JavaScript updates it live (see <see cref="FinanceMetrics.SavingsRatePercent"/>).
    /// </summary>
    public decimal SavingsRatePercent => FinanceMetrics.SavingsRatePercent(AnnualSavings, AnnualIncome) ?? 0m;

    /// <summary>
    /// The current parameters as a projection request for ApiService.
    /// </summary>
    public NetWorthProjectionRequestDto ToProjectionRequest()
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

    /// <summary>
    /// Replaces the defaults with the Account's saved parameters.
    /// </summary>
    public void ApplySaved(NetWorthParametersDto saved)
    {
        StartingBalance = saved.StartingBalance;
        AnnualSavings = saved.AnnualSavings;
        AnnualIncome = saved.AnnualIncome;
        AnnualSpending = saved.AnnualSpending;
        CurrentAge = saved.CurrentAge;
        RetirementAge = saved.RetirementAge;
        BaseRoi = saved.BaseRoi;
        ConservativeRoi = saved.ConservativeRoi;
        AggressiveRoi = saved.AggressiveRoi;
        AggressiveSavings = saved.AggressiveSavings;
    }
}
