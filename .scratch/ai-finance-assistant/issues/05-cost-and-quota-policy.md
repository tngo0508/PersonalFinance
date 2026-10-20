# 05: Usage quotas and the global spend kill switch

Type: grilling
Status: open
Blocked by: 01
Map: [AI Finance Assistant map](../map.md)

## Question

How is the $10/month ceiling enforced? Decide the per-user limits (messages and/or tokens per day and per month), a
global monthly budget computed from recorded token usage that hard-stops all AI calls (chat, news summary, insights)
when reached, whether usage is stored in a database table (an entity and migration) or kept in memory, whether there
is an admin override or a per-user allowance (RBAC from ADR 0004), how `max_completion_tokens` and model choice are
configured, what the user sees when a limit is hit, and how unauthenticated or `"anonymous"` callers are handled.
