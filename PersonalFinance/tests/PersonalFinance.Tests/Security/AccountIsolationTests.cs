using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Features.GoogleDrive.Adapters;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Data.Helpers;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.Helpers;
using ApiGoogleDriveController = PersonalFinance.ApiService.Features.GoogleDrive.GoogleDriveController;

namespace PersonalFinance.Tests.Security;

/// <summary>
/// An Account must only ever reach its own data through ApiService, whatever ids a caller supplies.
/// </summary>
public class AccountIsolationTests
{
    private const string OwnerApiKey = "OWNER-SECRET-KEY";

    [Fact]
    public void EveryApiServiceController_RequiresAuthorizationOrOptsOutExplicitly()
    {
        IEnumerable<Type> controllerTypes = typeof(ApiGoogleDriveController).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t));

        var unprotected = controllerTypes
            .Where(t => !t.IsDefined(typeof(AuthorizeAttribute), inherit: true)
                        && !t.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
            .Select(t => t.Name)
            .ToList();

        Assert.True(unprotected.Count == 0,
            $"ApiService controllers without [Authorize] or [AllowAnonymous]: {string.Join(", ", unprotected)}");
    }

    [Fact]
    public void GetUserId_ReturnsNameIdentifierClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "account-1"), new Claim(ClaimTypes.Name, "someone@example.com") },
            "Test"));

        Assert.Equal("account-1", user.GetUserId());
    }

    [Fact]
    public void GetUserId_WithoutNameIdentifier_Throws()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Throws<InvalidOperationException>(() => user.GetUserId());
    }

    [Theory]
    [InlineData("intruder")]
    [InlineData("")]
    public async Task SpreadsheetReader_WithAnotherAccountsConnectionId_NeverUsesThatAccountsApiKey(string callerUserId)
    {
        await using AppDbContext db = CreateDb();
        int connectionId = await SeedConnectionAsync(db, "owner");
        var requestedUris = new List<string>();
        SpreadsheetReaderService reader = CreateReader(db, requestedUris);

        await reader.GetSpreadsheetBudgetReportAsync(callerUserId, "file-1", "Budget.xlsx", connectionId);
        await reader.GetSpreadsheetTransactionsAsync(callerUserId, "file-1", "Budget.xlsx", connectionId);

        Assert.DoesNotContain(requestedUris, uri => uri.Contains(OwnerApiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SpreadsheetReader_WithOwnConnectionId_UsesOwnApiKey()
    {
        await using AppDbContext db = CreateDb();
        int connectionId = await SeedConnectionAsync(db, "owner");
        var requestedUris = new List<string>();
        SpreadsheetReaderService reader = CreateReader(db, requestedUris);

        await reader.GetSpreadsheetBudgetReportAsync("owner", "file-1", "Budget.xlsx", connectionId);

        Assert.Contains(requestedUris, uri => uri.Contains(OwnerApiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GoogleDriveApi_UsesAuthenticatedAccount()
    {
        var driveService = new RecordingDriveConnectionService();
        var controller = new ApiGoogleDriveController(driveService, new NoOpSpreadsheetReader(),
            NullLogger<ApiGoogleDriveController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContextFor("account-a") }
        };

        await controller.GetConnections();
        await controller.GetConnectionFiles(7);
        await controller.Delete(7);
        await controller.Sync(7, new SyncGoogleDriveRequestDto());
        await controller.Reauth(7, new ReauthGoogleDriveRequestDto { ApiKey = "new-key" });
        await controller.Connect(new ConnectGoogleDriveRequestDto { FolderUrl = "https://drive.google.com/drive/folders/abc" });

        Assert.Equal(6, driveService.UserIds.Count);
        Assert.All(driveService.UserIds, id => Assert.Equal("account-a", id));
    }

    private static AppDbContext CreateDb()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AccountIsolation_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<int> SeedConnectionAsync(AppDbContext db, string ownerUserId)
    {
        var connection = new GoogleDriveConnection
        {
            UserId = ownerUserId,
            FolderId = "folder-1",
            FolderUrl = "https://drive.google.com/drive/folders/folder-1",
            EncryptedApiKey = CredentialProtector.Encrypt(OwnerApiKey),
            CachedFiles =
            {
                new GoogleDriveCachedFile { DriveFileId = "file-1", Name = "Budget.xlsx", MimeType = "application/vnd.ms-excel" }
            }
        };
        db.GoogleDriveConnections.Add(connection);
        await db.SaveChangesAsync();
        return connection.Id;
    }

    private static SpreadsheetReaderService CreateReader(AppDbContext db, List<string> requestedUris)
    {
        var handler = new DelegatingTestHandler((request, _) =>
        {
            requestedUris.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var fileSource = new GoogleDriveFileSource(new HttpClient(handler), new ConfigurationBuilder().Build(), db,
            NullLogger<GoogleDriveFileSource>.Instance);
        return new SpreadsheetReaderService(fileSource, NullLogger<SpreadsheetReaderService>.Instance);
    }

    private static DefaultHttpContext HttpContextFor(string userId)
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Test"))
        };
    }

    private sealed class RecordingDriveConnectionService : IDriveConnectionService
    {
        public List<string> UserIds { get; } = new();

        public Task<GoogleDriveFolderResponseDto> GetFolderFilesAsync(GoogleDriveRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GoogleDriveFolderResponseDto());

        public Task<List<GoogleDriveConnectionDto>> GetUserConnectionsAsync(string userId,
            CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(new List<GoogleDriveConnectionDto>());
        }

        public Task<GoogleDriveFolderResponseDto> GetConnectionFilesAsync(int connectionId, string userId,
            bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(new GoogleDriveFolderResponseDto { Success = true });
        }

        public Task<GoogleDriveConnectionDto> ConnectDriveAsync(string userId, ConnectGoogleDriveRequestDto request,
            CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(new GoogleDriveConnectionDto());
        }

        public Task<GoogleDriveFolderResponseDto> SyncConnectionAsync(int connectionId, string userId,
            bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(new GoogleDriveFolderResponseDto { Success = true });
        }

        public Task<GoogleDriveConnectionDto> ReauthConnectionAsync(int connectionId, string userId, string? newApiKey,
            CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(new GoogleDriveConnectionDto());
        }

        public Task<bool> DeleteConnectionAsync(int connectionId, string userId,
            CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(true);
        }
    }

    private sealed class NoOpSpreadsheetReader : ISpreadsheetReader
    {
        public Task<PersonalFinance.Shared.DTOs.SpreadsheetAnalysis.MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(string userId, string fileId, string? fileName = null, int? connectionId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersonalFinance.Shared.DTOs.SpreadsheetAnalysis.SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(string userId, string fileId, string? fileName = null, int? connectionId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
