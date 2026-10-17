using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.Web.Models;

/// <summary>
/// View model for the Budget Tracker page.
/// </summary>
public class BudgetTrackerViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public MonthlyBudgetSheetDto? Sheet { get; set; }
    public List<BudgetCategoryDto> Categories { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? StatusMessage { get; set; }
}