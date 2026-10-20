# 01: OpenAI cost levers and pricing against a $10/month ceiling

Type: research
Status: resolved
Blocked by: None
Map: [AI Finance Assistant map](../map.md)

## Question

What does each OpenAI cost lever cost today, and how many chat turns a month does $10 buy for a small invite-only user
base? Check: current per-token prices of the cheap chat models (e.g. `gpt-4o-mini`, `gpt-4.1-mini`/`nano`, and newer
small models), automatic prompt caching (discount, minimum prefix, how to structure prompts to hit it), the
`max_completion_tokens` limit, whether the Moderation endpoint is free, project-level budget limits/alerts in the OpenAI
dashboard, and the token usage fields returned per response. Output: a price table plus a worked estimate (tokens per
turn with a summary-based prompt vs the current full-transaction prompt).

## Answer

- [Findings](../research/01-openai-cost-levers.md) records official model pricing, caching, API usage, moderation, spend-limit behavior, and the worked estimates, retrieved 2026-10-19.
- `gpt-4o-mini` costs $0.15/M input, $0.075/M cached input, and $0.60/M output; current newer small-model listings include `gpt-5.4-mini` and `gpt-5.4-nano`. Check model pages because several listed models show deprecated status.
- Prompt caching is implicit by default for eligible identical prefixes; cache prices imply 50–90% input savings by model. For models before GPT-5.6 the minimum varies with request settings; put stable instructions/tools before changing transaction data and history.
- `max_completion_tokens` caps all generated completion tokens, including reasoning tokens; the endpoint's moderation guide says Moderation is free.
- Project/org spend alerts are not enforcement by themselves; the current dashboard supports an optional enforced hard spend limit that rejects affected requests after the limit, separately from OpenAI's organization usage limit.
- Chat Completion usage reports `prompt_tokens`, `completion_tokens`, and `total_tokens`, with cached-input and reasoning-token detail fields where applicable.
- With `gpt-4o-mini` and no cache/tool-host fees, the estimates are ~11,500 input + 400 output tokens and $0.001965 per current-context turn (~5,089/$10), versus ~3,400 input + 500 output tokens across one tool round trip and $0.00081 per compact-summary turn (~12,345/$10); these are assumptions, not measured counts.
