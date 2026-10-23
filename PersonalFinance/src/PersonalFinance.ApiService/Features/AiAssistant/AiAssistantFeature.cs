using PersonalFinance.ApiService.Features.AiAssistant.Adapters;

namespace PersonalFinance.ApiService.Features.AiAssistant;

/// <summary>
/// Dependency-injection registrations of the AI Assistant Feature (GLOSSARY.md), called from <c>Program.cs</c>
/// (ADR 0007). Its per-user rate limiter is shared and registered by Infrastructure.
/// </summary>
internal static class AiAssistantFeature
{
    /// <summary>Scope filter, spend quota and, when <c>OpenAI:ApiKey</c> is configured, the OpenAI chat client.</summary>
    public static IServiceCollection AddAiAssistantFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAiAssistantService, AiAssistantService>();
        services.AddSingleton<IAiScopeFilter, AiScopeFilter>();
        services.AddScoped<IAiQuotaService, AiQuotaService>();
        services.AddOpenAiChatClient(configuration);
        return services;
    }
}
