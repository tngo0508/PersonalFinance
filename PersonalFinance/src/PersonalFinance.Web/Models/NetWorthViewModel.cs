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
    public decimal StartingBalance { get; set; } = 195033.14m;

    /// <summary>
    /// User-configurable annual savings amount.
    /// </summary>
    public decimal AnnualSavings { get; set; } = 32000m;

    /// <summary>
    /// User-configurable annual spending.
    /// </summary>
    public decimal AnnualSpending { get; set; } = 50000m;

    /// <summary>
    /// User-configurable annual income.
    /// </summary>
    public decimal AnnualIncome { get; set; } = 108409.60m;

    /// <summary>
    /// User-configurable current age.
    /// </summary>
    public int CurrentAge { get; set; } = 33;

    /// <summary>
    /// Target retirement age.
    /// </summary>
    public int RetirementAge { get; set; } = 65;

    /// <summary>
    /// Base scenario ROI percentage.
    /// </summary>
    public decimal BaseRoi { get; set; } = 7.0m;

    /// <summary>
    /// Conservative scenario ROI percentage.
    /// </summary>
    public decimal ConservativeRoi { get; set; } = 6.0m;

    /// <summary>
    /// Aggressive scenario ROI percentage.
    /// </summary>
    public decimal AggressiveRoi { get; set; } = 10.0m;

    /// <summary>
    /// Aggressive scenario annual savings (higher savings rate).
    /// </summary>
    public decimal AggressiveSavings { get; set; } = 45000m;

    /// <summary>
    /// Computed projection data for all scenarios.
    /// </summary>
    public NetWorthProjectionDto? Projection { get; set; }

    /// <summary>
    /// Selected scenario for the detail table view.
    /// </summary>
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
