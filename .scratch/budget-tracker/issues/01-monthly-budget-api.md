# 01: Monthly Budget service, validators, and API endpoints

**What to build:** The API service exposes a working `api/budget-tracker` surface that computes the monthly budget sheet
and manages categories, plans, overrides, and transactions. A caller can GET a sheet for a month (with Planned, Actual,
and Diff computed server-side), initialize a new month (copying planned amounts from the previous month or starting
blank), edit planned amounts and Actual overrides, and create, update, and delete categories and transactions. All
requests are validated server-side and scoped to the authenticated user.

**Blocked by:** None (data model and migration are already completed).

**Status:** closed

- [x] DTOs published in `PersonalFinance.Shared/DTOs/BudgetTracker/` for the sheet response, section/row data,
  transaction data, and all request types (initialize month, set planned, set actual override, save category, save
  transaction, data source).
- [x] Refit contract `IBudgetTrackerApi` added in `PersonalFinance.Shared/Contracts/` with endpoints for sheet GET,
  month POST, planned/override PUT, category CRUD, transaction CRUD, and data source GET/PUT.
- [x] `IMonthlyBudgetService`/`MonthlyBudgetService` implemented with lazy idempotent default-category seeding, sheet
  assembly (Actual equals override or date-range transaction sum, Diff rules, totals, archived-category visibility),
  month initialization (copy planned or blank), planned and override edits, category CRUD with soft delete, and
  transaction CRUD.
- [x] FluentValidation validators added for every request type (amounts, dates, category names, uniqueness, ownership).
- [x] `BudgetTrackerController` in ApiService injects the service directly, scopes all queries by user ID, and maps
  errors to BadRequest/NotFound/Conflict responses.
- [x] Services registered in `ApiService/Program.cs`.
- [x] Unit tests cover sheet calculation math (Actual, Diff for expenses and income, totals), month membership by date
  range, month initialization (copy versus blank), category rules (defaults, uniqueness, archive), transaction rules
  (amount, date, category kind), and ownership isolation.
- [x] `dotnet build` and `dotnet test` pass with no new warnings.
