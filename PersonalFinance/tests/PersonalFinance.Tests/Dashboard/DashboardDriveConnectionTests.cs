using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;

namespace PersonalFinance.Tests.Dashboard;

/// <summary>
/// Behaviour of the Dashboard summary's <c>HasGoogleDriveConnection</c> flag, exercised against an in-memory database.
/// </summary>
public class DashboardDriveConnectionTests
{
    private const string UserId = "drive-user";
    private const string OtherUserId = "other-drive-user";

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardDrive_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Task<DashboardSummaryDto> GetSummaryAsync(AppDbContext db)
    {
        return new DashboardService(db, TestBudgetDataProviderFactory.Create(db), TestCurrencyConverter.Create(db)).GetDashboardSummaryAsync(UserId);
    }

    [Fact]
    public async Task UserWithConnection_IsConnected()
    {
        await using AppDbContext db = CreateContext();
        db.GoogleDriveConnections.Add(new GoogleDriveConnection { UserId = UserId, FolderId = "folder" });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.True(summary.HasGoogleDriveConnection);
    }

    [Fact]
    public async Task UserWithConnectionNeedingReauth_IsStillConnected()
    {
        await using AppDbContext db = CreateContext();
        db.GoogleDriveConnections.Add(new GoogleDriveConnection
        {
            UserId = UserId,
            FolderId = "folder",
            IsValid = false,
            SyncStatus = "NeedsReauth"
        });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.True(summary.HasGoogleDriveConnection);
    }

    [Fact]
    public async Task UserWithoutConnection_IsNotConnected()
    {
        await using AppDbContext db = CreateContext();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.False(summary.HasGoogleDriveConnection);
    }

    [Fact]
    public async Task AnotherUsersConnection_IsIgnored()
    {
        await using AppDbContext db = CreateContext();
        db.GoogleDriveConnections.Add(new GoogleDriveConnection { UserId = OtherUserId, FolderId = "folder" });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.False(summary.HasGoogleDriveConnection);
    }

    [Fact]
    public async Task SoftDeletedConnection_IsIgnored()
    {
        await using AppDbContext db = CreateContext();
        db.GoogleDriveConnections.Add(new GoogleDriveConnection
        {
            UserId = UserId,
            FolderId = "folder",
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.False(summary.HasGoogleDriveConnection);
    }
}
