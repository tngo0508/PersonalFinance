# 03: Notification Triggers in Existing Services

**What to build:** Users receive automatic notifications when budget exceeds thresholds, savings goals hit milestones,
or Google Drive sync completes.

**Blocked by:** 02: Notification Backend API and Refit Contract

**Status:** ready-for-agent

- [ ] Budget service creates BudgetAlert notifications when spending exceeds 80% or 100% of allocation
- [ ] Savings Goals service creates SavingsMilestone notifications at 50%, 75%, 100% thresholds
- [ ] Google Drive sync creates SyncStatus notifications on success/failure
- [ ] Existing service behavior is not broken — triggers are additive only
- [ ] Unit tests verify notifications are created at correct thresholds
