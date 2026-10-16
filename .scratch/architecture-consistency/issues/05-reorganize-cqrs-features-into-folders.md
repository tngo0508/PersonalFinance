# 05: Reorganize existing CQRS features into feature folders

**What to build:** Existing CQRS features (Budget, Community, SavingsGoals, Watchlist, SpreadsheetAnalysis) are moved from type-based folders (Features/Budget/, Services/, Validators/) into feature-based folders under ApiService. All controllers move from the Controllers/ folder into their respective feature folders. API endpoints continue to work as before.

**Blocked by:** None (can start immediately, but should complete after Tickets 2-4 for a clean final state).

**Status:** ready-for-agent

- [ ] Budget features moved from Features/Budget/ into ApiService/Budget/ folder structure with the controller
- [ ] Community features moved into ApiService/Community/ folder structure with the controller
- [ ] SavingsGoals features moved into ApiService/SavingsGoals/ folder structure with the controller
- [ ] Watchlist features moved into ApiService/Watchlist/ folder structure with the controller
- [ ] SpreadsheetAnalysis features moved into ApiService/SpreadsheetAnalysis/ folder structure with the controller
- [ ] Remaining controllers moved from Controllers/ into their respective feature folders
- [ ] Services moved from Services/ into their respective feature folders
- [ ] Validators moved from Validators/ into their respective feature folders
- [ ] Namespaces updated to follow PersonalFinance.ApiService.[Feature] convention
- [ ] Solution builds successfully with no compilation errors
- [ ] API integration tests verify all endpoints still work after folder reorganization