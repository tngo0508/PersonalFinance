# 05: Dashboard Backend API with Aggregation

**What to build:** A Dashboard API endpoint that aggregates data from Net Worth, Budget, Savings Goals, and Analysis
services, returning a summary for the home page.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] DashboardService in ApiService aggregates data from existing services (NetWorth, Budget, SavingsGoals,
  SpreadsheetAnalysis)
- [ ] Dashboard API Controller with endpoint: GET `/api/Dashboard/summary`
- [ ] DashboardSummaryDto and child DTOs in Shared (NetWorthSummaryDto, BudgetSummaryDto, SavingsGoalSummaryDto,
  RecentActivityDto)
- [ ] IDashboardApi contract in Shared
- [ ] Refit client registered in Web `Program.cs`
- [ ] Unit tests for DashboardService: data aggregation, empty state handling, parallel queries
