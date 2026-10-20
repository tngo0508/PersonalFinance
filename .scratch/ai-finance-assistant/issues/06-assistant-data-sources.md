# 06: Which financial data sources the assistant treats as truth

Type: grilling
Status: open
Blocked by: None
Map: [AI Finance Assistant map](../map.md)

## Question

The assistant currently reads `NetWorthParameters`, `UserBudgetAllocations`, `SavingsGoals`, and the latest Google
Drive spreadsheet, but not the Budget Tracker (monthly budget sheets and transactions; kept separate by ADR 0002) or
the dashboard aggregates. Which sources should it use, which wins when two disagree (for example, spreadsheet actuals
vs Budget Tracker actuals), and how does it choose between several spreadsheets? Record any new domain terms in
`GLOSSARY.md`.
