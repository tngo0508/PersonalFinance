using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;

namespace PersonalFinance.Tests.GoogleDrive;

/// <summary>
/// Google Drive is a true external dependency, so these tests fake HTTP: they pin which Drive URLs are tried,
/// in which order, and with which API key.
/// </summary>
public class GoogleDriveFileSourceTests
{
    [Fact]
    public async Task GoogleSheet_WithoutApiKey_TriesPublicXlsxExportThenCsvExport()
    {
        (GoogleDriveFileSource? source, List<string>? requested) = Create(_ => HttpStatusCode.OK);

        List<SpreadsheetDownload> downloads = await DownloadAllAsync(source, "sheet-1", "Monthly budget 2026.gsheet");

        Assert.Equal(
        [
            "https://docs.google.com/spreadsheets/d/sheet-1/export?format=xlsx",
            "https://docs.google.com/spreadsheets/d/sheet-1/export?format=csv"
        ], requested);
        Assert.Equal([SpreadsheetFormat.Xlsx, SpreadsheetFormat.Csv], downloads.Select(d => d.Format));
    }

    [Fact]
    public async Task GoogleSheet_WithConfiguredApiKey_UsesDriveApiExportUrls()
    {
        (GoogleDriveFileSource? source, List<string>? requested) = Create(_ => HttpStatusCode.OK, defaultApiKey: "configured-key");

        await DownloadAllAsync(source, "sheet-1", "Monthly budget 2026.gsheet");

        Assert.All(requested, url =>
        {
            Assert.StartsWith("https://www.googleapis.com/drive/v3/files/sheet-1/export?", url);
            Assert.EndsWith("&key=configured-key", url);
        });
    }

    [Fact]
    public async Task UploadedWorkbook_IsDownloadedOnceAsStored()
    {
        (GoogleDriveFileSource? source, List<string>? requested) = Create(_ => HttpStatusCode.OK);

        SpreadsheetDownload download = Assert.Single(await DownloadAllAsync(source, "file-1", "Budget 2026.xlsx"));

        Assert.Equal("https://drive.google.com/uc?export=download&id=file-1", Assert.Single(requested));
        Assert.Equal(SpreadsheetFormat.Xlsx, download.Format);
    }

    [Fact]
    public async Task FailedDownloads_AreSkipped()
    {
        (GoogleDriveFileSource? source, List<string>? requested) = Create(url => url.Contains("format=xlsx") ? HttpStatusCode.Forbidden : HttpStatusCode.OK);

        SpreadsheetDownload download = Assert.Single(await DownloadAllAsync(source, "sheet-1", "Monthly budget 2026.gsheet"));

        Assert.Equal(2, requested.Count);
        Assert.Equal(SpreadsheetFormat.Csv, download.Format);
    }

    private static async Task<List<SpreadsheetDownload>> DownloadAllAsync(GoogleDriveFileSource source, string fileId,
        string fileName)
    {
        var downloads = new List<SpreadsheetDownload>();
        await foreach (SpreadsheetDownload download in source.DownloadAsync("account-1", fileId, fileName, connectionId: null))
        {
            downloads.Add(download);
        }

        return downloads;
    }

    private static (GoogleDriveFileSource Source, List<string> Requested) Create(Func<string, HttpStatusCode> respond,
        string? defaultApiKey = null)
    {
        var requested = new List<string>();
        var handler = new DelegatingTestHandler((request, _) =>
        {
            string url = request.RequestUri!.ToString();
            requested.Add(url);
            return Task.FromResult(new HttpResponseMessage(respond(url)) { Content = new StringContent("Income,1000") });
        });
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GoogleDrive:ApiKey"] = defaultApiKey })
            .Build();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FileSource_{Guid.NewGuid():N}")
            .Options);
        return (new GoogleDriveFileSource(new HttpClient(handler), configuration, db,
            NullLogger<GoogleDriveFileSource>.Instance), requested);
    }
}
