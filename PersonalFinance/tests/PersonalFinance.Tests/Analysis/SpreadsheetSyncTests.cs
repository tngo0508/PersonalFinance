using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;

namespace PersonalFinance.Tests.Analysis;

public class SpreadsheetSyncTests
{
    private const string UserId = "sync-user";

    [Fact]
    public async Task Sync_WhenSpreadsheetCannotBeRead_FailsWithReasonAndSavesNothing()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SpreadsheetSync_{Guid.NewGuid():N}")
            .Options);
        var connection = new GoogleDriveConnection { UserId = UserId, FolderId = "folder-1", FolderUrl = "folder-1" };
        db.GoogleDriveConnections.Add(connection);
        await db.SaveChangesAsync();

        MonthlyBudgetReportDto unreadable = BudgetSpreadsheetParser.Unavailable("Monthly budget 2026.xlsx", "file-1",
            "The spreadsheet could not be downloaded from Google Drive.");
        var service = new SpreadsheetAnalysisService(db, new FixedReportReader(unreadable), new NoOpNotifications(),
            TestBudgetDataProviderFactory.Create(db));

        SyncSpreadsheetBudgetResult result = await service.SyncSpreadsheetBudgetAsync(UserId, connection.Id, "file-1",
            "Monthly budget 2026.xlsx", overwrite: true);

        Assert.False(result.Success);
        Assert.Equal("The spreadsheet could not be downloaded from Google Drive.", result.ErrorMessage);
        Assert.Equal(0, result.SnapshotsCreated);
        Assert.Empty(await db.SpreadsheetBudgetSnapshots.ToListAsync());
    }

    private sealed class FixedReportReader(MonthlyBudgetReportDto report) : ISpreadsheetReader
    {
        public Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(string userId, string fileId,
            string? fileName = null, int? connectionId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(report);

        public Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(string userId, string fileId,
            string? fileName = null, int? connectionId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpreadsheetTransactionsDto());
    }

    private sealed class NoOpNotifications : INotificationService
    {
        public Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<List<NotificationDto>> GetNotificationsAsync(string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<NotificationDto>());

        public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> MarkAsReadAsync(string userId, int notificationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
