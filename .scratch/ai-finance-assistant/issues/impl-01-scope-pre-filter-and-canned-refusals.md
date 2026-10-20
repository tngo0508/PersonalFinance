# 01: Finance scope pre-filter and canned refusal logic

**Status:** ready-for-agent

**Blocked by:** None (can start immediately)

## What to build

Users can ask the AI assistant any question, including non-financial topics that waste OpenAI API tokens. This ticket adds a deterministic pre-filter that classifies incoming messages before they reach the LLM. Finance-related queries pass through normally. Non-financial queries (programming, trivia, creative writing, politics, etc.) receive an instant canned refusal with zero token cost. Small talk greetings receive a brief canned acknowledgement guiding the user back to financial topics. All AI responses include a non-fiduciary disclaimer field.

## Acceptance criteria

- [ ] A new `AiScopeFilter` service class determines whether a user message is finance-related, a greeting, or off-topic
- [ ] Finance-related queries (budgeting, spending analysis, savings goals, debt reduction, retirement, tax concepts, insurance, asset allocation, app navigation, salary/career) pass through without modification
- [ ] Non-financial queries (programming, trivia, creative writing, politics, medical/legal advice, stock picking) return a static canned refusal message immediately, without calling the LLM
- [ ] Small talk greetings ("hi", "thanks", "hello") return a short canned acknowledgement that guides the user toward financial questions
- [ ] `AiChatResponseDto` is extended with `IsRefusal` (boolean) and `Disclaimer` (nullable string) fields
- [ ] The `AiAssistantController.Chat` endpoint integrates the scope filter before calling the AI service
- [ ] Canned refusal messages are defined as constants in `PersonalFinance.Shared` so they are testable and easy to update
- [ ] Unit tests verify: finance queries pass, off-topic queries are refused without LLM calls, greetings return acknowledgements, and edge cases (mixed queries, prompt injection attempts, long inputs) are handled
- [ ] Input length is limited (e.g., 2,000 characters per message) to prevent abuse
- [ ] `dotnet test PersonalFinance.sln` passes

## Blocked by

None (can start immediately)
