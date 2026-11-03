using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.FinancialUniverse;

namespace PersonalFinance.Tests.FinancialUniverse;

/// <summary>
/// The Web page: the graph becomes the labelled view model, an unreachable ApiService shows the Unavailable State
/// (never an empty page), and a cancelled request is not swallowed.
/// </summary>
public class FinancialUniverseWebControllerTests
{
    [Fact]
    public async Task Index_BuildsTheListViewFromTheGraph()
    {
        var graph = new FinancialUniverseGraphDto
        {
            Year = 2026,
            Month = 10,
            CurrencyCode = "USD",
            Nodes =
            [
                new FinancialUniverseNodeDto { Id = "income", Kind = FinancialUniverseNodeKind.Income, Amount = 5_000m, CurrencyCode = "USD", LinkTarget = FinancialUniverseLinkTarget.BudgetTracker },
                new FinancialUniverseNodeDto { Id = "goal-1", Kind = FinancialUniverseNodeKind.SavingsGoal, Name = "Car", Amount = 150m, SecondaryAmount = 1_000m, CurrencyCode = "USD", LinkTarget = FinancialUniverseLinkTarget.SavingsGoals }
            ]
        };
        (IFinancialUniverseApi api, ApiProxy<IFinancialUniverseApi> proxy) = ApiProxy.Create<IFinancialUniverseApi>();
        proxy.Returns(nameof(IFinancialUniverseApi.GetGraphAsync), graph);
        var logger = new RecordingLogger<FinancialUniverseController>();

        IActionResult result;
        using (new RequestCultureScope("en-US"))
        {
            result = await CreateController(api, logger).Index();
        }

        ViewResult view = Assert.IsType<ViewResult>(result);
        FinancialUniverseViewModel model = Assert.IsType<FinancialUniverseViewModel>(view.Model);
        Assert.Equal(["Income", "Savings Goals"], model.Sections.Select(s => s.Title));
        Assert.Equal("October 2026", model.MonthLabel);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Index_WhenTheApiFails_RendersTheUnavailableState()
    {
        (IFinancialUniverseApi api, ApiProxy<IFinancialUniverseApi> _) = ApiProxy.Create<IFinancialUniverseApi>();
        var logger = new RecordingLogger<FinancialUniverseController>();

        IActionResult result = await CreateController(api, logger).Index();

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal(UnavailableStateExtensions.UnavailableViewName, view.ViewName);
        UnavailableStateViewModel model = Assert.IsType<UnavailableStateViewModel>(view.Model);
        Assert.Equal("FinancialUniverse", model.RetryController);
        Assert.False(string.IsNullOrWhiteSpace(model.Heading));
        (LogLevel Level, string Message, Exception? Exception) entry = Assert.Single(logger.Entries, e => e.Level >= LogLevel.Warning);
        Assert.IsType<HttpRequestException>(entry.Exception);
        Assert.DoesNotContain("ApiService unreachable", model.Heading, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_WhenTheRequestIsCancelled_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IFinancialUniverseApi api, ApiProxy<IFinancialUniverseApi> proxy) = ApiProxy.Create<IFinancialUniverseApi>();
        proxy.Throws(nameof(IFinancialUniverseApi.GetGraphAsync), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateController(api, new RecordingLogger<FinancialUniverseController>()).Index(cts.Token));
    }

    private static FinancialUniverseController CreateController(IFinancialUniverseApi api, ILogger<FinancialUniverseController> logger)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user-123")], "TestAuthType");
        return new FinancialUniverseController(api, logger, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }
}
