using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.MoneyPriorities;
using PersonalFinance.ApiService.Features.MoneyPriorities.Validators;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Helpers;
using MoneyPrioritiesApiController = PersonalFinance.ApiService.Features.MoneyPriorities.MoneyPrioritiesController;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The ApiService <c>api/MoneyPriorities</c> endpoints as a signed-in Account: a submit is validated before the service
/// is called and a rejected one returns its messages keyed by field; every endpoint is scoped to the Account.
/// </summary>
public class MoneyPrioritiesEndpointTests
{
    private const string UserId = "endpoint-user";
    private const string OtherUserId = "endpoint-other";

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MoneyPrioritiesEndpoint_{Guid.NewGuid():N}")
            .Options);

    private static MoneyPrioritiesApiController CreateController(AppDbContext db, string userId = UserId)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuthType");
        return new MoneyPrioritiesApiController(MoneyPrioritiesTestServices.Service(db),
            new SubmitPriorityWalkRequestValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static SubmitPriorityWalkRequest DebtRequest() => new()
    {
        MonthlyAmount = 500m,
        DebtApr = 22m,
        DebtBalance = 5_000m
    };

    [Fact]
    public async Task Submit_AValidWalk_ReturnsTheResult_AndSavesItForTheAccount()
    {
        await using AppDbContext db = CreateContext();

        IActionResult result = await CreateController(db).Submit(DebtRequest());

        PriorityWalkResultDto body = Assert.IsType<PriorityWalkResultDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("L1", body.LeafId);
        PriorityWalk walk = Assert.Single(await db.PriorityWalks.ToListAsync());
        Assert.Equal(UserId, walk.UserId);
        Assert.Equal(body.WalkId, walk.Id);
    }

    [Fact]
    public async Task Submit_AnInvalidWalk_Returns400_WithAKeyedEntryPerField_AndSavesNothing()
    {
        await using AppDbContext db = CreateContext();
        var request = new SubmitPriorityWalkRequest { MonthlyAmount = 0m, DebtApr = 22m };

        IActionResult result = await CreateController(db).Submit(request);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        ApiErrorDto body = Assert.IsType<ApiErrorDto>(badRequest.Value);
        Assert.Equal(
            [
                (ApiErrorKeys.MoneyPriorities.MonthlyAmountRange,
                    MoneyBounds.Describe(MoneyPrioritiesMessages.MonthlyAmountRange, MoneyPrioritiesLimits.MonthlyAmountMax, null),
                    nameof(SubmitPriorityWalkRequest.MonthlyAmount)),
                (ApiErrorKeys.MoneyPriorities.DebtBalanceRequired, MoneyPrioritiesMessages.DebtBalanceRequired,
                    nameof(SubmitPriorityWalkRequest.DebtBalance))
            ],
            body.Errors.Select(e => (e.Key, e.Message, e.Field)));
        Assert.Empty(await db.PriorityWalks.ToListAsync());
    }

    [Fact]
    public async Task List_ReturnsOnlyTheAccountsWalks()
    {
        await using AppDbContext db = CreateContext();
        await CreateController(db).Submit(DebtRequest());
        await CreateController(db, OtherUserId).Submit(DebtRequest());

        IActionResult result = await CreateController(db).List();

        List<PriorityWalkSummaryDto> walks =
            Assert.IsType<List<PriorityWalkSummaryDto>>(Assert.IsType<OkObjectResult>(result).Value);
        int mine = (await db.PriorityWalks.SingleAsync(w => w.UserId == UserId)).Id;
        Assert.Equal(mine, Assert.Single(walks).WalkId);
    }

    [Fact]
    public async Task Get_OwnWalk_ReturnsIt_AnotherAccountsWalk_IsNotFound()
    {
        await using AppDbContext db = CreateContext();
        await CreateController(db).Submit(DebtRequest());
        await CreateController(db, OtherUserId).Submit(DebtRequest());
        int mine = (await db.PriorityWalks.SingleAsync(w => w.UserId == UserId)).Id;
        int theirs = (await db.PriorityWalks.SingleAsync(w => w.UserId == OtherUserId)).Id;

        IActionResult own = await CreateController(db).Get(mine);
        IActionResult other = await CreateController(db).Get(theirs);

        Assert.Equal(mine, Assert.IsType<PriorityWalkResultDto>(Assert.IsType<OkObjectResult>(own).Value).WalkId);
        Assert.IsType<NotFoundResult>(other);
    }

    [Fact]
    public async Task Delete_OwnWalk_IsNoContent_AnotherAccountsWalk_IsNotFound_AndKept()
    {
        await using AppDbContext db = CreateContext();
        await CreateController(db).Submit(DebtRequest());
        await CreateController(db, OtherUserId).Submit(DebtRequest());
        int mine = (await db.PriorityWalks.SingleAsync(w => w.UserId == UserId)).Id;
        int theirs = (await db.PriorityWalks.SingleAsync(w => w.UserId == OtherUserId)).Id;

        Assert.IsType<NotFoundResult>(await CreateController(db).Delete(theirs));
        Assert.IsType<NoContentResult>(await CreateController(db).Delete(mine));
        Assert.IsType<NotFoundResult>(await CreateController(db).Delete(mine));

        Assert.Equal(theirs, (await db.PriorityWalks.SingleAsync()).Id);
    }

    [Fact]
    public async Task Tree_ReturnsTheWholeTree()
    {
        await using AppDbContext db = CreateContext();

        IActionResult result = CreateController(db).Tree();

        PriorityTreeDiagramDto tree = Assert.IsType<PriorityTreeDiagramDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(13, tree.Tree.Nodes.Count);
    }
}
