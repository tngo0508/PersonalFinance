using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Data;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// Builds the Research service the way the ApiService does (<c>AddResearchFeature</c>), over a test database, source
/// clients, settings and clock.
/// </summary>
internal static class ResearchServiceFactory
{
    /// <summary>
    /// The service with its real refreshers. <paramref name="logs"/>, when given, receives every Research log entry.
    /// Without <paramref name="fedClient"/> or <paramref name="usAgencyClient"/> those sources cannot be reached, as
    /// when their sites are down, so no test goes out to the network.
    /// </summary>
    public static ResearchService Create(AppDbContext db, ISecEdgarClient secClient, IWorldBankClient worldBankClient,
        ResearchSettings settings, TimeProvider timeProvider, ILoggerProvider? logs = null,
        IFedRatesClient? fedClient = null, IUsAgencyClient? usAgencyClient = null)
    {
        IServiceCollection services = new ServiceCollection()
            .AddLogging(logging =>
            {
                if (logs is not null)
                {
                    logging.AddProvider(logs);
                }
            })
            .AddResearchFeature(new ConfigurationBuilder().Build());
        services.Replace(ServiceDescriptor.Singleton(db));
        services.Replace(ServiceDescriptor.Singleton(secClient));
        services.Replace(ServiceDescriptor.Singleton(worldBankClient));
        services.Replace(ServiceDescriptor.Singleton(fedClient ?? new OfflineFedRatesClient()));
        services.Replace(ServiceDescriptor.Singleton(usAgencyClient ?? new OfflineUsAgencyClient()));
        services.Replace(ServiceDescriptor.Singleton(settings));
        services.Replace(ServiceDescriptor.Singleton(timeProvider));
        return (ResearchService)services.BuildServiceProvider().GetRequiredService<IResearchService>();
    }

    /// <summary>
    /// The SEC EDGAR client over <paramref name="handler"/>, with a request gate that never waits: the gate's clock
    /// moves forward a second on every read, so each request finds its slot already open. Over a frozen clock the gate
    /// sleeps for real on every request after the first (<c>Task.Delay</c> on a system timer, about 15 ms on Windows),
    /// which turned the 500-filer tests into seconds of waiting (#350). The service keeps its own fixed clock.
    /// </summary>
    public static SecEdgarClient SecClient(HttpMessageHandler handler, SecEdgarSettings settings) =>
        new(new HttpClient(handler), settings, new SecRequestGate(settings, new AdvancingTimeProvider()));

    /// <summary>A clock that moves forward one second each time it is read.</summary>
    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private static readonly DateTimeOffset Start = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        private long _reads;

        public override DateTimeOffset GetUtcNow() => Start.AddSeconds(Interlocked.Increment(ref _reads));
    }

    /// <summary>A Fed client whose site cannot be reached, so no test goes out to the network.</summary>
    private sealed class OfflineFedRatesClient : IFedRatesClient
    {
        public Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> GetTreasuryBillRatesAsync(
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("The Fed's site is not reachable in tests.");

        public Task<FedMonthlyRates> GetMonthlyRatesAsync(CancellationToken cancellationToken) =>
            throw new HttpRequestException("The Fed's site is not reachable in tests.");
    }

    /// <summary>A BLS and Treasury client whose sites cannot be reached.</summary>
    private sealed class OfflineUsAgencyClient : IUsAgencyClient
    {
        public Task<IReadOnlyList<(DateOnly Month, decimal Index)>> GetConsumerPricesAsync(int firstYear,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("BLS is not reachable in tests.");

        public Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> GetTreasuryBillRatesAsync(int year,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("The Treasury is not reachable in tests.");
    }
}
