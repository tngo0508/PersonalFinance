using Microsoft.Extensions.AI;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// An <see cref="IChatClient"/> that never calls OpenAI: it records every call's messages and options and answers with
/// the handler's response (or throws what the handler throws).
/// </summary>
internal sealed class FakeChatClient(Func<IList<ChatMessage>, ChatOptions?, ChatResponse> handler) : IChatClient
{
    public List<(IList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    /// <summary>Answers every call with <paramref name="text"/> and a small token usage.</summary>
    public static FakeChatClient Replying(string text) =>
        new((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = new UsageDetails { InputTokenCount = 300, OutputTokenCount = 90 }
        });

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        List<ChatMessage> list = messages.ToList();
        Calls.Add((list, options));
        return Task.FromResult(handler(list, options));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
