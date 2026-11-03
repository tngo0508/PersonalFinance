using System.Net;
using System.Security.Claims;
using System.Text;
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
using Refit;

namespace PersonalFinance.Tests.FinancialUniverse;

/// <summary>
/// The Web page: the graph becomes the labelled view model, the month comes from the query (as year and month, or as
/// the picker's yyyy-MM value) and is asked of the ApiService as given, a rejected month comes back to the current one,
/// an unreachable ApiService shows the Unavailable State (never an empty page), and a cancelled request is not swallowed.
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
        // No month in the query: the ApiService picks the current one.
        Assert.Equal([null, null], Assert.Single(proxy.Arguments).Take(2));
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Index_WithYearAndMonth_AsksForThatMonth()
    {
        (IFinancialUniverseApi api, ApiProxy<IFinancialUniverseApi> proxy) = ApiProxy.Create<IFinancialUniverseApi>();
        proxy.Returns(nameof(IFinancialUniverseApi.GetGraphAsync), new FinancialUniverseGraphDto { Year = 2025, Month = 3, CurrencyCode = "USD" });

        IActionResult result = await CreateController(api, new RecordingLogger<FinancialUniverseController>()).Index(2025, 3);

        Assert.IsType<ViewResult>(result);
        Assert.Equal([2025, 3], Assert.Single(proxy.Arguments).Take(2));
    }

    [Fact]
    public async Task Index_WithThePickersPeriod_AsksForThatMonth_AndThePeriodWins()
    {
        (IFinancialUniverseApi api, ApiProxy<IFinancialUniverseApi> proxy) = ApiProxy.Create<IFinancialUniverseApi>();
        proxy.Returns(nameof(IFinancialUniverseApi.GetGraphAsync), new FinancialUniverseGraphDto { Year = 2024, Month = 12, CurrencyCode = "USD" });

        IActionResult result = await CreateController(api, new RecordingLogger<FinancialUniverseController>()).Index(2025, 3, "2024-12");

        Assert.IsType<ViewResult>(result);
        Assert.Equal([2024, 12], Assert.Single(proxy.Arguments).Take(2));
    }

    [Theory]
    [InlineData("2024-13")]
    [InlineData("last month")]
    [InlineData("")]
    public async Task Index_WithAPeriodThatIsNotAMonth_ComesBackToTheCurrentMonth_WithoutAsking(string period)
    {
        (IFinancialUniverseApi api, ApiProxy<IFinancialUniverseApi> proxy) = ApiProxy.Create<IFinancialUniverseApi>();

        IActionResult result = await CreateController(api, new RecordingLogger<FinancialUniverseController>()).Index(period: period);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FinancialUniverseController.Index), redirect.ActionName);
        Assert.True(redirect.RouteValues is null || redirect.RouteValues.Count == 0, "the redirect must drop the month");
        Assert.Empty(proxy.Calls);
    }

    [Fact]
    public async Task Index_WhenTheApiRejectsTheMonth_ComesBackToTheCurrentMonth()
    {
        (IFinancialUniverseApi api, ApiProxy<IFinancialUniverseApi> proxy) = ApiProxy.Create<IFinancialUniverseApi>();
        proxy.Throws(nameof(IFinancialUniverseApi.GetGraphAsync), await ApiExceptionAsync(HttpStatusCode.BadRequest,
            """{"errors":[{"key":"FinancialUniverse.Validation.MonthRange","message":"Choose a month between January 2000 and December 2100.","field":"year"}]}"""));
        var logger = new RecordingLogger<FinancialUniverseController>();

        IActionResult result = await CreateController(api, logger).Index(1999, 6);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FinancialUniverseController.Index), redirect.ActionName);
        Assert.True(redirect.RouteValues is null || redirect.RouteValues.Count == 0, "the redirect must drop the month");
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
    public async Task Index_WhenTheApiRejectsTheCurrentMonth_RendersTheUnavailableState_NotARedirectLoop()
    {
        (IFinancialUniverseApi api, ApiProxy<IFinancialUniverseApi> proxy) = ApiProxy.Create<IFinancialUniverseApi>();
        proxy.Throws(nameof(IFinancialUniverseApi.GetGraphAsync), await ApiExceptionAsync(HttpStatusCode.BadRequest, """{"errors":[]}"""));

        IActionResult result = await CreateController(api, new RecordingLogger<FinancialUniverseController>()).Index();

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal(UnavailableStateExtensions.UnavailableViewName, view.ViewName);
    }

    [Fact]
    public async Task Index_WhenTheRequestIsCancelled_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IFinancialUniverseApi api, ApiProxy<IFinancialUniverseApi> proxy) = ApiProxy.Create<IFinancialUniverseApi>();
        proxy.Throws(nameof(IFinancialUniverseApi.GetGraphAsync), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateController(api, new RecordingLogger<FinancialUniverseController>()).Index(cancellationToken: cts.Token));
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

    private static async Task<ApiException> ApiExceptionAsync(HttpStatusCode status, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/financial-universe");
        var response = new HttpResponseMessage(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
        return await ApiException.Create(request, HttpMethod.Get, response, new RefitSettings());
    }
}
