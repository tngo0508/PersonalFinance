# 02: Budget Tracker page — Summary and Transactions tabs

**What to build:** Signed-in users can open Budget Tracker from the navigation, select a month, and interact with two
tabs mirroring the Google monthly budget sheet. The Summary tab shows Planned/Actual horizontal bars and Expenses and
Income tables with Planned, Actual, Diff columns, totals, an add-category row, and the ability to edit planned amounts
inline, type an Actual override (with a manual badge and one-click revert), and add categories. The Transactions tab
shows side-by-side Expenses and Income lists with add, inline edit, and delete. All edits save via AJAX and re-render
from server-calculated totals. The page includes a new-month prompt (copy from previous or start blank),
loading/empty/error states, and is responsive and keyboard-accessible.

**Blocked by:** 01: Monthly Budget service, validators, and API endpoints

**Status:** closed

- [x] Refit client `IBudgetTrackerApi` registered in `Web/Program.cs` following the existing pattern (typed HttpClient,
  resilience handler).
- [x] `BudgetTrackerController` in `PersonalFinance.Web/Controllers/` with `[Authorize]`, a view action, and
  anti-forgery-protected JSON actions that call only the Refit API.
- [x] `Views/BudgetTracker/Index.cshtml` with month selector (previous/next arrows, month and year pickers), new-month
  prompt modal, Summary tab (bars, two tables with Diff colors, totals, add category, rename and reorder category,
  override badge and revert), and Transactions tab (side-by-side lists with add, inline edit, delete with confirmation).
- [x] `wwwroot/js/budget-tracker/budget-tracker.js` as an IIFE module with async/await, try/catch, `textContent`
  rendering, stale-response guard, and request sequencing. ESLint compliant.
- [x] Navigation entry added to `_Layout.cshtml` sidebar, labeled "Budget Tracker", positioned near Budget Planner and
  Analysis.
- [x] Page styles using `bt-*` class prefix, consistent with the Analysis page conventions, with responsive horizontal
  scrolling on mobile.
- [x] Loading, empty, and error states handled for all sections. Currency formatting follows the existing `$`
  convention.
- [x] ESLint passes on the new JavaScript file with no warnings.
