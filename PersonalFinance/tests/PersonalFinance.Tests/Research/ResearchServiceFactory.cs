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
