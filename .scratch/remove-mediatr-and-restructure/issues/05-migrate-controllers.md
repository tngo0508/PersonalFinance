# 05: Migrate controllers and update namespaces (migrate batch 1)

**What to build:** All 11 API controllers moved from feature-based folders to `Controllers/` with updated namespaces. All `using` statements referencing controllers are updated throughout the codebase. The solution builds with zero errors. Controllers are: AiAssistant, Budget, Community, Dashboard, GoogleDrive, NetWorth, Notification, SavingsGoals, Search, SpreadsheetAnalysis, and Watchlist.

**Blocked by:** 04: Create type-based folder structure (expand phase).

**Status:** ready-for-agent

- [ ] Move all 11 controller files to `Controllers/`
- [ ] Update namespaces in all moved controller files
- [ ] Update `using` statements in files that reference controllers (e.g., Program.cs)
- [ ] Solution builds with zero errors
- [ ] API endpoints return identical responses (no functional change)
