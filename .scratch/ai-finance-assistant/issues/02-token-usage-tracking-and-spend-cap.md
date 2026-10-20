# 02: Token usage tracking and spend cap protection

**Status:** ready-for-agent

**Blocked by:** None (can start immediately)

## What to build

The AI assistant has no mechanism to track or limit OpenAI API costs. A user could consume the entire $10/month budget in a single session. This ticket adds persistent token usage tracking and a hard spend cap. Every AI request (chat, summarize, insights) records its token counts and estimated cost in the database. The system enforces per-user daily message limits and a global monthly budget ceiling, rejecting requests with HTTP 429 when limits are exceeded.

## Acceptance criteria

- [ ] A new `AiUsageRecord` entity in `PersonalFinance.Data/Entities/` inherits `AuditableEntity` and stores `Endpoint` (e.g., "chat", "summarize"), `PromptTokens`, `CompletionTokens`, `CachedPromptTokens`, and `EstimatedCostUsd` (decimal)
- [ ] EF Core migration creates the `AiUsageRecords` table with indexes on `UserId` and `CreatedAt` for efficient quota queries
- [ ] A new `AiQuotaService` checks per-user daily message limits (configurable, default 30 messages/day) and global monthly budget (configurable, default $10/month) before allowing an AI call
- [ ] The `AiQuotaService` calculates estimated cost per request using `gpt-4o-mini` pricing ($0.15/1M input tokens, $0.075/1M cached input, $0.60/1M output tokens)
- [ ] When the global monthly budget reaches the warning threshold (configurable, default 90%), a warning is included in the response
- [ ] When limits are exceeded, the `AiAssistantController` returns HTTP 429 with a friendly rate-limit message for both `/chat` and `/summarize` endpoints
- [ ] The `/chat` endpoint gains rate limiting (currently only `/summarize` has it)
- [ ] `max_completion_tokens` is applied to all OpenAI requests (configurable via `OpenAI:MaxTokens`, default 400)
- [ ] Token usage is recorded in the database after each successful AI call
- [ ] Anonymous users are handled (rejected or rate-limited more strictly than authenticated users)
- [ ] Unit tests verify: per-user daily limits are enforced, global monthly budget tracking is accurate, cost calculations match `gpt-4o-mini` pricing, quota state persists across service instances, and HTTP 429 is returned when limits are exceeded
- [ ] `dotnet test PersonalFinance.sln` passes

## Blocked by

None (can start immediately)
