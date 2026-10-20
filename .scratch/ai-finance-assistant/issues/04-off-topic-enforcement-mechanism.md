# 04: How the finance-only scope is enforced

Type: grilling
Status: open
Blocked by: 01, 03
Map: [AI Finance Assistant map](../map.md)

## Question

Which layers enforce the scope policy from the finance scope policy ticket, and in what order? Candidates: a hardened
system prompt only; a cheap deterministic pre-filter (length limits, obvious non-finance patterns); a separate
classifier call on a nano model before the main call; a structured `in_scope` flag returned by the main call; and
OpenAI Moderation. Also decide how to stop forged history (the client currently sends `assistant` turns), the maximum
message and history size, and whether refused requests count against the user's quota. Weigh the cost per refused
request (from the cost research ticket) against false refusals on legitimate questions.
