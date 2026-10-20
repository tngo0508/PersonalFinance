# 02: .NET options for tool calling, structured output, and usage tracking

Type: research
Status: resolved
Blocked by: None
Map: [AI Finance Assistant map](../map.md)

## Question

With the `OpenAI` .NET SDK the ApiService already references, how do function/tool calling (including the multi-turn
tool loop), structured outputs (JSON schema), streaming, and reading token usage (`ChatTokenUsage`) work? Compare with
`Microsoft.Extensions.AI` (license, automatic function invocation, `IChatClient` testability, fit with Aspire).
Output: a recommendation with license confirmation (MIT/BSD/Apache-2.0 only) and a minimal code sketch of a tool loop
for each option.

## Answer

- `OpenAI` 2.14.0 is already referenced by ApiService and is MIT-licensed; the service currently uses direct `OpenAIClient` and `CompleteChatAsync` calls.
- The SDK supports manual function loops using `ChatTool.CreateFunctionTool`, `ChatFinishReason.ToolCalls`, assistant tool-call messages, and `ToolChatMessage` results.
- SDK structured outputs use `ChatResponseFormat.CreateJsonSchemaFormat`; `MaxOutputTokenCount` caps output, streaming uses `CompleteChatStreamingAsync`, and `ChatTokenUsage` reports cached input token details when the API/model supplies them.
- `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.OpenAI` have stable MIT `10.10.0` packages; `IChatClient` supports injected fakes for xUnit tests, `AIFunctionFactory` defines tools, and `UseFunctionInvocation` runs the tool loop.
- The provider-neutral `UsageDetails` shape is less explicit than OpenAI's cached-token field, so verify cached-token mapping before relying on it for accounting.
- `Aspire.OpenAI` is MIT but its current `13.6.1-preview.1.26506.6` package is prerelease; it adds DI/telemetry for `OpenAIClient` and is not required for `IChatClient`.
- Recommendation: prefer the stable `IChatClient` adapter for a growing assistant needing fakes/provider portability, preserve and test provider-specific cached-token data, and avoid requiring the preview Aspire package; keep the existing SDK manual loop for a narrow single-provider feature prioritizing exact OpenAI usage access.
- Findings and minimal C# sketches: [findings](../research/02-dotnet-tool-calling.md).
