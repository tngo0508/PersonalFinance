namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Data Transfer Object representing the transaction log read from a spreadsheet's Transactions sheet.
/// </summary>
public class SpreadsheetTransactionsDto
{
    public string FileName { get; set; } = string.Empty;
    public string? FileId { get; set; }
    public string? SheetName { get; set; }
    public string DataSource { get; set; } = "Google Drive Spreadsheet (Actual Data)";
    public bool IsLiveSpreadsheetData { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public List<SpreadsheetTransactionDto> Transactions { get; set; } = new();
    public decimal TotalExpenses => Transactions.Where(t => t.Type == SpreadsheetTransactionDto.ExpenseType).Sum(t => t.Amount);
    public decimal TotalIncome => Transactions.Where(t => t.Type == SpreadsheetTransactionDto.IncomeType).Sum(t => t.Amount);
    public DateTime? FirstDate => Transactions.Where(t => t.Date.HasValue).Min(t => t.Date);
    public DateTime? LastDate => Transactions.Where(t => t.Date.HasValue).Max(t => t.Date);
}

/// <summary>
/// A single transaction row from a spreadsheet's Transactions sheet.
/// </summary>
public class SpreadsheetTransactionDto
{
    public const string ExpenseType = "Expense";
    public const string IncomeType = "Income";

    public DateTime? Date { get; set; }

    /// <summary>
    /// Date exactly as written in the sheet, used when it could not be parsed into <see cref="Date"/>.
    /// </summary>
    public string DateText { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// "Expense" or "Income", taken from the section header above the table (defaults to Expense).
    /// </summary>
    public string Type { get; set; } = ExpenseType;
}
