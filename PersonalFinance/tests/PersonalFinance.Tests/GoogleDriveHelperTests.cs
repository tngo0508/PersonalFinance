using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Shared.Helpers;
using Xunit;

namespace PersonalFinance.Tests;

public class GoogleDriveHelperTests
{
    [Theory]
    [InlineData("https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("https://drive.google.com/drive/u/0/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("https://drive.google.com/open?id=127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("https://drive.google.com/file/d/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_/view", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    public void ExtractFolderId_WithValidInputs_ExtractsCorrectId(string input, string expectedId)
    {
        var result = GoogleDriveHelper.ExtractFolderId(input);
        Assert.Equal(expectedId, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("https://google.com/search?q=test")]
    public void ExtractFolderId_WithInvalidInputs_ReturnsNull(string? input)
    {
        var result = GoogleDriveHelper.ExtractFolderId(input);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData(-5L, "-")]
    [InlineData(0L, "0 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1073741824L, "1 GB")]
    public void FormatBytes_FormatsCorrectly(long? bytes, string expected)
    {
        var result = GoogleDriveHelper.FormatBytes(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("application/vnd.google-apps.folder", "folder", "Folder", true)]
    [InlineData("application/pdf", "document.pdf", "PDF Document", false)]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "budget.xlsx", "Spreadsheet", false)]
    [InlineData("image/png", "scan.png", "Image", false)]
    [InlineData("video/mp4", "clip.mp4", "Video", false)]
    [InlineData("application/zip", "archive.zip", "Archive", false)]
    public void ResolveTypeInfo_IdentifiesExpectedTypes(string mimeType, string filename, string expectedCategory, bool expectedIsFolder)
    {
        var (category, _, isFolder) = GoogleDriveHelper.ResolveTypeInfo(mimeType, filename);
        Assert.Equal(expectedCategory, category);
        Assert.Equal(expectedIsFolder, isFolder);
    }

    [Fact]
    public async Task GoogleDriveService_WithValidFolderAndNoKey_ReturnsPreviewResponse()
    {
        var inMemorySettings = new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var httpClient = new HttpClient();
        var service = new GoogleDriveService(httpClient, configuration, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.Equal("127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", response.FolderId);
        Assert.NotEmpty(response.Files);
        Assert.True(response.IsDemoData);
        Assert.NotNull(response.WarningMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WithInvalidUrl_ReturnsErrorResponse()
    {
        var configuration = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new GoogleDriveService(httpClient, configuration, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "invalid-link"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.False(response.Success);
        Assert.True(string.IsNullOrEmpty(response.FolderId));
        Assert.NotNull(response.ErrorMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WhenHttpClientTimesOut_FallsBackToPreviewGracefully()
    {
        var configuration = new ConfigurationBuilder().Build();
        var handler = new DelegatingTestHandler((req, ct) =>
            throw new TaskCanceledException("A task was canceled.", new TimeoutException("The operation timed out.")));
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.True(response.IsDemoData);
        Assert.Contains("timed out", response.WarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GoogleDriveService_WhenCallerCancels_ReturnsCanceledResult()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        var handler = new DelegatingTestHandler((req, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        });
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request, cts.Token);

        Assert.False(response.Success);
        Assert.Equal("The request was canceled.", response.ErrorMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WhenHttp403Forbidden_FallsBackToPreviewGracefully()
    {
        var configuration = new ConfigurationBuilder().Build();
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            var msg = new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden)
            {
                ReasonPhrase = "Forbidden"
            };
            return Task.FromResult(msg);
        });
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.True(response.IsDemoData);
        Assert.Contains("403", response.WarningMessage);
        Assert.Contains("Anyone with the link", response.WarningMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WhenHttp404NotFound_ExplainsSharingPermissions()
    {
        var configuration = new ConfigurationBuilder().Build();
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            var msg = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            {
                ReasonPhrase = "Not Found"
            };
            return Task.FromResult(msg);
        });
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.True(response.IsDemoData);
        Assert.Contains("404", response.WarningMessage);
        Assert.Contains("Anyone with the link", response.WarningMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WhenLiveQueryReturnsEmptyFiles_ProvidesSharingGuidance()
    {
        var configuration = new ConfigurationBuilder().Build();
        var emptyJsonResponse = @"{ ""files"": [] }";
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            var msg = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(emptyJsonResponse, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        });
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.False(response.IsDemoData);
        Assert.Empty(response.Files);
        Assert.NotNull(response.WarningMessage);
        Assert.Contains("Anyone with the link", response.WarningMessage);
    }

    [Fact]
    public void GoogleDriveController_HasAuthorizeAttribute()
    {
        var controllerType = typeof(PersonalFinance.Web.Controllers.GoogleDriveController);
        var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));
        Assert.NotNull(authorizeAttribute);
    }

    private class DelegatingTestHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegatingTestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
