using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models;
using PersonalFinance.Web.Services;
using Xunit;

namespace PersonalFinance.Tests;

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
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        var result = await controller.Index(null, null, null, addNew: false);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<GoogleDriveViewModel>(viewResult.Model);
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

        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        var result = await controller.Index(null, null, null, addNew: false);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<GoogleDriveViewModel>(viewResult.Model);
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
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        var result = await controller.Sync(42);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal(42, redirectResult.RouteValues?["connectionId"]);
    }

    [Fact]
    public async Task Reauth_CallsApiAndRedirectsToIndex()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        var result = await controller.Reauth(42, "AIzaSyNewKey");

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal(42, redirectResult.RouteValues?["connectionId"]);
    }

    [Fact]
    public async Task Delete_CallsApiAndRedirectsToIndex()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

        // Act
        var result = await controller.Delete(42);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
    }

    [Fact]
    public async Task MonthlyBudgetReport_WithValidFileName_ReturnsJsonReport()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        // Act
        var result = await controller.MonthlyBudgetReport("Monthly budget 2026.xlsx", "file-123");

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var report = Assert.IsType<MonthlyBudgetReportDto>(jsonResult.Value);
        Assert.Equal("Monthly budget 2026.xlsx", report.FileName);
        Assert.Equal("file-123", report.FileId);
        Assert.Equal(2026, report.Year);
        Assert.Equal(12, report.Months.Count);
        Assert.True(report.TotalAnnualIncome > 0);
        Assert.True(report.TotalAnnualExpenses > 0);
    }

    [Fact]
    public async Task MonthlyBudgetReport_WithSingleMonthSpreadsheet_ReturnsOnlySingleMonth()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        // Act
        var result = await controller.MonthlyBudgetReport("Monthly budget - September 2026.xlsx", "file-sep-123");

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var report = Assert.IsType<MonthlyBudgetReportDto>(jsonResult.Value);
        Assert.Equal("Monthly budget - September 2026.xlsx", report.FileName);
        Assert.Equal("file-sep-123", report.FileId);
        Assert.Equal(2026, report.Year);
        Assert.True(report.IsSingleMonth);
        Assert.Single(report.Months);
        Assert.Equal(9, report.Months[0].MonthNumber);
        Assert.Equal("September", report.Months[0].MonthName);
    }

    [Fact]
    public async Task MonthlyBudgetReport_WithEmptyFileName_ReturnsBadRequest()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        // Act
        var result = await controller.MonthlyBudgetReport("");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SpreadsheetTransactions_WithValidFileId_ReturnsJsonTransactions()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        // Act
        var result = await controller.SpreadsheetTransactions("file-123", "Monthly budget 2026.xlsx");

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var transactions = Assert.IsType<SpreadsheetTransactionsDto>(jsonResult.Value);
        Assert.Equal("file-123", transactions.FileId);
        Assert.NotEmpty(transactions.Transactions);
    }

    [Fact]
    public async Task CreateMonthlyBudget_WithValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        var request = new CreateMonthlyBudgetTemplateRequestDto
        {
            ConnectionId = 1,
            Year = 2026,
            Month = 10,
            StartingBalance = 1500m
        };

        // Act
        var result = await controller.CreateMonthlyBudget(request);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<CreateMonthlyBudgetTemplateResponseDto>(jsonResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.FileId);
        Assert.NotNull(response.WebUrl);
    }

    [Fact]
    public async Task CreateMonthlyBudget_WithInvalidConnectionId_ReturnsErrorResponse()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        var request = new CreateMonthlyBudgetTemplateRequestDto
        {
            ConnectionId = 0
        };

        // Act
        var result = await controller.CreateMonthlyBudget(request);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<CreateMonthlyBudgetTemplateResponseDto>(jsonResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Valid ConnectionId is required.", response.ErrorMessage);
    }

    [Fact]
    public async Task CreateMonthlyBudget_WhenApiReturnsFailure_ReturnsJsonFailure()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi
        {
            CreateTemplateResponse = new CreateMonthlyBudgetTemplateResponseDto
            {
                Success = false,
                ErrorMessage = "Google Drive authorization failed (HTTP 401 Unauthorized)."
            }
        };
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        var request = new CreateMonthlyBudgetTemplateRequestDto
        {
            ConnectionId = 1,
            Year = 2026,
            Month = 10
        };

        // Act
        var result = await controller.CreateMonthlyBudget(request);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<CreateMonthlyBudgetTemplateResponseDto>(jsonResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Google Drive authorization failed (HTTP 401 Unauthorized).", response.ErrorMessage);
    }

    [Fact]
    public async Task CreateMonthlyBudget_UsesServerSideAccessToken_NotClientSupplied()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var fakeAuth = new FakeGoogleDriveAuthorizationService { AccessToken = "server-token" };
        var controller = new GoogleDriveController(fakeApi, fakeAuth, NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        var request = new CreateMonthlyBudgetTemplateRequestDto
        {
            ConnectionId = 1,
            AccessToken = "client-supplied-token"
        };

        // Act
        await controller.CreateMonthlyBudget(request);

        // Assert
        Assert.Equal("server-token", fakeApi.LastCreateTemplateRequest?.AccessToken);
        Assert.Equal("test-user-123", fakeApi.LastCreateTemplateRequest?.UserId);
    }

    [Fact]
    public async Task CreateMonthlyBudget_WhenAuthorizationRequired_ReturnsAuthorizationUrlAndClearsRejectedToken()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi
        {
            CreateTemplateResponse = new CreateMonthlyBudgetTemplateResponseDto
            {
                Success = false,
                RequiresAuthorization = true,
                ErrorMessage = "Your Google Drive authorization has expired or was revoked."
            }
        };
        var fakeAuth = new FakeGoogleDriveAuthorizationService { AccessToken = "revoked-token" };
        var controller = new GoogleDriveController(fakeApi, fakeAuth, NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123"),
            Url = new FakeUrlHelper()
        };

        // Act
        var result = await controller.CreateMonthlyBudget(new CreateMonthlyBudgetTemplateRequestDto { ConnectionId = 7 });

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<CreateMonthlyBudgetTemplateResponseDto>(jsonResult.Value);
        Assert.False(response.Success);
        Assert.True(response.RequiresAuthorization);
        Assert.Equal("/GoogleDrive/AuthorizeDrive?connectionId=7", response.AuthorizationUrl);
        Assert.True(fakeAuth.TokensCleared);
    }

    [Fact]
    public void DownloadMonthlyBudgetTemplate_ReturnsFileResultWithValidXlsx()
    {
        // Arrange
        var fakeApi = new FakeGoogleDriveApi();
        var controller = new GoogleDriveController(fakeApi, new FakeGoogleDriveAuthorizationService(), NullLogger<GoogleDriveController>.Instance)
        {
            ControllerContext = CreateControllerContext("test-user-123")
        };

        // Act
        var result = controller.DownloadMonthlyBudgetTemplate();

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.Equal("Monthly budget.xlsx", fileResult.FileDownloadName);
        Assert.NotEmpty(fileResult.FileContents);
        Assert.Equal(0x50, fileResult.FileContents[0]);
    }
}

public class FakeGoogleDriveApi : IGoogleDriveApi
{
    public List<GoogleDriveConnectionDto> SavedConnections { get; set; } = new();
    public GoogleDriveFolderResponseDto ConnectionFilesResponse { get; set; } = new() { Success = true };
    public CreateMonthlyBudgetTemplateResponseDto? CreateTemplateResponse { get; set; }
    public CreateMonthlyBudgetTemplateRequestDto? LastCreateTemplateRequest { get; private set; }

    public Task<GoogleDriveFolderResponseDto> ExploreFolderAsync(GoogleDriveRequestDto request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GoogleDriveFolderResponseDto { Success = true, FolderId = "fake-folder" });
    }

    public Task<List<GoogleDriveConnectionDto>> GetUserConnectionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SavedConnections);
    }

    public Task<GoogleDriveFolderResponseDto> GetConnectionFilesAsync(int connectionId, string userId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ConnectionFilesResponse);
    }

    public Task<GoogleDriveConnectionDto> ConnectDriveAsync(ConnectGoogleDriveRequestDto request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GoogleDriveConnectionDto { Id = 1, Name = request.ConnectionName ?? "Google Drive", FolderId = "folder-id", UserId = request.UserId });
    }

    public Task<GoogleDriveFolderResponseDto> SyncConnectionAsync(int connectionId, SyncGoogleDriveRequestDto request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GoogleDriveFolderResponseDto { Success = true });
    }

    public Task<GoogleDriveConnectionDto> ReauthConnectionAsync(int connectionId, ReauthGoogleDriveRequestDto request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GoogleDriveConnectionDto { Id = connectionId, IsValid = true });
    }

    public Task<bool> DeleteConnectionAsync(int connectionId, string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(string fileId, string? fileName = null, int? connectionId = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GoogleDriveHelper.GenerateMonthlyBudgetReport(fileName, fileId));
    }

    public Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(string fileId, string? fileName = null, int? connectionId = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GoogleDriveHelper.ParseCsvTransactions(GoogleDriveHelper.GetSampleTransactionsCsv(), fileName, fileId));
    }

    public Task<CreateMonthlyBudgetTemplateResponseDto> CreateMonthlyBudgetTemplateAsync(CreateMonthlyBudgetTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        LastCreateTemplateRequest = request;
        if (CreateTemplateResponse != null)
        {
            return Task.FromResult(CreateTemplateResponse);
        }

        return Task.FromResult(new CreateMonthlyBudgetTemplateResponseDto
        {
            Success = true,
            FileId = $"test_file_{Guid.NewGuid():N}",
            FileName = request.CustomFileName ?? $"Monthly budget - {request.Year}-{request.Month:D2}",
            WebUrl = "https://docs.google.com/spreadsheets/d/test_file/edit",
            FolderId = "test_folder"
        });
    }
}

public class FakeGoogleDriveAuthorizationService : IGoogleDriveAuthorizationService
{
    public string? AccessToken { get; set; }
    public bool IsConfigured { get; set; } = true;
    public bool TokensCleared { get; private set; }

    public Task<bool> IsConfiguredAsync() => Task.FromResult(IsConfigured);

    public Task StoreTokensAsync(string userId, IEnumerable<AuthenticationToken> tokens, CancellationToken cancellationToken = default)
    {
        AccessToken = tokens.FirstOrDefault(t => t.Name == "access_token")?.Value;
        return Task.CompletedTask;
    }

    public Task<string?> GetValidAccessTokenAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(AccessToken);

    public Task ClearTokensAsync(string userId, CancellationToken cancellationToken = default)
    {
        TokensCleared = true;
        AccessToken = null;
        return Task.CompletedTask;
    }
}

public class FakeUrlHelper : IUrlHelper
{
    public ActionContext ActionContext { get; } = new();

    public string? Action(UrlActionContext actionContext)
    {
        var values = new RouteValueDictionary(actionContext.Values);
        var query = string.Join("&", values.Where(v => v.Value != null).Select(v => $"{v.Key}={v.Value}"));
        return $"/GoogleDrive/{actionContext.Action}" + (query.Length > 0 ? "?" + query : string.Empty);
    }

    public string? Content(string? contentPath) => contentPath;
    public bool IsLocalUrl(string? url) => true;
    public string? Link(string? routeName, object? values) => null;
    public string? RouteUrl(UrlRouteContext routeContext) => null;
}

public class FakeTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object?> _data = new();

    public IDictionary<string, object?> LoadTempData(HttpContext context) => _data;

    public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
    {
        foreach (var (key, val) in values)
        {
            _data[key] = val;
        }
    }
}
