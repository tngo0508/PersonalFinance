using System.ComponentModel.DataAnnotations;

namespace PersonalFinance.Web.Models;

/// <summary>
/// View model for the Budget Planner page.
/// Represents monthly income and dollar amount allocations across 10 spending categories.
/// </summary>
public class BudgetViewModel
{
    /// <summary>
    /// Total monthly income before allocations.
    /// </summary>
    [Display(Name = "Monthly Income")]
    [Range(0, 10_000_000, ErrorMessage = "Monthly income must be between 0 and 10 million.")]
    public decimal MonthlyIncome { get; set; }

    /// <summary>
    /// Dollar amount allocated to Housing (rent, mortgage, property tax).
    /// </summary>
    [Display(Name = "Housing")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal Housing { get; set; }

    /// <summary>
    /// Dollar amount allocated to Food (groceries and dining).
    /// </summary>
    [Display(Name = "Food")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal Food { get; set; }

    /// <summary>
    /// Dollar amount allocated to Transportation (fuel, transit, vehicle costs).
    /// </summary>
    [Display(Name = "Transportation")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal Transportation { get; set; }

    /// <summary>
    /// Dollar amount allocated to Utilities (electricity, water, internet, phone).
    /// </summary>
    [Display(Name = "Utilities")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal Utilities { get; set; }

    /// <summary>
    /// Dollar amount allocated to Insurance (health, life, property, auto).
    /// </summary>
    [Display(Name = "Insurance")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal Insurance { get; set; }

    /// <summary>
    /// Dollar amount allocated to Healthcare (medical expenses, prescriptions).
    /// </summary>
    [Display(Name = "Healthcare")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal Healthcare { get; set; }

    /// <summary>
    /// Dollar amount allocated to Savings (emergency fund, investments).
    /// </summary>
    [Display(Name = "Savings")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal Savings { get; set; }

    /// <summary>
    /// Dollar amount allocated to Debt Repayment (loans, credit cards).
    /// </summary>
    [Display(Name = "Debt Repayment")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal DebtRepayment { get; set; }

    /// <summary>
    /// Dollar amount allocated to Entertainment (hobbies, subscriptions, leisure).
    /// </summary>
    [Display(Name = "Entertainment")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal Entertainment { get; set; }

    /// <summary>
    /// Dollar amount allocated to Miscellaneous (other expenses).
    /// </summary>
    [Display(Name = "Miscellaneous")]
    [Range(0, 10_000_000, ErrorMessage = "Amount must be between 0 and 10 million.")]
    public decimal Miscellaneous { get; set; }

    /// <summary>
    /// The budget period label (e.g., "current", "2025-06").
    /// </summary>
    public string? Period { get; set; }

    /// <summary>
    /// Total of all category dollar amounts.
    /// </summary>
    public decimal TotalAllocated
    {
        get
        {
            return Housing + Food + Transportation + Utilities + Insurance
                   + Healthcare + Savings + DebtRepayment + Entertainment + Miscellaneous;
        }
    }

    /// <summary>
    /// Remaining dollar amount that has not been allocated.
    /// </summary>
    public decimal RemainingAmount => MonthlyIncome - TotalAllocated;

    /// <summary>
    /// Percentage of monthly income represented by a dollar amount; 0 when income is 0.
    /// </summary>
    public decimal PercentFor(decimal amount)
    {
        if (MonthlyIncome <= 0) return 0m;
        return amount * 100m / MonthlyIncome;
    }

    /// <summary>
    /// Remaining percentage that has not been allocated.
    /// </summary>
    public decimal RemainingPercent => PercentFor(RemainingAmount);

    /// <summary>
    /// User-facing success message after saving.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// User-facing error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
