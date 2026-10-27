using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Analysis;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using ApiErrorEntryDto = PersonalFinance.Shared.Contracts.ApiErrorEntryDto;
using ISpreadsheetAnalysisApi = PersonalFinance.Shared.Contracts.ISpreadsheetAnalysisApi;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Tests.BudgetTracker;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using Keys = PersonalFinance.Shared.Constants.ResultMessageKeys;

namespace PersonalFinance.Tests.Analysis;

/// <summary>
/// Why a spreadsheet sync failed is keyed (#405), and the Analysis page shows it in the reader's language.
/// </summary>
public class SyncResultMessageTests
{
    private const string UserId = "sync-user";

    [Fact]
    public async Task Sync_OfAnotherUsersConnection_FailsWithAKey()
    {
        await using AppDbContext db = CreateDb();
        SpreadsheetAnalysisService service = CreateService(db, new MonthlyBudgetReportDto());

        SyncSpreadsheetBudgetResult result = await service.SyncSpreadsheetBudgetAsync(UserId, 999, "file-1",
            "Budget.xlsx", overwrite: false);

        Assert.False(result.Success);
        Assert.Equal(Keys.Analysis.ConnectionNotFound, result.Error?.Key);
        Assert.Equal(result.ErrorMessage, result.Error?.Message);
    }

    [Fact]
    public async Task Sync_OfASheetWithoutMonths_AndNoReason_FailsWithAKey()
    {
        await using AppDbContext db = CreateDb();
        int connectionId = await AddConnectionAsync(db);
        SpreadsheetAnalysisService service = CreateService(db, new MonthlyBudgetReportDto());

        SyncSpreadsheetBudgetResult result = await service.SyncSpreadsheetBudgetAsync(UserId, connectionId, "file-1",
            "Budget.xlsx", overwrite: false);

        Assert.Equal(Keys.Analysis.NoBudgetData, result.Error?.Key);
    }

    [Fact]
    public async Task Sync_OfMonthsAlreadySaved_AsksToConfirmWithAKey()
    {
        await using AppDbContext db = CreateDb();
        int connectionId = await AddConnectionAsync(db);
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId, ConnectionId = connectionId, FileId = "file-1", FileName = "Budget.xlsx", Year = 2026,
            MonthNumber = 1, MonthName = "January"
        });
        await db.SaveChangesAsync();
        var report = new MonthlyBudgetReportDto
        {
            Year = 2026,
            Months = [new MonthlyBudgetMonthSummaryDto { MonthNumber = 1, MonthName = "January", ActualIncome = 100m }]
        };
        SpreadsheetAnalysisService service = CreateService(db, report);

        SyncSpreadsheetBudgetResult result = await service.SyncSpreadsheetBudgetAsync(UserId, connectionId, "file-1",
            "Budget.xlsx", overwrite: false);

        Assert.True(result.HasConflict);
        Assert.Equal(Keys.Analysis.MonthsExist, result.Error?.Key);
    }

    [Fact]
    public async Task Sync_Failure_ReportsInVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        AnalysisController controller = CreateController(Failed(Keys.Analysis.ReadFailed, "The spreadsheet could not be read."));

        await controller.Sync(1, "file-1", "Budget.xlsx");

        Assert.Equal(TestLocalization.Vietnamese(Keys.Analysis.ReadFailed), controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task ConfirmOverwrite_Failure_ReportsInVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        AnalysisController controller = CreateController(Failed(Keys.Analysis.SyncFailed, "We couldn't sync your spreadsheet."));

        await controller.ConfirmOverwrite(1, "file-1", "Budget.xlsx");

        Assert.Equal(TestLocalization.Vietnamese(Keys.Analysis.SyncFailed), controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Sync_FailingWithTheSheetsOwnReason_ReportsThatReasonInVietnamese()
    {
        using var culture = new RequestCultureScope("vi-VN");
        AnalysisController controller =
            CreateController(Failed(Keys.GoogleDrive.NoBudgetTable, DriveResultMessages.NoBudgetTable.Message));

        await controller.Sync(1, "file-1", "Budget.xlsx");

        Assert.Equal(TestLocalization.Vietnamese(Keys.GoogleDrive.NoBudgetTable), controller.TempData["ErrorMessage"]);
    }

    private static SyncSpreadsheetResponseDto Failed(string key, string english) => new()
    {
        Success = false,
        ErrorMessage = english,
        Error = new ApiErrorEntryDto { Key = key, Message = english }
    };

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SyncResultMessages_{Guid.NewGuid():N}")
        .Options);

    private static async Task<int> AddConnectionAsync(AppDbContext db)
    {
        var connection = new GoogleDriveConnection { UserId = UserId, FolderId = "folder-1", FolderUrl = "folder-1" };
        db.GoogleDriveConnections.Add(connection);
        await db.SaveChangesAsync();
        return connection.Id;
    }

    private static SpreadsheetAnalysisService CreateService(AppDbContext db, MonthlyBudgetReportDto report) =>
        new(db, new FixedReader(report), new NoNotifications(), TestBudgetDataProviderFactory.Create(db),
            NullLogger<SpreadsheetAnalysisService>.Instance);

    private static AnalysisController CreateController(SyncSpreadsheetResponseDto syncResult)
    {
        var httpContext = new DefaultHttpContext();
        return new AnalysisController(new FailingSyncApi(syncResult), new FakeBudgetTrackerApi(),
            NullLogger<AnalysisController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private sealed class FixedReader(MonthlyBudgetReportDto report) : ISpreadsheetReader
    {
        public Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(string userId, string fileId,
            string? fileName = null, int? connectionId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(report);

        public Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(string userId, string fileId,
            string? fileName = null, int? connectionId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpreadsheetTransactionsDto());
    }

    private sealed class NoNotifications : INotificationService
    {
        public Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<List<NotificationDto>> GetNotificationsAsync(string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<NotificationDto>());

        public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> MarkAsReadAsync(string userId, int notificationId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FailingSyncApi(SyncSpreadsheetResponseDto syncResult) : ISpreadsheetAnalysisApi
    {
        public Task<AnalysisDashboardDto> GetAnalysisDashboardAsync(int? year = null, int? month = null,
            CancellationToken cancellationToken = default) => Task.FromResult(new AnalysisDashboardDto());

        public Task<SyncSpreadsheetResponseDto> SyncSpreadsheetAsync(SyncSpreadsheetRequestDto request,
            CancellationToken cancellationToken = default) => Task.FromResult(syncResult);

        public Task<BudgetDataSourceSummaryDto> RemoveSyncedDataAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BudgetDataSourceSummaryDto());

        public Task<List<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<int>());

        public Task<List<int>> GetAvailableMonthsAsync(int year, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<int>());
    }
}
