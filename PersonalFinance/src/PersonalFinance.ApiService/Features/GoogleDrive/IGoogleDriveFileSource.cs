using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Features.GoogleDrive;

/// <summary>
/// Downloads spreadsheet content from Google Drive for an Account. Hides the Drive URL formats, the choice between
/// a Google Sheets export and a direct file download, and which API key to use.
/// </summary>
public interface IGoogleDriveFileSource
{
    /// <summary>
    /// Yields the downloads worth trying for <paramref name="fileId"/>, best first (a Google Sheet as XLSX, then as
    /// CSV; any other file as stored). Each download is only requested when the caller asks for the next one, so a
    /// caller that can use the first result never triggers the others. Downloads that fail are skipped; an empty
    /// sequence means the file could not be downloaded at all.
    /// </summary>
    /// <param name="userId">Owning Account; only its own connections (and their API keys) are used.</param>
    /// <param name="fileId">Google Drive file id.</param>
    /// <param name="fileName">Name shown to the user; the cached name is used when omitted.</param>
    /// <param name="connectionId">Connection whose API key to use; it must belong to <paramref name="userId"/>.</param>
    IAsyncEnumerable<SpreadsheetDownload> DownloadAsync(
        string userId,
        string fileId,
        string? fileName,
        int? connectionId,
        CancellationToken cancellationToken = default);
}

/// <summary>Spreadsheet file formats the parser understands.</summary>
public enum SpreadsheetFormat
{
    Csv,
    Xlsx
}

/// <summary>
/// One downloaded copy of a spreadsheet.
/// </summary>
/// <param name="FileName">Display name of the file.</param>
/// <param name="Format">How <paramref name="Content"/> is encoded.</param>
/// <param name="Content">Raw file bytes.</param>
/// <param name="DataSource">User-facing description of where the data came from.</param>
/// <param name="CurrencyCode">The ISO 4217 currency of the connection the file belongs to (ADR 0016); US dollars when there is none.</param>
public sealed record SpreadsheetDownload(string FileName, SpreadsheetFormat Format, byte[] Content, string DataSource,
    string CurrencyCode = SupportedCurrencies.DefaultCode);
