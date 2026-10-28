using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.MoneyPriorities;
using Refit;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using MoneyPrioritiesService = PersonalFinance.ApiService.Features.MoneyPriorities.MoneyPrioritiesService;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The Web Money Priorities page: it loads the tree, Earlier results and the latest answers through
/// <see cref="IMoneyPrioritiesApi"/>, and its JSON actions pass results and field-keyed validation errors to the page.
/// </summary>
public class MoneyPrioritiesWebControllerTests
{
    [Fact]
    public async Task Index_ShowsTheUnhighlightedTree_EarlierResults_AndTheLatestAnswersForPreFill()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetTreeAsync),
            MoneyPrioritiesTestServices.TreeDiagramV1());
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetRecentWalksAsync), new List<PriorityWalkSummaryDto>
        {
            new() { WalkId = 9, MonthlyAmount = 500m, LeafId = "L1", LeafLabel = "Pay down the high-interest debt first" },
            new() { WalkId = 4, MonthlyAmount = 250.50m, LeafId = "L2", LeafLabel = "Build the emergency fund" }
        });
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetWalkAsync), new PriorityWalkResultDto
        {
            WalkId = 9,
            Answers = new PriorityWalkAnswersDto { MonthlyAmount = 500m, DebtApr = 22.99m, DebtBalance = 5000m }
        });
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Index();

        ViewResult view = Assert.IsType<ViewResult>(result);
        MoneyPrioritiesViewModel model = Assert.IsType<MoneyPrioritiesViewModel>(view.Model);
        Assert.StartsWith("flowchart TD", model.WholeTreeDiagram, StringComparison.Ordinal);
        Assert.Contains("  class L7 leaf", model.WholeTreeDiagram.Split('\n'));
        // The same tree in the dark palette, for the page to draw in dark mode (#489)
        Assert.Contains("  class L7 leaf", model.WholeTreeDiagramDark.Split('\n'));
        Assert.Contains($"  classDef question fill:{PriorityDiagramPalette.Dark.QuestionFill},", model.WholeTreeDiagramDark,
            StringComparison.Ordinal);
        Assert.Equal([9, 4], model.RecentWalks.Select(w => w.WalkId));
        Assert.NotNull(model.LatestAnswers);
        Assert.Equal(22.99m, model.LatestAnswers.DebtApr);
        Assert.Equal(5000m, model.LatestAnswers.DebtBalance);
    }

    [Fact]
    public async Task Index_CarriesTheTreeThresholdsFromTheApiService_ForTheFollowUpFields()
    {
        await using AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MoneyPrioritiesThresholds_{Guid.NewGuid():N}")
            .Options);
        MoneyPrioritiesService service = MoneyPrioritiesTestServices.Service(db);
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetTreeAsync), service.GetTree());
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetRecentWalksAsync), new List<PriorityWalkSummaryDto>());
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Index();

        MoneyPrioritiesViewModel model = Assert.IsType<MoneyPrioritiesViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(7m, model.Thresholds.HighInterestAprPercent);
        Assert.Equal(3m, model.Thresholds.EmergencyFundTargetMonths);
        Assert.Equal(10, model.Thresholds.LongHorizonYears);
    }

    [Fact]
    public async Task Index_CarriesTheAiDisclaimer_AndTheServersUnavailableMessage_ForThePurposeNote()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetTreeAsync),
            MoneyPrioritiesTestServices.TreeDiagramV1());
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetRecentWalksAsync), new List<PriorityWalkSummaryDto>());
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Index();

        MoneyPrioritiesViewModel model = Assert.IsType<MoneyPrioritiesViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(AiAssistantMessages.Disclaimer, model.PurposeNoteDisclaimer); // for a saved note shown without a request
        Assert.Equal("The note about your purpose is not available right now.", model.PurposeNoteUnavailable);
    }

    [Fact]
    public async Task Index_WithNoEarlierResults_LeavesTheFormEmpty_WithoutLoadingAWalk()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetTreeAsync),
            MoneyPrioritiesTestServices.TreeDiagramV1());
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetRecentWalksAsync), new List<PriorityWalkSummaryDto>());
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Index();

        MoneyPrioritiesViewModel model = Assert.IsType<MoneyPrioritiesViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.RecentWalks);
        Assert.Null(model.LatestAnswers);
        Assert.DoesNotContain(nameof(IMoneyPrioritiesApi.GetWalkAsync), proxy.Calls);
    }

    [Fact]
    public async Task Submit_ReturnsTheResultAsJson()
    {
        var walk = new PriorityWalkResultDto
        {
            WalkId = 12,
            LeafId = "L1",
            HeadlineLeafLabel = "Pay down the high-interest debt first.",
            MonthlyAmount = 500m,
            PathNodeIds = ["S", "Q1", "L1"],
            Tree = MoneyPrioritiesTestServices.TreeV1()
        };
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.SubmitWalkAsync), walk);
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 22.99m, DebtBalance = 5000m, AgeBand = RetirementAgeBand.Under50
        });

        MoneyPrioritiesResultViewModel view =
            Assert.IsType<MoneyPrioritiesResultViewModel>(Assert.IsType<JsonResult>(result).Value);
        Assert.Same(walk, view.Walk);
        Assert.Equal(
            ["Debt with APR of 7% or more? Yes.", "Suggested priority: Pay down the high-interest debt first."],
            view.PathSentences);
        Assert.Contains("  class L1 chosen", view.WholeTreeDiagram.Split('\n'));
        Assert.DoesNotContain("Q2", view.PathViewDiagram, StringComparison.Ordinal);
        // Both views again in the dark palette (#489)
        Assert.Contains("  class L1 chosen", view.WholeTreeDiagramDark.Split('\n'));
        Assert.Contains($"fill:{PriorityDiagramPalette.Dark.ChosenFill},", view.WholeTreeDiagramDark, StringComparison.Ordinal);
        Assert.DoesNotContain("Q2", view.PathViewDiagramDark, StringComparison.Ordinal);
        Assert.Contains($"fill:{PriorityDiagramPalette.Dark.ChosenFill},", view.PathViewDiagramDark, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_WhenThe400IsNotTheValidationShape_StillAnswers400WithAMessage()
    {
        const string problemDetails = """{"title":"One or more validation errors occurred.","errors":{"$.ageBand":["bad"]}}""";
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Throws(nameof(IMoneyPrioritiesApi.SubmitWalkAsync),
            await ApiExceptionAsync(HttpStatusCode.BadRequest, problemDetails));
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 0m });

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        MoneyPrioritiesErrorsViewModel errors = Assert.IsType<MoneyPrioritiesErrorsViewModel>(badRequest.Value);
        Assert.Empty(errors.FieldErrors);
        Assert.NotEmpty(errors.Errors);
        Assert.DoesNotContain("ageBand", string.Join(" ", errors.Errors));
    }

    [Fact]
    public async Task Submit_WhenTheApiIsDown_LetsJsonApiErrorsAnswer502()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> _) = ApiProxy.Create<IMoneyPrioritiesApi>();
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await FilteredAction.InvokeAsync(controller,
            () => controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 0m }));

        ObjectResult error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, error.StatusCode);
    }

    [Fact]
    public async Task Walk_ThatTheAccountDoesNotHave_Answers404()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Throws(nameof(IMoneyPrioritiesApi.GetWalkAsync), await ApiExceptionAsync(HttpStatusCode.NotFound, ""));
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.Walk(99));

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Delete_SoftDeletesThroughTheApi_AndAnswersNoContent()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.DeleteWalkAsync), null);
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Delete(4);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([nameof(IMoneyPrioritiesApi.DeleteWalkAsync)], proxy.Calls);
    }

    [Fact]
    public async Task Delete_OfAWalkTheAccountDoesNotHave_Answers404()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Throws(nameof(IMoneyPrioritiesApi.DeleteWalkAsync), await ApiExceptionAsync(HttpStatusCode.NotFound, ""));
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.Delete(99));

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Note_ForAWalkTheAccountDoesNotHave_Answers404()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Throws(nameof(IMoneyPrioritiesApi.WritePurposeNoteAsync), await ApiExceptionAsync(HttpStatusCode.NotFound, ""));
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.Note(99));

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Note_WhenTheApiIsDown_LetsJsonApiErrorsAnswer502()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> _) = ApiProxy.Create<IMoneyPrioritiesApi>();
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.Note(7));

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void Controller_RequiresSignIn_AndStateChangingActionsCheckAntiforgery()
    {
        Assert.NotNull(typeof(MoneyPrioritiesController).GetCustomAttribute<AuthorizeAttribute>());
        foreach (string action in new[]
                 {
                     nameof(MoneyPrioritiesController.Submit), nameof(MoneyPrioritiesController.Delete),
                     nameof(MoneyPrioritiesController.Note)
                 })
        {
            MethodInfo method = typeof(MoneyPrioritiesController).GetMethod(action)!;
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        }

        foreach (string action in new[]
                 {
                     nameof(MoneyPrioritiesController.Submit), nameof(MoneyPrioritiesController.Walk),
                     nameof(MoneyPrioritiesController.Delete), nameof(MoneyPrioritiesController.Recent),
                     nameof(MoneyPrioritiesController.Note)
                 })
        {
            Assert.NotNull(typeof(MoneyPrioritiesController).GetMethod(action)!
                .GetCustomAttribute<JsonApiErrorsAttribute>());
        }
    }

    private static async Task<ApiException> ApiExceptionAsync(HttpStatusCode statusCode, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.local/api/MoneyPriorities/walks");
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    private static MoneyPrioritiesController CreateController(IMoneyPrioritiesApi api)
    {
        var controller = new MoneyPrioritiesController(api, NullLogger<MoneyPrioritiesController>.Instance,
            TestLocalization.Localizer, new InMemoryUserPreferenceService());
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
