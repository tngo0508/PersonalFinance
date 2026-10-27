using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
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
/// Characterization of <see cref="ICommunityService"/>: the Pseudonym an Account gets and re-rolls and how every post
/// shows it, how a submitted Story's read time is derived, how an active poll is reported to the Account viewing it,
/// and the keyed <see cref="ApiErrorException"/> each rejection throws. Runs against an in-memory database; the
/// queries that join each post to its author's Pseudonym are also checked to translate to Azure SQL.
/// </summary>
public partial class CommunityServiceTests
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
        new(db, TimeProvider.System, NullLogger<CommunityService>.Instance);

    private static CommunityStoryRequestDto ValidStory() => new()
    {
        Title = "Paying off my car loan",
        Summary = "How I did it.",
        Body = string.Join(' ', Enumerable.Repeat("word", 120)),
        Category = "Debt",
    };

    [Fact]
    public async Task GetCommunity_CreatesThePseudonymOnTheFirstVisit_AndKeepsItAfter()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);

        CommunityResponseDto first = await service.GetCommunityAsync(UserId);
        CommunityResponseDto second = await service.GetCommunityAsync(UserId);

        Assert.Matches(PseudonymShape(), first.Pseudonym);
        Assert.Equal(first.Pseudonym, second.Pseudonym);
        CommunityPseudonym row = Assert.Single(db.CommunityPseudonyms);
        Assert.Equal(UserId, row.UserId);
        Assert.Equal(first.Pseudonym, row.Name);
    }

    [Fact]
    public async Task RerollPseudonym_ChangesTheName_AndEveryPastPostShowsIt()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        CommunityStoryDto story = await service.SubmitStoryAsync(UserId, ValidStory());
        CommunityCommentDto comment = await service.AddCommentAsync(UserId,
            new CommunityCommentRequestDto { Text = "Trying this", StoryId = story.Id });
        Assert.Equal(story.AuthorPseudonym, comment.AuthorPseudonym);

        CommunityPseudonymDto rerolled = await service.RerollPseudonymAsync(UserId);

        Assert.Matches(PseudonymShape(), rerolled.Name);
        Assert.NotEqual(story.AuthorPseudonym, rerolled.Name);
        CommunityResponseDto response = await service.GetCommunityAsync(UserId);
        Assert.Equal(rerolled.Name, response.Pseudonym);
        CommunityStoryDto shownStory = Assert.Single(response.Stories);
        Assert.Equal(rerolled.Name, shownStory.AuthorPseudonym);
        Assert.True(shownStory.IsOwn);
        Assert.Equal(rerolled.Name, Assert.Single(response.RecentComments).AuthorPseudonym);
        CommunityStoryDto? seenByAnother = await service.GetStoryByIdAsync("someone-else", story.Id);
        Assert.Equal(rerolled.Name, seenByAnother!.AuthorPseudonym);
        Assert.False(seenByAnother.IsOwn);
        Assert.Single(db.CommunityPseudonyms);
    }

    [Fact]
    public async Task Pseudonym_IsNeverOneAnotherAccountHolds_NorOneReservedByAnAccountInItsGracePeriod()
    {
        await using AppDbContext db = CreateContext();
        List<string> all = PseudonymGenerator.AllNames().ToList();
        string free = all[0];
        var reserved = new CommunityPseudonym { UserId = "leaving", Name = all[1] };
        reserved.SoftDelete();
        db.CommunityPseudonyms.Add(reserved);
        db.CommunityPseudonyms.AddRange(all.Skip(2)
            .Select((name, i) => new CommunityPseudonym { UserId = $"holder-{i}", Name = name }));
        await db.SaveChangesAsync();

        CommunityResponseDto response = await CreateService(db).GetCommunityAsync(UserId);

        Assert.Equal(free, response.Pseudonym);
    }

    [Fact]
    public async Task GetCommunity_ShowsAFormerMembersPosts_WithoutAPseudonym()
    {
        await using AppDbContext db = CreateContext();
        var story = new CommunityStory { UserId = DeletedAccount.UserId, Title = "Kept after the Purge" };
        db.CommunityStories.Add(story);
        db.CommunityComments.Add(new CommunityComment { UserId = DeletedAccount.UserId, Text = "Still here", Story = story });
        await db.SaveChangesAsync();

        CommunityResponseDto response = await CreateService(db).GetCommunityAsync(UserId);

        CommunityStoryDto shownStory = Assert.Single(response.Stories);
        Assert.Null(shownStory.AuthorPseudonym);
        Assert.False(shownStory.IsOwn);
        Assert.Null(Assert.Single(response.RecentComments).AuthorPseudonym);
    }

    [Fact]
    public async Task GetPollComments_AreReportedAsTheViewerSeesThem()
    {
        await using AppDbContext db = CreateContext();
        db.CommunityPolls.Add(new CommunityPoll { PollCode = "habits", Question = "Which habit stuck?", Category = "Goals" });
        await db.SaveChangesAsync();
        CommunityService service = CreateService(db);
        CommunityCommentDto posted = await service.AddCommentAsync(UserId,
            new CommunityCommentRequestDto { Text = "Meal plans", PollCode = "habits" });
        await service.ToggleCommentLikeAsync(UserId, posted.Id);

        CommunityCommentDto mine = Assert.Single(await service.GetPollCommentsAsync(UserId, "habits"));
        CommunityCommentDto theirs = Assert.Single(await service.GetPollCommentsAsync("someone-else", "habits"));

        Assert.Equal(posted.AuthorPseudonym, mine.AuthorPseudonym);
        Assert.True(mine.IsOwn);
        Assert.True(mine.IsLikedByUser);
        Assert.Equal(1, mine.Likes);
        Assert.Equal(posted.AuthorPseudonym, theirs.AuthorPseudonym);
        Assert.False(theirs.IsOwn);
        Assert.False(theirs.IsLikedByUser);
    }

    [Fact]
    public void NoCommunityDto_CarriesADisplayNameOrEmail()
    {
        List<Type> dtos = typeof(CommunityResponseDto).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(CommunityResponseDto).Namespace)
            .ToList();

        Assert.NotEmpty(dtos);
        foreach (PropertyInfo property in dtos.SelectMany(t => t.GetProperties()))
        {
            string name = $"{property.DeclaringType!.Name}.{property.Name}";
            Assert.False(property.Name.Contains("Email", StringComparison.OrdinalIgnoreCase), name);
            Assert.False(property.Name.Contains("UserId", StringComparison.OrdinalIgnoreCase), name);
            Assert.False(property.Name is "Author" or "AuthorName" or "DisplayName" or "UserName", name);
        }
    }

    [Fact]
    public void StoryAndCommentQueries_TranslateToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        CommunityService service = CreateService(db);

        AzureSqlTranslation.AssertTranslates(service.VisibleStoriesQuery(UserId).Take(6));
        AzureSqlTranslation.AssertTranslates(service.VisibleStoriesQuery(UserId).Where(s => s.Id == 1));
        AzureSqlTranslation.AssertTranslates(service.VisibleCommentsQuery(UserId).Take(10));
        AzureSqlTranslation.AssertTranslates(service.VisibleCommentsQuery(UserId).Where(c => c.PollId == 1));
    }

    [Fact]
    public void PseudonymGenerator_WordsAreDistinctTitleCaseWords_AndEveryNameFitsTheColumn()
    {
        string[] words = [.. PseudonymGenerator.Adjectives, .. PseudonymGenerator.Nouns];

        Assert.Equal(words.Length, words.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(words, word => Assert.Matches("^[A-Z][a-z]+$", word));
        List<string> names = PseudonymGenerator.AllNames().ToList();
        Assert.Equal(PseudonymGenerator.Combinations, names.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, name => Assert.InRange(name.Length, 1, 60));
    }

    [Fact]
    public void PseudonymGenerator_Pick_NeverReturnsATakenName_AndNumbersOneOnceEveryNameIsTaken()
    {
        var random = new Random(7);
        var taken = new HashSet<string>(PseudonymGenerator.AllNames().Skip(1), StringComparer.OrdinalIgnoreCase);

        string onlyFree = PseudonymGenerator.Pick(taken, random);
        taken.Add(onlyFree);
        string numbered = PseudonymGenerator.Pick(taken, random);
        taken.Add(numbered);
        string numberedAgain = PseudonymGenerator.Pick(taken, random);

        Assert.Equal(PseudonymGenerator.AllNames().First(), onlyFree);
        Assert.Matches("^[A-Z][a-z]+ [A-Z][a-z]+ 2$", numbered);
        Assert.Matches("^[A-Z][a-z]+ [A-Z][a-z]+ [23]$", numberedAgain);
        Assert.DoesNotContain(numberedAgain, taken);
        Assert.All(Enumerable.Range(0, 200).Select(_ => PseudonymGenerator.Pick(new HashSet<string>(), random)),
            name => Assert.Matches(PseudonymShape(), name));
    }

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

        CommunityStoryDto story = await service.SubmitStoryAsync(UserId, request);

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
    public async Task GetCommunity_StoryCountsComeFromLiveLikesAndVisibleComments()
    {
        await using AppDbContext db = CreateContext();
        CommunityService service = CreateService(db);
        var story = new CommunityStory { UserId = "author", Title = "Meal plans" };
        db.CommunityStories.Add(story);
        db.CommunityComments.Add(new CommunityComment
        { UserId = "author", Text = "hidden", Story = story, IsVisible = false });
        await db.SaveChangesAsync();

        await service.ToggleStoryLikeAsync(UserId, story.Id);
        await service.ToggleStoryLikeAsync("other", story.Id);
        StoryLikeResult unliked = await service.ToggleStoryLikeAsync("other", story.Id);
        CommunityCommentDto comment = await service.AddCommentAsync(UserId,
            new CommunityCommentRequestDto { Text = "Trying this", StoryId = story.Id });

        CommunityResponseDto response = await service.GetCommunityAsync(UserId);

        Assert.Equal(1, unliked.NewLikeCount);
        CommunityStoryDto dto = Assert.Single(response.Stories);
        Assert.Equal(1, dto.Likes);
        Assert.Equal(1, dto.CommentCount);
        Assert.True(dto.IsLikedByUser);
        Assert.Equal(story.Id, comment.StoryId);
        Assert.Equal(story.Id, Assert.Single(response.RecentComments).StoryId);
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
        var request = new CommunityCommentRequestDto { Text = new string('a', 2001) };

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => CreateService(db).AddCommentAsync(UserId, request));

        Assert.Equal(ApiErrorKeys.Community.CommentTooLong, ex.Key);
        Assert.Equal("Text", ex.Field);
        Assert.NotNull(ex.Args);
        Assert.Equal("2000", ex.Args["max"].Value);
        Assert.Equal(ApiErrorArgKind.Number, ex.Args["max"].Kind);
        Assert.Equal("Comment text is too long (max 2000 characters).", ex.Message);
        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.CommunityPseudonyms);
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
            () => CreateService(db).SubmitStoryAsync(UserId, request));

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
            () => CreateService(db).GetPollCommentsAsync(UserId, "no-such-poll"));

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

    /// <summary>"Adjective Noun", each a capitalised word.</summary>
    [GeneratedRegex("^[A-Z][a-z]+ [A-Z][a-z]+$")]
    private static partial Regex PseudonymShape();
}
