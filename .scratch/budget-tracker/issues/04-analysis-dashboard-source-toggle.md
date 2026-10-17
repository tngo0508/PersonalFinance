# 04: Switch Analysis and Dashboard to active source with source toggle

**What to build:** The Analysis page and Dashboard budget views show data from the user's chosen source (Google Sheet or
Budget Tracker). A segmented "Data source: Google Sheet | Budget Tracker" control appears in the Analysis header and the
Dashboard budget card area, posting to the shared data-source endpoint and reloading the same period. When the active
source has no data for the month, an empty state names the source with links to add data or switch source—never falling
back silently. The release is documented with a version bump and changelog entry.

**Blocked by:** 03: Data source preference and unified budget data provider

**Status:** closed

- [x] `SpreadsheetAnalysisService` read methods refactored to use `IBudgetDataProvider` instead of querying
  `Spreadsheet*` entities directly (sync methods and `SearchService` unchanged).
- [x] `BudgetService` and `DashboardService` refactored to use `IBudgetDataProvider` for budget breakdown and recent
  transactions.
- [x] Analysis dashboard DTO updated to include the active source so the UI can display it.
- [x] Shared partial view `_DataSourceToggle.cshtml` added with a segmented control (Data source: Google Sheet | Budget
  Tracker) that posts to the single data-source endpoint and reloads the current period.
- [x] `_DataSourceToggle.cshtml` integrated into the Analysis page header and the Dashboard Monthly budget card
  (alongside the independent Budget Planner allocator).
- [x] Empty states added to both Analysis and Dashboard, naming the active source with links to the Budget Tracker or
  Google Drive sync (no silent fallback to the other source, including no Planner fallback on provider-backed figures).
- [x] `AppVersion.cs` bumped (minor) following the version management guidelines.
- [x] `CHANGELOG.md` updated with an `[Unreleased]` entry under "Added" for the Budget Tracker feature.
- [x] `dotnet build`, `dotnet test`, and ESLint pass with no regressions in existing Dashboard and Analysis tests.
