using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.Community;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// The Guild Hall page's controller over a faked ApiService: the view gets the viewer's Pseudonym and each post's
/// author as the API reports it, a Former member's post is labelled in the request's Language, and the re-roll answers
/// with the new name.
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
            Stories =
            [
                new CommunityStoryDto { Id = 1, AuthorPseudonym = "Quiet Kestrel" },
                new CommunityStoryDto { Id = 2, AuthorPseudonym = null },
            ],
            RecentComments = [new CommunityCommentDto { Id = 3, AuthorPseudonym = "Steady Heron", IsOwn = true }],
        });

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Index());

        CommunityViewModel model = Assert.IsType<CommunityViewModel>(view.Model);
        Assert.Equal("Steady Heron", model.Pseudonym);
        Assert.Collection(model.Stories,
            story => Assert.Equal("Quiet Kestrel", story.AuthorPseudonym),
            story => Assert.Null(story.AuthorPseudonym));
        CommunityComment comment = Assert.Single(model.RecentComments);
        Assert.Equal("Steady Heron", comment.AuthorPseudonym);
        Assert.True(comment.IsOwn);
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
    public async Task GetPollComments_LabelsAFormerMembersComment_InTheRequestLanguage()
    {
        using var culture = new UiCultureScope("vi");
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.GetPollCommentsAsync), new List<CommunityCommentDto>
        {
            new() { Id = 1, AuthorPseudonym = null, Text = "Still here" },
            new() { Id = 2, AuthorPseudonym = "Kind Otter", Text = "Hello", IsOwn = true },
        });

        JsonResult json = Assert.IsType<JsonResult>(await CreateController(api).GetPollComments("habits"));

        using JsonDocument answer = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        JsonElement[] comments = answer.RootElement.GetProperty("comments").EnumerateArray().ToArray();
        Assert.Equal(TestLocalization.Vietnamese("Community.FormerMember"), comments[0].GetProperty("author").GetString());
        Assert.False(comments[0].GetProperty("isOwn").GetBoolean());
        Assert.Equal("Kind Otter", comments[1].GetProperty("author").GetString());
        Assert.True(comments[1].GetProperty("isOwn").GetBoolean());
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
