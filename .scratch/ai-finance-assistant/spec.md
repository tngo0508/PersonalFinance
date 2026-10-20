# Enhanced AI Finance Assistant

Status: ready-for-agent
Map: [AI Finance Assistant wayfinder map](map.md)

## Problem Statement

The current AI assistant in the PersonalFinance application is a basic chatbot that provides insufficient value to users and lacks safeguards against API abuse. Every user message sends the entire transaction history (potentially hundreds of transactions) plus 12 months of budget breakdowns to OpenAI on every turn, making each call expensive and slow. There is no rate limit on the `/chat` endpoint, no token usage tracking, and no mechanism to enforce the hard $10/month OpenAI budget ceiling. The assistant has no restriction to finance-related topics, so a user could consume the entire monthly budget on non-financial questions. The assistant cannot proactively surface insights, coach savings goals, detect spending patterns, or propose actionable changes — it only responds to direct questions with generic advice based on stale, over-included context.
## Solution

Transform the AI assistant into a finance-focused, cost-controlled, and genuinely useful personal finance assistant. The assistant will be strictly restricted to personal finance topics through deterministic pre-filtering and canned refusals, ensuring out-of-scope requests cost zero tokens. Token usage will be tracked persistently per user and globally, with a hard kill switch when the $10/month ceiling is approached. Instead of stuffing every transaction into the prompt, the assistant will use on-demand tool calls (via `Microsoft.Extensions.AI` `IChatClient`) to fetch only the data relevant to the user's question. The assistant will provide proactive insights (unprompted alerts about budget overruns), goal coaching (savings goal progress projections), spending reduction analysis (subscription detection, outlier transactions, category trends), and assisted actions (proposing budget or goal changes that the user confirms before they are applied).

## User Stories

1. As a user, I want to ask questions about my spending and receive answers based on my actual financial data, so that I can understand where my money goes.
2. As a user, I want to ask "How much did I spend on Groceries last month?" and get a precise answer, so that I don't need to manually calculate from spreadsheets.
3. As a user, I want the assistant to tell me when a budget category is over budget, so that I can adjust my spending before the month ends.
4. As a user, I want the assistant to suggest specific spending cuts based on my transaction history, so that I can reduce unnecessary expenses.
5. As a user, I want to see how close I am to reaching my savings goals and when I'll reach them, so that I can plan my contributions.
6. As a user, I want the assistant to propose budget adjustments (like reducing Entertainment by $50) and let me confirm before applying, so that I can make changes without navigating forms.
7. As a user, I want to ask about retirement planning based on my net worth parameters, so that I can understand if I'm on track.
8. As a user, I want to ask general financial literacy questions (tax concepts, emergency fund targets, debt payoff strategies), so that I can learn and make better decisions.
9. As a user, I want to ask how to use app features, so that I can navigate the PersonalFinance app more effectively.
10. As a user, I want to ask about salary negotiation and career compensation, so that I can improve my income planning.
11. As a user, I want to see a disclaimer that the assistant's advice is informational and not professional financial advice, so that I understand the limitations.
12. As a user, I want to greet the assistant ("hi", "thanks") and receive a brief, friendly response, so that the interaction feels natural.
13. As a system owner, I want non-finance questions to be rejected instantly with a canned message, so that no API tokens are wasted on off-topic requests.
14. As a system owner, I want the total OpenAI spend to never exceed $10/month, so that I control my costs.
15. As a system owner, I want to see how much each user has spent on API calls, so that I can identify heavy users.
16. As a system owner, I want the assistant to stop accepting requests when the monthly budget is nearly exhausted, so that I don't overspend.
17. As a system owner, I want token usage recorded in the database, so that the data persists across app restarts.
18. As a system owner, I want the assistant to fetch data on demand through tools instead of sending everything, so that prompts are smaller and cheaper.
19. As a system owner, I want the conversation history to be protected from client-side forgery, so that a user cannot fake the assistant's earlier replies.
20. As a system owner, I want the assistant to detect recurring charges and subscriptions in my transaction data, so that I can identify and cut unnecessary subscriptions.
21. As a system owner, I want the assistant to identify outlier transactions (unusually large expenses), so that I can review them.
22. As a system owner, I want proactive insights shown in the chat widget or dashboard, so that I see warnings without having to ask.
23. As a system owner, I want the assistant to handle small talk gracefully without wasting tokens, so that the experience is polished.
24. As a system owner, I want the `/chat` endpoint to have rate limiting, so that a single user cannot flood the API.
25. As a system owner, I want `max_completion_tokens` to be set, so that a single response cannot generate excessive output.
26. As a system owner, I want the assistant to use prompt caching for stable system instructions, so that repeated calls benefit from cached-input discounts.
27. As a system owner, I want the assistant to scope all tool calls to the authenticated user, so that one user cannot access another user's financial data.
28. As a system owner, I want the assistant to never write to the database without explicit user confirmation, so that accidental changes are prevented.
29. As a system owner, I want to configure the OpenAI hard spend limit in the dashboard, so that there is a second layer of protection beyond application code.
30. As a system owner, I want the assistant to provide actionable recommendations with specific numbers (e.g., "Cut Food spending by $100/month"), so that advice is concrete and useful.

## Implementation Decisions

### Scope Enforcement

- **Deterministic pre-filter:** Before any OpenAI call, the user's message is checked against a set of deterministic rules (length limits, obvious non-finance patterns, greeting detection). If the message is clearly off-topic, a canned refusal is returned immediately with zero token cost.
- **Canned refusal messages:** Fixed, deterministic strings for out-of-scope requests. No LLM-generated refusals, to eliminate completion token costs.
- **Small talk handling:** Standard greetings ("hi", "thanks", "hello") receive a concise canned acknowledgement that guides the user back to financial topics.
- **System prompt reinforcement:** The system prompt includes explicit instructions about the finance-only scope, but this is a defense-in-depth layer, not the primary enforcement.
- **In-scope domains:** Broad personal finance (budgeting, expense categorization, emergency funds, debt reduction, retirement horizons, tax concepts, insurance basics, general asset allocation), app navigation and feature support, and income/career compensation discussions.
- **Out-of-scope domains:** General programming, non-financial trivia, creative writing, political/medical/legal advice, and individual speculative stock or cryptocurrency picking.
- **Advice depth:** The assistant provides specific, actionable recommendations accompanied by an explicit non-fiduciary disclaimer stating it is an AI tool for informational and planning purposes only.

### Cost Controls

- **Persistent token tracking:** A new `AiUsageRecord` entity stores per-request token counts (prompt tokens, completion tokens, cached tokens, estimated cost) in the database via EF Core. This replaces the in-memory rate limiter as the primary quota mechanism.
- **Global monthly budget:** The system tracks the aggregate estimated cost across all users. When the running total approaches the $10/month ceiling (configurable threshold, e.g., 90%), all AI calls are rejected with HTTP 429.
- **Per-user daily limits:** Each user is limited to a configurable number of messages per day (e.g., 30 messages) to prevent a single user from consuming the entire budget.
- **Rate limiting on `/chat`:** The `/chat` endpoint gains rate limiting (currently only `/summarize` has it). The rate limiter is enhanced to use persistent storage instead of in-memory state.
- **`max_completion_tokens`:** Set to a reasonable cap (e.g., 400 tokens) to prevent excessive output. This is configured via the `OpenAI:MaxTokens` configuration key, which is currently documented but never applied.
- **OpenAI dashboard hard limit:** In addition to application-level controls, the OpenAI project dashboard should have a hard enforced spend limit configured (alerts alone do not block requests).

### Prompt Architecture

- **Compact system prompt:** Instead of including every transaction, the system prompt contains a concise financial snapshot (net worth parameters, budget allocations, savings goals summary, recent budget report highlights). This prompt is stable across calls and benefits from prompt caching.
- **On-demand tool calling:** The model fetches detailed data through read-only function tools when needed. Tools include:
  - `get_budget_summary`: Returns the current month's budget vs actual spending by category.
  - `get_recent_transactions`: Searches transactions by date range, category, or description.
  - `get_savings_goals`: Returns savings goal progress and projections.
  - `get_net_worth_details`: Returns net worth parameters and retirement projections.
  - `get_category_trend`: Returns spending trends for a specific category over time.
  - `get_recurring_charges`: Detects and returns recurring charges and subscriptions.
- **Tool catalogue location:** Tool code lives in services within `PersonalFinance.ApiService/Services/`, following the existing pattern where services own business logic. No MediatR.
- **Maximum tool rounds:** A single user turn may trigger up to 2-3 tool call rounds (model calls a tool, gets results, may call another tool, then responds). This prevents infinite loops and controls cost.

### SDK and Framework

- **Microsoft.Extensions.AI:** The service will use `IChatClient` from `Microsoft.Extensions.AI` (stable MIT `10.10.0`) with the OpenAI adapter (`Microsoft.Extensions.AI.OpenAI`). This provides provider-neutral abstraction, test fakeability, and automatic function invocation via `UseFunctionInvocation()`.
- **OpenAI SDK retained:** The existing `OpenAI` `2.14.0` package is retained as a dependency (the adapter uses it). The service no longer constructs `OpenAIClient` directly per request.
- **No Aspire.OpenAI:** The preview `Aspire.OpenAI` package is avoided while it remains prerelease. The `IChatClient` is registered with normal DI.
- **Cached-token verification:** Before relying on cached-token accounting for quota tracking, verify that the `Microsoft.Extensions.AI` adapter preserves OpenAI's `InputTokenDetails.CachedTokenCount` field.

### Data Sources

- **Primary sources:** The assistant reads from `NetWorthParameters`, `UserBudgetAllocations`, `SavingsGoals`, and Google Drive spreadsheet data (budget reports and transactions) via the existing `ISpreadsheetReader`.
- **Budget Tracker:** The Budget Tracker (monthly budget sheets and transactions, kept separate by ADR 0002) is accessible through tools but is not the default source. The assistant should clarify which source it's using when data conflicts exist.
- **Data hierarchy:** When spreadsheet actuals and Budget Tracker actuals disagree, the assistant should prefer the most recently synced source and note the discrepancy.
- **Raw data allowed:** Sending raw financial data (individual transactions) to OpenAI is acceptable when it improves the answer, as confirmed by the user during charting.

### Proactive Insights

- **Detection mechanism:** Deterministic C# rules detect conditions (e.g., "Food category is 30% over budget with 10 days left"). The LLM is used only to write the natural-language wording of the insight, not to detect the condition.
- **When it runs:** Insights are generated on page load (when the chat widget initializes) and cached for a configurable period (e.g., 1 hour). They are also regenerated after significant data changes (new spreadsheet sync).
- **Where they appear:** Insights appear as a greeting message in the chat widget when the user opens it, and potentially as dashboard cards or notifications in future iterations.
- **Budget impact:** Insight generation counts against the global monthly budget. Insights should use a compact prompt and minimal tool calls to stay within budget.

### Assisted Actions

- **Proposal flow:** The assistant proposes a state-changing action (e.g., "Adjust Entertainment budget from $200 to $150") and presents it as a structured proposal. The user must explicitly confirm before the change is applied.
- **Confirmation mechanism:** The chat widget renders a proposal card with the exact change, Confirm/Cancel buttons. Confirmation sends an anti-forgery-protected request through the normal Web -> ApiService endpoint (never a direct write by the model).
- **Allowed actions:** Adjust a budget category amount, create or update a savings goal, add a Budget Tracker transaction. The exact list is refined during implementation.
- **Audit trail:** Each assisted action is logged with the user ID, timestamp, proposed change, and confirmation status.
- **Undo:** An assisted action can be undone within a configurable window (e.g., 24 hours).

### Conversation History Security

- **Server-side history validation:** The client currently sends the full conversation history, allowing a user to forge `assistant` turns. The server will validate or reconstruct history. Options include:
  - Server-side session storage (short-term, per-session history stored in memory or a cache).
  - History hash verification (the server maintains a hash of the last assistant response and rejects history that doesn't match).
- **History size limits:** Maximum 10 history messages per request (already implemented). Maximum message length is enforced (e.g., 2,000 characters per message).

### Model Selection

- **Primary model:** `gpt-4o-mini` is the current model. It remains the default for chat and insights, given its low cost (~$0.002/turn with current prompt, ~$0.0008 with tools).
- **Moderation:** The OpenAI Moderation endpoint is free and can be used as an additional layer for content filtering, but it is not the primary scope enforcement mechanism.

### Configuration

- **OpenAI settings:** The following configuration keys are used:
  - `OpenAI:ApiKey` — the OpenAI API key (required).
  - `OpenAI:Model` — the model name (default: `gpt-4o-mini`).
  - `OpenAI:MaxTokens` — maximum completion tokens per response (default: 400).
  - `OpenAI:Temperature` — response randomness (default: 0.7).
- **Quota settings:** New configuration keys:
  - `AiAssistant:MonthlyBudgetUsd` — global monthly budget ceiling (default: 10).
  - `AiAssistant:PerUserDailyMessages` — per-user daily message limit (default: 30).
  - `AiAssistant:BudgetWarningThreshold` — percentage of budget at which warnings are shown (default: 0.9).

### Entity and Migration

- **`AiUsageRecord` entity:** A new entity in `PersonalFinance.Data/Entities/` that inherits `AuditableEntity`. It stores `UserId`, `Endpoint` (e.g., "chat", "summarize", "insights"), `PromptTokens`, `CompletionTokens`, `CachedPromptTokens`, `EstimatedCostUsd`, and `CreatedAtUtc`.
- **EF Core migration:** A migration is added to `PersonalFinance.Data/Migrations/AzureSql/` to create the `AiUsageRecords` table with appropriate indexes (e.g., on `UserId` and `CreatedAtUtc` for quota queries).

### DTO Changes

- **`AiChatResponseDto`:** Extended with new fields:
  - `IsRefusal` — boolean indicating if the response is a canned refusal.
  - `Disclaimer` — the non-fiduciary disclaimer text (or null if not applicable).
  - `EstimatedCostUsd` — the estimated cost of this request (for transparency).
- **New DTOs:**
  - `AiUsageRecordDto` — for exposing usage data to the client (if needed for admin views).
  - `AiProposalDto` — for assisted action proposals (action type, description, current value, proposed value).

## Testing Decisions

### Testing Philosophy

Tests verify external behavior, not implementation details. The service is tested through its public interface (`IAiAssistantService`) with a fake `IChatClient` and an in-memory database. No live OpenAI calls are made during tests.

### Test Modules

1. **`AiAssistantServiceTests.cs`** — Tests the core chat flow:
   - In-scope financial queries trigger tool calls and return responses with disclaimers.
   - Out-of-scope queries return canned refusals without calling the LLM.
   - Small talk returns canned acknowledgements.
   - Tool calling works correctly (e.g., `get_recent_transactions` is called for spending questions).
   - System prompt caching is respected (stable prefix is preserved across calls).
   - Conversation history is validated (forged history is rejected or sanitized).
   - Quota enforcement works (user exceeds daily limit, global budget is exceeded).
   - Token usage is recorded in the database after each call.
   - `max_completion_tokens` is applied to requests.

2. **`AiQuotaServiceTests.cs`** — Tests the quota and rate limiting logic:
   - Per-user daily message limits are enforced.
   - Global monthly budget tracking is accurate.
   - Budget warning threshold triggers correctly.
   - Token cost calculations are correct (using `gpt-4o-mini` pricing).
   - Anonymous users are handled (rejected or rate-limited more strictly).
   - Quota state persists across service instances (database-backed, not in-memory).

3. **`AiScopeFilterTests.cs`** — Tests the deterministic pre-filter:
   - Finance-related queries pass through.
   - Non-finance queries are caught (programming, trivia, creative writing, politics).
   - Greetings are detected and handled.
   - Edge cases: mixed queries (partly finance, partly not), ambiguous queries, prompt injection attempts.
   - Input length limits are enforced.

### Prior Art

The existing `DashboardServiceTests.cs` uses `UseInMemoryDatabase` with `AppDbContext`, creates test data directly in the database, and calls services directly. The same pattern is followed here, with the addition of a fake `IChatClient` that returns predetermined `ChatResponse` objects.

## Out of Scope

- **Server-side saved conversations:** Conversation history remains in client state. The server validates history but does not persist full conversation logs per user. This was explicitly ruled out during charting.
- **Streaming responses:** The assistant does not stream responses in this iteration. Streaming adds complexity and is not required for the core functionality.
- **Multi-model support:** The assistant uses a single model (`gpt-4o-mini`) for all tasks. A separate classifier model for scope detection is not used (deterministic pre-filtering is sufficient).
- **Admin dashboard for AI usage:** While usage is tracked, a dedicated admin UI for viewing AI usage metrics is out of scope. The data is available in the database for future admin features.
- **Prompt injection protection beyond basics:** Basic input sanitization and tool parameter validation are included. Advanced prompt injection defense (e.g., adversarial training, specialized filters) is out of scope but noted as a future concern.
- **Chat widget UX upgrades:** Visual improvements to the chat widget (quick actions, streaming UI, quota indicators) are out of scope. The backend supports these features, but the frontend changes are a separate effort.
- **Scheduled background jobs:** Proactive insights are generated on demand (page load), not via a scheduled background job. Background jobs would require a separate worker service.
- **Aspire.OpenAI integration:** The preview `Aspire.OpenAI` package is not adopted. The `IChatClient` is registered manually.

## Further Notes

- The wayfinder map (`map.md`) contains 6 open tickets (04-09) that represent decisions not yet resolved. These tickets cover:
  - **04:** How the finance-only scope is enforced (layers, order, forged history handling).
  - **05:** Usage quotas and the global spend kill switch (per-user limits, storage, admin override).
  - **06:** Which financial data sources the assistant treats as truth (hierarchy, conflicts).
  - **07:** Prompt context strategy and the read-only tool catalogue (tool names, parameters, result shapes).
  - **08:** How proactive insights are produced and surfaced (detection, timing, UI).
  - **09:** Assisted actions: which writes, and how users confirm them (proposal flow, undo).
- These open tickets will be resolved during the `/to-tickets` phase, where implementation tickets are created and each decision is finalized. The spec above captures the decisions that are already locked (from resolved tickets 01-03) and the standing preferences from the charting interview.
- The spec assumes the `Microsoft.Extensions.AI` approach is chosen. If the manual OpenAI SDK loop is preferred (narrower, single-provider alternative), the tool calling logic changes but the overall architecture remains the same.
- The $10/month budget is a hard ceiling. The OpenAI dashboard should have a hard enforced limit configured as a second layer of protection. Application-level controls are the primary guardrail.
- All money values use `decimal` with explicit precision, following the project's data rules. No `float` or `double` for financial calculations.
- The assistant never writes to the database without explicit user confirmation. All writes go through the normal Web -> ApiService endpoint with anti-forgery protection.
