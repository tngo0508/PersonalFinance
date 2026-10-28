namespace PersonalFinance.ApiService.Features.CurrencyConverter;

/// <summary>
/// Dependency-injection registrations of the Currency Converter Feature (GLOSSARY.md), called from <c>Program.cs</c>
/// (ADR 0007). Its validator is registered with the others from the assembly.
/// </summary>
internal static class CurrencyConverterFeature
{
    /// <summary>The stateless Currency Converter over the shared <c>ICurrencyConverter</c>.</summary>
    public static IServiceCollection AddCurrencyConverterFeature(this IServiceCollection services)
    {
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        return services;
    }
}
