using Microsoft.Extensions.AI;
using OpenAI;

namespace PersonalFinance.ApiService.Features.AiAssistant.Adapters;

/// <summary>
/// The OpenAI chat client the AI Assistant talks to, with automatic tool calling. Registered only when
/// <c>OpenAI:ApiKey</c> is configured; without it no <see cref="IChatClient"/> exists and the assistant answers with
/// its "unavailable" fallback.
/// </summary>
internal static class OpenAiChatClient
{
    private const string DefaultModel = "gpt-4o-mini";

    public static IServiceCollection AddOpenAiChatClient(this IServiceCollection services, IConfiguration configuration)
    {
        string? openAiApiKey = configuration.GetValue<string>("OpenAI:ApiKey");
        if (string.IsNullOrWhiteSpace(openAiApiKey))
        {
            return services;
        }

        string openAiModel = configuration.GetValue<string>("OpenAI:Model") ?? DefaultModel;
        services.AddSingleton<IChatClient>(sp =>
            new OpenAIClient(openAiApiKey)
                .GetChatClient(openAiModel)
                .AsIChatClient()
                .AsBuilder()
                .UseFunctionInvocation(sp.GetService<ILoggerFactory>(),
                    client => client.MaximumIterationsPerRequest = 3)
                .Build());
        return services;
    }
}
