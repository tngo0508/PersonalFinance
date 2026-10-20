# 03: Refactor to on-demand tool calling with compact prompt

**Status:** ready-for-agent

**Blocked by:** 01, 02

## What to build

The AI assistant currently sends the user's entire transaction list (hundreds of transactions) plus 12 months of budget breakdowns to OpenAI on every message, making each call expensive and slow. This ticket refactors the assistant to use `Microsoft.Extensions.AI` `IChatClient` with on-demand tool calling. Instead of stuffing all data into the prompt, the model fetches only the data it needs through read-only function tools. The system prompt is reduced to a compact, cacheable financial snapshot.

## Acceptance criteria

- [ ] `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.OpenAI` packages are added to the ApiService project (MIT licensed, stable version)
- [ ] `AiAssistantService` is refactored to use `IChatClient` with `UseFunctionInvocation()` instead of constructing `OpenAIClient` directly per request
- [ ] `IChatClient` is registered in `Program.cs` with normal DI (no preview `Aspire.OpenAI` package)
- [ ] The system prompt is replaced with a compact financial snapshot (net worth parameters, budget allocations summary, savings goals overview) that is stable across calls to benefit from prompt caching
- [ ] Read-only financial tools are implemented for on-demand data retrieval:
  - `get_budget_summary`: Returns current month's budget vs actual spending by category
  - `get_recent_transactions`: Searches transactions by date range, category, or description
  - `get_savings_goals`: Returns savings goal progress and projections
  - `get_net_worth_details`: Returns net worth parameters and retirement projections
- [ ] Tool code lives in services within `PersonalFinance.ApiService/Services/`, following the existing pattern where services own business logic
- [ ] All tool calls are scoped to the authenticated `X-User-Id` — one user cannot access another user's financial data
- [ ] Maximum tool rounds per user turn is limited (2-3 rounds) to prevent infinite loops and control cost
- [ ] The `max_completion_tokens` setting from ticket 02 is applied through `IChatClient`
- [ ] Token usage (including cached tokens if available through the adapter) is recorded via the quota service from ticket 02
- [ ] Unit tests use a fake `IChatClient` (via `Microsoft.Extensions.AI`) to verify: tool calling works correctly (e.g., `get_recent_transactions` is called for spending questions), system prompt caching is respected, and end-to-end conversation flows produce responses with disclaimers
- [ ] `dotnet test PersonalFinance.sln` passes

## Blocked by

- 01: Finance scope pre-filter and canned refusal logic (needed for scope enforcement before tool calls)
- 02: Token usage tracking and spend cap protection (needed for quota checks and token recording)
