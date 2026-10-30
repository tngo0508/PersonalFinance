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
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// Characterization of <see cref="ICommunityService"/>: the Pseudonym an Account gets and re-rolls and how every post
/// shows it, how an active poll is reported to the Account viewing it, and the keyed <see cref="ApiErrorException"/>
/// each rejection throws. Runs against an in-memory database; the queries that join each post to its author's
/// Pseudonym are also checked to translate to Azure SQL. Strategies and their review are in
/// <c>CommunityStrategyTests</c>.
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

    internal static CommunityStrategyRequestDto ValidStrategy(string category = "Groceries") => new()
    {
        Category = category,
        Title = "The Sunday pantry audit",
        Tried = "Checking the cupboard before writing the shopping list.",
        Happened = "Far fewer duplicate buys.",
        Keeping = "Ten minutes, coffee in hand, every Sunday.",
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
        CommunityStrategyDto strategy = await service.SubmitStrategyAsync(UserId, ValidStrategy());
        await service.ReviewStrategyAsync(strategy.Id, CommunityStrategyStatus.Approved);
        CommunityCommentDto comment = await service.AddCommentAsync(UserId,
            new CommunityCommentRequestDto { Text = "Trying this", StrategyId = strategy.Id });
        Assert.Equal(strategy.AuthorPseudonym, comment.AuthorPseudonym);

        CommunityPseudonymDto rerolled = await service.RerollPseudonymAsync(UserId);

        Assert.Matches(PseudonymShape(), rerolled.Name);
        Assert.NotEqual(strategy.AuthorPseudonym, rerolled.Name);
        CommunityResponseDto response = await service.GetCommunityAsync(UserId);
        Assert.Equal(rerolled.Name, response.Pseudonym);
        CommunityStrategyDto shown = Assert.Single(response.Strategies);
        Assert.Equal(rerolled.Name, shown.AuthorPseudonym);
        Assert.True(shown.IsOwn);
        Assert.Equal(rerolled.Name, Assert.Single(shown.Comments).AuthorPseudonym);
        CommunityStrategyDto seenByAnother = Assert.Single((await service.GetCommunityAsync("someone-else")).Strategies);
        Assert.Equal(rerolled.Name, seenByAnother.AuthorPseudonym);
        Assert.False(seenByAnother.IsOwn);
        Assert.Equal(2, db.CommunityPseudonyms.Count());
        Assert.Single(db.CommunityPseudonyms, p => p.UserId == UserId);
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
        var strategy = new CommunityStrategy
        {
            UserId = DeletedAccount.UserId,
            Title = "Kept after the Purge",
            Status = CommunityStrategyStatus.Approved,
        };
        db.CommunityStrategies.Add(strategy);
        db.CommunityComments.Add(new CommunityComment { UserId = DeletedAccount.UserId, Text = "Still here", Strategy = strategy });
        await db.SaveChangesAsync();

        CommunityResponseDto response = await CreateService(db).GetCommunityAsync(UserId);

        CommunityStrategyDto shown = Assert.Single(response.Strategies);
        Assert.Null(shown.AuthorPseudonym);
        Assert.False(shown.IsOwn);
        Assert.Null(Assert.Single(shown.Comments).AuthorPseudonym);
    }

    [Fact]
    public async Task GetCommunity_GivesEachPollItsThread_OldestFirst_AsTheViewerSeesIt()
    {
        await using AppDbContext db = CreateContext();
        db.CommunityPolls.Add(new CommunityPoll { PollCode = "habits", Question = "Which habit stuck?", Category = "Goals" });
        await db.SaveChangesAsync();
        CommunityService service = CreateService(db);
        CommunityCommentDto first = await service.AddCommentAsync(UserId,
            new CommunityCommentRequestDto { Text = "Meal plans", PollCode = "habits" });
        CommunityCommentDto second = await service.AddCommentAsync("someone-else",
            new CommunityCommentRequestDto { Text = "A spending pause", PollCode = "habits" });

        CommunityPollDto mine = Assert.Single((await service.GetCommunityAsync(UserId)).ActivePolls);
        CommunityPollDto theirs = Assert.Single((await service.GetCommunityAsync("someone-else")).ActivePolls);

        Assert.Equal([first.Id, second.Id], mine.Comments.Select(c => c.Id));
        Assert.True(mine.Comments[0].IsOwn);
        Assert.False(mine.Comments[1].IsOwn);
        Assert.False(theirs.Comments[0].IsOwn);
        Assert.Equal(first.AuthorPseudonym, theirs.Comments[0].AuthorPseudonym);
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
    public void StrategyAndCommentQueries_TranslateToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        CommunityService service = CreateService(db);

        AzureSqlTranslation.AssertTranslates(service.BoardStrategiesQuery(UserId, null).Take(CommunityStrategyLimits.BoardSize));
        AzureSqlTranslation.AssertTranslates(service.BoardStrategiesQuery(UserId, "Debt").Take(CommunityStrategyLimits.BoardSize));
        AzureSqlTranslation.AssertTranslates(service.ReviewQueueQuery(CommunityStrategyStatus.Pending));
        AzureSqlTranslation.AssertTranslates(service.ThreadCommentsQuery(UserId, [1, 2], [3]));
        AzureSqlTranslation.AssertTranslates(service.CommentReviewQuery().Take(CommunityCommentLimits.ReviewListSize));
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

            Assert.Equal(17, keys.Count);
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
            ApiErrorKeys.Community.CommentTooLong => ErrorArgs.Number("max", CommunityCommentLimits.TextMaxLength),
            ApiErrorKeys.Community.StrategyTitle => ErrorArgs.Number("max", CommunityStrategyLimits.TitleMaxLength),
            ApiErrorKeys.Community.StrategyTried or ApiErrorKeys.Community.StrategyHappened
                or ApiErrorKeys.Community.StrategyKeeping => ErrorArgs.Number("max", CommunityStrategyLimits.TextMaxLength),
            _ => null,
        };
        return args?.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    /// <summary>"Adjective Noun", each a capitalised word.</summary>
    [GeneratedRegex("^[A-Z][a-z]+ [A-Z][a-z]+$")]
    private static partial Regex PseudonymShape();
}
