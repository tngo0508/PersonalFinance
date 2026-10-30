using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.Community;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// The Guild Hall page's controller over a faked ApiService: the view gets the viewer's Pseudonym, each post's author
/// and each Strategy's Reactions and thread as the API reports them, the board is filtered only by a known category,
/// the re-roll answers with the new name, a pinned Strategy answers with its waiting note (its category in the
/// request's Language), and a Reaction is sent only with a known kind and one target.
/// </summary>
public class CommunityControllerTests
{
    [Fact]
    public async Task Index_PassesTheViewersPseudonym_AndEachPostsAuthor_ToTheView()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.GetCommunityAsync), new CommunityResponseDto
        {
            Pseudonym = "Steady Heron",
            Strategies =
            [
                new CommunityStrategyDto { Id = 1, AuthorPseudonym = "Steady Heron", IsOwn = true, Status = CommunityStrategyStatus.Pending },
                new CommunityStrategyDto
                {
                    Id = 2, AuthorPseudonym = null, Status = CommunityStrategyStatus.Approved, HelpfulCount = 4,
                    CheeringByViewer = true,
                    Comments = [new CommunityCommentDto { Id = 3, AuthorPseudonym = "Steady Heron", IsOwn = true, HelpfulByViewer = true }],
                },
            ],
        });

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Index());

        CommunityViewModel model = Assert.IsType<CommunityViewModel>(view.Model);
        Assert.Equal("Steady Heron", model.Pseudonym);
        Assert.Null(model.Category);
        Assert.Collection(model.Strategies,
            strategy =>
            {
                Assert.Equal("Steady Heron", strategy.AuthorPseudonym);
                Assert.True(strategy.IsOwn);
                Assert.Equal(CommunityStrategyStatus.Pending, strategy.Status);
            },
            strategy =>
            {
                Assert.Null(strategy.AuthorPseudonym);
                Assert.Equal(4, strategy.HelpfulCount);
                Assert.True(strategy.CheeringByViewer);
                CommunityComment comment = Assert.Single(strategy.Comments);
                Assert.Equal("Steady Heron", comment.AuthorPseudonym);
                Assert.True(comment.IsOwn);
                Assert.True(comment.HelpfulByViewer);
            });
    }

    [Theory]
    [InlineData("Debt", "Debt")]
    [InlineData("Lottery", null)]
    [InlineData(null, null)]
    public async Task Index_FiltersTheBoard_OnlyByOneOfTheBoardsCategories(string? asked, string? filter)
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.GetCommunityAsync), new CommunityResponseDto());

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Index(asked));

        Assert.Equal(filter, Assert.IsType<CommunityViewModel>(view.Model).Category);
        Assert.Equal(filter, proxy.Arguments.Single()[0]);
    }

    [Fact]
    public async Task SubmitStrategy_AnswersWithTheWaitingNote_ItsCategoryInTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.SubmitStrategyAsync), new CommunityStrategyDto
        {
            Id = 5,
            AuthorPseudonym = "Kind Otter",
            IsOwn = true,
            Category = "Groceries",
            Title = "Pantry audit",
            Tried = "t",
            Happened = "h",
            Keeping = "k",
            Status = CommunityStrategyStatus.Pending,
        });

        JsonResult json = Assert.IsType<JsonResult>(await CreateController(api).SubmitStrategy(
            new CommunityController.StrategyRequest { Category = "Groceries", Title = "Pantry audit", Tried = "t", Happened = "h", Keeping = "k" }));

        CommunityStrategyRequestDto sent = Assert.IsType<CommunityStrategyRequestDto>(proxy.Arguments.Single()[0]);
        Assert.Equal("Pantry audit", sent.Title);
        using JsonDocument answer = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        JsonElement strategy = answer.RootElement.GetProperty("strategy");
        Assert.True(answer.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(5, strategy.GetProperty("id").GetInt32());
        Assert.Equal(TestLocalization.Vietnamese("Community.Category.Groceries"), strategy.GetProperty("category").GetString());
        Assert.Equal("Kind Otter", strategy.GetProperty("author").GetString());
        Assert.Equal("Pantry audit", strategy.GetProperty("title").GetString());
    }

    [Fact]
    public async Task RerollPseudonym_AnswersWithTheNewName()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.RerollPseudonymAsync), new CommunityPseudonymDto { Name = "Amber Wren" });

        JsonResult json = Assert.IsType<JsonResult>(await CreateController(api).RerollPseudonym());

        using JsonDocument answer = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        Assert.True(answer.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Amber Wren", answer.RootElement.GetProperty("pseudonym").GetString());
        Assert.Equal([nameof(ICommunityApi.RerollPseudonymAsync)], proxy.Calls);
    }

    [Fact]
    public async Task React_SendsTheKindAndTarget_AndAnswersWithTheNewState()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ToggleReactionAsync), new CommunityReactionResultDto
        {
            Kind = CommunityReactionKind.Helpful, ByViewer = true, Count = 3,
        });

        JsonResult json = Assert.IsType<JsonResult>(await CreateController(api).React(
            new CommunityController.ReactionRequest { Kind = "Helpful", StrategyId = 7 }));

        using JsonDocument answer = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        Assert.True(answer.RootElement.GetProperty("byViewer").GetBoolean());
        Assert.Equal(3, answer.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("Helpful", answer.RootElement.GetProperty("kind").GetString());
        Assert.Equal([nameof(ICommunityApi.ToggleReactionAsync)], proxy.Calls);
    }

    [Theory]
    [InlineData("Downvote", 7, null)]
    [InlineData("3", 7, null)]
    [InlineData("Helpful", null, null)]
    [InlineData("Helpful", 7, 8)]
    public async Task React_RejectsAnUnknownKindOrNotExactlyOneTarget_WithoutCallingTheApi(string kind, int? strategyId,
        int? commentId)
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();

        IActionResult result = await CreateController(api).React(
            new CommunityController.ReactionRequest { Kind = kind, StrategyId = strategyId, CommentId = commentId });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(proxy.Calls);
    }

    [Theory]
    [InlineData(nameof(CommunityController.SubmitStrategy))]
    [InlineData(nameof(CommunityController.AddComment))]
    [InlineData(nameof(CommunityController.React))]
    public void PostingActions_ValidateTheAntiforgeryToken(string action)
    {
        Assert.NotNull(typeof(CommunityController).GetMethod(action)!
            .GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: false).SingleOrDefault());
    }

    private static CommunityController CreateController(ICommunityApi api)
    {
        var controller = new CommunityController(api, NullLogger<CommunityController>.Instance, TestLocalization.Localizer);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, "community-user") }, "TestAuthType")),
            },
        };
        return controller;
    }
}
