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
    [Display(Name = "NetWorth.Field.StartingBalance")]
    [Range(0, 1_000_000_000, ErrorMessage = "NetWorth.Validation.StartingBalanceRange")]
    public decimal StartingBalance { get; set; } = NetWorthDefaults.StartingBalance;

    /// <summary>
    /// The ISO 4217 currency of the amounts entered (ADR 0016), from the page's selector; null (an old form) reads as
    /// the saved currency, or US dollars before anything is saved. The ApiService converts the projection into the
    /// Reporting Currency.
    /// </summary>
    [Display(Name = "NetWorth.Field.Currency")]
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// User-configurable annual savings amount.
    /// </summary>
    [Display(Name = "NetWorth.Field.AnnualSavings")]
    [Range(0, 10_000_000, ErrorMessage = "NetWorth.Validation.AnnualSavingsRange")]
    public decimal AnnualSavings { get; set; } = NetWorthDefaults.AnnualSavings;

    /// <summary>
    /// User-configurable annual spending.
    /// </summary>
    [Display(Name = "NetWorth.Field.AnnualSpending")]
    [Range(0, 10_000_000, ErrorMessage = "NetWorth.Validation.AnnualSpendingRange")]
    public decimal AnnualSpending { get; set; } = NetWorthDefaults.AnnualSpending;

    /// <summary>
    /// User-configurable annual income.
    /// </summary>
    [Display(Name = "NetWorth.Field.AnnualIncome")]
    [Range(0, 100_000_000, ErrorMessage = "NetWorth.Validation.AnnualIncomeRange")]
    public decimal AnnualIncome { get; set; } = NetWorthDefaults.AnnualIncome;

    /// <summary>
    /// User-configurable current age.
    /// </summary>
    [Display(Name = "NetWorth.Field.CurrentAge")]
    [Range(18, 100, ErrorMessage = "NetWorth.Validation.CurrentAgeRange")]
    public int CurrentAge { get; set; } = NetWorthDefaults.CurrentAge;

    /// <summary>
    /// Target retirement age.
    /// </summary>
    [Display(Name = "NetWorth.Field.RetirementAge")]
    [Range(18, 120, ErrorMessage = "NetWorth.Validation.RetirementAgeRange")]
    public int RetirementAge { get; set; } = NetWorthDefaults.RetirementAge;

    /// <summary>
    /// Base scenario ROI percentage.
    /// </summary>
    [Display(Name = "NetWorth.Field.BaseRoi")]
    [Range(0, 50, ErrorMessage = "NetWorth.Validation.BaseRoiRange")]
    public decimal BaseRoi { get; set; } = NetWorthDefaults.BaseRoi;

    /// <summary>
    /// Conservative scenario ROI percentage.
    /// </summary>
    [Display(Name = "NetWorth.Field.ConservativeRoi")]
    [Range(0, 50, ErrorMessage = "NetWorth.Validation.ConservativeRoiRange")]
    public decimal ConservativeRoi { get; set; } = NetWorthDefaults.ConservativeRoi;

    /// <summary>
    /// Aggressive scenario ROI percentage.
    /// </summary>
    [Display(Name = "NetWorth.Field.AggressiveRoi")]
    [Range(0, 50, ErrorMessage = "NetWorth.Validation.AggressiveRoiRange")]
    public decimal AggressiveRoi { get; set; } = NetWorthDefaults.AggressiveRoi;

    /// <summary>
    /// Aggressive scenario annual savings (higher savings rate).
    /// </summary>
    [Display(Name = "NetWorth.Field.AggressiveSavings")]
    [Range(0, 10_000_000, ErrorMessage = "NetWorth.Validation.AggressiveSavingsRange")]
    public decimal AggressiveSavings { get; set; } = NetWorthDefaults.AggressiveSavings;

    /// <summary>
    /// Computed projection data for all scenarios.
    /// </summary>
    public NetWorthProjectionDto? Projection { get; set; }

    /// <summary>
    /// Selected scenario for the detail table view.
    /// </summary>
    [RegularExpression("^(Base|Conservative|Aggressive)$", ErrorMessage = "NetWorth.Validation.ScenarioInvalid")]
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
            CurrencyCode = CurrencyCode,
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
        CurrencyCode = saved.CurrencyCode;
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
