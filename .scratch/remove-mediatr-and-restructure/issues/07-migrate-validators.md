# 07: Migrate validators and helpers (migrate batch 3)

**What to build:** Validators and helper classes moved from feature-based folders to `Validators/` with updated namespaces. This includes files like NetWorthProjectionValidator and SpreadsheetAnalysisResults. All `using` statements updated. The solution builds with zero errors.

**Blocked by:** 06: Migrate services and update namespaces (migrate batch 2).

**Status:** ready-for-agent

- [ ] Move validator files to `Validators/`
- [ ] Move helper classes (e.g., SpreadsheetAnalysisResults) to `Validators/` or appropriate folder
- [ ] Update namespaces in all moved files
- [ ] Update `using` statements in files that reference validators or helpers
- [ ] Solution builds with zero errors
