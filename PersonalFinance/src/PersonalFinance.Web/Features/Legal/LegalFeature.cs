namespace PersonalFinance.Web.Features.Legal;

/// <summary>
/// Dependency-injection registrations of the Legal Feature, called from <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class LegalFeature
{
    /// <summary>The Consent an Account gives to the Terms of Service and Privacy Policy at registration.</summary>
    public static IServiceCollection AddLegalFeature(this IServiceCollection services)
    {
        services.AddScoped<IUserConsentService, UserConsentService>();
        return services;
    }
}
