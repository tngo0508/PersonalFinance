using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// Characterization of <see cref="ICommunityService"/>: how a submitted Community Story's read time is derived and
/// how an active poll is reported to the Account viewing it. Runs against an in-memory database.
/// </summary>
public class CommunityServiceTests
{
    private const string UserId = "community-user";

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Community_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static CommunityService CreateService(AppDbContext db) =>
        new(db, NullLogger<CommunityService>.Instance);

    [Fact]
    public async Task SubmitStory_ReadTimeCountsWordsSeparatedByRepeatedSpaces_AndIsNotFeatured()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);

        // 401 words, separated by double spaces: 401 / 200 rounds up to 3 minutes.
        string body = string.Join("  ", Enumerable.Repeat("word", 401));
        var request = new CommunityStoryRequestDto
        {
            Title = "Paying off my car loan",
            Summary = "How I did it.",
            Body = body,
            Category = "Debt",
        };

        CommunityStoryDto story = await service.SubmitStoryAsync(UserId, request, "Alex");

        Assert.Equal(3, story.ReadTimeMinutes);
        Assert.False(story.IsFeatured);
        Assert.False((await db.CommunityStories.SingleAsync()).IsFeatured);
    }

    [Fact]
    public async Task GetCommunity_ReportsTheViewersVote_AndOptionPercentages()
    {
        await using AppDbContext db = CreateContext();
        var poll = new CommunityPoll { PollCode = "save-rate", Question = "How much do you save?", Category = "Saving" };
        var low = new CommunityPollOption { Text = "Under 10%", Order = 1 };
        var high = new CommunityPollOption { Text = "10% or more", Order = 2 };
        poll.Options.Add(high);
        poll.Options.Add(low);
        db.CommunityPolls.Add(poll);
        await db.SaveChangesAsync();

        db.CommunityPollVotes.AddRange(
            new CommunityPollVote { UserId = UserId, PollId = poll.Id, OptionId = high.Id },
            new CommunityPollVote { UserId = "other-1", PollId = poll.Id, OptionId = high.Id },
            new CommunityPollVote { UserId = "other-2", PollId = poll.Id, OptionId = low.Id });
        await db.SaveChangesAsync();

        CommunityResponseDto response = await CreateService(db).GetCommunityAsync(UserId);

        CommunityPollDto dto = Assert.Single(response.ActivePolls);
        Assert.Equal("save-rate", dto.Id);
        Assert.Equal(string.Empty, dto.Description);
        Assert.Equal(3, dto.TotalVotes);
        Assert.True(dto.HasVoted);
        Assert.Equal(high.Id, dto.VotedOptionId);
        Assert.Collection(dto.Options,
            o => { Assert.Equal("Under 10%", o.Text); Assert.Equal(1, o.Votes); Assert.Equal(33.3, o.Percentage); },
            o => { Assert.Equal("10% or more", o.Text); Assert.Equal(2, o.Votes); Assert.Equal(66.7, o.Percentage); });
    }
}
