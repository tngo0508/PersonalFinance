using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.MoneyPriorities;
using Refit;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The Web Money Priorities page: it loads the tree, Earlier results and the latest answers through
/// <see cref="IMoneyPrioritiesApi"/>, and its JSON actions pass results and field-keyed validation errors to the page.
/// </summary>
public class MoneyPrioritiesWebControllerTests
{
    private const string TreeText = "flowchart TD\n  S --> Q1";

    [Fact]
    public async Task Index_ShowsTheUnhighlightedTree_EarlierResults_AndTheLatestAnswersForPreFill()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetTreeAsync),
            new PriorityTreeDiagramDto { TreeVersion = 1, WholeTreeDiagram = TreeText });
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
        Assert.Equal(TreeText, model.WholeTreeDiagram);
        Assert.Equal([9, 4], model.RecentWalks.Select(w => w.WalkId));
        Assert.NotNull(model.LatestAnswers);
        Assert.Equal(22.99m, model.LatestAnswers.DebtApr);
        Assert.Equal(5000m, model.LatestAnswers.DebtBalance);
    }

    [Fact]
    public async Task Index_WithNoEarlierResults_LeavesTheFormEmpty_WithoutLoadingAWalk()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetTreeAsync),
            new PriorityTreeDiagramDto { TreeVersion = 1, WholeTreeDiagram = TreeText });
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
            PathSentences = ["Your highest debt APR is 7% or more.", "Pay down the high-interest debt first."]
        };
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.SubmitWalkAsync), walk);
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 22.99m, DebtBalance = 5000m, AgeBand = RetirementAgeBand.Under50
        });

        JsonResult json = Assert.IsType<JsonResult>(result);
        Assert.Same(walk, json.Value);
    }

    [Fact]
    public async Task Submit_WhenTheApiRejectsTheAnswers_Returns400WithTheErrorsKeyedByField()
    {
        const string body =
            """{"errors":["Enter the balance of that debt."],"fieldErrors":{"DebtBalance":["Enter the balance of that debt."]}}""";
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Throws(nameof(IMoneyPrioritiesApi.SubmitWalkAsync), await ApiExceptionAsync(HttpStatusCode.BadRequest, body));
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 22m });

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        MoneyPrioritiesValidationErrorsDto errors = Assert.IsType<MoneyPrioritiesValidationErrorsDto>(badRequest.Value);
        Assert.Equal(["Enter the balance of that debt."], errors.FieldErrors["DebtBalance"]);
        Assert.Equal(["Enter the balance of that debt."], errors.Errors);
    }

    [Fact]
    public async Task Submit_WithNoBody_Returns400WithoutCallingTheApi()
    {
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Submit(null);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotEmpty(Assert.IsType<MoneyPrioritiesValidationErrorsDto>(badRequest.Value).Errors);
        Assert.Empty(proxy.Calls);
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
    public async Task Walk_ReturnsTheWalkAsJson()
    {
        var walk = new PriorityWalkResultDto { WalkId = 4, LeafId = "L2" };
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetWalkAsync), walk);
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Walk(4);

        Assert.Same(walk, Assert.IsType<JsonResult>(result).Value);
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
    public async Task Recent_ReturnsTheEarlierResultsAsJson()
    {
        var walks = new List<PriorityWalkSummaryDto> { new() { WalkId = 3 } };
        (IMoneyPrioritiesApi api, ApiProxy<IMoneyPrioritiesApi> proxy) = ApiProxy.Create<IMoneyPrioritiesApi>();
        proxy.Returns(nameof(IMoneyPrioritiesApi.GetRecentWalksAsync), walks);
        MoneyPrioritiesController controller = CreateController(api);

        IActionResult result = await controller.Recent();

        Assert.Same(walks, Assert.IsType<JsonResult>(result).Value);
    }

    [Fact]
    public void Controller_RequiresSignIn_AndStateChangingActionsCheckAntiforgery()
    {
        Assert.NotNull(typeof(MoneyPrioritiesController).GetCustomAttribute<AuthorizeAttribute>());
        foreach (string action in new[] { nameof(MoneyPrioritiesController.Submit), nameof(MoneyPrioritiesController.Delete) })
        {
            MethodInfo method = typeof(MoneyPrioritiesController).GetMethod(action)!;
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        }

        foreach (string action in new[]
                 {
                     nameof(MoneyPrioritiesController.Submit), nameof(MoneyPrioritiesController.Walk),
                     nameof(MoneyPrioritiesController.Delete), nameof(MoneyPrioritiesController.Recent)
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
        var controller = new MoneyPrioritiesController(api, NullLogger<MoneyPrioritiesController>.Instance);
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
