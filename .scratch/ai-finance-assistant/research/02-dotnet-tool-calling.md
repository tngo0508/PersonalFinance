# .NET tool calling, structured output, and usage

Research date: 2026-10-19

## Repository findings

- `PersonalFinance.ApiService` targets `net10.0` and references `OpenAI`; central package management pins it to `2.14.0` in `PersonalFinance/Directory.Packages.props`.
- `AiAssistantService` currently constructs `OpenAIClient` and calls `ChatClient.CompleteChatAsync` directly. The AppHost uses Aspire `13.6.1`; the solution does not currently reference `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`, or `Aspire.OpenAI`.

## Packages, maturity, and license

| Package | Status observed on 2026-10-19 | License | Fit |
|---|---|---|---|
| `OpenAI` `2.14.0` | Existing stable package in this repository. | MIT ([NuGet](https://www.nuget.org/packages/OpenAI/2.14.0), [SDK license](https://github.com/openai/openai-dotnet/blob/main/LICENSE)). | Direct access to OpenAI-specific request, tool-call, and usage types. No new dependency is required for the manual loop. |
| `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.OpenAI` `10.10.0` | Stable, non-prerelease NuGet releases; both packages had stable `10.10.0` releases by this research date. | MIT ([core package](https://www.nuget.org/packages/Microsoft.Extensions.AI/10.10.0), [OpenAI adapter](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI/10.10.0), [dotnet/extensions license](https://github.com/dotnet/extensions/blob/main/LICENSE)). | Adds the provider-neutral `IChatClient` contract, `AIFunctionFactory`, and function-invocation middleware over the OpenAI SDK. The adapter is an additional package. |
| `Aspire.OpenAI` `13.6.1-preview.1.26506.6` | Prerelease (the NuGet version explicitly contains `preview`); it is not the same maturity as stable Aspire `13.6.1`. | MIT ([NuGet package](https://www.nuget.org/packages/Aspire.OpenAI/13.6.1-preview.1.26506.6), [Aspire license](https://github.com/microsoft/aspire/blob/main/LICENSE)). | Aspire client integration registers an `OpenAIClient` and configures logging, metrics, and tracing. The package documents connection-string configuration and says no dedicated AppHost resource extension is needed. It is optional, and the current solution does not use it. |

The named packages meet the repository's MIT/BSD/Apache-2.0 dependency policy. This is a license check for these packages, not an audit of every transitive dependency.

## OpenAI .NET SDK

The official SDK documents these APIs in its [README](https://github.com/openai/openai-dotnet):

- **Function tools and manual multi-turn loop:** describe each function with `ChatTool.CreateFunctionTool`, add it to `ChatCompletionOptions.Tools`, and call `CompleteChatAsync`. When `ChatCompletion.FinishReason` is `ChatFinishReason.ToolCalls`, append the assistant completion, execute every `ChatCompletion.ToolCalls` entry in application code, append a `ToolChatMessage` for each tool-call id, then call the model again. Continue until the model returns a non-tool finish reason. The application—not the SDK—owns function dispatch, argument validation, authorization, limits, and error handling.
- **Structured output:** set `ChatCompletionOptions.ResponseFormat` using `ChatResponseFormat.CreateJsonSchemaFormat(name, BinaryData.FromString(schema), jsonSchemaIsStrict: true)`. `MaxOutputTokenCount` limits generated output tokens. See the [OpenAI structured outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs) and the SDK [changelog](https://github.com/openai/openai-dotnet/blob/main/CHANGELOG.md) for API/property details.
- **Streaming:** `CompleteChatStreamingAsync` returns streaming updates for `await foreach`; accumulate content updates for text. Tool calls arrive as partial updates and must be assembled before dispatch. Usage should be read only when the service emits it in the completed stream update; the non-streaming completion usage is more direct.
- **Usage:** read `ChatCompletion.Usage` (`ChatTokenUsage`) for `InputTokenCount`, `OutputTokenCount`, and `TotalTokenCount`; `InputTokenDetails.CachedTokenCount` exposes cached input tokens when supplied by the API/model. Do not assume every model or compatible endpoint reports cached-token detail.

Minimal sketch; the tool implementation must independently enforce that `accountId` belongs to the signed-in user:

```csharp
async Task<string> RunAsync(ChatClient chatClient, string prompt, CancellationToken cancellationToken)
{
    var options = new ChatCompletionOptions { MaxOutputTokenCount = 400 };
    options.Tools.Add(ChatTool.CreateFunctionTool(
        functionName: "get_balance",
        functionDescription: "Gets the signed-in user's balance for an authorized account.",
        functionParameters: BinaryData.FromString("""{"type":"object","properties":{"accountId":{"type":"string"}},"required":["accountId"],"additionalProperties":false}""")));

    var messages = new List<ChatMessage> { new UserChatMessage(prompt) };
    while (true)
    {
        ChatCompletion completion = (await chatClient.CompleteChatAsync(
            messages, options, cancellationToken: cancellationToken)).Value;
        if (completion.FinishReason != ChatFinishReason.ToolCalls)
        {
            return completion.Content[0].Text;
        }

        messages.Add(new AssistantChatMessage(completion));
        foreach (ChatToolCall call in completion.ToolCalls)
        {
            if (call.FunctionName != "get_balance")
            {
                throw new InvalidOperationException("Unsupported tool.");
            }

            using JsonDocument args = JsonDocument.Parse(call.FunctionArguments);
            string accountId = args.RootElement.GetProperty("accountId").GetString()!;
            string result = await GetAuthorizedBalanceAsync(accountId, cancellationToken);
            messages.Add(new ToolChatMessage(call.Id, result));
        }
    }
}
```

## Microsoft.Extensions.AI and OpenAI adapter

`Microsoft.Extensions.AI.OpenAI` adapts an OpenAI `ChatClient` to `IChatClient` ([adapter README](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.OpenAI/README.md)). `ChatClientBuilder.UseFunctionInvocation()` installs middleware that runs the tool-call/response loop automatically; `AIFunctionFactory.Create` exposes a delegate as a callable tool. This removes loop plumbing, not the need to restrict functions, validate inputs, check user authorization, cap calls, and handle failures. See the [`UseFunctionInvocation` API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.functioninvokingchatclientbuilderextensions.usefunctioninvocation?view=net-10.0).

Minimal automatic-loop sketch:

```csharp
IChatClient provider = new OpenAI.Chat.ChatClient(model, apiKey).AsIChatClient();
IChatClient client = new ChatClientBuilder(provider)
    .UseFunctionInvocation()
    .Build();

ChatOptions options = new()
{
    Tools = [AIFunctionFactory.Create(GetAuthorizedBalance)]
};
ChatResponse response = await client.GetResponseAsync(prompt, options, cancellationToken);
Console.WriteLine(response.Text);

[Description("Gets a balance for an account the signed-in user is authorized to access.")]
static Task<decimal> GetAuthorizedBalance(string accountId) =>
    LoadAuthorizedBalanceAsync(accountId);
```

- **Testability:** application services can depend on `IChatClient`; tests can inject a small fake that returns predetermined `ChatResponse` objects, exercising prompts and service behavior without an OpenAI network call. This is useful with the repository's xUnit tests. The current direct `new OpenAIClient(...)` construction is less convenient to fake.
- **Structured output:** use `ChatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema<T>()` to express a schema through the common API; the OpenAI adapter maps it to the provider's structured-output request. See the [`ForJsonSchema` API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatresponseformat.forjsonschema?view=net-10.0).
- **Streaming and usage:** `IChatClient` has streaming response APIs and a common `ChatResponse.Usage` value (`UsageDetails`) for normalized token counts. Provider-specific detail such as OpenAI cached-token counts is not as explicit in the provider-neutral contract as `ChatTokenUsage.InputTokenDetails.CachedTokenCount`; verify whether the chosen adapter/version preserves the required cached-token field (for example, via additional/raw provider data) before using it for accounting.
- **Aspire:** [Aspire's OpenAI client integration](https://aspire.dev/integrations/ai/openai/openai-connect) provides named DI clients and OpenTelemetry configuration; adding Microsoft.Extensions.AI is a separate choice for the `IChatClient` abstraction. The package currently surfaced by NuGet, `Aspire.OpenAI`, is preview. Avoid making that prerelease package a requirement unless its features justify accepting preview status; the stable Microsoft.Extensions.AI/OpenAI adapter can be registered with normal DI without it.

## Recommendation

For this .NET 10 service, prefer injecting `IChatClient` through the stable MIT `Microsoft.Extensions.AI.OpenAI` adapter if the assistant is being expanded and service-level fakeability/provider portability matter; use `UseFunctionInvocation` for the basic loop, while keeping the tool allowlist, authorization, argument checks, and call limits explicit. Retain or expose the OpenAI-specific usage data where cached-token accounting is needed, and add tests for that mapping. Do not adopt `Aspire.OpenAI` solely for tool calling while its current package is preview. If the feature remains a single-provider, narrow change and exact OpenAI token details outweigh provider-neutral testing, the existing SDK's manual loop is also a sound minimal option.

## Sources

- [OpenAI .NET SDK README](https://github.com/openai/openai-dotnet)
- [OpenAI .NET SDK changelog](https://github.com/openai/openai-dotnet/blob/main/CHANGELOG.md)
- [OpenAI structured outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs)
- [Microsoft.Extensions.AI.OpenAI README](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.OpenAI/README.md)
- [Microsoft.Extensions.AI `UseFunctionInvocation` API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.functioninvokingchatclientbuilderextensions.usefunctioninvocation?view=net-10.0)
- [Microsoft.Extensions.AI JSON schema API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatresponseformat.forjsonschema?view=net-10.0)
- [Aspire OpenAI client integration](https://aspire.dev/integrations/ai/openai/openai-connect)
- [NuGet `OpenAI` 2.14.0](https://www.nuget.org/packages/OpenAI/2.14.0), [NuGet `Microsoft.Extensions.AI` 10.10.0](https://www.nuget.org/packages/Microsoft.Extensions.AI/10.10.0), [NuGet `Microsoft.Extensions.AI.OpenAI` 10.10.0](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI/10.10.0), [NuGet `Aspire.OpenAI` preview](https://www.nuget.org/packages/Aspire.OpenAI/13.6.1-preview.1.26506.6)