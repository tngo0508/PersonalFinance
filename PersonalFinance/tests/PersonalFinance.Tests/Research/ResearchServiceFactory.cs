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
    /// Without <paramref name="fedClient"/> the Fed's rates cannot be read, as when its site is down.
    /// </summary>
    public static ResearchService Create(AppDbContext db, ISecEdgarClient secClient, IWorldBankClient worldBankClient,
        ResearchSettings settings, TimeProvider timeProvider, ILoggerProvider? logs = null,
        IFedRatesClient? fedClient = null)
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
    }
}
