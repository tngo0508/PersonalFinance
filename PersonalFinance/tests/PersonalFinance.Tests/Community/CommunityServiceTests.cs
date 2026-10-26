using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Community;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// Characterization of <see cref="ICommunityService"/>: how a submitted Community Story's read time is derived and
/// how an active poll is reported to the Account viewing it, and the keyed <see cref="ApiErrorException"/> each
/// rejection throws. Runs against an in-memory database.
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

    [Fact]
    public async Task GetCommunity_QuickStatsCarryAStableKey_ThePageTranslatesThemBy()
    {
        await using AppDbContext db = CreateContext();

        CommunityResponseDto response = await CreateService(db).GetCommunityAsync(UserId);

        Assert.Equal(["ActivePolls", "TotalVotes", "Stories", "Comments"], response.QuickStats.Select(s => s.Key));
        IReadOnlyCollection<string> english = TestLocalization.Catalog.Keys("en");
        Assert.All(response.QuickStats, stat =>
        {
            Assert.Contains($"Community.Stats.{stat.Key}.Title", english);
            Assert.Contains($"Community.Stats.{stat.Key}.Description", english);
        });
    }

    [Fact]
    public async Task VoteAsync_WithUnknownPoll_ThrowsPollNotFound()
    {
        await using AppDbContext db = CreateContext();
        var request = new CommunityVoteRequestDto { PollCode = "no-such-poll", OptionId = 1 };

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => CreateService(db).VoteAsync(UserId, request));

        Assert.Equal(ApiErrorKeys.Community.PollNotFound, ex.Key);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task AddCommentAsync_WithLongText_ThrowsCommentTooLong_WithMaxArg()
    {
        await using AppDbContext db = CreateContext();
        var request = new CommunityCommentRequestDto { Text = new string('a', 2001), Topic = "General" };

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => CreateService(db).AddCommentAsync(UserId, request, "Alex"));

        Assert.Equal(ApiErrorKeys.Community.CommentTooLong, ex.Key);
        Assert.Equal("Text", ex.Field);
        Assert.NotNull(ex.Args);
        Assert.Equal("2000", ex.Args["max"].Value);
        Assert.Equal(ApiErrorArgKind.Number, ex.Args["max"].Kind);
        Assert.Equal("Comment text is too long (max 2000 characters).", ex.Message);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task SubmitStoryAsync_WithShortBody_ThrowsStoryBodyMin()
    {
        await using AppDbContext db = CreateContext();
        var request = new CommunityStoryRequestDto
        {
            Title = "Paying off my car loan",
            Summary = "How I did it.",
            Body = "Too short.",
            Category = "Debt",
        };

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => CreateService(db).SubmitStoryAsync(UserId, request, "Alex"));

        Assert.Equal(ApiErrorKeys.Community.StoryBodyMin, ex.Key);
        Assert.Equal("Body", ex.Field);
        Assert.NotNull(ex.Args);
        Assert.Equal("100", ex.Args["min"].Value);
        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.CommunityStories);
    }

    [Fact]
    public async Task ToggleCommentLikeAsync_Unknown_Throws404()
    {
        await using AppDbContext db = CreateContext();

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => CreateService(db).ToggleCommentLikeAsync(UserId, 999));

        Assert.Equal(ApiErrorKeys.Community.CommentNotFound, ex.Key);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ToggleStoryLikeAsync_Unknown_Throws404()
    {
        await using AppDbContext db = CreateContext();

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => CreateService(db).ToggleStoryLikeAsync(UserId, 999));

        Assert.Equal(ApiErrorKeys.Community.StoryNotFound, ex.Key);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetPollCommentsAsync_WithUnknownPoll_ThrowsPollNotFound404()
    {
        await using AppDbContext db = CreateContext();

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => CreateService(db).GetPollCommentsAsync("no-such-poll"));

        Assert.Equal(ApiErrorKeys.Community.PollNotFound, ex.Key);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public void EveryCommunityKey_ResolvesInEnglish_WithoutPlaceholder()
    {
        using var uiCulture = new UiCultureScope("en");
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            List<string> keys = typeof(ApiErrorKeys.Community)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToList();

            Assert.Equal(15, keys.Count);
            foreach (string key in keys)
            {
                var entry = new ApiErrorEntryDto { Key = key, Message = key, Args = ArgsOf(key) };
                Assert.False(TestLocalization.Localizer[key].ResourceNotFound, key);
                string text = ApiErrorMessages.Resolve(TestLocalization.Localizer, entry);
                Assert.DoesNotContain("{", text);
                Assert.DoesNotContain("}", text);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = formatting;
        }
    }

    private static Dictionary<string, ApiErrorArgDto>? ArgsOf(string key)
    {
        IReadOnlyDictionary<string, ApiErrorArgDto>? args = key switch
        {
            ApiErrorKeys.Community.CommentTooLong => ErrorArgs.Number("max", 2000),
            ApiErrorKeys.Community.StoryTitle => ErrorArgs.Number("max", 500),
            ApiErrorKeys.Community.StorySummary => ErrorArgs.Number("max", 2000),
            ApiErrorKeys.Community.StoryBodyMin => ErrorArgs.Number("min", 100),
            ApiErrorKeys.Community.StoryBodyMax => ErrorArgs.Number("max", 10000),
            ApiErrorKeys.Community.StoryCategory => ErrorArgs.Number("max", 50),
            _ => null,
        };
        return args?.ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
