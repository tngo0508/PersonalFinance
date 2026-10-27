using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.GoogleDrive;
using GoogleDriveController = PersonalFinance.Web.Controllers.GoogleDriveController;
using Keys = PersonalFinance.Shared.Constants.ResultMessageKeys.GoogleDrive;

namespace PersonalFinance.Tests.GoogleDrive;

/// <summary>
/// The errors and warnings inside a successful Google Drive result are keyed (#405), and the Web app shows them in the
/// reader's language: the ApiService sets the key beside the English text, and the controller resolves it before a
/// view, TempData or the page's JavaScript sees it.
/// </summary>
public class DriveResultMessageTests
{
    private const string FolderId = "1FakeFolderIdForTests0000000000ab";

    [Fact]
    public async Task Explore_WithoutAnApiKey_WarnsWithAKey()
    {
        DriveConnectionService service = CreateService(Respond(HttpStatusCode.OK, """{ "files": [] }"""), out _);

        GoogleDriveFolderResponseDto response = await service.GetFolderFilesAsync(new GoogleDriveRequestDto { FolderUrl = FolderId });

        Assert.Equal(Keys.NoApiKey, response.Warning?.Key);
        Assert.Equal(response.WarningMessage, response.Warning?.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, Keys.FolderNotFound)]
    [InlineData(HttpStatusCode.Forbidden, Keys.AccessDenied)]
    [InlineData(HttpStatusCode.BadRequest, Keys.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError, Keys.HttpStatus)]
    public async Task Explore_WhenGoogleAnswersAnError_WarnsWithTheKeyOfThatStatus(HttpStatusCode status, string key)
    {
        DriveConnectionService service = CreateService(Respond(status, "{}"), out _);

        GoogleDriveFolderResponseDto response = await service.GetFolderFilesAsync(
            new GoogleDriveRequestDto { FolderUrl = FolderId, ApiKey = "test-key" });

        Assert.True(response.IsDemoData);
        Assert.Equal(key, response.Warning?.Key);
        Assert.Equal(response.WarningMessage, response.Warning?.Message);
    }

    [Fact]
    public async Task Explore_WhenGoogleAnswersAnotherStatus_NamesItAsAnArgument()
    {
        DriveConnectionService service = CreateService(Respond(HttpStatusCode.InternalServerError, "{}"), out _);

        GoogleDriveFolderResponseDto response = await service.GetFolderFilesAsync(
            new GoogleDriveRequestDto { FolderUrl = FolderId, ApiKey = "test-key" });

        ApiErrorArgDto status = response.Warning!.Args!["status"];
        Assert.Equal(ApiErrorArgKind.Text, status.Kind);
        Assert.Equal("500 (InternalServerError)", status.Value);
    }

    [Fact]
    public async Task Explore_OfAnEmptyFolder_WarnsWithAKey()
    {
        DriveConnectionService service = CreateService(Respond(HttpStatusCode.OK, """{ "files": [] }"""), out _);

        GoogleDriveFolderResponseDto response = await service.GetFolderFilesAsync(
            new GoogleDriveRequestDto { FolderUrl = FolderId, ApiKey = "test-key" });

        Assert.False(response.IsDemoData);
        Assert.Equal(Keys.EmptyFolder, response.Warning?.Key);
    }

    [Fact]
    public async Task ASavedConnectionWithRejectedCredentials_KeysItsStoredError_WithoutStoringAKey()
    {
        DriveConnectionService service = CreateService(Respond(HttpStatusCode.Unauthorized, "{}"), out AppDbContext db);

        GoogleDriveConnectionDto connection = await service.ConnectDriveAsync("user-1",
            new ConnectGoogleDriveRequestDto { FolderUrl = FolderId, ApiKey = "expired-key" });
        GoogleDriveFolderResponseDto files = await service.GetConnectionFilesAsync(connection.Id, "user-1", forceRefresh: true);
        List<GoogleDriveConnectionDto> listed = await service.GetUserConnectionsAsync("user-1");

        // The column holds the English text only; the key comes from the NeedsReauth status.
        Assert.Equal(DriveResultMessages.CredentialsInvalidText,
            (await db.GoogleDriveConnections.SingleAsync()).ErrorMessage);
        Assert.Equal(Keys.CredentialsInvalid, connection.Error?.Key);
        Assert.Equal(Keys.CredentialsInvalid, files.Error?.Key);
        Assert.Equal(Keys.CredentialsInvalid, Assert.Single(listed).Error?.Key);
    }

    [Theory]
    [InlineData("Some older text", "NeedsReauth", true)]
    [InlineData("Some older text", "Synced", false)]
    [InlineData(null, "NeedsReauth", false)]
    public void AStoredError_IsKeyedOnlyWhenTheConnectionNeedsReauth(string? stored, string status, bool keyed)
    {
        ApiErrorEntryDto? entry = DriveResultMessages.ForStoredError(stored, status);

        Assert.Equal(keyed ? Keys.CredentialsInvalid : null, entry?.Key);
    }

    [Theory]
    [InlineData("", Keys.SpreadsheetEmpty)]
    [InlineData("Not,a,budget\n1,2,3", Keys.NoBudgetTable)]
    public void AnUnreadableBudgetReport_SaysWhyWithAKey(string csv, string key)
    {
        MonthlyBudgetReportDto report = BudgetSpreadsheetParser.ParseSpreadsheetBudgetReport(csv);

        Assert.Empty(report.Months);
        Assert.Equal(key, report.Error?.Key);
        Assert.Equal(report.ErrorMessage, report.Error?.Message);
    }

    [Fact]
    public void AnUnreadableWorkbook_SaysWhyWithAKey()
    {
        MonthlyBudgetReportDto report = BudgetSpreadsheetParser.ParseXlsxBudgetReport(new MemoryStream([1, 2, 3]));
        SpreadsheetTransactionsDto transactions = BudgetSpreadsheetParser.ParseXlsxTransactions(new MemoryStream([1, 2, 3]));

        Assert.Equal(Keys.WorkbookUnreadable, report.Error?.Key);
        Assert.Equal(Keys.WorkbookUnreadable, transactions.Error?.Key);
    }

    [Fact]
    public void ATransactionLogWithoutATable_SaysWhyWithAKey()
    {
        SpreadsheetTransactionsDto transactions = BudgetSpreadsheetParser.ParseCsvTransactions("Name,Value\nRent,10");

        Assert.Equal(Keys.NoTransactionsTable, transactions.Error?.Key);
        Assert.Equal(transactions.ErrorMessage, transactions.Error?.Message);
    }

    [Fact]
    public async Task Index_ShowsTheConnectionErrorAndTheWarning_InVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var api = new FakeGoogleDriveApi
        {
            SavedConnections =
            [
                new GoogleDriveConnectionDto
                {
                    Id = 7, Name = "Household", IsValid = false, SyncStatus = "NeedsReauth",
                    ErrorMessage = DriveResultMessages.CredentialsInvalid.Message, Error = DriveResultMessages.CredentialsInvalid
                }
            ],
            ConnectionFilesResponse = new GoogleDriveFolderResponseDto
            {
                Success = false,
                ErrorMessage = DriveResultMessages.CredentialsInvalid.Message,
                Error = DriveResultMessages.CredentialsInvalid,
                WarningMessage = DriveResultMessages.Unreachable.Message,
                Warning = DriveResultMessages.Unreachable
            }
        };

        IActionResult result = await CreateController(api).Index(null, null, null);

        GoogleDriveViewModel model = Assert.IsType<GoogleDriveViewModel>(Assert.IsType<ViewResult>(result).Model);
        string credentials = TestLocalization.Vietnamese(Keys.CredentialsInvalid);
        Assert.Equal(credentials, Assert.Single(model.Connections).ErrorMessage);
        Assert.Equal(credentials, model.ErrorMessage);
        Assert.Equal(TestLocalization.Vietnamese(Keys.Unreachable), model.Response!.WarningMessage);
    }

    [Fact]
    public async Task Sync_WithRejectedCredentials_ReportsInVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var api = new FakeGoogleDriveApi
        {
            SyncResponse = new GoogleDriveFolderResponseDto
            {
                Success = false,
                ErrorMessage = DriveResultMessages.CredentialsInvalid.Message,
                Error = DriveResultMessages.CredentialsInvalid
            }
        };
        GoogleDriveController controller = CreateController(api);

        await controller.Sync(7);

        Assert.Equal(TestLocalization.Vietnamese(Keys.CredentialsInvalid), controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Reauth_WithRejectedCredentials_ReportsInVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var api = new FakeGoogleDriveApi
        {
            ReauthResponse = new GoogleDriveConnectionDto
            {
                Id = 7, IsValid = false, SyncStatus = "NeedsReauth",
                ErrorMessage = DriveResultMessages.CredentialsInvalid.Message, Error = DriveResultMessages.CredentialsInvalid
            }
        };
        GoogleDriveController controller = CreateController(api);

        await controller.Reauth(7, "still-expired");

        Assert.Equal(TestLocalization.Vietnamese(Keys.CredentialsInvalid), controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task MonthlyBudgetReport_SaysWhyItIsUnavailable_InVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var api = new FakeGoogleDriveApi
        {
            BudgetReport = BudgetSpreadsheetParser.Unavailable("Budget.xlsx", "file-1", DriveResultMessages.DownloadFailed)
        };

        IActionResult result = await CreateController(api).MonthlyBudgetReport(fileId: "file-1");

        Assert.Equal(TestLocalization.Vietnamese(Keys.DownloadFailed), ErrorMessageOf(result));
    }

    [Fact]
    public async Task SpreadsheetTransactions_SaysWhyThereAreNone_InVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var api = new FakeGoogleDriveApi
        {
            TransactionsResponse = BudgetSpreadsheetParser.ParseCsvTransactions("Name,Value\nRent,10")
        };

        IActionResult result = await CreateController(api).SpreadsheetTransactions("file-1");

        Assert.Equal(TestLocalization.Vietnamese(Keys.NoTransactionsTable)
            .Replace("{date}", "Date", StringComparison.Ordinal)
            .Replace("{amount}", "Amount", StringComparison.Ordinal), ErrorMessageOf(result));
    }

    [Fact]
    public void TheSheetAndColumnNamesTheParserReads_StayInEnglishInVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var budget = new MonthlyBudgetReportDto { Error = DriveResultMessages.NoBudgetTable };
        var transactions = new SpreadsheetTransactionsDto { Error = DriveResultMessages.NoTransactionsSheet };

        GoogleDriveText.Localize(TestLocalization.Localizer, budget);
        GoogleDriveText.Localize(TestLocalization.Localizer, transactions);

        Assert.Contains("\"Summary\"", budget.ErrorMessage);
        Assert.Contains("\"Transactions\"", transactions.ErrorMessage);
        Assert.Contains("Date", transactions.ErrorMessage);
        Assert.Contains("Amount", transactions.ErrorMessage);
        Assert.DoesNotContain("{", transactions.ErrorMessage);
        Assert.NotEqual(DriveResultMessages.NoTransactionsSheet.Message, transactions.ErrorMessage);
    }

    [Fact]
    public void AnHttpStatusWarning_KeepsTheStatusInVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var response = new GoogleDriveFolderResponseDto { Warning = DriveResultMessages.HttpFailure(HttpStatusCode.BadGateway) };

        GoogleDriveText.Localize(TestLocalization.Localizer, response);

        Assert.Contains("502 (BadGateway)", response.WarningMessage);
        Assert.DoesNotContain("{status}", response.WarningMessage);
        Assert.NotEqual(response.Warning!.Message, response.WarningMessage);
    }

    [Fact]
    public void AMessageWithoutAKey_IsShownAsSent()
    {
        using var culture = new RequestCultureScope("vi-VN");

        Assert.Equal("Older English text", ApiErrorMessages.Resolve(TestLocalization.Localizer, null, "Older English text"));
        Assert.Null(ApiErrorMessages.Resolve(TestLocalization.Localizer, null, null));
    }

    private static string? ErrorMessageOf(IActionResult result)
    {
        JsonResult json = Assert.IsType<JsonResult>(result);
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(json.Value, JsonSerializerOptions.Web));
        return document.RootElement.GetProperty("errorMessage").GetString();
    }

    private static DelegatingTestHandler Respond(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        }));

    private static DriveConnectionService CreateService(DelegatingTestHandler handler, out AppDbContext db)
    {
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DriveResultMessages_{Guid.NewGuid():N}")
            .Options);
        return new DriveConnectionService(new HttpClient(handler), new ConfigurationBuilder().Build(), db,
            NullLogger<DriveConnectionService>.Instance);
    }

    private static GoogleDriveController CreateController(FakeGoogleDriveApi api)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"))
        };
        return new GoogleDriveController(api, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }
}
