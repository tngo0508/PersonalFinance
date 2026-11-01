using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.BudgetPlanner;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Research;
using BudgetController = PersonalFinance.Web.Controllers.BudgetController;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// A page whose data cannot be loaded shows the Unavailable State, never defaults or an empty state,
/// and no error path shows exception text to the user.
/// </summary>
public class UnavailableStateTests
{
    private const string ExceptionText = "ApiService unreachable";

    [Fact]
    public async Task BudgetIndex_WhenApiFails_RendersUnavailableStateInsteadOfDefaults()
    {
        (IBudgetPlannerApi? api, ApiProxy<IBudgetPlannerApi> _) = ApiProxy.Create<IBudgetPlannerApi>();
        BudgetController controller = WithContext(new BudgetController(api, NullLogger<BudgetController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService()));

        IActionResult result = await controller.Index();

        AssertUnavailable(result, "Budget");
    }

    [Fact]
    public async Task BudgetGetBudgetData_WhenApiFails_LetsJsonApiErrorsAnswerInsteadOfEmptyBudget()
    {
        (IBudgetPlannerApi? api, ApiProxy<IBudgetPlannerApi> _) = ApiProxy.Create<IBudgetPlannerApi>();
        BudgetController controller = WithContext(new BudgetController(api, NullLogger<BudgetController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService()));

        await Assert.ThrowsAsync<HttpRequestException>(() => controller.GetBudgetData());
        Assert.NotNull(typeof(BudgetController).GetMethod(nameof(BudgetController.GetBudgetData))!
            .GetCustomAttributes(typeof(JsonApiErrorsAttribute), inherit: true).SingleOrDefault());
    }

    [Fact]
    public async Task NetWorthIndex_WhenParametersFailToLoad_RendersUnavailableState()
    {
        (INetWorthApi? api, ApiProxy<INetWorthApi>? proxy) = ApiProxy.Create<INetWorthApi>();
        NetworthController controller = WithContext(new NetworthController(api, NullLogger<NetworthController>.Instance, TestLocalization.Localizer));

        IActionResult result = await controller.Index();

        AssertUnavailable(result, "Networth");
        Assert.DoesNotContain(nameof(INetWorthApi.CalculateProjectionAsync), proxy.Calls);
    }

    [Fact]
    public async Task NetWorthIndex_WhenProjectionFails_RendersUnavailableState()
    {
        (INetWorthApi? api, ApiProxy<INetWorthApi>? proxy) = ApiProxy.Create<INetWorthApi>();
        proxy.Returns(nameof(INetWorthApi.LoadParametersAsync), new NetWorthParametersDto());
        NetworthController controller = WithContext(new NetworthController(api, NullLogger<NetworthController>.Instance, TestLocalization.Localizer));

        IActionResult result = await controller.Index();

        AssertUnavailable(result, "Networth");
    }

    [Fact]
    public async Task NetWorthDeleteParameters_WhenApiFails_ShowsSafeErrorAfterRedirect()
    {
        (INetWorthApi? api, ApiProxy<INetWorthApi> _) = ApiProxy.Create<INetWorthApi>();
        NetworthController controller = WithContext(new NetworthController(api, NullLogger<NetworthController>.Instance, TestLocalization.Localizer));

        IActionResult result = await controller.DeleteParameters();

        Assert.IsType<RedirectToActionResult>(result);
        string error = Assert.IsType<string>(controller.TempData["SaveError"]);
        Assert.DoesNotContain(ExceptionText, error);
    }

    [Fact]
    public async Task SavingsGoalsIndex_WhenApiFails_RendersUnavailableStateInsteadOfEmptyList()
    {
        (ISavingsGoalsApi? api, ApiProxy<ISavingsGoalsApi> _) = ApiProxy.Create<ISavingsGoalsApi>();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance, TestLocalization.Localizer,
            TimeProvider.System, new InMemoryUserPreferenceService()));

        IActionResult result = await controller.Index();

        AssertUnavailable(result, "SavingsGoals");
    }

    [Fact]
    public async Task SavingsGoalsCreate_WhenApiFails_ShowsSafeErrorWithGoals()
    {
        (ISavingsGoalsApi? api, ApiProxy<ISavingsGoalsApi>? proxy) = ApiProxy.Create<ISavingsGoalsApi>();
        proxy.Returns(nameof(ISavingsGoalsApi.GetSavingsGoalsAsync), new List<SavingsGoalDto>());
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance, TestLocalization.Localizer,
            TimeProvider.System, new InMemoryUserPreferenceService()));

        IActionResult result = await controller.Create("Emergency fund", 1000m, 0m, null, null);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(SavingsGoalsController.Index), view.ViewName);
        string error = Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage;
        Assert.DoesNotContain(ExceptionText, error);
    }

    [Fact]
    public async Task SavingsGoalsCreate_WhenGoalsAlsoFailToLoad_RendersUnavailableState()
    {
        (ISavingsGoalsApi? api, ApiProxy<ISavingsGoalsApi> _) = ApiProxy.Create<ISavingsGoalsApi>();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance, TestLocalization.Localizer,
            TimeProvider.System, new InMemoryUserPreferenceService()));

        IActionResult result = await controller.Create("Emergency fund", 1000m, 0m, null, null);

        AssertUnavailable(result, "SavingsGoals");
    }

    [Theory]
    [InlineData(nameof(SavingsGoalsController.AddContribution))]
    [InlineData(nameof(SavingsGoalsController.Delete))]
    public async Task SavingsGoalsContributionAndDelete_WhenApiFails_ShowSafeErrorAfterRedirect(string action)
    {
        (ISavingsGoalsApi? api, ApiProxy<ISavingsGoalsApi> _) = ApiProxy.Create<ISavingsGoalsApi>();
        SavingsGoalsController controller = WithContext(new SavingsGoalsController(api, NullLogger<SavingsGoalsController>.Instance, TestLocalization.Localizer,
            TimeProvider.System, new InMemoryUserPreferenceService()));

        IActionResult result = action == nameof(SavingsGoalsController.AddContribution)
            ? await controller.AddContribution(1, 50m, "deposit", null, null)
            : await controller.Delete(1);

        Assert.IsType<RedirectToActionResult>(result);
        string error = Assert.IsType<string>(controller.TempData["Error"]);
        Assert.DoesNotContain(ExceptionText, error);
    }

    [Fact]
    public async Task MoneyPrioritiesIndex_WhenApiFails_RendersUnavailableStateInsteadOfAnEmptyForm()
    {
        (IMoneyPrioritiesApi? api, ApiProxy<IMoneyPrioritiesApi> _) = ApiProxy.Create<IMoneyPrioritiesApi>();
        MoneyPrioritiesController controller =
            WithContext(new MoneyPrioritiesController(api, NullLogger<MoneyPrioritiesController>.Instance,
            TestLocalization.Localizer, new InMemoryUserPreferenceService()));

        IActionResult result = await controller.Index();

        AssertUnavailable(result, "MoneyPriorities");
    }

    [Fact]
    public async Task MoneyPrioritiesIndex_WhenOnlyEarlierResultsFail_RendersUnavailableState()
    {
        (IMoneyPrioritiesApi? api, ApiProxy<IMoneyPrioritiesApi>? proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetTreeAsync), new PriorityTreeDiagramDto());
        MoneyPrioritiesController controller =
            WithContext(new MoneyPrioritiesController(api, NullLogger<MoneyPrioritiesController>.Instance,
            TestLocalization.Localizer, new InMemoryUserPreferenceService()));

        IActionResult result = await controller.Index();

        AssertUnavailable(result, "MoneyPriorities");
    }

    [Fact]
    public async Task MoneyPrioritiesRecent_WhenApiFails_LetsJsonApiErrorsAnswerInsteadOfAnEmptyList()
    {
        (IMoneyPrioritiesApi? api, ApiProxy<IMoneyPrioritiesApi> _) = ApiProxy.Create<IMoneyPrioritiesApi>();
        MoneyPrioritiesController controller =
            WithContext(new MoneyPrioritiesController(api, NullLogger<MoneyPrioritiesController>.Instance,
            TestLocalization.Localizer, new InMemoryUserPreferenceService()));

        await Assert.ThrowsAsync<HttpRequestException>(() => controller.Recent());
        Assert.NotNull(typeof(MoneyPrioritiesController).GetMethod(nameof(MoneyPrioritiesController.Recent))!
            .GetCustomAttributes(typeof(JsonApiErrorsAttribute), inherit: true).SingleOrDefault());
    }

    [Fact]
    public async Task MoneyPrioritiesIndex_WhenRequestIsCancelled_DoesNotSwallowCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IMoneyPrioritiesApi? api, ApiProxy<IMoneyPrioritiesApi>? proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Throws(nameof(IMoneyPrioritiesApi.GetTreeAsync), new OperationCanceledException(cts.Token));
        MoneyPrioritiesController controller =
            WithContext(new MoneyPrioritiesController(api, NullLogger<MoneyPrioritiesController>.Instance,
            TestLocalization.Localizer, new InMemoryUserPreferenceService()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Index(cts.Token));
    }

    [Fact]
    public async Task CommunityIndex_WhenApiFails_RendersUnavailableState()
    {
        (ICommunityApi? api, ApiProxy<ICommunityApi> _) = ApiProxy.Create<ICommunityApi>();
        CommunityController controller = WithContext(new CommunityController(api, NullLogger<CommunityController>.Instance, TestLocalization.Localizer));

        IActionResult result = await controller.Index();

        AssertUnavailable(result, "Community");
    }

    [Fact]
    public async Task ResearchSectors_WhenApiFails_RendersThePage_WithTheUnavailableStateOnTheChartOnly()
    {
        (IResearchApi? api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();
        ResearchController controller = WithContext(new ResearchController(api, TestResearchCache.Create(), NullLogger<ResearchController>.Instance, TestLocalization.Localizer));

        IActionResult result = await controller.Sectors();

        // The page itself renders, with the search and the basket; the section whose data failed carries its own state.
        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.NotEqual(UnavailableStateExtensions.UnavailableViewName, view.ViewName);
        ResearchSectorsPageViewModel model = Assert.IsType<ResearchSectorsPageViewModel>(view.Model);
        Assert.True(model.Sectors.Unavailable);
        Assert.Equal("Research", model.Sectors.UnavailableState.RetryController);
        Assert.False(string.IsNullOrWhiteSpace(model.Sectors.UnavailableState.Heading));
        Assert.DoesNotContain(ExceptionText, model.Sectors.UnavailableState.Heading);
    }

    [Fact]
    public async Task Index_WhenRequestIsCancelled_DoesNotSwallowCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IBudgetPlannerApi? api, ApiProxy<IBudgetPlannerApi>? proxy) = ApiProxy.Create<IBudgetPlannerApi>();
        proxy.Throws(nameof(IBudgetPlannerApi.GetBudgetAsync), new OperationCanceledException(cts.Token));
        BudgetController controller = WithContext(new BudgetController(api, NullLogger<BudgetController>.Instance, TestLocalization.Localizer, new InMemoryUserPreferenceService()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Index(cts.Token));
    }

    private static void AssertUnavailable(IActionResult result, string retryController)
    {
        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal(UnavailableStateExtensions.UnavailableViewName, view.ViewName);
        UnavailableStateViewModel model = Assert.IsType<UnavailableStateViewModel>(view.Model);
        Assert.Equal(retryController, model.RetryController);
        Assert.False(string.IsNullOrWhiteSpace(model.PageTitle));
        Assert.False(string.IsNullOrWhiteSpace(model.Heading));
    }

    private static TController WithContext<TController>(TController controller) where TController : Controller
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "test-user-123") }, "TestAuthType"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return controller;
    }
}
