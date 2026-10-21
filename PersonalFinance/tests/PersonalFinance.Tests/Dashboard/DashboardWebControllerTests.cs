using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Web.Controllers;
using Xunit;

namespace PersonalFinance.Tests.Dashboard;

public class DashboardWebControllerTests
{
    private static DashboardController CreateController(IDashboardApi api, ILogger<DashboardController> logger)
    {
        var identity = new ClaimsIdentity(new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-123"),
            new(ClaimTypes.Name, "test-user-123@example.com")
        }, "TestAuthType");

        return new DashboardController(api, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    [Fact]
    public async Task Index_WhenApiReturnsSummary_PassesSummaryToView()
    {
        var summary = new DashboardSummaryDto
        {
            NetWorth = new NetWorthSummaryDto { CurrentNetWorth = 125_000m, AnnualSavings = 18_000m },
            Budget = new BudgetSummaryDto { MonthlyIncome = 6_500m, Period = "2026-10" }
        };
        var logger = new RecordingLogger<DashboardController>();
        var controller = CreateController(new FakeDashboardApi { Summary = summary }, logger);

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DashboardSummaryDto>(viewResult.Model);
        Assert.Equal(125_000m, model.NetWorth!.CurrentNetWorth);
        Assert.Equal("2026-10", model.Budget!.Period);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Index_WhenApiFails_LogsFailureAndRendersEmptySummary()
    {
        var logger = new RecordingLogger<DashboardController>();
        var controller = CreateController(
            new FakeDashboardApi { Failure = new HttpRequestException("API unavailable") }, logger);

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DashboardSummaryDto>(viewResult.Model);
        Assert.Null(model.NetWorth);
        Assert.Null(model.Budget);
        Assert.Empty(model.SavingsGoals);
        Assert.Empty(model.RecentActivity);
        var entry = Assert.Single(logger.Entries, e => e.Level >= LogLevel.Warning);
        Assert.IsType<HttpRequestException>(entry.Exception);
    }

    [Fact]
    public async Task Index_WhenRequestIsCancelled_DoesNotSwallowCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var controller = CreateController(
            new FakeDashboardApi { Failure = new OperationCanceledException(cts.Token) },
            new RecordingLogger<DashboardController>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Index(cts.Token));
    }

    [Fact]
    public async Task Index_WhenApiTimesOutWithoutCallerCancellation_RendersEmptySummary()
    {
        var logger = new RecordingLogger<DashboardController>();
        var controller = CreateController(
            new FakeDashboardApi { Failure = new TaskCanceledException("The request timed out.") }, logger);

        var result = await controller.Index(CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DashboardSummaryDto>(viewResult.Model);
        Assert.Null(model.NetWorth);
        var entry = Assert.Single(logger.Entries, e => e.Level >= LogLevel.Warning);
        Assert.IsType<TaskCanceledException>(entry.Exception);
    }
}

public class FakeDashboardApi : IDashboardApi
{
    public DashboardSummaryDto Summary { get; set; } = new();
    public Exception? Failure { get; set; }

    public Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        return Failure is null ? Task.FromResult(Summary) : Task.FromException<DashboardSummaryDto>(Failure);
    }
}
