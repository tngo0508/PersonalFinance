namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// A section (Expenses or Income) of the monthly budget sheet containing rows and calculated totals.
/// </summary>
public class SheetSectionDto
{
    /// <summary>Category rows in display order.</summary>
    public List<SheetRowDto> Rows { get; set; } = new();

    /// <summary>Sum of planned amounts across all rows in this section.</summary>
    public decimal TotalPlanned { get; set; }

    /// <summary>Sum of actual amounts across all rows in this section.</summary>
    public decimal TotalActual { get; set; }

    /// <summary>
    /// Calculated difference.
    /// For Expenses: TotalPlanned - TotalActual (positive = under budget).
    /// For Income: TotalActual - TotalPlanned (positive = earned more than planned).
    /// </summary>
    public decimal TotalDiff { get; set; }
}