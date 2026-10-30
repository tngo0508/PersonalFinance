using System.Reflection;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Community;
using PersonalFinance.ApiService.Features.Community.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// Comments and Reactions through <see cref="ICommunityService"/> (ADR 0018): each Approved Strategy carries its thread,
/// Reactions toggle once per kind per Account, a Strategy shows how many of each kind it has while a Comment shows only
/// whether the viewer reacted, and an Admin can hide a Comment and show it again. Also the Reaction request validator.
/// </summary>
public class CommunityReactionTests
{
    private const string Author = "reaction-author";
    private const string Reader = "reaction-reader";
    private const string Other = "reaction-other";

    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero));

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CommunityReaction_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private CommunityService CreateService(AppDbContext db) =>
        new(db, _clock, NullLogger<CommunityService>.Instance);

    private static async Task<CommunityStrategyDto> ApprovedStrategyAsync(CommunityService service)
    {
        CommunityStrategyDto strategy = await service.SubmitStrategyAsync(Author, CommunityServiceTests.ValidStrategy());
        await service.ReviewStrategyAsync(strategy.Id, CommunityStrategyStatus.Approved);
        return strategy;
    }

    private static Task<CommunityReactionResultDto> ReactAsync(CommunityService service, string userId,
        CommunityReactionKind kind, int? strategyId = null, int? commentId = null) =>
        service.ToggleReactionAsync(userId,
            new CommunityReactionRequestDto { Kind = kind, StrategyId = strategyId, CommentId = commentId });

    [Fact]
    public async Task StrategyReactions_ToggleOncePerKindPerAccount_AndTheBoardCountsThem()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto strategy = await ApprovedStrategyAsync(service);

        CommunityReactionResultDto first = await ReactAsync(service, Reader, CommunityReactionKind.Helpful, strategy.Id);
        CommunityReactionResultDto second = await ReactAsync(service, Other, CommunityReactionKind.Helpful, strategy.Id);
        await ReactAsync(service, Reader, CommunityReactionKind.CheeringYouOn, strategy.Id);

        Assert.True(first.ByViewer);
        Assert.Equal(1, first.Count);
        Assert.Equal(2, second.Count);
        CommunityStrategyDto seen = Assert.Single((await service.GetCommunityAsync(Reader)).Strategies);
        Assert.Equal(2, seen.HelpfulCount);
        Assert.Equal(1, seen.CheeringCount);
        Assert.True(seen.HelpfulByViewer);
        Assert.True(seen.CheeringByViewer);
        CommunityStrategyDto seenByAuthor = Assert.Single((await service.GetCommunityAsync(Author)).Strategies);
        Assert.False(seenByAuthor.HelpfulByViewer);
        Assert.Equal(2, seenByAuthor.HelpfulCount);
    }

    [Fact]
    public async Task StrategyReaction_TakenBack_IsSoftDeleted_AndCanBeGivenAgain()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto strategy = await ApprovedStrategyAsync(service);
        await ReactAsync(service, Reader, CommunityReactionKind.Helpful, strategy.Id);

        CommunityReactionResultDto takenBack = await ReactAsync(service, Reader, CommunityReactionKind.Helpful, strategy.Id);

        Assert.False(takenBack.ByViewer);
        Assert.Equal(0, takenBack.Count);
        CommunityReaction row = Assert.Single(db.CommunityReactions.IgnoreQueryFilters());
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);

        CommunityReactionResultDto again = await ReactAsync(service, Reader, CommunityReactionKind.Helpful, strategy.Id);

        Assert.True(again.ByViewer);
        Assert.Equal(1, again.Count);
        Assert.Equal(2, db.CommunityReactions.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task Reaction_OnAStrategyNobodyElseCanSee_Throws404()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto pending = await service.SubmitStrategyAsync(Author, CommunityServiceTests.ValidStrategy());

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => ReactAsync(service, Reader, CommunityReactionKind.Helpful, pending.Id));

        Assert.Equal(ApiErrorKeys.Community.InvalidStrategy, ex.Key);
        Assert.Equal(404, ex.StatusCode);
        Assert.Empty(db.CommunityReactions);
    }

    [Fact]
    public async Task CommentReactions_ShowOnlyWhetherTheViewerReacted_NeverACount()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto strategy = await ApprovedStrategyAsync(service);
        CommunityCommentDto comment = await service.AddCommentAsync(Author,
            new CommunityCommentRequestDto { Text = "Thanks for trying it", StrategyId = strategy.Id });

        CommunityReactionResultDto result = await ReactAsync(service, Reader, CommunityReactionKind.CheeringYouOn,
            commentId: comment.Id);

        Assert.True(result.ByViewer);
        Assert.Null(result.Count);
        CommunityCommentDto mine = Assert.Single(Assert.Single((await service.GetCommunityAsync(Reader)).Strategies).Comments);
        Assert.True(mine.CheeringByViewer);
        Assert.False(mine.HelpfulByViewer);
        CommunityCommentDto theirs = Assert.Single(Assert.Single((await service.GetCommunityAsync(Author)).Strategies).Comments);
        Assert.False(theirs.CheeringByViewer);
        Assert.DoesNotContain(typeof(CommunityCommentDto).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            p => p.PropertyType == typeof(int) && p.Name != nameof(CommunityCommentDto.Id));
    }

    [Fact]
    public async Task Threads_HoldEachApprovedStrategysShownComments_OldestFirst()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto strategy = await ApprovedStrategyAsync(service);
        CommunityStrategyDto other = await ApprovedStrategyAsync(service);
        CommunityCommentDto first = await service.AddCommentAsync(Reader,
            new CommunityCommentRequestDto { Text = "Trying this week", StrategyId = strategy.Id });
        CommunityCommentDto second = await service.AddCommentAsync(Author,
            new CommunityCommentRequestDto { Text = "Let me know how it goes", StrategyId = strategy.Id });
        await service.AddCommentAsync(Reader, new CommunityCommentRequestDto { Text = "Elsewhere", StrategyId = other.Id });

        List<CommunityStrategyDto> board = (await service.GetCommunityAsync(Reader)).Strategies;

        CommunityStrategyDto shown = Assert.Single(board, s => s.Id == strategy.Id);
        Assert.Equal([first.Id, second.Id], shown.Comments.Select(c => c.Id));
        Assert.True(shown.Comments[0].IsOwn);
        Assert.False(shown.Comments[1].IsOwn);
        Assert.Equal("Elsewhere", Assert.Single(Assert.Single(board, s => s.Id == other.Id).Comments).Text);
    }

    [Fact]
    public async Task HiddenComment_LeavesEveryThread_TakesNoReactions_AndCanBeShownAgain()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStrategyDto strategy = await ApprovedStrategyAsync(service);
        CommunityCommentDto comment = await service.AddCommentAsync(Reader,
            new CommunityCommentRequestDto { Text = "Something harmful", StrategyId = strategy.Id });

        Assert.True(await service.SetCommentHiddenAsync(comment.Id, isHidden: true));

        Assert.Empty(Assert.Single((await service.GetCommunityAsync(Reader)).Strategies).Comments);
        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => ReactAsync(service, Other, CommunityReactionKind.Helpful, commentId: comment.Id));
        Assert.Equal(ApiErrorKeys.Community.CommentNotFound, ex.Key);
        Assert.Equal(404, ex.StatusCode);

        Assert.True(await service.SetCommentHiddenAsync(comment.Id, isHidden: false));

        Assert.Single(Assert.Single((await service.GetCommunityAsync(Reader)).Strategies).Comments);
        Assert.False(await service.SetCommentHiddenAsync(999, isHidden: true));
    }

    [Fact]
    public async Task CommentReviewList_IsNewestFirst_HiddenIncluded_WithWhatEachRepliesTo_AndAmountsFlagged()
    {
        await using AppDbContext db = CreateContext();
        db.CommunityPolls.Add(new CommunityPoll { PollCode = "habits", Question = "Which habit stuck?", Category = "Goals" });
        await db.SaveChangesAsync();
        CommunityService service = CreateService(db);
        CommunityStrategyDto strategy = await ApprovedStrategyAsync(service);
        CommunityCommentDto onStrategy = await service.AddCommentAsync(Reader,
            new CommunityCommentRequestDto { Text = "Saved $400 doing this", StrategyId = strategy.Id });
        CommunityCommentDto onPoll = await service.AddCommentAsync(Other,
            new CommunityCommentRequestDto { Text = "Meal plans", PollCode = "habits" });
        await service.SetCommentHiddenAsync(onStrategy.Id, isHidden: true);

        List<CommunityCommentReviewItemDto> items = await service.ListCommentsForReviewAsync();

        Assert.Equal([onPoll.Id, onStrategy.Id], items.Select(i => i.Id));
        Assert.Equal("Which habit stuck?", items[0].PollQuestion);
        Assert.Null(items[0].StrategyTitle);
        Assert.False(items[0].IsHidden);
        Assert.False(items[0].HasAmount);
        Assert.Equal(strategy.Title, items[1].StrategyTitle);
        Assert.True(items[1].IsHidden);
        Assert.True(items[1].HasAmount);
        Assert.Equal(onStrategy.AuthorPseudonym, items[1].AuthorPseudonym);
    }

    [Theory]
    [InlineData(CommunityReactionKind.Helpful, 1, null, true)]
    [InlineData(CommunityReactionKind.CheeringYouOn, null, 2, true)]
    [InlineData(CommunityReactionKind.Helpful, null, null, false)]
    [InlineData(CommunityReactionKind.Helpful, 1, 2, false)]
    [InlineData(CommunityReactionKind.Helpful, 0, null, false)]
    public void ReactionValidator_NeedsExactlyOneTarget(CommunityReactionKind kind, int? strategyId, int? commentId,
        bool valid)
    {
        ValidationResult result = new CommunityReactionRequestValidator().Validate(
            new CommunityReactionRequestDto { Kind = kind, StrategyId = strategyId, CommentId = commentId });

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(ApiErrorKeys.Community.ReactionTarget, Assert.Single(result.Errors).ErrorCode);
        }
    }

    [Fact]
    public void ReactionValidator_RejectsAnUnknownKind()
    {
        ValidationResult result = new CommunityReactionRequestValidator().Validate(
            new CommunityReactionRequestDto { Kind = (CommunityReactionKind)9, StrategyId = 1 });

        Assert.Equal(ApiErrorKeys.Community.ReactionKind, Assert.Single(result.Errors).ErrorCode);
    }
}
