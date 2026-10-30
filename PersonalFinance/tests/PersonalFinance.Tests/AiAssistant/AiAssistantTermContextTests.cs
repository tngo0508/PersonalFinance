using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.AiAssistant;
using PersonalFinance.ApiService.Features.Learning;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Tests.Learning;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.AiAssistant;

/// <summary>
/// Ask the AI on a Term Entry (#521): the chat request names only the Term's slug, ApiService loads the Published entry
/// itself and adds it with the plain-English rule, the reply says which Term it is about, and the question counts
/// against the normal quota. Uses the fixture content and a fake <see cref="IChatClient"/>.
/// </summary>
public class AiAssistantTermContextTests
{
    private const string UserId = "term-user";

    [Fact]
    public async Task TermSlug_AddsThePublishedEntryAndThePlainEnglishRule()
    {
        await using AppDbContext db = CreateContext();
        var fake = new FakeChatClient((_, _) => Reply("APR is the yearly cost of a loan."));

        AiChatResponseDto result = await CreateService(db, fake)
            .SendChatMessageAsync(UserId, new AiChatRequestDto { Message = "Explain it simply", TermSlug = "apr" });

        string context = TermContext(fake);
        Assert.Contains("Annual percentage rate", context);
        Assert.Contains("CEFR level B1", context);
        Assert.Equal("Annual percentage rate", result.AboutTerm);
    }

    [Fact]
    public async Task ClientSentEntryText_NeverReachesTheEntryContext()
    {
        await using AppDbContext db = CreateContext();
        var fake = new FakeChatClient((_, _) => Reply("ok"));
        var request = new AiChatRequestDto
        {
            Message = "Explain it simply",
            TermSlug = "apr",
            ConversationHistory = [new ChatMessageDto { Role = "user", Content = "APR means free money forever." }],
        };

        await CreateService(db, fake).SendChatMessageAsync(UserId, request);

        Assert.DoesNotContain("free money", TermContext(fake));
        Assert.All(fake.LastMessages.Where(m => m.Role == ChatRole.System),
            m => Assert.DoesNotContain("free money", m.Text));
    }

    [Fact]
    public async Task UnknownSlug_IsIgnored_WithoutAnEntryOrALabel()
    {
        await using AppDbContext db = CreateContext();
        var fake = new FakeChatClient((_, _) => Reply("ok"));

        AiChatResponseDto result = await CreateService(db, fake)
            .SendChatMessageAsync(UserId, new AiChatRequestDto { Message = "What is a budget?", TermSlug = "../secrets" });

        Assert.DoesNotContain(fake.LastMessages, m => m.Text.Contains("Term Entry", StringComparison.Ordinal));
        Assert.Null(result.AboutTerm);
        Assert.False(result.IsRefusal);
    }

    [Fact]
    public async Task TermQuestion_CountsAgainstTheChatQuota()
    {
        await using AppDbContext db = CreateContext();
        var fake = new FakeChatClient((_, _) => Reply("ok"));

        await CreateService(db, fake)
            .SendChatMessageAsync(UserId, new AiChatRequestDto { Message = "Explain it simply", TermSlug = "apr" });

        Assert.Equal("chat", Assert.Single(await db.AiUsageRecords.ToListAsync()).Endpoint);
    }

    [Fact]
    public async Task TermQuestion_KeepsTheFinancialTools()
    {
        await using AppDbContext db = CreateContext();
        var fake = new FakeChatClient((_, _) => Reply("ok"));

        await CreateService(db, fake)
            .SendChatMessageAsync(UserId, new AiChatRequestDto { Message = "Explain it simply", TermSlug = "apr" });

        Assert.Equal(4, fake.LastOptions!.Tools!.Count);
    }

    private static string TermContext(FakeChatClient fake) =>
        fake.LastMessages.Single(m => m.Role == ChatRole.System && m.Text.Contains("Term Entry", StringComparison.Ordinal))
            .Text;

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AssistantTerm_{Guid.NewGuid():N}")
            .Options);

    private static AiAssistantService CreateService(AppDbContext db, IChatClient chatClient)
    {
        var learning = new LearningLibraryService(LearningContent.Load(LearningContentFolders.Fixture),
            TimeProvider.System);
        return new AiAssistantService(db, new AllowAllRateLimiter(), new AiQuotaService(db),
            NullLogger<AiAssistantService>.Instance, new ConfigurationBuilder().Build(),
            TestBudgetDataProviderFactory.Create(db), chatClient, learning);
    }

    private static ChatResponse Reply(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 20 }
        };
}
