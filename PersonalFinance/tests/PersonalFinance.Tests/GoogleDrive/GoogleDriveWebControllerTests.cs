using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.GoogleDrive;
using Xunit;
using GoogleDriveController = PersonalFinance.Web.Controllers.GoogleDriveController;

namespace PersonalFinance.Tests.GoogleDrive;

public class GoogleDriveWebControllerTests
{
    private static ControllerContext CreateControllerContext(string userId = "test-user-123")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"{userId}@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Index_WhenUserHasNoConnections_ShowsConnectForm()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        IActionResult result = await controller.Index(null, null, null, addNew: false);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        GoogleDriveViewModel model = Assert.IsType<GoogleDriveViewModel>(viewResult.Model);
        Assert.True(model.ShowConnectForm);
        Assert.Empty(model.Connections);
    }

    [Fact]
    public async Task Index_WhenUserHasConnections_LoadsActiveCachedConnection()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi
        {
            SavedConnections = new List<GoogleDriveConnectionDto>
            {
                new()
                {
                    Id = 42,
                    UserId = "test-user-123",
                    Name = "Saved Project Drive",
                    FolderId = "folder-42",
                    CachedFilesCount = 5
                }
            },
            ConnectionFilesResponse = new GoogleDriveFolderResponseDto
            {
                Success = true,
                FolderId = "folder-42",
                FolderName = "Saved Project Drive",
                Files = new List<GoogleDriveFileDto>
                {
                    new() { Id = "file-1", Name = "Doc1.pdf", FileType = "PDF Document" }
                }
            }
        };

        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        IActionResult result = await controller.Index(null, null, null, addNew: false);

        // Assert
        ViewResult viewResult = Assert.IsType<ViewResult>(result);
        GoogleDriveViewModel model = Assert.IsType<GoogleDriveViewModel>(viewResult.Model);
        Assert.False(model.ShowConnectForm);
        Assert.Equal(42, model.SelectedConnectionId);
        Assert.NotNull(model.Response);
        Assert.True(model.Response.Success);
        Assert.Single(model.Response.Files);
    }

    [Fact]
    public async Task Sync_CallsApiAndRedirectsToIndex()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        IActionResult result = await controller.Sync(42);

        // Assert
        RedirectToActionResult redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal(42, redirectResult.RouteValues?["connectionId"]);
    }

    [Fact]
    public async Task Reauth_CallsApiAndRedirectsToIndex()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        IActionResult result = await controller.Reauth(42, "AIzaSyNewKey");

        // Assert
        RedirectToActionResult redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal(42, redirectResult.RouteValues?["connectionId"]);
    }

    [Fact]
    public async Task Delete_CallsApiAndRedirectsToIndex()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        IActionResult result = await controller.Delete(42);

        // Assert
        RedirectToActionResult redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
    }

    [Fact]
    public async Task MonthlyBudgetReport_ReturnsTheReportFromApiService()
    {
        var apiReport = new MonthlyBudgetReportDto { FileName = "Monthly budget 2026.xlsx", FileId = "file-123" };
        var fakeApi = new FakeGoogleDriveApi { BudgetReport = apiReport };
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        IActionResult result = await controller.MonthlyBudgetReport("Monthly budget 2026.xlsx", "file-123");

        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Same(apiReport, jsonResult.Value);
    }

    [Fact]
    public async Task MonthlyBudgetReport_WhenApiServiceFails_ReturnsBadGatewayInsteadOfInventedData()
    {
        var fakeApi = new FakeGoogleDriveApi { BudgetReportFailure = new HttpRequestException("ApiService down") };
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.MonthlyBudgetReport("Monthly budget 2026.xlsx", "file-123"));

        ObjectResult status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
    }

    [Fact]
    public async Task SpreadsheetTransactions_WithValidFileId_ReturnsJsonTransactions()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        // Act
        IActionResult result = await controller.SpreadsheetTransactions("file-123", "Monthly budget 2026.xlsx");

        // Assert
        JsonResult jsonResult = Assert.IsType<JsonResult>(result);
        SpreadsheetTransactionsDto transactions = Assert.IsType<SpreadsheetTransactionsDto>(jsonResult.Value);
        Assert.Equal("file-123", transactions.FileId);
        Assert.NotEmpty(transactions.Transactions);
    }

    [Fact]
    public async Task SpreadsheetTransactions_WithEmptyFileId_ReturnsBadRequest()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService())
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        // Act
        IActionResult result = await controller.SpreadsheetTransactions("");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}

public class FakeGoogleDriveApi : IGoogleDriveApi
{
    public List<GoogleDriveConnectionDto> SavedConnections { get; set; } = new();
    public GoogleDriveFolderResponseDto ConnectionFilesResponse { get; set; } = new() { Success = true };
    public MonthlyBudgetReportDto BudgetReport { get; set; } = new();
    public Exception? BudgetReportFailure { get; set; }
    public GoogleDriveFolderResponseDto SyncResponse { get; set; } = new() { Success = true };
    public GoogleDriveConnectionDto? ReauthResponse { get; set; }
    public SpreadsheetTransactionsDto? TransactionsResponse { get; set; }

    public Task<GoogleDriveFolderResponseDto> ExploreFolderAsync(GoogleDriveRequestDto request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GoogleDriveFolderResponseDto { Success = true, FolderId = "fake-folder" });
    }

    public Task<List<GoogleDriveConnectionDto>> GetUserConnectionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SavedConnections);
    }

    public Task<GoogleDriveFolderResponseDto> GetConnectionFilesAsync(int connectionId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ConnectionFilesResponse);
    }

    public Task<GoogleDriveConnectionDto> ConnectDriveAsync(ConnectGoogleDriveRequestDto request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GoogleDriveConnectionDto { Id = 1, Name = request.ConnectionName ?? "Google Drive", FolderId = "folder-id" });
    }

    public Task<GoogleDriveFolderResponseDto> SyncConnectionAsync(int connectionId, SyncGoogleDriveRequestDto request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SyncResponse);
    }

    public Task<GoogleDriveConnectionDto> ReauthConnectionAsync(int connectionId, ReauthGoogleDriveRequestDto request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ReauthResponse ?? new GoogleDriveConnectionDto { Id = connectionId, IsValid = true });
    }

    public Task<bool> DeleteConnectionAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(string fileId, string? fileName = null, int? connectionId = null, CancellationToken cancellationToken = default)
    {
        return BudgetReportFailure is null
            ? Task.FromResult(BudgetReport)
            : Task.FromException<MonthlyBudgetReportDto>(BudgetReportFailure);
    }

    public Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(string fileId, string? fileName = null, int? connectionId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(TransactionsResponse
            ?? BudgetSpreadsheetParser.ParseCsvTransactions(SampleSpreadsheets.GetSampleTransactionsCsv(), fileName, fileId));
    }
}

public class FakeTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object?> _data = new();

    public IDictionary<string, object?> LoadTempData(HttpContext context) => _data;

    public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
    {
        foreach ((string? key, object? val) in values)
        {
            _data[key] = val;
        }
    }
}
