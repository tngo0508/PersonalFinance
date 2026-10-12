using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Web.Models;

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
    public decimal StartingBalance { get; set; } = 195033.14m;

    /// <summary>
    /// User-configurable annual savings amount.
    /// </summary>
    [Display(Name = "Annual Savings")]
    [Range(0, 10_000_000, ErrorMessage = "Annual savings must be between 0 and 10 million.")]
    public decimal AnnualSavings { get; set; } = 32000m;

    /// <summary>
    /// User-configurable annual spending.
    /// </summary>
    [Display(Name = "Annual Spending")]
    [Range(0, 10_000_000, ErrorMessage = "Annual spending must be between 0 and 10 million.")]
    public decimal AnnualSpending { get; set; } = 50000m;

    /// <summary>
    /// User-configurable annual income.
    /// </summary>
    [Display(Name = "Annual Income")]
    [Range(0, 100_000_000, ErrorMessage = "Annual income must be between 0 and 100 million.")]
    public decimal AnnualIncome { get; set; } = 108409.60m;

    /// <summary>
    /// User-configurable current age.
    /// </summary>
    [Display(Name = "Current Age")]
    [Range(18, 100, ErrorMessage = "Current age must be between 18 and 100.")]
    public int CurrentAge { get; set; } = 33;

    /// <summary>
    /// Target retirement age.
    /// </summary>
    [Display(Name = "Retirement Age")]
    [Range(18, 120, ErrorMessage = "Retirement age must be between 18 and 120.")]
    public int RetirementAge { get; set; } = 65;

    /// <summary>
    /// Base scenario ROI percentage.
    /// </summary>
    [Display(Name = "Base ROI %")]
    [Range(0, 50, ErrorMessage = "Base ROI must be between 0% and 50%.")]
    public decimal BaseRoi { get; set; } = 7.0m;

    /// <summary>
    /// Conservative scenario ROI percentage.
    /// </summary>
    [Display(Name = "Conservative ROI %")]
    [Range(0, 50, ErrorMessage = "Conservative ROI must be between 0% and 50%.")]
    public decimal ConservativeRoi { get; set; } = 6.0m;

    /// <summary>
    /// Aggressive scenario ROI percentage.
    /// </summary>
    [Display(Name = "Aggressive ROI %")]
    [Range(0, 50, ErrorMessage = "Aggressive ROI must be between 0% and 50%.")]
    public decimal AggressiveRoi { get; set; } = 10.0m;

    /// <summary>
    /// Aggressive scenario annual savings (higher savings rate).
    /// </summary>
    [Display(Name = "Aggressive Savings")]
    [Range(0, 10_000_000, ErrorMessage = "Aggressive savings must be between 0 and 10 million.")]
    public decimal AggressiveSavings { get; set; } = 45000m;

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
}
