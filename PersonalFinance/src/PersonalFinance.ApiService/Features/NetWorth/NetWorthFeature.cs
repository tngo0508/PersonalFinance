using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.NetWorth;

/// <summary>
/// Dependency-injection registrations of the Net Worth Feature (GLOSSARY.md), called from <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class NetWorthFeature
{
    /// <summary>Net Worth parameters and projections.</summary>
    public static IServiceCollection AddNetWorthFeature(this IServiceCollection services)
    {
        services.AddScoped<INetWorthParametersService, NetWorthParametersService>();
        return services;
    }
}
