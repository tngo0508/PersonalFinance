# 04: Unified Search Backend API

**What to build:** Users can search across transactions, budget items, and savings goals through a single API endpoint
that returns unified results with source labels.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] SearchService in ApiService queries across SpreadsheetTransactionRecord, UserBudgetAllocation, and SavingsGoal
  entities
- [ ] Search API Controller with endpoint: GET `/api/Search?q={query}`
- [ ] SearchResultsDto and SearchResultDto in Shared with Source field ("Transaction", "Budget", "SavingsGoal")
- [ ] ISearchApi contract in Shared
- [ ] Refit client registered in Web `Program.cs`
- [ ] Unit tests for SearchService: filtering, result formatting, empty results
