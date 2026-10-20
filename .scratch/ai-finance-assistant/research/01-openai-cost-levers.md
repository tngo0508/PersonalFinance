# OpenAI cost levers and pricing

**Research date:** 2026-10-19. Prices below are USD per 1 million text tokens, as displayed on the linked official model pages when retrieved. Verify model availability and pricing again before launch; model pages may be updated or deprecated.

## Standard text token prices

| Model | Input | Cached input | Output | Cached-input discount |
|---|---:|---:|---:|---:|
| [gpt-4o-mini](https://developers.openai.com/api/docs/models/gpt-4o-mini) | $0.15 | $0.075 | $0.60 | 50% |
| [gpt-4.1-mini](https://developers.openai.com/api/docs/models/gpt-4.1-mini) | $0.40 | $0.10 | $1.60 | 75% |
| [gpt-4.1-nano](https://developers.openai.com/api/docs/models/gpt-4.1-nano) | $0.10 | $0.025 | $0.40 | 75% |
| [gpt-5-mini](https://developers.openai.com/api/docs/models/gpt-5-mini) | $0.25 | $0.025 | $2.00 | 90% |
| [gpt-5-nano](https://developers.openai.com/api/docs/models/gpt-5-nano) | $0.05 | $0.005 | $0.40 | 90% |
| [gpt-5.4-mini](https://developers.openai.com/api/docs/models/gpt-5.4-mini) | $0.75 | $0.075 | $4.50 | 90% |
| [gpt-5.4-nano](https://developers.openai.com/api/docs/models/gpt-5.4-nano) | $0.20 | $0.02 | $1.25 | 90% |

These are the regular text-token rates, not Batch API prices or any separate hosted-tool charges. OpenAI's model pages marked `gpt-4o-mini`, `gpt-4.1-mini`, `gpt-4.1-nano`, `gpt-5-mini`, and `gpt-5.4-nano` as deprecated at retrieval; check the individual page and model availability before choosing one. `gpt-5.4-mini` and `gpt-5.4-nano` are newer small-model listings than the GPT-5 mini/nano pages.

## Prompt caching

OpenAI prompt caching is automatic/implicit by default for eligible matching prefixes; explicit breakpoints are also available on supported models. A later request must have the same rendered prefix through an eligible cache breakpoint to reuse it. It is not enough that two requests belong to the same conversation. The cached-input prices above represent the discounted rate for tokens reported as cached; the rest of the input remains at the normal input rate.

**Minimum prefix length caveat:** the current guide states 1,024 tokens for GPT-5.6 and later, but says that for models before GPT-5.6 the minimum varies with request settings (including tools, images, output schemas, reasoning effort, and verbosity). Consequently, the exact current minimum for the listed GPT-4o/GPT-4.1/GPT-5/GPT-5.4 models is **not verified here** and should not be assumed to be a single universal threshold.

For cache hits, keep stable system/developer instructions and stable tool definitions first; then put reusable, unchanged context; put changing transaction data, changing summaries, recent history, and the latest user message later. Any change before a breakpoint prevents reuse of the later part of that prefix. A long transaction list that changes on each request can therefore shorten or invalidate the reusable prefix. Track `usage.prompt_tokens_details.cached_tokens` in actual responses to measure hits rather than assuming them.

## Completion limit, moderation, and spend controls

- **`max_completion_tokens`:** the Chat Completions API reference defines this as an upper bound on generated completion tokens, including visible output and reasoning tokens. It is not only a visible-character/output-text limit for reasoning models. The legacy `max_tokens` parameter is deprecated in favor of it and is not compatible with o-series models. Omitting an application-set cap does not remove the model/context constraints; it does mean there is no explicit per-call output budget chosen by this application. Choose a cap that still allows the intended answer to complete.
- **Moderation:** OpenAI's moderation guide says the moderation endpoint is free to use. Its direct API fee is therefore $0 according to that guide; this does not imply unlimited throughput or that every other OpenAI feature/tool is free.
- **Project/org spend limits:** the current Spend limits guide distinguishes spend alerts from hard enforcement. Monthly spend thresholds/alerts are notifications and do not by themselves stop requests. The dashboard allows an owner to turn on **Enforce a hard limit** at the project or organization level; affected requests then fail with HTTP 429 when that spend limit is reached. Organization and project hard limits can both apply. OpenAI's separately assigned organization usage limit is distinct from a configured spend limit. For a genuine $10 guardrail, configure a $10 enforced limit for the relevant project (and an appropriate organization-level limit), plus earlier alerts; don't rely on a budget notification alone.

## Usage fields in Chat Completions

For a non-streaming Chat Completion, inspect the response's `usage` object. The main token totals are `prompt_tokens`, `completion_tokens`, and `total_tokens`. Depending on model/modality, details include `prompt_tokens_details.cached_tokens` (and possibly `audio_tokens`) and `completion_tokens_details.reasoning_tokens` (and possibly `audio_tokens`, `accepted_prediction_tokens`, and `rejected_prediction_tokens`). Optional modality-specific fields may not apply to text-only traffic. The API reference's response example includes these nested details, so record them alongside totals to measure cached-input savings and output consumption.

## Worked estimate for the $10/month ceiling

These are planning estimates, not measured token counts. They use the application's current model, `gpt-4o-mini`, regular rates above, no cache hit, no hosted-tool fee, and approximate tokenization. A local function/tool call has no separate model-tool-call price, but its arguments/results and every model round trip consume tokens. Hosted OpenAI tools can have additional prices and are excluded.

### (a) Current prompt: 500 transactions and 12 months of budgets

| Component | Assumption | Input tokens |
|---|---|---:|
| Transaction lines | 500 x ~17 tokens per formatted line | 8,500 |
| Budget breakdowns | 12 months x ~10 categories x ~12 tokens per category line | 1,440 |
| System instructions and headings | Approximation | 700 |
| Up to 10 history messages | 10 x ~80 tokens | 800 |
| Latest user message | Approximation | 60 |
| **Input total** | Rounded planning estimate | **11,500** |

Assume a 400-token answer. Cost per turn is `(11,500 x $0.15 + 400 x $0.60) / 1,000,000 = $0.001965`, approximately **0.197 cents**. $10 buys about **5,089 turns** at these assumptions. This does not count a cache hit; actual costs may differ substantially with line lengths, history length, output size, tokenization, or caching.

### (b) Compact summary prompt (~1,500 tokens) plus one application tool round trip

Assume the first call sends a 1,500-token prompt and emits a 100-token tool call. The second call resends that context plus the 100-token tool-call message and a 300-token tool result (about 1,900 input tokens) and emits a 400-token answer. Aggregate per user turn: **3,400 input tokens and 500 output tokens** across two model calls. At `gpt-4o-mini` rates, cost is `(3,400 x $0.15 + 500 x $0.60) / 1,000,000 = $0.00081`, approximately **0.081 cents**. $10 buys about **12,345 tool-using turns**. This conservatively assumes no cache hits and includes one tool cycle; additional tool rounds, larger results, or separately priced hosted tools increase cost.

The compact scenario is about 2.4x as many turns for the same $10 budget under these assumptions. Actual per-user monthly allowance is this aggregate count divided by the number of active users and their usage share.

## Official sources (retrieved 2026-10-19)

- [OpenAI API pricing](https://openai.com/api/pricing) and the linked individual model pages in the table.
- [Prompt caching guide](https://developers.openai.com/api/docs/guides/prompt-caching).
- [Spend limits guide](https://developers.openai.com/api/docs/guides/spend-limits).
- [Moderation guide](https://platform.openai.com/docs/guides/moderation).
- [Chat Completions create API reference](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create).