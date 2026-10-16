namespace PersonalFinance.Data.Entities;

/// <summary>
///     Database persistence entity representing an individual transaction record parsed from a Google Drive spreadsheet.
///     Stores transaction details for analysis without re-reading from Google Drive.
/// </summary>
public class SpreadsheetTransactionRecord : AuditableEntity
{
    /// <summary>
    ///     Foreign key to the budget snapshot this transaction belongs to.
    /// </summary>
    public int SnapshotId { get; set; }

    /// <summary>
    ///     Foreign key to the Google Drive connection this transaction was parsed from.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    ///     Date of the transaction.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    ///     Amount of the transaction (always positive).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    ///     Description or memo of the transaction.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Category of the transaction (e.g., "Food", "Transportation").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    ///     Type of transaction: "Income" or "Expense".
    /// </summary>
    public string Type { get; set; } = "Expense";

    /// <summary>
    ///     Navigation property to the source budget snapshot.
    /// </summary>
    public SpreadsheetBudgetSnapshot? Snapshot { get; set; }

    /// <summary>
    ///     Navigation property to the source Google Drive connection.
    /// </summary>
    public GoogleDriveConnection? Connection { get; set; }
}