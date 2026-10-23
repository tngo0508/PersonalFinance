namespace PersonalFinance.ApiService.Features.AccountLifecycle;

/// <summary>
/// Dependency-injection registrations of the Account Lifecycle Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class AccountLifecycleFeature
{
    /// <summary>The Data Export, the Deletion Request and the Purge (ADR 0005).</summary>
    public static IServiceCollection AddAccountLifecycleFeature(this IServiceCollection services)
    {
        services.AddScoped<IDataExportService, DataExportService>();
        services.AddScoped<IAccountDeletionService, AccountDeletionService>();
        services.AddScoped<IAccountPurgeService, AccountPurgeService>();
        return services;
    }
}
