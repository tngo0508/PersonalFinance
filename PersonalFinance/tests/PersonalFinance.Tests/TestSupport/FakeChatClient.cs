using Microsoft.Extensions.AI;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// An <see cref="IChatClient"/> that never calls OpenAI: it records every call's messages and options and answers with
/// the handler's response (or throws what the handler throws). A handler may be asynchronous, for tests that invoke
/// the tools it is offered before answering.
/// </summary>
internal sealed class FakeChatClient(Func<IList<ChatMessage>, ChatOptions?, Task<ChatResponse>> handler) : IChatClient
{
    public FakeChatClient(Func<IList<ChatMessage>, ChatOptions?, ChatResponse> handler)
        : this((messages, options) => Task.FromResult(handler(messages, options)))
    {
    }

    public List<(IList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    /// <summary>The messages of the latest call; none before the first.</summary>
    public IList<ChatMessage> LastMessages => Calls.Count == 0 ? [] : Calls[^1].Messages;

    /// <summary>The options of the latest call; null before the first.</summary>
    public ChatOptions? LastOptions => Calls.Count == 0 ? null : Calls[^1].Options;

    /// <summary>Fails every call with <paramref name="failure"/>, as an unreachable or rejecting OpenAI would.</summary>
    public static FakeChatClient Throwing(Exception failure) =>
        new((_, _) => Task.FromException<ChatResponse>(failure));

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
        return handler(list, options);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
