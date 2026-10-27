using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Community;
using PersonalFinance.ApiService.Features.Community.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// Strategies through <see cref="ICommunityService"/> (ADR 0018): a pinned Strategy is Pending and only its author
/// sees it, an Admin's approval puts it on everyone's board, a rejection leaves it with its author only, and the Admin
/// queue flags money amounts without blocking them. Also the request validators and <see cref="MoneyAmountRule"/>.
/// </summary>
public class CommunityStrategyTests
{
    private const string Author = "strategy-author";
    private const string Reader = "strategy-reader";

    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero));

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CommunityStrategy_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private CommunityService CreateService(AppDbContext db) =>
        new(db, _clock, NullLogger<CommunityService>.Instance);

    private async Task<CommunityStrategyDto> SubmitAsync(CommunityService service, string userId, string title,
        string category = "Groceries")
    {
        CommunityStrategyRequestDto request = CommunityServiceTests.ValidStrategy(category);
        request.Title = title;
        CommunityStrategyDto strategy = await service.SubmitStrategyAsync(userId, request);
        _clock.Advance(TimeSpan.FromMinutes(1));
        return strategy;
    }

    [Fact]
    public async Task SubmitStrategy_IsPending_AndOnlyItsAuthorSeesIt()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyRequestDto request = CommunityServiceTests.ValidStrategy();
        request.Title = "  The Sunday pantry audit  ";

        CommunityStrategyDto sent = await service.SubmitStrategyAsync(Author, request);

        Assert.Equal(CommunityStrategyStatus.Pending, sent.Status);
        Assert.Equal("The Sunday pantry audit", sent.Title);
        Assert.True(sent.IsOwn);
        CommunityStrategyDto own = Assert.Single((await service.GetCommunityAsync(Author)).Strategies);
        Assert.Equal(sent.Id, own.Id);
        Assert.Equal(CommunityStrategyStatus.Pending, own.Status);
        Assert.Equal(sent.AuthorPseudonym, own.AuthorPseudonym);
        Assert.Empty((await service.GetCommunityAsync(Reader)).Strategies);
        Assert.Equal(CommunityStrategyStatus.Pending, (await db.CommunityStrategies.SingleAsync()).Status);
    }

    [Fact]
    public async Task ReviewStrategy_Approved_ReachesEveryone_Rejected_StaysWithItsAuthor()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto approved = await SubmitAsync(service, Author, "Approved one");
        CommunityStrategyDto rejected = await SubmitAsync(service, Author, "Rejected one");

        Assert.True(await service.ReviewStrategyAsync(approved.Id, CommunityStrategyStatus.Approved));
        Assert.True(await service.ReviewStrategyAsync(rejected.Id, CommunityStrategyStatus.Rejected));

        CommunityStrategyDto seen = Assert.Single((await service.GetCommunityAsync(Reader)).Strategies);
        Assert.Equal(approved.Id, seen.Id);
        Assert.Equal(CommunityStrategyStatus.Approved, seen.Status);
        Assert.False(seen.IsOwn);
        Assert.Equal(approved.AuthorPseudonym, seen.AuthorPseudonym);
        Assert.Equal([rejected.Id, approved.Id], (await service.GetCommunityAsync(Author)).Strategies.Select(s => s.Id));
        CommunityStrategy row = await db.CommunityStrategies.SingleAsync(s => s.Id == approved.Id);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, row.ReviewedAt);
    }

    [Fact]
    public async Task Board_ShowsTheViewersPendingThenRejected_ThenApproved_NewestFirst_AndNeverAnotherAccountsUnapproved()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto olderApproved = await SubmitAsync(service, Reader, "Older approved");
        CommunityStrategyDto othersPending = await SubmitAsync(service, Reader, "Someone else's pending");
        CommunityStrategyDto ownRejected = await SubmitAsync(service, Author, "Own rejected");
        CommunityStrategyDto newerApproved = await SubmitAsync(service, Reader, "Newer approved");
        CommunityStrategyDto ownPending = await SubmitAsync(service, Author, "Own pending");
        await service.ReviewStrategyAsync(olderApproved.Id, CommunityStrategyStatus.Approved);
        await service.ReviewStrategyAsync(newerApproved.Id, CommunityStrategyStatus.Approved);
        await service.ReviewStrategyAsync(ownRejected.Id, CommunityStrategyStatus.Rejected);

        List<CommunityStrategyDto> board = (await service.GetCommunityAsync(Author)).Strategies;

        Assert.Equal([ownPending.Id, ownRejected.Id, newerApproved.Id, olderApproved.Id], board.Select(s => s.Id));
        Assert.DoesNotContain(board, s => s.Id == othersPending.Id);
    }

    [Fact]
    public async Task Board_FilteredByCategory_HoldsOnlyThatCategory_TheViewersOwnIncluded()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto debt = await SubmitAsync(service, Reader, "Highest rate first", "Debt");
        CommunityStrategyDto groceries = await SubmitAsync(service, Reader, "Pantry audit", "Groceries");
        CommunityStrategyDto ownDebt = await SubmitAsync(service, Author, "Paper chart on the fridge", "Debt");
        await service.ReviewStrategyAsync(debt.Id, CommunityStrategyStatus.Approved);
        await service.ReviewStrategyAsync(groceries.Id, CommunityStrategyStatus.Approved);

        List<CommunityStrategyDto> board = (await service.GetCommunityAsync(Author, "Debt")).Strategies;

        Assert.Equal([ownDebt.Id, debt.Id], board.Select(s => s.Id));
        Assert.Equal(3, (await service.GetCommunityAsync(Author)).Strategies.Count);
    }

    [Fact]
    public async Task ReviewQueue_ListsTheStatusOldestFirst_FlaggingAmounts_WithoutBlockingThem()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyRequestDto flexing = CommunityServiceTests.ValidStrategy();
        flexing.Happened = "Saved $400 in two months.";
        CommunityStrategyDto first = await service.SubmitStrategyAsync(Author, flexing);
        _clock.Advance(TimeSpan.FromMinutes(1));
        CommunityStrategyRequestDto modest = CommunityServiceTests.ValidStrategy();
        modest.Happened = "Grocery spend down about 30% over 4 weeks.";
        CommunityStrategyDto second = await service.SubmitStrategyAsync(Author, modest);
        CommunityStrategyDto approved = await SubmitAsync(service, Reader, "Already approved");
        await service.ReviewStrategyAsync(approved.Id, CommunityStrategyStatus.Approved);

        List<CommunityStrategyReviewItemDto> queue =
            await service.ListStrategiesForReviewAsync(CommunityStrategyStatus.Pending);

        Assert.Collection(queue,
            item =>
            {
                Assert.Equal(first.Id, item.Id);
                Assert.True(item.HasAmount);
                Assert.Equal(first.AuthorPseudonym, item.AuthorPseudonym);
                Assert.Null(item.ReviewedAtUtc);
            },
            item =>
            {
                Assert.Equal(second.Id, item.Id);
                Assert.False(item.HasAmount);
            });
        Assert.Equal(approved.Id,
            Assert.Single(await service.ListStrategiesForReviewAsync(CommunityStrategyStatus.Approved)).Id);
    }

    [Fact]
    public async Task ReviewStrategy_UnknownStrategy_ReturnsFalse()
    {
        await using AppDbContext db = CreateContext();

        Assert.False(await CreateService(db).ReviewStrategyAsync(999, CommunityStrategyStatus.Approved));
    }

    [Fact]
    public async Task AddComment_OnAStrategyNobodyElseCanSee_ThrowsInvalidStrategy()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto pending = await SubmitAsync(service, Author, "Still pending");

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(() => service.AddCommentAsync(Reader,
            new CommunityCommentRequestDto { Text = "Nice", StrategyId = pending.Id }));

        Assert.Equal(ApiErrorKeys.Community.InvalidStrategy, ex.Key);
        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.CommunityComments);
    }

    [Fact]
    public async Task RecentComments_LeaveOutCommentsOnAStrategyRejectedAfterApproval()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto strategy = await SubmitAsync(service, Author, "Approved, then rejected");
        await service.ReviewStrategyAsync(strategy.Id, CommunityStrategyStatus.Approved);
        await service.AddCommentAsync(Reader, new CommunityCommentRequestDto { Text = "Trying this", StrategyId = strategy.Id });
        Assert.Single((await service.GetCommunityAsync(Reader)).RecentComments);

        await service.ReviewStrategyAsync(strategy.Id, CommunityStrategyStatus.Rejected);

        Assert.Empty((await service.GetCommunityAsync(Reader)).RecentComments);
    }

    [Theory]
    [InlineData("Saved $400 in two months")]
    [InlineData("about 40 USD less")]
    [InlineData("USD 40 a week")]
    [InlineData("cut it by €25")]
    [InlineData("2 triệu đồng mỗi tháng")]
    [InlineData("1.200.000đ")]
    [InlineData("50k vnd")]
    [InlineData("twenty bucks, 20 dollars")]
    public void MoneyAmountRule_FlagsAmounts(string text)
    {
        Assert.True(MoneyAmountRule.ContainsAmount(text));
    }

    [Theory]
    [InlineData("cut grocery spend by about a third")]
    [InlineData("down 30% over 4 weeks")]
    [InlineData("14 months to clear 3 cards")]
    [InlineData("ran a 5k every Sunday")]
    [InlineData("5 đêm mỗi tuần")]
    [InlineData("we visited Europe twice")]
    public void MoneyAmountRule_LeavesPercentagesAndCountsAlone(string text)
    {
        Assert.False(MoneyAmountRule.ContainsAmount(text));
    }

    [Fact]
    public void StrategyValidator_RejectsAnUnknownCategory_AndEachMissingOrOverlongText_WithItsKeyAndMax()
    {
        var validator = new CommunityStrategyRequestValidator();
        CommunityStrategyRequestDto request = CommunityServiceTests.ValidStrategy("Lottery");
        request.Title = new string('a', CommunityStrategyLimits.TitleMaxLength + 1);
        request.Tried = "   ";
        request.Keeping = new string('a', CommunityStrategyLimits.TextMaxLength + 1);

        ValidationResult result = validator.Validate(request);

        Assert.Equal(
            [
                ApiErrorKeys.Community.StrategyCategory, ApiErrorKeys.Community.StrategyTitle,
                ApiErrorKeys.Community.StrategyTried, ApiErrorKeys.Community.StrategyKeeping,
            ],
            result.Errors.Select(e => e.ErrorCode));
        Assert.Equal(nameof(CommunityStrategyRequestDto.Tried), result.Errors[2].PropertyName);
        ApiErrorDto error = result.ToApiError();
        Assert.Equal(CommunityStrategyLimits.TitleMaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            error.Errors[1].Args!["max"].Value);
        Assert.True(validator.Validate(CommunityServiceTests.ValidStrategy()).IsValid);
    }

    [Theory]
    [InlineData(CommunityStrategyStatus.Approved, true)]
    [InlineData(CommunityStrategyStatus.Rejected, true)]
    [InlineData(CommunityStrategyStatus.Pending, false)]
    [InlineData((CommunityStrategyStatus)9, false)]
    public void ReviewValidator_AcceptsOnlyApproveOrReject(CommunityStrategyStatus status, bool valid)
    {
        ValidationResult result = new CommunityStrategyReviewRequestValidator()
            .Validate(new CommunityStrategyReviewRequestDto { Status = status });

        Assert.Equal(valid, result.IsValid);
        Assert.All(result.Errors, e => Assert.Equal(ApiErrorKeys.Community.StrategyReviewStatus, e.ErrorCode));
    }
}
