using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.AiAssistant;
using PersonalFinance.ApiService.Features.MoneyPriorities;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// Builds <see cref="MoneyPrioritiesService"/> with the real AI Assistant behind its public interface, over the same
/// database, with a fake <see cref="IChatClient"/> (or none, as when <c>OpenAI:ApiKey</c> is not set).
/// </summary>
internal static class MoneyPrioritiesTestServices
{
    public static MoneyPrioritiesService Service(AppDbContext db, TimeProvider? timeProvider = null,
        IChatClient? chatClient = null) =>
        new(db, timeProvider ?? TimeProvider.System, AiAssistant(db, chatClient));

    public static AiAssistantService AiAssistant(AppDbContext db, IChatClient? chatClient = null) =>
        new(db, new AllowAllRateLimiter(), new AiQuotaService(db), NullLogger<AiAssistantService>.Instance,
            new ConfigurationBuilder().Build(), TestBudgetDataProviderFactory.Create(db), chatClient);

    private sealed class AllowAllRateLimiter : IRateLimiterService
    {
        public bool TryAllow(string userId) => true;
    }
}
