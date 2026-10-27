using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Web.Models.Budget;

/// <summary>
/// View model for the Budget Planner page.
/// Represents monthly income and amount allocations across 10 spending categories, all in one currency.
/// </summary>
public class BudgetViewModel
{
    /// <summary>
    /// The ISO 4217 currency of the income and every allocation (ADR 0016): the saved budget's, or the Reporting
    /// Currency for a new one.
    /// </summary>
    [Display(Name = "BudgetPlanner.Field.Currency")]
    public string CurrencyCode { get; set; } = SupportedCurrencies.DefaultCode;

    /// <summary>
    /// Total monthly income before allocations.
    /// </summary>
    [Display(Name = "BudgetPlanner.Field.MonthlyIncome")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.IncomeRange")]
    public decimal MonthlyIncome { get; set; }

    /// <summary>
    /// Dollar amount allocated to Housing (rent, mortgage, property tax).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.Housing")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
    public decimal Housing { get; set; }

    /// <summary>
    /// Dollar amount allocated to Food (groceries and dining).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.Food")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
    public decimal Food { get; set; }

    /// <summary>
    /// Dollar amount allocated to Transportation (fuel, transit, vehicle costs).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.Transportation")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
    public decimal Transportation { get; set; }

    /// <summary>
    /// Dollar amount allocated to Utilities (electricity, water, internet, phone).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.Utilities")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
    public decimal Utilities { get; set; }

    /// <summary>
    /// Dollar amount allocated to Insurance (health, life, property, auto).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.Insurance")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
    public decimal Insurance { get; set; }

    /// <summary>
    /// Dollar amount allocated to Healthcare (medical expenses, prescriptions).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.Healthcare")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
    public decimal Healthcare { get; set; }

    /// <summary>
    /// Dollar amount allocated to Savings (emergency fund, investments).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.Savings")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
    public decimal Savings { get; set; }

    /// <summary>
    /// Dollar amount allocated to Debt Repayment (loans, credit cards).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.DebtRepayment")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
    public decimal DebtRepayment { get; set; }

    /// <summary>
    /// Dollar amount allocated to Entertainment (hobbies, subscriptions, leisure).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.Entertainment")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
    public decimal Entertainment { get; set; }

    /// <summary>
    /// Dollar amount allocated to Miscellaneous (other expenses).
    /// </summary>
    [Display(Name = "BudgetPlanner.Category.Miscellaneous")]
    [Range(0, 10_000_000, ErrorMessage = "BudgetPlanner.Validation.AmountRange")]
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
