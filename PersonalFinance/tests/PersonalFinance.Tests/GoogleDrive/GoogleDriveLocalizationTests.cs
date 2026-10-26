using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Tests.Web.Localization;
using PersonalFinance.Web.Models.GoogleDrive;
using GoogleDriveController = PersonalFinance.Web.Controllers.GoogleDriveController;

namespace PersonalFinance.Tests.GoogleDrive;

/// <summary>
/// The Google Drive page speaks the request's Language: its own messages, file-type labels and "synced … ago".
/// Drive, folder and file names are the user's own and are never translated (spec §1).
/// </summary>
public class GoogleDriveLocalizationTests
{
    private const string English = """
        {
          "GoogleDrive.Message.Connected": "Connected to '{name}' and cached {count} items.",
          "GoogleDrive.Message.Removed": "Google Drive connection and cached data removed successfully.",
          "GoogleDrive.Message.FolderUrlRequired": "Please enter a valid Google Drive folder URL or Folder ID.",
          "GoogleDrive.Message.FileIdRequired": "File ID is required.",
          "GoogleDrive.FileType.Spreadsheet": "Spreadsheet",
          "GoogleDrive.TimeAgo.JustNow": "just now",
          "GoogleDrive.TimeAgo.Hours": "{count}h ago"
        }
        """;

    private const string Vietnamese = """
        {
          "GoogleDrive.Message.Connected": "Đã kết nối '{name}' và lưu {count} mục.",
          "GoogleDrive.Message.Removed": "Đã gỡ kết nối Google Drive và dữ liệu đã lưu.",
          "GoogleDrive.Message.FolderUrlRequired": "Vui lòng nhập URL hoặc ID thư mục Google Drive hợp lệ.",
          "GoogleDrive.Message.FileIdRequired": "Cần có ID tệp.",
          "GoogleDrive.FileType.Spreadsheet": "Bảng tính",
          "GoogleDrive.TimeAgo.JustNow": "vừa xong",
          "GoogleDrive.TimeAgo.Hours": "{count} giờ trước"
        }
        """;

    private static readonly IStringLocalizer Localizer = LocalizationTestCatalog.Localizer(English, Vietnamese);

    [Fact]
    public async Task Connect_ReportsInTheRequestLanguage_KeepingTheDriveName()
    {
        using var culture = new RequestCultureScope("vi-VN");
        GoogleDriveController controller = CreateController(new FakeGoogleDriveApi());

        await controller.Index(new GoogleDriveViewModel { FolderUrl = "folder-1", ConnectionName = "Household" });

        Assert.Equal("Đã kết nối 'Household' và lưu 0 mục.", controller.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task Connect_WithoutAFolder_AsksInTheRequestLanguage()
    {
        using var culture = new RequestCultureScope("vi-VN");
        GoogleDriveController controller = CreateController(new FakeGoogleDriveApi());

        IActionResult result = await controller.Index(new GoogleDriveViewModel { FolderUrl = " " });

        GoogleDriveViewModel model = Assert.IsType<GoogleDriveViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Vui lòng nhập URL hoặc ID thư mục Google Drive hợp lệ.", model.ErrorMessage);
    }

    [Fact]
    public async Task Delete_ReportsInTheRequestLanguage()
    {
        using var culture = new RequestCultureScope("vi-VN");
        GoogleDriveController controller = CreateController(new FakeGoogleDriveApi());

        await controller.Delete(7);

        Assert.Equal("Đã gỡ kết nối Google Drive và dữ liệu đã lưu.", controller.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task MonthlyBudgetReport_WithoutAFile_AnswersInTheRequestLanguage()
    {
        using var culture = new RequestCultureScope("vi-VN");
        GoogleDriveController controller = CreateController(new FakeGoogleDriveApi());

        IActionResult result = await controller.MonthlyBudgetReport(fileId: null);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        Assert.Equal("Cần có ID tệp.", json.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("Spreadsheet", "Bảng tính")]
    [InlineData("CAD Drawing", "CAD Drawing")]
    public void FileType_IsTranslatedWhenTheAppKnowsIt_OtherwiseShownAsSent(string fileType, string expected)
    {
        using var culture = new RequestCultureScope("vi-VN");

        Assert.Equal(expected, GoogleDriveText.FileType(Localizer, fileType));
    }

    [Fact]
    public void TimeAgo_IsInTheRequestLanguage()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var now = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal("vừa xong", GoogleDriveText.TimeAgo(Localizer, now.AddSeconds(-20), now));
        Assert.Equal("3 giờ trước", GoogleDriveText.TimeAgo(Localizer, now.AddHours(-3), now));
    }

    private static GoogleDriveController CreateController(FakeGoogleDriveApi api)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "drive-user") }, "TestAuthType"))
        };
        return new GoogleDriveController(api, NullLogger<GoogleDriveController>.Instance, Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
        };
    }
}
