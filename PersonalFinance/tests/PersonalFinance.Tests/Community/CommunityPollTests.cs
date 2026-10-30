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
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// Admin-written Polls through <see cref="ICommunityService"/> (ADR 0018): an Admin writes a Poll, which is stored
/// closed, and opens or closes it; only open Polls reach the Guild Hall; results are whole shares adding up to 100,
/// returned only to an Account that has voted, and never as counts. Also the Poll request validator.
/// </summary>
public class CommunityPollTests
{
    private const string Voter = "poll-voter";
    private const string Reader = "poll-reader";

    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero));

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CommunityPoll_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private CommunityService CreateService(AppDbContext db) =>
        new(db, _clock, NullLogger<CommunityService>.Instance);

    private static CommunityPollRequestDto ValidPoll(params string[] options) => new()
    {
        Question = "Which habit stuck this month?",
        Description = "Pick the one you kept up most days.",
        Category = "Habits",
        Options = options.Length > 0 ? options.ToList() : ["Meal planning", "A spending pause", "Packed lunches"],
    };

    [Fact]
    public async Task CreatePoll_StoresItClosed_WithTrimmedDistinctOptionsInOrder_AndOffTheGuildHall()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);

        CommunityPollAdminItemDto created = await service.CreatePollAsync(
            ValidPoll("  Meal planning ", "A spending pause", "meal planning", "Packed lunches"));

        Assert.False(created.IsOpen);
        Assert.Equal(["Meal planning", "A spending pause", "Packed lunches"], created.Options);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, created.CreatedAtUtc);
        Assert.Empty((await service.GetCommunityAsync(Reader)).ActivePolls);
        CommunityPollAdminItemDto listed = Assert.Single(await service.ListPollsAsync());
        Assert.Equal(created.Id, listed.Id);
        Assert.Equal(created.Options, listed.Options);
    }

    [Fact]
    public async Task SetPollOpen_PutsThePollOnTheGuildHall_AndClosingTakesItOff()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityPollAdminItemDto created = await service.CreatePollAsync(ValidPoll());

        Assert.True(await service.SetPollOpenAsync(created.Id, isOpen: true));
        CommunityPollDto shown = Assert.Single((await service.GetCommunityAsync(Reader)).ActivePolls);
        Assert.Equal("Which habit stuck this month?", shown.Question);
        Assert.Equal(["Meal planning", "A spending pause", "Packed lunches"], shown.Options.Select(o => o.Text));

        Assert.True(await service.SetPollOpenAsync(created.Id, isOpen: false));
        Assert.Empty((await service.GetCommunityAsync(Reader)).ActivePolls);
        Assert.False(Assert.Single(await service.ListPollsAsync()).IsOpen);
        Assert.False(await service.SetPollOpenAsync(999, isOpen: true));
    }

    [Fact]
    public async Task Vote_OnAClosedPoll_IsRejected()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityPollAdminItemDto created = await service.CreatePollAsync(ValidPoll());
        CommunityPoll poll = await db.CommunityPolls.Include(p => p.Options).SingleAsync(p => p.Id == created.Id);

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(() => service.VoteAsync(Voter,
            new CommunityVoteRequestDto { PollCode = poll.PollCode, OptionId = poll.Options.First().Id }));

        Assert.Equal(ApiErrorKeys.Community.PollNotFound, ex.Key);
    }

    [Fact]
    public async Task Results_AreHiddenUntilTheViewerVotes_ThenShownAsWholeSharesAddingUpTo100()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityPollAdminItemDto created = await service.CreatePollAsync(ValidPoll());
        await service.SetPollOpenAsync(created.Id, isOpen: true);
        CommunityPoll poll = await db.CommunityPolls.Include(p => p.Options).SingleAsync(p => p.Id == created.Id);
        int[] optionIds = poll.Options.OrderBy(o => o.Order).Select(o => o.Id).ToArray();
        await service.VoteAsync("other-1", new CommunityVoteRequestDto { PollCode = poll.PollCode, OptionId = optionIds[1] });
        await service.VoteAsync("other-2", new CommunityVoteRequestDto { PollCode = poll.PollCode, OptionId = optionIds[2] });

        CommunityPollDto before = Assert.Single((await service.GetCommunityAsync(Voter)).ActivePolls);
        Assert.False(before.HasVoted);
        Assert.All(before.Options, o => Assert.Null(o.Percentage));

        CommunityPollDto after = Assert.Single((await service.VoteAsync(Voter,
            new CommunityVoteRequestDto { PollCode = poll.PollCode, OptionId = optionIds[0] })).ActivePolls);

        Assert.True(after.HasVoted);
        Assert.Equal(optionIds[0], after.VotedOptionId);
        Assert.Equal([34, 33, 33], after.Options.Select(o => o.Percentage));
        Assert.Equal(after.Options.Select(o => o.Percentage),
            Assert.Single((await service.GetCommunityAsync(Voter)).ActivePolls).Options.Select(o => o.Percentage));
        Assert.All(Assert.Single((await service.GetCommunityAsync(Reader)).ActivePolls).Options,
            o => Assert.Null(o.Percentage));
        Assert.Equal(3, Assert.Single(await service.ListPollsAsync()).VoteCount);
    }

    [Fact]
    public void PollDtos_CarryNoVoteCounts()
    {
        Assert.Equal(
            ["Category", "Comments", "Description", "HasVoted", "Id", "IsActive", "Options", "Question", "VotedOptionId"],
            typeof(CommunityPollDto).GetProperties().Select(p => p.Name).Order());
        Assert.Equal(["Id", "Percentage", "Text"],
            typeof(CommunityPollOptionDto).GetProperties().Select(p => p.Name).Order());
    }

    [Theory]
    [InlineData("", "Habits", ApiErrorKeys.Community.PollQuestion, "Yes", "No")]
    [InlineData("Which habit?", "Goals", ApiErrorKeys.Community.PollCategory, "Yes", "No")]
    [InlineData("Which habit?", "Habits", ApiErrorKeys.Community.PollOptions, "Yes")]
    [InlineData("Which habit?", "Habits", ApiErrorKeys.Community.PollOptions, "Yes", " yes ")]
    [InlineData("Which habit?", "Habits", ApiErrorKeys.Community.PollOptions, "1", "2", "3", "4", "5", "6", "7")]
    [InlineData("Which habit?", "Habits", ApiErrorKeys.Community.PollOptionText, "Yes", "No", " ")]
    public void PollValidator_RejectsWithTheKeyOfTheRuleBroken(string question, string category, string key,
        params string[] options)
    {
        ValidationResult result = new CommunityPollRequestValidator().Validate(new CommunityPollRequestDto
        {
            Question = question,
            Category = category,
            Options = options.ToList(),
        });

        Assert.Equal(key, Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public void PollValidator_AcceptsAQuestionWithTwoToSixDistinctOptions()
    {
        var validator = new CommunityPollRequestValidator();

        Assert.True(validator.Validate(ValidPoll("Yes", "No")).IsValid);
        Assert.True(validator.Validate(ValidPoll("1", "2", "3", "4", "5", "6")).IsValid);
    }
}
