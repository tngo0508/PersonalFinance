# 08: Remove old feature-based folders and verify (contract phase)

**What to build:** Old feature-based folders (NetWorth/, Budget/, Community/, Dashboard/, GoogleDrive/, Notification/, SavingsGoals/, Search/, SpreadsheetAnalysis/, Watchlist/) removed after all files have been migrated to type-based folders. Final build verification confirms the solution compiles with zero errors and API endpoints return identical responses. The Infrastructure folder remains in place as it's already type-based.

**Blocked by:** 07: Migrate validators and helpers (migrate batch 3).

**Status:** ready-for-agent

- [ ] Verify all files have been migrated (no remaining .cs files in old feature folders)
- [ ] Remove old feature-based folders: NetWorth/, Budget/, Community/, Dashboard/, GoogleDrive/, Notification/, SavingsGoals/, Search/, SpreadsheetAnalysis/, Watchlist/
- [ ] Keep Infrastructure/ folder (already type-based)
- [ ] Solution builds with zero errors
- [ ] API endpoints return identical responses (no functional change)
