namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Request to programmatically instantiate a Monthly Budget Google Sheet template in a user's connected Google Drive.
/// </summary>
public class CreateMonthlyBudgetTemplateRequestDto
{
    /// <summary>
    /// ID of the user requesting template creation.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the Google Drive connection where the spreadsheet will be created.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Google OAuth access token with the drive.file scope, used to create the spreadsheet on the user's behalf.
    /// Populated server-side by the Web frontend; never supplied by the browser.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Optional custom file name for the spreadsheet (e.g. "Monthly Budget - 2026-10").
    /// </summary>
    public string? CustomFileName { get; set; }

    /// <summary>
    /// Optional starting balance for the budget summary (e.g. 1000.00).
    /// </summary>
    public decimal? StartingBalance { get; set; }

    /// <summary>
    /// Target year for the budget.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Target month (1-12) for the budget.
    /// </summary>
    public int? Month { get; set; }

    /// <summary>
    /// Optional custom expense categories.
    /// </summary>
    public List<string>? CustomExpenseCategories { get; set; }

    /// <summary>
    /// Optional custom income categories.
    /// </summary>
    public List<string>? CustomIncomeCategories { get; set; }

    /// <summary>
    /// Optional initial transactions to seed in the Transactions sheet.
    /// </summary>
    public List<SpreadsheetTransactionDto>? InitialTransactions { get; set; }
}
