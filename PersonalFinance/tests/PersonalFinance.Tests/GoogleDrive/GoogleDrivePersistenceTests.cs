using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Data.Helpers;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using Xunit;

namespace PersonalFinance.Tests.GoogleDrive;

public class GoogleDrivePersistenceTests
{
    private static AppDbContext CreateTestDbContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"GoogleDriveTest_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task ConnectDriveAsync_FirstTimeSetup_PersistsConnectionAndCachesFiles()
    {
        // Arrange
        using AppDbContext dbContext = CreateTestDbContext();
        IConfigurationRoot config = new ConfigurationBuilder().Build();
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            string sampleJson = @"{ ""files"": [ { ""id"": ""file-1"", ""name"": ""TestDocument.pdf"", ""mimeType"": ""application/pdf"", ""size"": ""1024"" } ] }";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sampleJson, System.Text.Encoding.UTF8, "application/json")
            });
        });
        var httpClient = new HttpClient(handler);
        var service = new DriveConnectionService(httpClient, config, dbContext, NullLogger<DriveConnectionService>.Instance);

        var request = new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ConnectionName = "My Financial Docs",
            ApiKey = "AIzaSyTestApiKey123456"
        };

        // Act
        GoogleDriveConnectionDto result = await service.ConnectDriveAsync("user-101", request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("user-101", result.UserId);
        Assert.Equal("127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", result.FolderId);
        Assert.True(result.HasApiKey);
        Assert.StartsWith("AIza", result.MaskedApiKey!);
        Assert.True(result.IsValid);
        Assert.Equal("Synced", result.SyncStatus);
        Assert.NotNull(result.LastSyncedAtUtc);
        Assert.True(result.CachedFilesCount > 0);

        // Verify in database
        GoogleDriveConnection? dbConnection = await dbContext.GoogleDriveConnections
            .Include(c => c.CachedFiles)
            .FirstOrDefaultAsync(c => c.Id == result.Id);

        Assert.NotNull(dbConnection);
        Assert.Equal("user-101", dbConnection.UserId);
        Assert.NotEmpty(dbConnection.CachedFiles);
        Assert.False(string.IsNullOrEmpty(dbConnection.EncryptedApiKey));
        // Verify key is encrypted in database
        Assert.NotEqual("AIzaSyTestApiKey123456", dbConnection.EncryptedApiKey);
        Assert.Equal("AIzaSyTestApiKey123456", CredentialProtector.Decrypt(dbConnection.EncryptedApiKey));
    }

    [Fact]
    public async Task SubsequentLogin_LoadsSavedConfigurationAndSkipsSetupFlow()
    {
        // Arrange
        using AppDbContext dbContext = CreateTestDbContext();
        IConfigurationRoot config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new DriveConnectionService(httpClient, config, dbContext, NullLogger<DriveConnectionService>.Instance);

        // First setup
        GoogleDriveConnectionDto conn = await service.ConnectDriveAsync("user-subsequent", new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ConnectionName = "Saved Drive"
        });

        // Act - On subsequent login, retrieve saved connections
        List<GoogleDriveConnectionDto> connections = await service.GetUserConnectionsAsync("user-subsequent");

        // Assert
        Assert.Single(connections);
        Assert.Equal(conn.Id, connections[0].Id);
        Assert.Equal("Saved Drive", connections[0].Name);
        Assert.True(connections[0].CachedFilesCount > 0);
    }

    [Fact]
    public async Task SupportMultipleGoogleDrives_PerUser_WithoutReplacingExisting()
    {
        // Arrange
        using AppDbContext dbContext = CreateTestDbContext();
        IConfigurationRoot config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new DriveConnectionService(httpClient, config, dbContext, NullLogger<DriveConnectionService>.Instance);

        // Connect Drive 1
        GoogleDriveConnectionDto drive1 = await service.ConnectDriveAsync("user-multi", new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ConnectionName = "Drive 1 - Financial"
        });

        // Connect Drive 2 (Add another Drive)
        GoogleDriveConnectionDto drive2 = await service.ConnectDriveAsync("user-multi", new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "1ABCdEfGhIjKlMnOpQrStUvWxYz12345_",
            ConnectionName = "Drive 2 - Receipts"
        });

        // Act
        List<GoogleDriveConnectionDto> allConnections = await service.GetUserConnectionsAsync("user-multi");

        // Assert
        Assert.Equal(2, allConnections.Count);
        Assert.Contains(allConnections, c => c.Id == drive1.Id && c.Name == "Drive 1 - Financial");
        Assert.Contains(allConnections, c => c.Id == drive2.Id && c.Name == "Drive 2 - Receipts");
    }

    [Fact]
    public async Task GetConnectionFilesAsync_UsesDatabaseCache_AndAvoidsGoogleApiRequests()
    {
        // Arrange
        using AppDbContext dbContext = CreateTestDbContext();
        IConfigurationRoot config = new ConfigurationBuilder().Build();

        int apiCallCount = 0;
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            apiCallCount++;
            string sampleJson = @"{ ""files"": [ { ""id"": ""file-1"", ""name"": ""Budget.xlsx"", ""mimeType"": ""application/vnd.google-apps.spreadsheet"" } ] }";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sampleJson, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var httpClient = new HttpClient(handler);
        var service = new DriveConnectionService(httpClient, config, dbContext, NullLogger<DriveConnectionService>.Instance);

        // Initial setup calls API once to cache
        GoogleDriveConnectionDto conn = await service.ConnectDriveAsync("user-cache", new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyTest"
        });

        int callsAfterSetup = apiCallCount;
        Assert.True(callsAfterSetup > 0);

        // Act - Request files multiple times (subsequent requests within TTL)
        GoogleDriveFolderResponseDto cached1 = await service.GetConnectionFilesAsync(conn.Id, "user-cache", forceRefresh: false);
        GoogleDriveFolderResponseDto cached2 = await service.GetConnectionFilesAsync(conn.Id, "user-cache", forceRefresh: false);

        // Assert - Zero additional API requests were made; served strictly from database cache!
        Assert.Equal(callsAfterSetup, apiCallCount);
        Assert.True(cached1.Success);
        Assert.True(cached2.Success);
        Assert.NotEmpty(cached1.Files);
    }

    [Fact]
    public async Task SyncConnectionAsync_PerformsIncrementalSync_WhenDriveFilesChange()
    {
        // Arrange
        using AppDbContext dbContext = CreateTestDbContext();
        IConfigurationRoot config = new ConfigurationBuilder().Build();

        string filesResponse1 = @"{ ""files"": [
            { ""id"": ""f1"", ""name"": ""OldName.pdf"", ""mimeType"": ""application/pdf"", ""size"": ""1000"" },
            { ""id"": ""f2"", ""name"": ""DeletedSoon.docx"", ""mimeType"": ""application/docx"", ""size"": ""2000"" }
        ] }";

        string filesResponse2 = @"{ ""files"": [
            { ""id"": ""f1"", ""name"": ""NewNameUpdated.pdf"", ""mimeType"": ""application/pdf"", ""size"": ""1500"" },
            { ""id"": ""f3"", ""name"": ""NewlyAdded.xlsx"", ""mimeType"": ""application/vnd.google-apps.spreadsheet"", ""size"": ""3000"" }
        ] }";

        bool returnSecondBatch = false;
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            string content = returnSecondBatch ? filesResponse2 : filesResponse1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var httpClient = new HttpClient(handler);
        var service = new DriveConnectionService(httpClient, config, dbContext, NullLogger<DriveConnectionService>.Instance);

        GoogleDriveConnectionDto conn = await service.ConnectDriveAsync("user-sync", new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyTest"
        });

        GoogleDriveConnection dbConnInitial = await dbContext.GoogleDriveConnections.Include(c => c.CachedFiles).FirstAsync(c => c.Id == conn.Id);
        Assert.Equal(2, dbConnInitial.CachedFiles.Count);

        // Act - Switch to second response and run manual sync
        returnSecondBatch = true;
        GoogleDriveFolderResponseDto syncResult = await service.SyncConnectionAsync(conn.Id, "user-sync", forceRefresh: true);

        // Assert
        Assert.True(syncResult.Success);
        Assert.Equal(2, syncResult.Files.Count);

        GoogleDriveConnection dbConnAfter = await dbContext.GoogleDriveConnections.Include(c => c.CachedFiles).FirstAsync(c => c.Id == conn.Id);

        // f1 updated
        GoogleDriveCachedFile? f1 = dbConnAfter.CachedFiles.FirstOrDefault(f => f.DriveFileId == "f1");
        Assert.NotNull(f1);
        Assert.Equal("NewNameUpdated.pdf", f1.Name);
        Assert.Equal(1500, f1.Size);

        // f2 deleted from cache
        GoogleDriveCachedFile? f2 = dbConnAfter.CachedFiles.FirstOrDefault(f => f.DriveFileId == "f2");
        Assert.Null(f2);

        // f3 inserted
        GoogleDriveCachedFile? f3 = dbConnAfter.CachedFiles.FirstOrDefault(f => f.DriveFileId == "f3");
        Assert.NotNull(f3);
        Assert.Equal("NewlyAdded.xlsx", f3.Name);
    }

    [Fact]
    public async Task DeleteConnectionAsync_RemovesConnectionAndCascadeDeletesCachedFiles()
    {
        // Arrange
        using AppDbContext dbContext = CreateTestDbContext();
        IConfigurationRoot config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new DriveConnectionService(httpClient, config, dbContext, NullLogger<DriveConnectionService>.Instance);

        GoogleDriveConnectionDto conn = await service.ConnectDriveAsync("user-delete", new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_"
        });

        Assert.True(await dbContext.GoogleDriveConnections.AnyAsync(c => c.Id == conn.Id));
        Assert.True(await dbContext.GoogleDriveCachedFiles.AnyAsync(f => f.ConnectionId == conn.Id));

        // Act
        bool deleted = await service.DeleteConnectionAsync(conn.Id, "user-delete");

        // Assert
        Assert.True(deleted);
        // Soft delete: connection is marked IsDeleted, EF Core filter excludes it from normal queries
        Assert.True(await dbContext.GoogleDriveConnections.IgnoreQueryFilters().AnyAsync(c => c.Id == conn.Id && c.IsDeleted));
        // Cached files are cascade-deleted by EF Core relationship (hard delete)
        Assert.False(await dbContext.GoogleDriveCachedFiles.AnyAsync(f => f.ConnectionId == conn.Id));
    }

    [Fact]
    public async Task ReauthConnectionAsync_HandlesInvalidCredentials_AndReauthenticatesSuccessfully()
    {
        // Arrange
        using AppDbContext dbContext = CreateTestDbContext();
        IConfigurationRoot config = new ConfigurationBuilder().Build();

        bool returnUnauthorized = true;
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            if (returnUnauthorized)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    ReasonPhrase = "Unauthorized"
                });
            }

            string sampleJson = @"{ ""files"": [ { ""id"": ""f-auth"", ""name"": ""Authed.pdf"", ""mimeType"": ""application/pdf"" } ] }";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sampleJson, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var httpClient = new HttpClient(handler);
        var service = new DriveConnectionService(httpClient, config, dbContext, NullLogger<DriveConnectionService>.Instance);

        // Connect with invalid credentials
        GoogleDriveConnectionDto conn = await service.ConnectDriveAsync("user-auth", new ConnectGoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "ExpiredApiKey"
        });

        // Check connection marked as NeedsReauth
        GoogleDriveConnection dbConn = await dbContext.GoogleDriveConnections.FirstAsync(c => c.Id == conn.Id);
        Assert.False(dbConn.IsValid);
        Assert.Equal("NeedsReauth", dbConn.SyncStatus);
        Assert.NotNull(dbConn.ErrorMessage);

        // Act - User provides valid credentials via Reauth
        returnUnauthorized = false;
        GoogleDriveConnectionDto reauthResult = await service.ReauthConnectionAsync(conn.Id, "user-auth", "FreshValidApiKey");

        // Assert
        Assert.True(reauthResult.IsValid);
        Assert.Equal("Synced", reauthResult.SyncStatus);
        Assert.Null(reauthResult.ErrorMessage);

        GoogleDriveConnection dbConnAfter = await dbContext.GoogleDriveConnections.Include(c => c.CachedFiles).FirstAsync(c => c.Id == conn.Id);
        Assert.True(dbConnAfter.IsValid);
        Assert.Equal("Synced", dbConnAfter.SyncStatus);
        Assert.Equal("FreshValidApiKey", CredentialProtector.Decrypt(dbConnAfter.EncryptedApiKey));
        Assert.NotEmpty(dbConnAfter.CachedFiles);
    }

    [Fact]
    public void CredentialProtector_EncryptsAndMasksCredentialsSecurely()
    {
        string rawKey = "AIzaSyTestGoogleDriveApiKey987654";
        string? encrypted = CredentialProtector.Encrypt(rawKey);

        Assert.NotNull(encrypted);
        Assert.NotEqual(rawKey, encrypted);

        string? decrypted = CredentialProtector.Decrypt(encrypted);
        Assert.Equal(rawKey, decrypted);

        string? masked = CredentialProtector.Mask(rawKey);
        Assert.StartsWith("AIza", masked!);
        Assert.EndsWith("7654", masked!);
        Assert.DoesNotContain("TestGoogleDrive", masked!);
    }

    // Ciphertext produced by the current CredentialProtector scheme for a dummy plaintext.
    // Credentials already stored in the database use this scheme, so it must keep decrypting
    // until the re-encryption migration in #133 lands.
    private const string LegacyPlaintext = "AIzaSyLegacyStoredKey_0123456789";
    private const string LegacyCiphertext = "kvBlKu1EuwUG6tnNbdEd6gB3/EKyLyMMTLERE64Xe7tpy125d5cMl7mzS0KF+izr";

    [Fact]
    public void CredentialProtector_DecryptsCiphertextStoredByCurrentScheme()
    {
        Assert.Equal(LegacyPlaintext, CredentialProtector.Decrypt(LegacyCiphertext));
    }

    [Fact]
    public void CredentialProtector_EncryptIsStableForStoredCredentials()
    {
        Assert.Equal(LegacyCiphertext, CredentialProtector.Encrypt(LegacyPlaintext));
    }
}
