namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Data Transfer Object representing a comprehensive monthly budget report generated for a spreadsheet.
/// </summary>
public class MonthlyBudgetReportDto
{
    public string FileName { get; set; } = string.Empty;
    public string? FileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; } = DateTime.UtcNow.Year;
    public decimal TotalAnnualIncome { get; set; }
    public decimal TotalAnnualExpenses { get; set; }
    public decimal TotalAnnualBudgetedIncome => Months.Sum(m => m.BudgetedIncome);
    public decimal TotalAnnualBudgetedExpenses => Months.Sum(m => m.BudgetedExpenses);
    public decimal TotalAnnualSavings => TotalAnnualIncome - TotalAnnualExpenses;
    public decimal TotalAnnualBudgetedSavings => TotalAnnualBudgetedIncome - TotalAnnualBudgetedExpenses;
    public double AverageSavingsRate => TotalAnnualIncome > 0 ? (double)(TotalAnnualSavings / TotalAnnualIncome) * 100 : 0;
    public decimal? StartingBalance => Months.FirstOrDefault(m => m.StartingBalance.HasValue)?.StartingBalance;
    public decimal? EndingBalance => Months.LastOrDefault(m => m.EndingBalance.HasValue)?.EndingBalance;
    public string DataSource { get; set; } = "Google Drive Spreadsheet (Actual Data)";
    public bool IsLiveSpreadsheetData { get; set; } = true;
    public int ParsedRowCount { get; set; }
    public bool IsSingleMonth => Months.Count == 1;
    public string? SingleMonthName => Months.Count == 1 ? Months[0].MonthName : null;
    public List<MonthlyBudgetMonthSummaryDto> Months { get; set; } = new();
}

/// <summary>
/// Summary metrics and category details for an individual month.
/// </summary>
public class MonthlyBudgetMonthSummaryDto
{
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal BudgetedIncome { get; set; }
    public decimal ActualIncome { get; set; }
    public decimal BudgetedExpenses { get; set; }
    public decimal ActualExpenses { get; set; }
    public decimal? StartingBalance { get; set; }
    public decimal? EndingBalance { get; set; }
    public decimal NetSavings => ActualIncome - ActualExpenses;
    public decimal BudgetedNetSavings => BudgetedIncome - BudgetedExpenses;
    public double SavingsRate => ActualIncome > 0 ? (double)(NetSavings / ActualIncome) * 100 : 0;
    public decimal ExpenseVariance => BudgetedExpenses - ActualExpenses;
    public string Status
    {
        get
        {
            if (BudgetedExpenses > 0)
            {
                if (ActualExpenses <= BudgetedExpenses)
                {
                    return "Under Budget";
                }
                if (ActualExpenses <= BudgetedExpenses * 1.05m)
                {
                    return "On Track";
                }
                return "Over Budget";
            }
            return NetSavings >= 0 ? "Under Budget" : "Over Budget";
        }
    }
    public List<MonthlyBudgetCategoryItemDto> Categories { get; set; } = new();
}

/// <summary>
/// Detailed breakdown for a single budget category within a month.
/// </summary>
public class MonthlyBudgetCategoryItemDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance => BudgetedAmount - ActualAmount;
    public double PercentageUsed => BudgetedAmount > 0 ? (double)(ActualAmount / BudgetedAmount) * 100 : 0;
    public string ColorHex { get; set; } = "#3b82f6";

    public string Status
    {
        get
        {
            if (ActualAmount <= BudgetedAmount * 0.9m)
            {
                return "Within Budget";
            }
            if (ActualAmount <= BudgetedAmount)
            {
                return "Near Limit";
            }
            return "Over Budget";
        }
    }
}
