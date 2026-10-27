using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Analysis;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
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
            DriveResultMessages.DownloadFailed);
        var service = new SpreadsheetAnalysisService(db, new FixedReportReader(unreadable), new NoOpNotifications(),
            TestBudgetDataProviderFactory.Create(db), NullLogger<SpreadsheetAnalysisService>.Instance);

        SyncSpreadsheetBudgetResult result = await service.SyncSpreadsheetBudgetAsync(UserId, connection.Id, "file-1",
            "Monthly budget 2026.xlsx", overwrite: true);

        Assert.False(result.Success);
        Assert.Equal(DriveResultMessages.DownloadFailed.Message, result.ErrorMessage);
        Assert.Equal(ResultMessageKeys.GoogleDrive.DownloadFailed, result.Error?.Key);
        Assert.Equal(0, result.SnapshotsCreated);
        Assert.Empty(await db.SpreadsheetBudgetSnapshots.ToListAsync());
    }

    [Fact]
    public async Task Sync_WhenItFails_NotifiesWithAGenericMessageAndNeverTheExceptionText()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SpreadsheetSync_{Guid.NewGuid():N}")
            .Options);
        var connection = new GoogleDriveConnection { UserId = UserId, FolderId = "folder-1", FolderUrl = "folder-1" };
        db.GoogleDriveConnections.Add(connection);
        await db.SaveChangesAsync();

        var report = new MonthlyBudgetReportDto
        {
            FileName = "Monthly budget 2026.xlsx",
            Year = 2026,
            Months = [new MonthlyBudgetMonthSummaryDto { MonthNumber = 1, MonthName = "January", ActualIncome = 100m }]
        };
        // The success notification throws, which sends the sync down its failure path.
        var notifications = new RecordingNotifications(failFirst: new InvalidOperationException(SecretDetail));
        var service = new SpreadsheetAnalysisService(db, new FixedReportReader(report), notifications,
            TestBudgetDataProviderFactory.Create(db), NullLogger<SpreadsheetAnalysisService>.Instance);

        SyncSpreadsheetBudgetResult result = await service.SyncSpreadsheetBudgetAsync(UserId, connection.Id, "file-1",
            report.FileName, overwrite: true);

        Assert.False(result.Success);
        Assert.DoesNotContain(SecretDetail, result.ErrorMessage ?? string.Empty);
        CreateNotificationRequestDto failure = Assert.Single(notifications.Created);
        Assert.Equal("Notifications.SyncFailed.Title", failure.TitleKey);
        Assert.Equal("Notifications.SyncFailed.Message", failure.MessageKey);
        Assert.DoesNotContain(SecretDetail, failure.Message);
        Assert.True(failure.Arguments is null || failure.Arguments.Values.All(v => !v.Contains(SecretDetail)));
    }

    [Fact]
    public async Task Sync_WhenTheSpreadsheetReaderThrows_FailsWithoutTheExceptionText()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SpreadsheetSync_{Guid.NewGuid():N}")
            .Options);
        var connection = new GoogleDriveConnection { UserId = UserId, FolderId = "folder-1", FolderUrl = "folder-1" };
        db.GoogleDriveConnections.Add(connection);
        await db.SaveChangesAsync();

        var service = new SpreadsheetAnalysisService(db, new ThrowingReader(new HttpRequestException(SecretDetail)),
            new NoOpNotifications(), TestBudgetDataProviderFactory.Create(db),
            NullLogger<SpreadsheetAnalysisService>.Instance);

        SyncSpreadsheetBudgetResult result = await service.SyncSpreadsheetBudgetAsync(UserId, connection.Id, "file-1",
            "Monthly budget 2026.xlsx", overwrite: true);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.DoesNotContain(SecretDetail, result.ErrorMessage);
    }

    private const string SecretDetail = "Server=sql-prod;Password=hunter2";

    private sealed class ThrowingReader(Exception exception) : ISpreadsheetReader
    {
        public Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(string userId, string fileId,
            string? fileName = null, int? connectionId = null, CancellationToken cancellationToken = default) =>
            Task.FromException<MonthlyBudgetReportDto>(exception);

        public Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(string userId, string fileId,
            string? fileName = null, int? connectionId = null, CancellationToken cancellationToken = default) =>
            Task.FromException<SpreadsheetTransactionsDto>(exception);
    }

    private sealed class RecordingNotifications(Exception failFirst) : INotificationService
    {
        private bool _failed;

        public List<CreateNotificationRequestDto> Created { get; } = [];

        public Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw failFirst;
            }

            Created.Add(request);
            return Task.FromResult(Created.Count);
        }

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
