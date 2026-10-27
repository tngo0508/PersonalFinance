namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The localization keys of the error and warning messages a successful (200) result carries in an
/// <see cref="PersonalFinance.Shared.Contracts.ApiErrorEntryDto"/> beside its English text (#405). One nested class per
/// Feature; each value is the key of the message in the Web app's <c>en.json</c>, named
/// <c>&lt;Feature&gt;.Result.&lt;Rule&gt;</c>. A 4xx rejection's keys are <see cref="ApiErrorKeys"/> instead.
/// </summary>
public static class ResultMessageKeys
{
    /// <summary>The keys of the Google Drive Feature's result messages.</summary>
    public static class GoogleDrive
    {
        /// <summary>The caller canceled the folder request.</summary>
        public const string Canceled = "GoogleDrive.Result.Canceled";

        /// <summary>Google rejected the connection's API key; the connection needs a new one.</summary>
        public const string CredentialsInvalid = "GoogleDrive.Result.CredentialsInvalid";

        /// <summary>No API key was given to explore a folder, so sample files are shown.</summary>
        public const string NoApiKey = "GoogleDrive.Result.NoApiKey";

        /// <summary>A saved connection has no API key, so sample files are shown.</summary>
        public const string NoApiKeySample = "GoogleDrive.Result.NoApiKeySample";

        /// <summary>The Google Drive API did not answer in time, so sample files are shown.</summary>
        public const string Timeout = "GoogleDrive.Result.Timeout";

        /// <summary>The Google Drive API answered 404, so sample files are shown.</summary>
        public const string FolderNotFound = "GoogleDrive.Result.FolderNotFound";

        /// <summary>The Google Drive API answered 403, so sample files are shown.</summary>
        public const string AccessDenied = "GoogleDrive.Result.AccessDenied";

        /// <summary>The Google Drive API answered 400, so sample files are shown.</summary>
        public const string BadRequest = "GoogleDrive.Result.BadRequest";

        /// <summary>The Google Drive API answered the HTTP <c>status</c> argument, so sample files are shown.</summary>
        public const string HttpStatus = "GoogleDrive.Result.HttpStatus";

        /// <summary>The Google Drive API failed with no HTTP status, so sample files are shown.</summary>
        public const string HttpError = "GoogleDrive.Result.HttpError";

        /// <summary>The Google Drive API's answer could not be read, so sample files are shown.</summary>
        public const string UnexpectedResponse = "GoogleDrive.Result.UnexpectedResponse";

        /// <summary>Connecting to the Google Drive API failed, so sample files are shown.</summary>
        public const string ConnectFailed = "GoogleDrive.Result.ConnectFailed";

        /// <summary>A saved connection could not reach the Google Drive API, so sample or cached files are shown.</summary>
        public const string Unreachable = "GoogleDrive.Result.Unreachable";

        /// <summary>The Google Drive API returned no files for the folder.</summary>
        public const string EmptyFolder = "GoogleDrive.Result.EmptyFolder";

        /// <summary>A spreadsheet request named no file.</summary>
        public const string NoFileSelected = "GoogleDrive.Result.NoFileSelected";

        /// <summary>The spreadsheet has no content.</summary>
        public const string SpreadsheetEmpty = "GoogleDrive.Result.SpreadsheetEmpty";

        /// <summary>The spreadsheet has no rows.</summary>
        public const string SpreadsheetNoRows = "GoogleDrive.Result.SpreadsheetNoRows";

        /// <summary>The workbook has no worksheets.</summary>
        public const string WorkbookNoSheets = "GoogleDrive.Result.WorkbookNoSheets";

        /// <summary>Every worksheet of the workbook is empty.</summary>
        public const string WorkbookSheetsEmpty = "GoogleDrive.Result.WorkbookSheetsEmpty";

        /// <summary>The file could not be read as an Excel workbook.</summary>
        public const string WorkbookUnreadable = "GoogleDrive.Result.WorkbookUnreadable";

        /// <summary>No sheet has a monthly budget table.</summary>
        public const string NoBudgetTable = "GoogleDrive.Result.NoBudgetTable";

        /// <summary>No sheet has a transactions table.</summary>
        public const string NoTransactionsTable = "GoogleDrive.Result.NoTransactionsTable";

        /// <summary>A workbook that opened has no transactions table in any sheet.</summary>
        public const string NoTransactionsSheet = "GoogleDrive.Result.NoTransactionsSheet";

        /// <summary>The spreadsheet could not be downloaded from Google Drive.</summary>
        public const string DownloadFailed = "GoogleDrive.Result.DownloadFailed";
    }

    /// <summary>The keys of the Analysis Feature's result messages.</summary>
    public static class Analysis
    {
        /// <summary>A sync named a connection that does not exist or belongs to another user.</summary>
        public const string ConnectionNotFound = "Analysis.Result.ConnectionNotFound";

        /// <summary>Reading the spreadsheet to sync failed.</summary>
        public const string ReadFailed = "Analysis.Result.ReadFailed";

        /// <summary>The spreadsheet to sync has no budget data.</summary>
        public const string NoBudgetData = "Analysis.Result.NoBudgetData";

        /// <summary>Some synced months already exist; the user must confirm the overwrite.</summary>
        public const string MonthsExist = "Analysis.Result.MonthsExist";

        /// <summary>The sync failed.</summary>
        public const string SyncFailed = "Analysis.Result.SyncFailed";
    }
}
