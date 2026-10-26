using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.BudgetTracker;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Tests.Web.Localization;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.Analysis;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Analysis;

/// <summary>
/// The Analysis page speaks the request's Language: its messages, the Money Flow labels, the status badges and the
/// month names. Category names and file names are the user's own and are never translated (spec §1).
/// </summary>
public class AnalysisLocalizationTests
{
    private const string English = """
        {
          "Analysis.Source.GoogleSheet": "Google Sheet",
          "Analysis.Source.BudgetTracker": "Budget Tracker",
          "Analysis.Message.Synced": "Synced {months} month(s) and {transactions} transactions.",
          "Analysis.Message.NothingToRemove": "There is no {source} data to remove.",
          "Analysis.Message.TypeToConfirm": "Type {word} to confirm.",
          "Analysis.Velocity.Week": "Week {number}",
          "Analysis.MoneyFlow.Income": "Income",
          "Analysis.MoneyFlow.Saved": "Saved",
          "Analysis.Status.OverBudget": "Over budget"
        }
        """;

    private const string Vietnamese = """
        {
          "Analysis.Source.GoogleSheet": "Google Trang tính",
          "Analysis.Source.BudgetTracker": "Theo dõi ngân sách",
          "Analysis.Message.Synced": "Đã đồng bộ {months} tháng và {transactions} giao dịch.",
          "Analysis.Message.NothingToRemove": "Không có dữ liệu {source} để xóa.",
          "Analysis.Message.TypeToConfirm": "Nhập {word} để xác nhận.",
          "Analysis.Velocity.Week": "Tuần {number}",
          "Analysis.MoneyFlow.Income": "Thu nhập",
          "Analysis.MoneyFlow.Saved": "Đã tiết kiệm",
          "Analysis.Status.OverBudget": "Vượt ngân sách"
        }
        """;

    private static readonly IStringLocalizer Localizer = LocalizationTestCatalog.Localizer(English, Vietnamese);

    [Fact]
    public void MoneyFlow_InVietnamese_LabelsItsOwnNodesButNotTheUsersCategories()
    {
        using var culture = new RequestCultureScope("vi-VN");

        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(1000m, 700m, ("Rent", 700m)), Localizer);

        Assert.NotNull(flow);
        Assert.Equal(["Thu nhập", "Rent", "Đã tiết kiệm"], flow.Nodes.Select(n => n.Label).ToList());
        Assert.Equal(["Rent", "Đã tiết kiệm"], flow.TableRows.Select(r => r.Name).ToList());
    }

    [Fact]
    public void MoneyFlow_MonthLabel_FollowsTheRequestCulture()
    {
        using var culture = new RequestCultureScope("vi-VN");

        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(1000m, 700m, ("Rent", 700m)), Localizer);

        Assert.NotNull(flow);
        CultureInfo vi = CultureInfo.GetCultureInfo("vi-VN");
        Assert.Equal(new DateTime(2026, 9, 1).ToString("Y", vi), flow.MonthLabel);
        Assert.Equal(vi.DateTimeFormat.GetMonthName(9), flow.MonthName);
    }

    [Fact]
    public void PeriodLabel_FollowsTheRequestCulture_AndIsNullWithoutAPeriod()
    {
        using var culture = new RequestCultureScope("vi-VN");

        Assert.Equal(new DateTime(2026, 3, 1).ToString("Y", CultureInfo.GetCultureInfo("vi-VN")),
            new AnalysisDashboardViewModel { SelectedYear = 2026, SelectedMonth = 3 }.PeriodLabel);
        Assert.Null(new AnalysisDashboardViewModel().PeriodLabel);
    }

    [Theory]
    [InlineData("Over Budget", "Vượt ngân sách")]
    [InlineData("Something new", "Something new")]
    public void Status_IsTranslatedWhenTheAppKnowsIt_OtherwiseShownAsSent(string status, string expected)
    {
        using var culture = new RequestCultureScope("vi-VN");

        Assert.Equal(expected, AnalysisText.Status(Localizer, status));
    }

    [Fact]
    public async Task RemoveSource_WithoutData_ExplainsInTheRequestLanguage()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var trackerApi = new FakeBudgetTrackerApi
        {
            DataSourceSummaries = [new BudgetDataSourceSummaryDto { Source = BudgetDataSource.BudgetTracker }]
        };
        AnalysisController controller = CreateController(new SyncingAnalysisApi(), trackerApi);

        await controller.RemoveSource(BudgetDataSource.BudgetTracker);

        Assert.Equal("Không có dữ liệu Theo dõi ngân sách để xóa.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task RemoveSource_WithoutTheTypedWord_ExplainsInTheRequestLanguage()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var trackerApi = new FakeBudgetTrackerApi
        {
            DataSourceSummaries =
            [
                new BudgetDataSourceSummaryDto { Source = BudgetDataSource.BudgetTracker, MonthCount = 1, TransactionCount = 2 }
            ]
        };
        AnalysisController controller = CreateController(new SyncingAnalysisApi(), trackerApi);

        await controller.RemoveSourceConfirmed(new RemoveSourceViewModel { Source = BudgetDataSource.BudgetTracker });

        Assert.Equal("Nhập REMOVE để xác nhận.",
            controller.ModelState[nameof(RemoveSourceViewModel.Confirmation)]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public async Task Sync_Success_ReportsInTheRequestLanguage()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var api = new SyncingAnalysisApi
        {
            SyncResult = new SyncSpreadsheetResponseDto { Success = true, SnapshotsCreated = 3, TransactionsCreated = 41 }
        };
        AnalysisController controller = CreateController(api, new FakeBudgetTrackerApi());

        await controller.Sync(1, "file-1", "Monthly budget.xlsx");

        Assert.Equal("Đã đồng bộ 3 tháng và 41 giao dịch.", controller.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task Index_NamesWeeksAndMonthsInTheRequestLanguage()
    {
        using var culture = new RequestCultureScope("vi-VN");
        var api = new SyncingAnalysisApi
        {
            Dashboard = new AnalysisDashboardDto
            {
                Year = 2026,
                Month = 3,
                Velocity = new SpendingVelocityDto
                {
                    WeeklyTrend = [new WeeklySpendingDto { WeekNumber = 2, WeekLabel = "Week 2", TotalSpent = 10m }]
                },
                Trends = new TrendDataDto
                {
                    TrendPoints = [new MonthlyTrendPointDto { MonthNumber = 3, MonthName = "March" }]
                }
            }
        };
        AnalysisController controller = CreateController(api, new FakeBudgetTrackerApi());

        IActionResult result = await controller.Index(2026, 3);

        AnalysisDashboardViewModel model =
            Assert.IsType<AnalysisDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Tuần 2", model.WeeklyTrend.Single().WeekLabel);
        Assert.Equal(CultureInfo.GetCultureInfo("vi-VN").DateTimeFormat.GetMonthName(3),
            model.TrendPoints.Single().MonthName);
    }

    private static CategoryInsightsDto Month(decimal income, decimal expenses,
        params (string Name, decimal Actual)[] categories) => new()
    {
        Year = 2026,
        MonthNumber = 9,
        MonthName = "September",
        ActualIncome = income,
        ActualExpenses = expenses,
        Categories = categories
            .Select(c => new CategoryBreakdownDto { CategoryName = c.Name, ActualAmount = c.Actual })
            .ToList()
    };

    private static AnalysisController CreateController(ISpreadsheetAnalysisApi analysisApi,
        IBudgetTrackerApi trackerApi)
    {
        var httpContext = new DefaultHttpContext();
        return new AnalysisController(analysisApi, trackerApi, NullLogger<AnalysisController>.Instance, Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private sealed class SyncingAnalysisApi : ISpreadsheetAnalysisApi
    {
        public AnalysisDashboardDto Dashboard { get; init; } = new();
        public SyncSpreadsheetResponseDto SyncResult { get; init; } = new();

        public Task<AnalysisDashboardDto> GetAnalysisDashboardAsync(int? year = null, int? month = null,
            CancellationToken cancellationToken = default) => Task.FromResult(Dashboard);

        public Task<SyncSpreadsheetResponseDto> SyncSpreadsheetAsync(SyncSpreadsheetRequestDto request,
            CancellationToken cancellationToken = default) => Task.FromResult(SyncResult);

        public Task<BudgetDataSourceSummaryDto> RemoveSyncedDataAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BudgetDataSourceSummaryDto());

        public Task<List<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<int>());

        public Task<List<int>> GetAvailableMonthsAsync(int year, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<int>());
    }
}
