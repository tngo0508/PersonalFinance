# 07: Prompt context strategy and the read-only tool catalogue

Type: grilling
Status: open
Blocked by: 01, 02, 06
Map: [AI Finance Assistant map](../map.md)

## Question

What replaces the current prompt, which includes every transaction? Decide what always goes in the system prompt (a
compact financial snapshot that can be cached) and what the model fetches on demand through read-only tools. Settle
the tool catalogue: names, parameters, and result shapes (for example `get_month_summary`, `search_transactions`,
`get_category_trend`, `get_goal_progress`, `get_recurring_charges`), the maximum number of tool rounds per turn, and
where tool code lives (services own the logic; no MediatR).
