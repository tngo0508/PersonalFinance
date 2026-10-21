using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Data.Helpers;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Downloads spreadsheets over the Google Drive REST API (with an API key) or the public export links (without one).
/// </summary>
public class GoogleDriveFileSource : IGoogleDriveFileSource
{
    private const string GoogleSheetMimeType = "application/vnd.google-apps.spreadsheet";
    private const string XlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<GoogleDriveFileSource> _logger;

    public GoogleDriveFileSource(
        HttpClient httpClient,
        IConfiguration configuration,
        AppDbContext dbContext,
        ILogger<GoogleDriveFileSource> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SpreadsheetDownload> DownloadAsync(
        string fileId,
        string? fileName,
        int? connectionId,
        string? userId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connection = await FindOwnConnectionAsync(fileId, connectionId, userId, cancellationToken);
        var cachedFile = connection?.CachedFiles.FirstOrDefault(f => f.DriveFileId == fileId);
        var name = fileName ?? cachedFile?.Name ?? "Spreadsheet.xlsx";
        var mimeType = cachedFile?.MimeType ?? string.Empty;
        var apiKey = GoogleDriveApiKey.Resolve(CredentialProtector.Decrypt(connection?.EncryptedApiKey), _configuration);
        var id = Uri.EscapeDataString(fileId);

        var isGoogleSheet = mimeType == GoogleSheetMimeType ||
                            Path.GetExtension(name).Equals(".gsheet", StringComparison.OrdinalIgnoreCase) ||
                            (string.IsNullOrEmpty(Path.GetExtension(name)) && cachedFile?.FileType == "Spreadsheet");

        if (isGoogleSheet)
        {
            // The XLSX export keeps every sheet (Summary, Transactions); the CSV export only has the first one.
            var xlsxUrl = apiKey is null
                ? $"https://docs.google.com/spreadsheets/d/{id}/export?format=xlsx"
                : $"https://www.googleapis.com/drive/v3/files/{id}/export?mimeType=application%2Fvnd.openxmlformats-officedocument.spreadsheetml.sheet&key={Uri.EscapeDataString(apiKey)}";
            var xlsx = await TryGetAsync(xlsxUrl, fileId, cancellationToken);
            if (xlsx is not null)
            {
                yield return new SpreadsheetDownload(name, SpreadsheetFormat.Xlsx, xlsx,
                    "Google Drive Live Spreadsheet (Exported XLSX)");
            }

            var csvUrl = apiKey is null
                ? $"https://docs.google.com/spreadsheets/d/{id}/export?format=csv"
                : $"https://www.googleapis.com/drive/v3/files/{id}/export?mimeType=text%2Fcsv&key={Uri.EscapeDataString(apiKey)}";
            var csv = await TryGetAsync(csvUrl, fileId, cancellationToken);
            if (csv is not null)
            {
                yield return new SpreadsheetDownload(name, SpreadsheetFormat.Csv, csv,
                    "Google Drive Live Spreadsheet (Exported Data)");
            }

            yield break;
        }

        // Uploaded .xlsx / .csv file: download it as stored.
        var downloadUrl = apiKey is null
            ? $"https://drive.google.com/uc?export=download&id={id}"
            : $"https://www.googleapis.com/drive/v3/files/{id}?alt=media&key={Uri.EscapeDataString(apiKey)}";
        var file = await TryGetAsync(downloadUrl, fileId, cancellationToken);
        if (file is not null)
        {
            var isXlsx = name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || mimeType == XlsxMimeType;
            yield return isXlsx
                ? new SpreadsheetDownload(name, SpreadsheetFormat.Xlsx, file, "Google Drive Live XLSX (Actual Data)")
                : new SpreadsheetDownload(name, SpreadsheetFormat.Csv, file, "Google Drive Live CSV (Actual Data)");
        }
    }

    /// <summary>
    /// Finds the Account's connection for the file. A connection (and its stored API key) is only ever resolved
    /// for the Account that owns it; without an Account none is used.
    /// </summary>
    private async Task<GoogleDriveConnection?> FindOwnConnectionAsync(
        string fileId, int? connectionId, string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var ownConnections = _dbContext.GoogleDriveConnections
            .AsNoTracking()
            .Include(c => c.CachedFiles)
            .Where(c => c.UserId == userId);

        return connectionId is > 0
            ? await ownConnections.FirstOrDefaultAsync(c => c.Id == connectionId.Value, cancellationToken)
            : await ownConnections.FirstOrDefaultAsync(c => c.CachedFiles.Any(f => f.DriveFileId == fileId),
                cancellationToken);
    }

    /// <summary>Returns the response body, or <c>null</c> when the request fails or the body is empty.</summary>
    private async Task<byte[]?> TryGetAsync(string url, string fileId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Google Drive returned HTTP {StatusCode} for file '{FileId}'.",
                        (int)response.StatusCode, fileId);
                }
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return content.Length > 0 ? content : null;
        }
        catch (Exception ex) when (ex is HttpRequestException ||
                                   (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Downloading file '{FileId}' from Google Drive failed.", fileId);
            return null;
        }
    }
}
