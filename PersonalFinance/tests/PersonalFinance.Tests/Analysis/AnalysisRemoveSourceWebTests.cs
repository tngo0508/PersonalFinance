using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.BudgetTracker;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.Analysis;

namespace PersonalFinance.Tests.Analysis;

/// <summary>
/// The Analysis page's Data Source Removal: which sources it offers, the confirmation page, and the typed
/// confirmation the Budget Tracker needs.
/// </summary>
public class AnalysisRemoveSourceWebTests
{
    private static readonly BudgetDataSourceSummaryDto SheetWithData =
        new() { Source = BudgetDataSource.GoogleSheet, MonthCount = 4, TransactionCount = 120 };

    private static readonly BudgetDataSourceSummaryDto TrackerWithData =
        new() { Source = BudgetDataSource.BudgetTracker, MonthCount = 2, TransactionCount = 7 };

    private static readonly BudgetDataSourceSummaryDto EmptySheet = new() { Source = BudgetDataSource.GoogleSheet };
    private static readonly BudgetDataSourceSummaryDto EmptyTracker = new() { Source = BudgetDataSource.BudgetTracker };

    private static AnalysisController CreateController(FakeSpreadsheetAnalysisApi analysisApi,
        FakeBudgetTrackerApi trackerApi)
    {
        var httpContext = new DefaultHttpContext();
        return new AnalysisController(analysisApi, trackerApi, NullLogger<AnalysisController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    [Fact]
    public async Task Index_OffersRemoval_OnlyForSourcesWithData()
    {
        var analysisApi = new FakeSpreadsheetAnalysisApi
        {
            Dashboard = new AnalysisDashboardDto { SourceSummaries = [EmptySheet, TrackerWithData] }
        };
        AnalysisController controller = CreateController(analysisApi, new FakeBudgetTrackerApi());

        IActionResult result = await controller.Index(null, null);

        AnalysisDashboardViewModel model =
            Assert.IsType<AnalysisDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal([BudgetDataSource.BudgetTracker], model.RemovableSources);
    }

    [Fact]
    public async Task RemoveSource_Get_ShowsTheCountsToBeRemoved()
    {
        var trackerApi = new FakeBudgetTrackerApi { DataSourceSummaries = [SheetWithData, TrackerWithData] };
        AnalysisController controller = CreateController(new FakeSpreadsheetAnalysisApi(), trackerApi);

        IActionResult result = await controller.RemoveSource(BudgetDataSource.GoogleSheet);

        RemoveSourceViewModel model = Assert.IsType<RemoveSourceViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(4, model.MonthCount);
        Assert.Equal(120, model.TransactionCount);
        Assert.False(model.RequiresTypedConfirmation);
    }

    [Fact]
    public async Task RemoveSource_Get_ForASourceWithoutData_RedirectsWithAnError()
    {
        var trackerApi = new FakeBudgetTrackerApi { DataSourceSummaries = [SheetWithData, EmptyTracker] };
        AnalysisController controller = CreateController(new FakeSpreadsheetAnalysisApi(), trackerApi);

        IActionResult result = await controller.RemoveSource(BudgetDataSource.BudgetTracker);

        Assert.Equal(nameof(AnalysisController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("There is no Budget Tracker data to remove.", controller.TempData["ErrorMessage"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("remove")]
    [InlineData("REMOVE IT")]
    public async Task RemoveSource_Post_BudgetTrackerWithoutTheWord_RedisplaysAndRemovesNothing(string? typed)
    {
        var trackerApi = new FakeBudgetTrackerApi { DataSourceSummaries = [SheetWithData, TrackerWithData] };
        AnalysisController controller = CreateController(new FakeSpreadsheetAnalysisApi(), trackerApi);

        IActionResult result = await controller.RemoveSourceConfirmed(
            new RemoveSourceViewModel { Source = BudgetDataSource.BudgetTracker, Confirmation = typed });

        RemoveSourceViewModel model = Assert.IsType<RemoveSourceViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(7, model.TransactionCount);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(0, trackerApi.RemoveDataCalls);
    }

    [Fact]
    public async Task RemoveSource_Post_BudgetTrackerWithTheWord_RemovesAndReportsTheCounts()
    {
        var analysisApi = new FakeSpreadsheetAnalysisApi();
        var trackerApi = new FakeBudgetTrackerApi { DataSourceSummaries = [SheetWithData, TrackerWithData] };
        AnalysisController controller = CreateController(analysisApi, trackerApi);

        IActionResult result = await controller.RemoveSourceConfirmed(
            new RemoveSourceViewModel { Source = BudgetDataSource.BudgetTracker, Confirmation = " REMOVE " });

        Assert.Equal(nameof(AnalysisController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(1, trackerApi.RemoveDataCalls);
        Assert.Equal(0, analysisApi.RemoveSyncedDataCalls);
        Assert.Equal("Removed your Budget Tracker data: 2 month(s) and 7 transaction(s).",
            controller.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task RemoveSource_Post_GoogleSheet_NeedsNoTypedWord()
    {
        var analysisApi = new FakeSpreadsheetAnalysisApi { Removed = SheetWithData };
        var trackerApi = new FakeBudgetTrackerApi();
        AnalysisController controller = CreateController(analysisApi, trackerApi);

        IActionResult result = await controller.RemoveSourceConfirmed(
            new RemoveSourceViewModel { Source = BudgetDataSource.GoogleSheet });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, analysisApi.RemoveSyncedDataCalls);
        Assert.Equal(0, trackerApi.RemoveDataCalls);
    }

    [Fact]
    public async Task RemoveSource_Post_UnknownSource_RemovesNothing()
    {
        var analysisApi = new FakeSpreadsheetAnalysisApi();
        var trackerApi = new FakeBudgetTrackerApi();
        AnalysisController controller = CreateController(analysisApi, trackerApi);

        IActionResult result = await controller.RemoveSourceConfirmed(
            new RemoveSourceViewModel { Source = (BudgetDataSource)99, Confirmation = "REMOVE" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(0, analysisApi.RemoveSyncedDataCalls);
        Assert.Equal(0, trackerApi.RemoveDataCalls);
    }

    [Fact]
    public void RemoveSource_Post_IsAntiforgeryProtected()
    {
        MethodInfo method = typeof(AnalysisController).GetMethod(nameof(AnalysisController.RemoveSourceConfirmed))!;

        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    private sealed class FakeSpreadsheetAnalysisApi : ISpreadsheetAnalysisApi
    {
        public AnalysisDashboardDto Dashboard { get; init; } = new();
        public BudgetDataSourceSummaryDto Removed { get; init; } = new() { Source = BudgetDataSource.GoogleSheet };
        public int RemoveSyncedDataCalls { get; private set; }

        public Task<AnalysisDashboardDto> GetAnalysisDashboardAsync(int? year = null, int? month = null,
            CancellationToken cancellationToken = default) => Task.FromResult(Dashboard);

        public Task<SyncSpreadsheetResponseDto> SyncSpreadsheetAsync(SyncSpreadsheetRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BudgetDataSourceSummaryDto> RemoveSyncedDataAsync(CancellationToken cancellationToken = default)
        {
            RemoveSyncedDataCalls++;
            return Task.FromResult(Removed);
        }

        public Task<List<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<int>> GetAvailableMonthsAsync(int year, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
