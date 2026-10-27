using System.Net;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Contracts;
using Keys = PersonalFinance.Shared.Constants.ResultMessageKeys.GoogleDrive;

namespace PersonalFinance.ApiService.Features.GoogleDrive;

/// <summary>
/// The error and warning messages Google Drive results carry, each keyed with its English text (#405) so the Web app
/// can show it in the reader's language.
/// </summary>
internal static class DriveResultMessages
{
    /// <summary>The English text stored on a connection whose API key Google rejected.</summary>
    public const string CredentialsInvalidText =
        "Google credentials are invalid or expired. Please re-authenticate or update your API key.";

    /// <summary>The <see cref="Data.Entities.GoogleDriveConnection.SyncStatus"/> of a connection whose API key Google rejected.</summary>
    public const string NeedsReauthStatus = "NeedsReauth";

    public static ApiErrorEntryDto Canceled => ResultMessage.Entry(Keys.Canceled, "The request was canceled.");

    public static ApiErrorEntryDto CredentialsInvalid => ResultMessage.Entry(Keys.CredentialsInvalid, CredentialsInvalidText);

    public static ApiErrorEntryDto NoApiKey => ResultMessage.Entry(Keys.NoApiKey,
        "No Google Drive API Key was provided. Showing preview files for this folder. To query live Google Drive folders, provide a Google Cloud API Key with Google Drive API enabled.");

    public static ApiErrorEntryDto NoApiKeySample => ResultMessage.Entry(Keys.NoApiKeySample,
        "No Google Drive API Key was provided. Showing preview files for this folder.");

    public static ApiErrorEntryDto Timeout => ResultMessage.Entry(Keys.Timeout,
        "The request to Google Drive API timed out. Verify your network connectivity and Google Cloud status. Showing preview demo data.");

    public static ApiErrorEntryDto FolderNotFound => ResultMessage.Entry(Keys.FolderNotFound,
        "Google Drive API returned HTTP 404 Not Found. This occurs when the folder ID is incorrect OR when folder permissions are set to 'Restricted' (Google masks private folders as 404). To resolve: open your folder in Google Drive, click 'Share', and change General access from 'Restricted' to 'Anyone with the link' (Viewer). Showing preview demo data.");

    public static ApiErrorEntryDto AccessDenied => ResultMessage.Entry(Keys.AccessDenied,
        "Google Drive API returned HTTP 403 Forbidden. This occurs when 'Google Drive API' is not enabled in your Google Cloud project, API key restrictions prevent access, or folder access is denied. To resolve: enable the Google Drive API in Google Cloud Console and ensure folder sharing is set to 'Anyone with the link'. Showing preview demo data.");

    public static ApiErrorEntryDto BadRequest => ResultMessage.Entry(Keys.BadRequest,
        "Google Drive API returned HTTP 400 Bad Request. Please verify that your API key is valid and formatted correctly. Showing preview demo data.");

    public static ApiErrorEntryDto UnexpectedResponse => ResultMessage.Entry(Keys.UnexpectedResponse,
        "Google Drive API returned an unexpected response format. Showing preview demo data.");

    public static ApiErrorEntryDto ConnectFailed => ResultMessage.Entry(Keys.ConnectFailed,
        "An error occurred while connecting to Google Drive API. Showing preview demo data.");

    public static ApiErrorEntryDto Unreachable => ResultMessage.Entry(Keys.Unreachable,
        "Unable to reach Google Drive API. Showing preview/cached data.");

    public static ApiErrorEntryDto EmptyFolder => ResultMessage.Entry(Keys.EmptyFolder,
        "No files were returned by the Google Drive API for this folder. Verify that the folder contains files, that General access is set to 'Anyone with the link' (Viewer), and that files are not restricted.");

    public static ApiErrorEntryDto NoFileSelected => ResultMessage.Entry(Keys.NoFileSelected, "No spreadsheet was selected.");

    public static ApiErrorEntryDto SpreadsheetEmpty => ResultMessage.Entry(Keys.SpreadsheetEmpty, "The spreadsheet is empty.");

    public static ApiErrorEntryDto SpreadsheetNoRows => ResultMessage.Entry(Keys.SpreadsheetNoRows, "The spreadsheet has no rows.");

    public static ApiErrorEntryDto WorkbookNoSheets => ResultMessage.Entry(Keys.WorkbookNoSheets, "The workbook has no worksheets.");

    public static ApiErrorEntryDto WorkbookSheetsEmpty => ResultMessage.Entry(Keys.WorkbookSheetsEmpty, "The workbook's sheets are empty.");

    public static ApiErrorEntryDto WorkbookUnreadable => ResultMessage.Entry(Keys.WorkbookUnreadable,
        "The spreadsheet could not be read as an Excel workbook.");

    // The sheet and column names below are what the parser looks for, so they are arguments: a translation keeps them
    // in English.
    public static ApiErrorEntryDto NoBudgetTable => ResultMessage.Entry(Keys.NoBudgetTable,
        "No monthly budget table was found. The sheet needs income and expense rows with month columns, or a Summary sheet.",
        Names(("summary", "Summary")));

    public static ApiErrorEntryDto NoTransactionsTable => ResultMessage.Entry(Keys.NoTransactionsTable,
        "No transactions table was found. Add a sheet with Date and Amount column headers.",
        Names(("date", "Date"), ("amount", "Amount")));

    public static ApiErrorEntryDto NoTransactionsSheet => ResultMessage.Entry(Keys.NoTransactionsSheet,
        "No transactions table was found in any sheet. Ensure the spreadsheet has a sheet named 'Transactions' with Date and Amount column headers.",
        Names(("sheet", "Transactions"), ("date", "Date"), ("amount", "Amount")));

    public static ApiErrorEntryDto DownloadFailed => ResultMessage.Entry(Keys.DownloadFailed,
        "The spreadsheet could not be downloaded from Google Drive. Check that the drive is still shared and your API key is valid.");

    /// <summary>
    /// The warning for an HTTP failure of the Google Drive API other than 400, 403 and 404, naming the status when
    /// there is one.
    /// </summary>
    public static ApiErrorEntryDto HttpFailure(HttpStatusCode? statusCode) => statusCode.HasValue
        ? ResultMessage.Entry(Keys.HttpStatus,
            $"Google Drive API returned HTTP {(int)statusCode.Value} ({statusCode.Value}) (Verify that your API key is valid, Google Drive API is enabled, and folder access is set to 'Anyone with the link'). Showing preview demo data.",
            ErrorArgs.Text("status", $"{(int)statusCode.Value} ({statusCode.Value})"))
        : ResultMessage.Entry(Keys.HttpError,
            "Google Drive API returned an HTTP error (Verify that your API key is valid, Google Drive API is enabled, and folder access is set to 'Anyone with the link'). Showing preview demo data.");

    /// <summary>
    /// The keyed form of a connection's stored <see cref="Data.Entities.GoogleDriveConnection.ErrorMessage"/>. The
    /// column only ever holds <see cref="CredentialsInvalidText"/>, set together with <see cref="NeedsReauthStatus"/>,
    /// so the key comes from the status and the column needs none (#405). Any other text has no key.
    /// </summary>
    public static ApiErrorEntryDto? ForStoredError(string? errorMessage, string syncStatus) =>
        errorMessage is not null && syncStatus == NeedsReauthStatus ? CredentialsInvalid : null;

    /// <summary>Text arguments that name a sheet or a column.</summary>
    private static Dictionary<string, ApiErrorArgDto> Names(params (string Name, string Value)[] names) =>
        names.ToDictionary(name => name.Name,
            name => new ApiErrorArgDto { Kind = ApiErrorArgKind.Text, Value = name.Value }, StringComparer.Ordinal);
}
