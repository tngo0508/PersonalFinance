using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Data.Entities;

/// <summary>
///     Database persistence entity representing a parsed monthly budget report snapshot from a Google Drive spreadsheet.
///     Stores the summary data and category breakdowns for analysis without re-reading from Google Drive.
/// </summary>
public class SpreadsheetBudgetSnapshot : AuditableEntity
{
    /// <summary>
    /// ISO 4217 code, one of <see cref="SupportedCurrencies"/> (ADR 0016). Every amount of this snapshot is in this
    /// currency, inherited from its connection. Rows saved before amounts carried a currency are <c>USD</c>.
    /// </summary>
    public string CurrencyCode { get; set; } = SupportedCurrencies.DefaultCode;

    /// <summary>
    ///     Foreign key to the Google Drive connection this snapshot was parsed from.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    ///     Human-readable name of the source spreadsheet file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     Google Drive file ID of the source spreadsheet.
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    ///     Month number (1-12) of the budget data.
    /// </summary>
    public int MonthNumber { get; set; }

    /// <summary>
    ///     Human-readable month name (e.g., "January").
    /// </summary>
    public string MonthName { get; set; } = string.Empty;

    /// <summary>
    ///     Year of the budget data.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    ///     Budgeted income for the month.
    /// </summary>
    public decimal BudgetedIncome { get; set; }

    /// <summary>
    ///     Actual income for the month.
    /// </summary>
    public decimal ActualIncome { get; set; }

    /// <summary>
    ///     Budgeted expenses for the month.
    /// </summary>
    public decimal BudgetedExpenses { get; set; }

    /// <summary>
    ///     Actual expenses for the month.
    /// </summary>
    public decimal ActualExpenses { get; set; }

    /// <summary>
    ///     Starting balance at the beginning of the month.
    /// </summary>
    public decimal? StartingBalance { get; set; }

    /// <summary>
    ///     Ending balance at the end of the month.
    /// </summary>
    public decimal? EndingBalance { get; set; }

    /// <summary>
    ///     JSON-serialized category breakdowns for the month.
    ///     Each entry contains category name, budgeted amount, and actual amount.
    /// </summary>
    public string CategoryBreakdownJson { get; set; } = "[]";

    /// <summary>
    ///     Timestamp when this snapshot was created or last updated.
    /// </summary>
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Navigation property to the source Google Drive connection.
    /// </summary>
    public GoogleDriveConnection? Connection { get; set; }

    /// <summary>
    ///     Navigation property to the transactions stored for this snapshot.
    /// </summary>
    public ICollection<SpreadsheetTransactionRecord> Transactions { get; set; } =
        new List<SpreadsheetTransactionRecord>();
}
