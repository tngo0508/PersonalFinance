# 03: Data source preference and unified budget data provider

**What to build:** Budget reads for the Analysis page and Dashboard budget views go through a single provider that
returns sheet or tracker data according to the user's preference. The provider resolves the default source at read time
(Google Sheet if the user has synced snapshots, otherwise Budget Tracker) and persists the preference only when the user
explicitly switches. A new API endpoint reads and writes the preference. The existing sheet path in Analysis and
Dashboard is preserved through characterization tests before any refactoring.

**Blocked by:** None (can start immediately alongside Ticket 01).

**Status:** closed

- [x] Characterization tests added for the current `SpreadsheetAnalysisService` read paths, `BudgetService` breakdown,
  and `DashboardService` recent transactions, capturing their output before any changes.
- [x] Normalized read model defined (`BudgetMonthData`, `BudgetTransactionData`) with interfaces `IBudgetMonthReader`
  and `IBudgetDataProvider` in `ApiService/Services/`.
- [x] `SheetBudgetMonthReader` wraps the current snapshot and `CategoryBreakdownJson` logic, returning the normalized
  shape without changing behavior.
- [x] `TrackerBudgetMonthReader` builds the same normalized shape from the new Budget Tracker tables using the Actual
  override rules.
- [x] `IBudgetDataSourceService` implemented with the default rule (sheet if synced snapshots exist, else tracker),
  stored in the `UserBudgetSourcePreference` entity, persisted only on explicit switch.
- [x] `IBudgetDataProvider` delegates to the appropriate reader based on the user's preference and exposes
  `GetActiveSourceAsync`.
- [x] `GET/PUT api/budget-tracker/data-source` endpoints added to `BudgetTrackerController` with the corresponding Refit
  contract methods.
- [x] All new services registered in `ApiService/Program.cs`.
- [ ] Update the manual factory registration of `SpreadsheetAnalysisService` when its read methods switch to the
  provider in Ticket 04.
- [x] Provider tests cover default resolution, explicit preference override, tracker reader mapping, and the no-fallback
  rule (empty active source never falls back silently).
- [x] Characterization tests still pass after implementation, confirming no regression in the sheet path.

The normalized month reports `SourceRecordCount` as the number of synced snapshot rows for the sheet (one for a Tracker
month with a plan or transactions). `GetTrendRecordsAsync` returns one observation per sheet snapshot, including
duplicates in the same month, and one observation per available Tracker month. Ticket 04 can use these to preserve the
sheet's `SnapshotCount` and per-snapshot trend chart without querying synced tables directly.
