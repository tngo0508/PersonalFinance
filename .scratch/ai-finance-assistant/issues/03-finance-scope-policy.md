# 03: What counts as "finance-related", and how a refusal reads

Type: grilling
Status: resolved
Blocked by: None
Map: [AI Finance Assistant map](../map.md)

## Question

Where exactly is the boundary of the assistant's scope? Decide which grey areas are in or out: taxes, investing and
specific securities/crypto, insurance, real estate, career/salary negotiation, questions about how to use the app,
financial news, general math, and small talk ("hi", "thanks"). Also decide how individualised advice may get (e.g.
"buy X stock"), what disclaimer applies, and the exact behaviour and wording of a refusal (fixed canned text vs model
generated, and whether it suggests an in-scope rephrasing).

## Answer

- **In-scope domains:** Broad personal finance (budgeting, expense categorization, emergency funds, debt reduction, retirement horizons, tax concepts, insurance basics, and general asset allocation), app navigation and feature support, and income/career compensation discussions.
- **Out-of-scope domains:** General programming, non-financial trivia, creative writing, political/medical/legal advice, and individual speculative stock/cryptocurrency picking.
- **Advice depth & guardrails:** The assistant provides specific, actionable recommendations (e.g. proposed budget cuts, target savings allocations) accompanied by an explicit non-fiduciary disclaimer stating it is an AI tool for informational and planning purposes only.
- **Refusal behavior:** Out-of-scope requests receive a standard, deterministic canned refusal message to minimize token waste and eliminate completion costs.
- **Small talk & greetings:** Short standard acknowledgements ("hi", "thanks") are allowed with concise responses that prompt the user on financial topics.
- Resolution details and decisions: [Decisions in Map](../map.md).
