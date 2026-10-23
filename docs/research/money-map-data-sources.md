# Money Map: month-level money data the existing APIs already provide

Research for issue #195 (child of map #193). Source: local code at `origin/main` 4719016. All paths are under
`PersonalFinance/src/`; `Api/` stands for `PersonalFinance.ApiService/`, `Shared/` for `PersonalFinance.Shared/`,
`Data/` for `PersonalFinance.Data/`.

## TL;DR

- **Flow View, one month:** total income, total expenses, amount saved (`income − expenses`) and per-expense-category
  actuals already exist, source-resolved, in `GET /api/SpreadsheetAnalysis/dashboard?year=&month=` (`Summary` +
  `Categories`). They all come from one normalized model, `BudgetMonthData`, served by `IBudgetDataProvider`.
- **Income by category is not available** through that model: the Tracker reader drops income rows, and the Sheet
  sync never persists income categories. Only the Budget Tracker's own `GET /api/budget-tracker/sheet` has it (Tracker
  source only).
- **There is no "Savings" kind.** `BudgetCategoryKind` is `Expense | Income`. The default Tracker category named
  "Savings" is an **Income** category (money drawn from savings, as in Google's template). "Amount saved" can only be
  `Actual income − Actual expenses`, which can be negative.
- **The source rule is not "Tracker first, then Sheet".** Today: the explicit preference wins; with no preference the
  default is **Google Sheet if the user has any synced Sheet data, otherwise Budget Tracker**. It never falls back to
  the other source for a month with no data, and never mixes sources.
- **Plan View:** Net Worth parameters, Savings Rate, FI Target, Progress to FI, the Budget Planner allocation and
  Savings Goals are all in `GET /api/Dashboard/summary`; Progress to FI and the goals overview exist **only** there.
- **Conclusion:** one new read-only endpoint is warranted (e.g. `GET /api/money-map/flow?year=&month=`, and a plan
  read), composing `IBudgetDataProvider` with a small addition that carries income categories. The Plan View can reuse
  the Dashboard summary as-is, or a thin endpoint that reuses the same calculations.

## 1. How the Data Source is resolved today

| Step | Where |
|---|---|
| Explicit `UserBudgetSourcePreference` row wins | `Api/Infrastructure/BudgetDataSourceService.cs:28-39` |
| No row: `GoogleSheet` if any `SpreadsheetBudgetSnapshots` or `SpreadsheetTransactionRecords` exist for the user, else `BudgetTracker` (`IsDefault = true`) | `Api/Infrastructure/BudgetDataSourceService.cs:41-52` |
| `BudgetDataProvider` picks `TrackerBudgetMonthReader` or `SheetBudgetMonthReader` from that source; "Never falls back to the other data source" | `Api/Infrastructure/BudgetDataProvider.cs:5-8, 78-84` |
| Source is cached per scoped provider instance and invalidated on `PreferenceChanged` | `Api/Infrastructure/BudgetDataProvider.cs:24, 28-38`; registered scoped at `Api/Infrastructure/InfrastructureServiceExtensions.cs:17-20` |
| Read/set preference over HTTP: `GET/PUT /api/budget-tracker/data-source` (`BudgetDataSourceDto { Source, IsDefault }`) | `Api/Features/BudgetTracker/BudgetTrackerController.cs:340-367` |
| `BudgetDataSource` enum: `GoogleSheet = 1`, `BudgetTracker = 2` | `Shared/Enums/BudgetDataSource.cs` |

GLOSSARY "Data Source": the source applies to a whole view and is never mixed within one month.

Consumers of `IBudgetDataProvider`: Analysis (`Api/Features/Analysis/SpreadsheetAnalysisService.cs:20-31`) and the
Dashboard (`Api/Features/Dashboard/DashboardService.cs:28-35`). The Web "Budget Report" on the Google Drive page is
different: it reads the spreadsheet live (`GET /api/SpreadsheetAnalysis/spreadsheet-report`,
`Api/Features/Analysis/SpreadsheetAnalysisController.cs:144-162`) and ignores the preference.

## 2. The normalized month model

`BudgetMonthData` (`Api/Infrastructure/IBudgetMonthReader.cs:20-36`): `Year, Month, MonthName, SourceRecordCount,
BudgetedIncome, ActualIncome, BudgetedExpenses, ActualExpenses, StartingBalance?, EndingBalance?,
Categories: List<BudgetMonthCategoryData { Name, Budgeted, Actual, Kind }>`. It is an internal ApiService type, not a
Shared DTO.

### Budget Tracker reader (`Api/Infrastructure/TrackerBudgetMonthReader.cs`)

- A month exists when it has a `MonthlyBudgetPlan` or any `BudgetTransaction` dated in it (`:99-102`, `:200-203`).
  A plan with no transactions yields a month of zero actuals.
- Actual per category = `MonthlyBudgetLine.ActualOverride` if set, else the sum of the month's transactions
  (`Api/Infrastructure/MonthlyBudgetCalculator.cs:52-56`). Archived categories appear only when they have a plan,
  override or transactions (`:41-44`).
- Kind comes from `BudgetCategory.Kind` (`Data/Entities/BudgetCategory.cs:15`): `ActualIncome` = sum of Income rows,
  `ActualExpenses` = sum of Expense rows (`:212-236`).
- **`Categories` holds only Expense rows** (`:215-221`). Income categories are summed into `ActualIncome` and dropped.
- Balances are never set (null).

### Google Sheet reader (`Api/Infrastructure/SheetBudgetMonthReader.cs`)

- Reads `SpreadsheetBudgetSnapshot` rows (`Data/Entities/SpreadsheetBudgetSnapshot.cs`) for the month and **sums all
  snapshots** of that month (one per Google Drive connection/file) (`:56-71`, `:142-197`).
- Income/expense totals are the snapshot's `ActualIncome` / `ActualExpenses`, decided at sync time by the parser.
- `Categories` comes from `CategoryBreakdownJson`, and every entry is stamped `Kind = Expense` (`:215-222`).
- Snapshot categories are **expense-only**: the parser keeps income rows in a separate `monthlyIncomeCategories` list
  and only uses it to total income (`Api/Features/GoogleDrive/BudgetSpreadsheetParser.cs:811-815`); `Categories = cats`
  is the expense list (`:824, 877`). The sync serializes only those (`Api/Features/Analysis/SpreadsheetAnalysisService.cs:391-397`).
- An explicit "Net Savings" row in the sheet is parsed (`BudgetSpreadsheetParser.cs:639-647`) but only used to detect
  data; it is not stored.

## 3. Income vs expense vs savings mapping

| Source | Income | Expense | Savings |
|---|---|---|---|
| Budget Tracker | `BudgetCategoryKind.Income` (`Shared/Enums/BudgetCategoryKind.cs`) | `BudgetCategoryKind.Expense` | **No kind.** Default Income categories are `Savings, Paycheck, Bonus, Interest, Other`; default Expense categories `Food, Gifts, Health/medical, Home, Transportation, Personal, Pets, Utilities, Travel, Debt, Other` (`Api/Features/BudgetTracker/MonthlyBudgetService.cs:18-40`). The "Savings" category is *income* (withdrawal from savings). |
| Sheet budget table | Section label ("Income"/"Earnings") or name keyword match `income, salary, paycheck, wage, bonus, dividend, freelance, consulting, interest, investment return, side hustle` (`BudgetSpreadsheetParser.cs:747, 984-1005`) | Everything else in the table | Not modelled |
| Sheet transactions (`SpreadsheetTransactionRecord.Type`, free text "Income"/"Expense") | Type column or section label containing `income, deposit, credit` (`BudgetSpreadsheetParser.cs:311-318`); unlabelled signed amounts: positive = income (`:346-357`) | `expense, spending, debit, withdrawal`; negatives | Not modelled |
| Tracker transactions in the shared model | `Type = "Income"` when the category's Kind is Income, else `"Expense"` (`TrackerBudgetMonthReader.cs:168`) | | |

So "amount saved" for a month is only derivable as **net savings = Actual income − Actual expenses**. That is what
every existing DTO already calls `NetSavings` (e.g. `SummaryStatisticsDto`, `Api/Features/Analysis/AnalysisDashboardResult.cs`
via `ISpreadsheetAnalysisService.cs`; `BudgetTrendRecordData.NetSavings`, `IBudgetMonthReader.cs:47`). The Actual
Savings Rate is `FinanceMetrics.ActualSavingsRatePercent` (`Shared/Helpers/FinanceMetrics.cs:21-26`). A "Debt" expense
category or a user-made expense category called "Savings/Investing" would be counted as spending, not saving.

## 4. Flow View figures: where each one comes from

| Figure | Already returned by | Gaps |
|---|---|---|
| Total income (month) | `SummaryStatisticsDto.TotalIncome` from `GET /api/SpreadsheetAnalysis/dashboard?year=&month=` (`SpreadsheetAnalysisController.cs:37-83`, `SpreadsheetAnalysisService.cs:37-57`); also `DashboardMonthlyBudgetDto.ActualIncome` but **latest month only** (`DashboardService.cs:133-163`) | none |
| Income by category | **Tracker only:** `MonthlyBudgetSheetDto.Income.Rows[]` (`Name, Planned, Actual`) from `GET /api/budget-tracker/sheet?year=&month=` (`BudgetTrackerController.cs:53-67`, `Shared/DTOs/BudgetTracker/MonthlyBudgetSheetDto.cs`) | Not in `BudgetMonthData`. For Sheet: not stored in snapshots; could only be approximated from `SpreadsheetTransactionRecord` rows with `Type = "Income"` grouped by `Category`, which exist only if the sheet has a transaction log, and won't reconcile with the snapshot total |
| Per-category expense totals | `CategoryInsightsDto.Categories[]` (`CategoryName, BudgetedAmount, ActualAmount, ...`) in the same dashboard call (`SpreadsheetAnalysisService.cs:180-218`); also `BudgetVarianceDto` | Sheet months with totals but no category rows have an empty breakdown (`BudgetSpreadsheetParser.cs:858-859`), so categories won't sum to `TotalExpenses` |
| Total expenses | `SummaryStatisticsDto.TotalExpenses` | none |
| Amount saved | `SummaryStatisticsDto.NetSavings` (income − expenses), can be negative | no transfer-to-savings concept |
| Actual Savings Rate | `SummaryStatisticsDto.SavingsRate` | none |
| Months that have data | `GET /api/SpreadsheetAnalysis/years`, `/months/{year}` (`SpreadsheetAnalysisController.cs:122-139`) — source-resolved | none |
| Active source / is default | `AnalysisDashboardResult.ActiveSource`; `GET /api/budget-tracker/data-source` for `IsDefault` | none |

Caveat on the Analysis dashboard endpoint: it also computes velocity, a full-year trend and variance for every call
(`SpreadsheetAnalysisController.cs:61-67`), and returns ApiService-local result types mapped to Shared
`AnalysisDashboardDto` for the Web. Usable, but heavier than the Flow View needs.

## 5. Plan View figures

| Figure | Already returned by | Notes |
|---|---|---|
| Net Worth parameters (`StartingBalance, AnnualSavings, AnnualIncome, AnnualSpending, CurrentAge, RetirementAge, BaseRoi, ConservativeRoi, AggressiveRoi, AggressiveSavings, UpdatedAtUtc`) | `GET /api/NetWorth/parameters` → `NetWorthParametersDto` (`Api/Features/NetWorth/NetWorthController.cs:68-84`) | Returns an empty DTO when none saved |
| FI Target (`AnnualSpending × 25`) | `NetWorthProjectionDto.FiTarget` from `POST /api/NetWorth/calculate` (`NetWorthController.cs:31-46`, `Api/Infrastructure/NetWorthProjectionService.cs:14, 30`); `NetWorthSummaryDto.FiTarget` in the Dashboard | |
| Progress to FI (`StartingBalance / FiTarget × 100`, uncapped) | **Only** `NetWorthSummaryDto.ProgressToFiPercent` in `GET /api/Dashboard/summary` (`DashboardService.cs:65-106`, private method) | "Net worth" is the parameter `StartingBalance`, not a measured value |
| Savings Rate (planned, `AnnualSavings / AnnualIncome`) | `NetWorthSummaryDto.SavingsRatePercent` (Dashboard); computable from parameters via `FinanceMetrics.SavingsRatePercent` (`FinanceMetrics.cs:12-15`) | Distinct from Actual Savings Rate (GLOSSARY) |
| FI reached age / years, projection points | `NetWorthSummaryDto.FiReachedAge, FiYears, ProjectionPoints` (Dashboard) | |
| Budget figures (Budget Planner allocation: `MonthlyIncome` + 10 fixed categories incl. `Savings`) | `GET /api/Budget` / `GET /api/Budget/budget/{period}` (`Api/Features/BudgetPlanner/BudgetController.cs`); `BudgetSummaryDto.Allocations` (Dashboard, `DashboardService.cs:165-200`) | Planner categories are fixed (`UserBudgetAllocation`) and unrelated to Tracker/Sheet category names. Its `Savings` allocation is the only *planned savings* figure besides Net Worth `AnnualSavings` |
| Monthly budget actuals (latest month) | `DashboardMonthlyBudgetDto` (Dashboard) | Latest month only |
| Savings Goals (`Name, TargetAmount, CurrentAmount, TargetDate, IsCompleted, ProgressPercent`) | `GET /api/SavingsGoals` (`Api/Features/SavingsGoals/SavingsGoalsController.cs:30`, `ISavingsGoalService.cs:14`); Dashboard lists 5 + `GoalsOverviewDto` totals (`DashboardService.cs:261-290`) | No contribution history (`Data/Entities/SavingsGoal.cs`), no link to budget categories or Net Worth |

Nothing in the data links these figures to each other: `NetWorthParameters.AnnualSavings`, the Planner `Savings`
allocation, the actual month's net savings and Savings Goals' `CurrentAmount` are four independent numbers. Any Plan
View edge between them is conceptual (a formula or a "feeds" label), not a stored relation.

## 6. What is missing

1. **Income by category in the provider model** (both sources). Tracker: cheap — keep Income rows in
   `BudgetMonthData.Categories` with `Kind = Income` (consumers that assume expense-only, Analysis category insights and
   variance, would need to filter by Kind). Sheet: needs the sync to persist income categories (snapshot JSON change +
   re-sync); existing snapshots stay expense-only.
2. **A savings concept.** Only net savings exists. A real "saved/invested" flow would need a new Kind or category flag
   (schema change) — likely out of scope; the Flow View should show net savings (and a deficit when negative).
3. **"Tracker first, then Sheet" does not exist.** The map's note contradicts the current default rule (Sheet wins
   when any Sheet data exists). Either the Flow View follows the existing Data Source rule (consistent with Analysis
   and the Dashboard, per GLOSSARY), or a new rule must be specified.
4. **No per-month fallback.** If the active source has no data for the chosen month the provider returns null.
5. **A lightweight, month-parameterized, Shared DTO.** Existing month data is either inside the heavy Analysis
   dashboard or latest-month-only in the Dashboard.
6. **Progress to FI / Savings Rate as a reusable calculation.** Both live in a private `DashboardService` method.

## 7. Recommendation

Add one Money Map Feature folder (`Api/Features/MoneyMap/`, ADR 0007) with read-only endpoints:

- `GET /api/money-map/flow?year=&month=` composing `IBudgetDataProvider.GetActiveSourceAsync` +
  `GetMonthAsync` (plus `GetAvailableYearsAsync`/months for the picker), returning a Shared DTO:
  source, month, total income, income categories (once the model carries them), expense categories, total expenses,
  net savings, Actual Savings Rate, and an "uncategorized expenses" remainder when categories don't sum to the total.
- Plan View: either reuse `GET /api/Dashboard/summary` (`NetWorth` + `Budget` + `SavingsGoals` + `GoalsOverview`
  already hold every figure), or a `GET /api/money-map/plan` that reuses the same calculations after moving the Progress
  to FI / Savings Rate code out of the private Dashboard method into `Infrastructure/` (it would then be shared by two
  Features).

No new endpoint is strictly required for a v1 Flow View with **one Income node**: the Analysis dashboard call with a
month already returns everything. A new endpoint is needed for income-by-category, and is recommended anyway for a
small month-scoped contract.
