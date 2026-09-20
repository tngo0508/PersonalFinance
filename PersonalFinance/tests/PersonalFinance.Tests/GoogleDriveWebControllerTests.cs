using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models;
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
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance)
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

        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance)
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
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance)
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
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance)
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
        var controller = new GoogleDriveController(fakeApi, NullLogger<GoogleDriveController>.Instance)
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
}

public class FakeGoogleDriveApi : IGoogleDriveApi
{
    public List<GoogleDriveConnectionDto> SavedConnections { get; set; } = new();
    public GoogleDriveFolderResponseDto ConnectionFilesResponse { get; set; } = new() { Success = true };

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
