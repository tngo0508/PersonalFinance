# Map: AI Finance Assistant: finance-only, cost-capped, and actually useful

Label: wayfinder:map

## Destination

A locked set of decisions, recorded as ADRs in `docs/adr/` (numbered from `0005`) plus a short scope list in this map,
that settles how the AI assistant (1) refuses non-finance requests, (2) stays under a hard **$10/month** OpenAI spend,
and (3) delivers proactive insights, on-demand data lookups, goal coaching, spending reduction, and assisted actions.
Specifying and building happen in separate later efforts (`/to-spec`, `/to-tickets`).

## Notes

- **Domain:** personal finance web app (.NET 10, Aspire). Web (MVC + `wwwroot/js/core/ai-chat-widget.js`) proxies to
  ApiService (`AiAssistantController`, `AiAssistantService`), which calls OpenAI via the `OpenAI` .NET SDK.
- **Skills every session should consult:** `grilling` + `domain-modeling` for grilling tickets, `research` for research
  tickets, `grill-with-docs` when a decision deserves an ADR or a `GLOSSARY.md` term.
- **Standing preferences (from the charting interview on 2026-10-19):**
  - Audience is **invite-only / small circle**. Caps can be light per user, but the global ceiling is hard: **$10/month**.
  - Sending **raw financial data** (individual transactions) to OpenAI is acceptable when it improves the answer.
  - Value priorities: proactive insights, on-demand data lookups (tool calling), goal coaching, spending reduction,
    assisted actions with user confirmation.
  - Repo rules still apply: no MediatR or AutoMapper; MIT/BSD/Apache-2.0 dependencies only; services own logic; soft
    delete; `decimal` money.
- **Current-state facts found while charting** (evidence for tickets, not decisions):
  - `/chat` has **no rate limit**. Only `/summarize` calls `IRateLimiterService`, which is in-memory (10 requests/60 s)
    and resets when the app restarts.
  - `OpenAI:MaxTokens` / `Temperature` are documented in `docs/AI_ASSISTANT_SETUP.md` but **never applied**.
  - The system prompt includes **every transaction** from the latest Drive spreadsheet plus the full monthly breakdown,
    so input cost grows with the data and is paid again on every message.
  - Finance-only scope is enforced only by the system prompt. There is no input-length limit, and the client sends the
    conversation history, so a user can forge `assistant` turns.
  - A new `OpenAIClient` is created for every request. No token usage is logged or stored.
  - `GetUserId()` falls back to `"anonymous"`, so all callers without the header share a single bucket.

## Decisions so far

<!-- one line per closed ticket: [title](link): gist -->

- [OpenAI cost levers and pricing against a $10/month ceiling](issues/01-openai-cost-levers-and-pricing.md): `gpt-4o-mini`
  costs about $0.002 per turn with the current prompt vs about $0.0008 with a compact prompt plus tools (estimated
  ~5k vs ~12k turns per $10). Prompt caching is automatic for stable prefixes, Moderation is free, and the dashboard
  hard spend limit must be switched on explicitly (alerts alone do not block requests).
- [.NET options for tool calling, structured output, and usage tracking](issues/02-dotnet-sdk-tool-calling-and-usage.md):
  `OpenAI` 2.14.0 and `Microsoft.Extensions.AI` 10.10.0 are both MIT and stable. The recommendation is the `IChatClient`
  abstraction with `UseFunctionInvocation`, after checking that cached-token usage still comes through. Avoid the
  preview `Aspire.OpenAI` package.
- [What counts as "finance-related", and how a refusal reads](issues/03-finance-scope-policy.md): In-scope spans broad
  personal finance, app navigation, and salary/career negotiation; out-of-scope requests receive a zero-cost deterministic canned refusal, and advice includes actionable numbers with an informational non-fiduciary disclaimer.

## Not yet specified

- **Goal coaching mechanics:** projection math (contribution needed, ETA), and whether it is deterministic C# exposed as
  a tool or LLM reasoning. Depends on the data-source and context-strategy decisions.
- **Spending-reduction detection:** recurring-charge and subscription detection, outliers, and category trend deltas,
  and which of them are computed server-side vs left to the model.
- **Chat widget UX upgrades:** page-aware quick actions, how proactive insights appear in the widget, streaming
  responses, how quota and "out of scope" states are shown.
- **Quality and guard evaluation:** a fixed set of on-topic, off-topic and jailbreak prompts used to check the guard and
  the answer quality before release, and where that set lives.
- **Prompt-injection surface:** transaction descriptions and spreadsheet text are untrusted input inside the prompt. How
  far to go once tool calling and assisted actions exist.
- **Model selection:** which model(s) to use per job (classifier, chat, insights). Depends on the cost research.

## Out of scope

- **Server-side saved conversations:** the user did not choose it as a priority during charting (2026-10-19). History
  stays in client state unless a later decision requires otherwise.
