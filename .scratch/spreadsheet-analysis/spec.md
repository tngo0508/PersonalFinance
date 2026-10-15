Status: ready-for-agent

# Spreadsheet Analysis Feature

## Problem Statement

The user maintains personal financial data across multiple Google Sheets spreadsheets—monthly budget reports and
transaction logs—but currently can only view raw data. There is no way to analyze spending patterns, track trends over
time, understand category performance, or project future financial outcomes. The user must manually calculate insights,
compare months, and identify spending anomalies, which is time-consuming and error-prone.

## Solution

Add a Spreadsheet Analysis feature that persists parsed spreadsheet data to the database and provides a comprehensive
analysis dashboard. The system will sync data from multiple spreadsheets on demand, merge and deduplicate the data, and
present actionable insights including spending velocity, trend analysis, category insights, and budget variance reports.
A quick summary appears in the existing budget report modal, with a link to a dedicated Analysis page for deep dives.
Phase 2 adds forecasting, anomaly detection, and comparison views.

## User Stories

1. As a user with Google Drive spreadsheets, I want to sync my spreadsheet data to the database, so that I can analyze
   it without re-reading from Google Drive each time.

2. As a user, I want to sync each spreadsheet manually on demand, so that I control when data is captured and stored.

3. As a user with multiple spreadsheets, I want data from all my spreadsheets to be merged for analysis, so that I see a
   unified view of my finances.

4. As a user, I want to be warned before overwriting existing month data during sync, so that I don't accidentally lose
   historical data.

5. As a user, I want to see a summary of key metrics (savings rate, daily burn rate, budget status, top overspend
   category) in the budget report modal, so that I get quick insights without navigating away.

6. As a user, I want to see a mini trend chart in the budget report modal, so that I can spot recent spending patterns
   at a glance.

7. As a user, I want to navigate to a dedicated Analysis page from the budget report modal, so that I can explore
   detailed insights.

8. As a user, I want to see my daily burn rate (average daily spending projected to month-end), so that I know if I'm on
   track to overspend before the month ends.

9. As a user, I want to see my weekly spending trend (week-over-week pattern), so that I can identify whether my
   spending is accelerating or decelerating.

10. As a user, I want to see per-category budget consumption rate versus time elapsed, so that I can see which
    categories are burning through their budget fastest.

11. As a user, I want to see month-over-month trend charts for income, expenses, and savings rate, so that I can track
    how my finances are changing over time.

12. As a user, I want to see a pie or bar chart of my spending by category, so that I understand where my money goes.

13. As a user, I want to see which categories are over or under budget, so that I can identify areas to adjust.

14. As a user, I want to see budget vs actual variance ranked by category, so that I can focus on the biggest
    discrepancies first.

15. As a user, I want to see total income, total expenses, net savings, and savings rate for selected months, so that I
    have context for all other metrics.

16. As a user, I want to select which months to analyze, so that I can focus on specific periods.

17. As a user, I want all currency values formatted in USD, so that amounts are clear and consistent.

18. As a user, I want to see simple linear projections of my current spending trends, so that I can forecast future
    balances (Phase 2).

19. As a user, I want unusual transactions flagged automatically, so that I can identify spending outliers (Phase 2).

20. As a user, I want to be alerted when a category exceeds a configurable percentage of its budget, so that I can take
    corrective action early (Phase 2).

21. As a user, I want to compare two months side by side, so that I can see category-level differences (Phase 2).

22. As a user, I want to compare quarters (Q1 vs Q2), so that I can see longer-term trends (Phase 2).

23. As a user, I want to compare the same month across years (January 2025 vs January 2026), so that I can track
    year-over-year performance (Phase 2).

24. As a user, I want all my historical data retained forever, so that I have a complete financial record.

25. As an admin, I want role-based access control, so that I can manage permissions when the app supports multiple
    users.

26. As a user, I want the Analysis page in the main navigation next to Google Drive, so that I can find it easily.

27. As a user, I want charts rendered with Chart.js, so that the analysis page looks consistent with the rest of the
    application.

28. As a user, I want empty states when no data is synced yet, so that I understand what to do next.

29. As a user, I want loading states during sync operations, so that I know the system is working.

30. As a user, I want error messages when sync fails, so that I can troubleshoot connection issues.

## Implementation Decisions

### Data Layer

- Two new entities: SpreadsheetBudgetSnapshot (stores parsed monthly budget report data per spreadsheet) and
  SpreadsheetTransactionRecord (stores individual transactions). Both link to GoogleDriveConnection via foreign key and
  include UserId for multi-user isolation.
- SpreadsheetBudgetSnapshot stores: MonthNumber, MonthName, Year, BudgetedIncome, ActualIncome, BudgetedExpenses,
  ActualExpenses, StartingBalance, EndingBalance, and a JSON column for category breakdowns.
- SpreadsheetTransactionRecord stores: Date, Amount, Description, Category, Type (Income/Expense), and a reference to
  the source SpreadsheetBudgetSnapshot or GoogleDriveConnection.
- Both entities configured in AppDbContext.OnModelCreating with appropriate indexes on UserId, ConnectionId, and date
  ranges for efficient analysis queries.
- Decimal properties use explicit precision (18, 4) consistent with existing financial entities like NetWorthParameters.
- All historical data is retained indefinitely; no retention policy or rolling window.

### Sync Flow

- Manual "Sync Budget" button per spreadsheet on the Google Drive page triggers a SyncSpreadsheetBudgetCommand via
  MediatR.
- On sync, the system parses the spreadsheet via the existing GoogleDriveService.GetSpreadsheetBudgetReportAsync and
  GetSpreadsheetTransactionsAsync methods.
- If a month already exists in the database for the same connection, the system shows a warning dialog asking the user
  to keep existing data or overwrite.
- Transactions are stored individually; budget summaries are stored as monthly snapshots.
- Data from multiple spreadsheets is merged for analysis queries, filtered by UserId.

### Analysis Service

- New ISpreadsheetAnalysisService interface and SpreadsheetAnalysisService implementation in
  PersonalFinance.ApiService.Services.
- The service provides methods for:
    - GetSummaryStatisticsAsync (userId, year, month) — total income, expenses, net savings, savings rate
    - GetSpendingVelocityAsync (userId, year, month) — daily burn rate, weekly trend, category velocity
    - GetTrendDataAsync (userId, year) — month-over-month series for income, expenses, savings rate
    - GetCategoryInsightsAsync (userId, year, month) — spending by category with over/under budget highlights
    - GetBudgetVarianceAsync (userId, year, month) — categories ranked by variance
- All methods accept CancellationToken for async cancellation.
- The service queries the database entities directly using EF Core with AsNoTracking () for read-only operations.

### CQRS Commands and Queries

- SyncSpreadsheetBudgetCommand — triggers parsing and persistence of budget report and transactions from a spreadsheet.
- GetAnalysisDashboardQuery — returns a comprehensive dashboard ViewModel with summary statistics, spending velocity,
  trends, and category insights for a selected period.
- GetMonthConflictQuery — checks whether a month already exists in the database before sync, returning the existing data
  for the overwrite confirmation dialog.
- Handlers follow the existing pattern: thin handlers delegating to services, with constructor-injected dependencies.

### Controllers and Endpoints

- New AnalysisController in PersonalFinance.Web.Controllers with actions for:
    - GET /Analysis/Index — displays the full analysis dashboard
    - POST /Analysis/Sync — triggers spreadsheet sync
    - GET /Analysis/DashboardData — returns dashboard data as JSON for JavaScript rendering
    - POST /Analysis/ConfirmOverwrite — confirms whether to overwrite existing month data
- The existing GoogleDriveController is extended with a "Sync Budget" action per spreadsheet and a "View Analysis" link.
- The existing budget report modal is extended with a summary section showing key metric cards, a mini sparkline chart,
  and a "View Full Analysis" link.

### Presentation Layer

- New Razor Views under PersonalFinance.Web/Views/Analysis/:
    - Index.cshtml — full analysis dashboard with Chart.js charts
    - Partial views for individual chart sections (summary cards, velocity charts, trend charts, category charts)
- New ViewModel AnalysisDashboardViewModel in PersonalFinance.Web/Models containing all dashboard data.
- JavaScript module in PersonalFinance.Web/wwwroot/js/analysis/ for Chart.js rendering, following the existing module
  pattern (IIFE with 'use strict', DOM ready pattern, ESLint-compliant).
- All charts use Chart.js (existing library), consistent with net worth projection charts.

### Role-Based Authorization

- Plan for Admin and User roles using ASP.NET Core Identity roles infrastructure.
- Phase 1 enforces UserId filtering on all queries to ensure users only access their own data.
- Role infrastructure is prepared but full role-based access control is a Phase 2 concern.

### UI Placement

- New "Analysis" navigation item placed next to "Google Drive" in the main navigation bar.
- Modal summary in the existing budget report modal with:
    - 3-4 key metric cards (savings rate, daily burn rate, budget status, top overspend category)
    - Mini sparkline chart showing last 3 months of spending trend
    - "View Full Analysis" link to the dedicated Analysis page

### Currency and Formatting

- All values formatted in USD with $ prefix.
- Consistent number formatting across charts, tables, and summary cards.

## Testing Decisions

### Testing Philosophy

Test external behavior, not implementation details. The analysis service should be tested with known input data to
verify calculations are correct. The sync handler should be tested with mock Google Drive service responses to verify
persistence logic. Controller integration should follow the existing ApiServiceEndpointTests.cs pattern.

### Test Seams

**Seam 1: Analysis Service Layer** (highest seam)

- Unit tests for SpreadsheetAnalysisService verifying all calculation methods:
    - Summary statistics calculations with known budget and transaction data
    - Spending velocity calculations (daily burn rate, weekly trend, category velocity)
    - Trend data aggregation across months
    - Category insights and variance rankings
    - Edge cases: empty data, single month, zero income, negative savings
- Tests use in-memory EF Core context or direct service method calls with constructed entity lists.

**Seam 2: Sync Command Handler**

- Unit tests for SyncSpreadsheetBudgetCommandHandler:
    - Sync with no existing data creates new records
    - Sync with existing month data returns conflict information
    - Overwrite confirmation replaces existing data correctly
    - Multiple spreadsheets merge data correctly
    - Error handling for failed parsing
- Tests mock IGoogleDriveService and use in-memory EF Core context.

**Seam 3: Controller Integration** (existing pattern)

- Integration tests following ApiServiceEndpointTests.cs:
    - Analysis controller returns correct views with dashboard data
    - Sync endpoint triggers sync and redirects appropriately
    - Overwrite confirmation flow works end-to-end
    - Authorization checks prevent cross-user data access

### Prior Art

- GoogleDriveHelperTests.cs — unit tests for helper methods with theory/inline data attributes
- GoogleDrivePersistenceTests.cs — database persistence tests with in-memory EF Core
- GoogleDriveWebControllerTests.cs — controller-level tests
- ApiServiceEndpointTests.cs — integration tests for the Web → ApiService layer

## Out of Scope

- **Phase 2 features**: Forecasting (simple linear projection), Anomaly Detection (statistical outliers and budget
  threshold alerts), and Comparisons (month vs month, quarterly, year over year) are designed but not implemented in
  Phase 1.
- **Automatic sync**: No background jobs or scheduled sync. All syncs are manual.
- **Multiple spreadsheet formats**: The parser supports only the user's existing fixed spreadsheet format.
- **Multi-currency support**: Only USD is supported.
- **Data retention policies**: All data is kept forever; no deletion or rolling window.
- **Full role-based access control**: UserId filtering is enforced, but Admin/User role infrastructure is only prepared,
  not fully implemented.
- **Mobile-specific UI**: The analysis page follows existing responsive patterns but has no mobile-specific
  optimizations.
- **Export functionality**: No CSV, PDF, or report export capabilities.
- **Real-time updates**: The dashboard reflects data as of the last sync; no live updates.

## Further Notes

- The existing GoogleDriveService already provides GetSpreadsheetBudgetReportAsync and GetSpreadsheetTransactionsAsync
  methods that parse spreadsheets into DTOs. The sync command will reuse these methods rather than duplicating parsing
  logic.
- The MonthlyBudgetReportDto and SpreadsheetTransactionsDto DTOs are already well-structured for analysis. The new
  entities will map from these DTOs during sync.
- Chart.js is already loaded in the project for net worth projections. The analysis page will reuse the same library and
  follow similar rendering patterns.
- The project uses ESLint for JavaScript with strict rules (no ar, single quotes, semicolons, IIFE modules). The
  analysis JavaScript module must comply.
- Version should be bumped in PersonalFinance.Shared/Constants/AppVersion.cs after implementation (Minor bump for new
  feature).
