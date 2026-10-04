# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- **Custom HTML Email Templates & Identity Registration Scaffolding**:
  - Implemented `EmailTemplateHelper` in `PersonalFinance.Web/Services` providing responsive, branded HTML email templates with prominent call-to-action (CTA) confirmation buttons, cross-client email client styling, and fallback URL rendering.
  - Scaffolded Identity Razor pages `Register.cshtml`/`Register.cshtml.cs` and `ResendEmailConfirmation.cshtml`/`ResendEmailConfirmation.cshtml.cs` under `Areas/Identity/Pages/Account/` to integrate custom HTML confirmation email dispatch.
  - Added unit test suite `EmailTemplateHelperTests` validating URL and token interpolation, parameter HTML escaping, and button styling.
- **Modernized Identity Login UI**:
  - Scaffolded and customized `Areas/Identity/Pages/Account/Login.cshtml` and `Login.cshtml.cs` with a clean, responsive card-based layout featuring floating input labels, validation feedback, persistent login ("Remember me"), external login provider support, and direct navigation links to registration, password recovery, and email confirmation resend flows.
- **Brevo Transactional Email Sender (REST API)**:
  - Implemented `BrevoEmailSender` implementing ASP.NET Core Identity's `IEmailSender` interface to support asynchronous verification email dispatch via Brevo REST API (`https://api.brevo.com/v3/smtp/email`) for user registration, account confirmation, and password reset flows.
  - Added `BrevoOptions` strongly-typed configuration model supporting Brevo REST API keys (`ApiKey`) and sender identity (`SenderEmail`, `SenderName`).
  - Registered `BrevoOptions` and `BrevoEmailSender` via `AddHttpClient<IEmailSender, BrevoEmailSender>()` in `PersonalFinance.Web/Program.cs`.
  - Added unit test suite `BrevoEmailSenderTests` covering REST API payload dispatch, API error handling, missing credentials handling, HTML email construction, and dependency injection service resolution.
- **Multi-Sheet OpenXML Spreadsheet & Google Sheets Parsing**:
  - Implemented multi-sheet OpenXML (`.xlsx`) parsing in `GoogleDriveHelper` to support complex budget workbooks containing multiple sheets (e.g., `Summary` and `Transactions`).
  - Added direct `.xlsx` export integration in `GoogleDriveService` for Google Spreadsheets with automatic fallback to CSV format, preserving multi-sheet access across export types.
  - Added transaction table aggregation to parse individual line-item expenses from transactions sheets, dynamically mapping and rolling up category expense amounts into monthly budget totals.
  - Added intelligent filtering to exclude non-expense balance change records (such as "Increase in total savings", "Decrease in total savings", and "Change in total savings") from expense categorization and savings rate metrics.
  - Added test coverage in `GoogleDriveHelperTests` validating multi-sheet parsing, transaction aggregation, and balance delta exclusions.
- **Google Drive Monthly Budget Report & Analytics**:
  - Implemented Google Drive monthly budget spreadsheet detection, parsing, and financial analytics engine in `GoogleDriveHelper`.
  - Added support for parsing exported Google Sheets CSVs and local spreadsheet data with automatic detection of single-month and full-year layouts, planned vs. actual income and expenses, net savings, savings rate, and category breakdowns.
  - Added starting and ending balance detection and tracking across monthly budgets.
  - Added DTO models (`MonthlyBudgetReportDto`, `BudgetMonthSummaryDto`, `BudgetCategorySummaryDto`) in `PersonalFinance.Shared` representing structured budget reporting data.
  - Added `GET /api/googledrive/spreadsheet-report` endpoint in `PersonalFinance.ApiService` and Refit client method `IGoogleDriveApi.GetMonthlyBudgetReportAsync`.
  - Added `MonthlyBudgetReport` controller action in `PersonalFinance.Web`'s `GoogleDriveController` to fetch and deliver structured report data to the frontend.
  - Integrated interactive "Budget Report" modal in `Views/GoogleDrive/Index.cshtml` featuring KPI summary cards (Total Income, Total Expenses, Net Savings/Surplus, Savings Rate), Chart.js visualizations (bar/line comparison and category doughnut charts), month-by-month switcher with full-year overview, variance tracking progress bars, and printable report layout.
  - Added unit test suites in `GoogleDriveHelperTests` and `GoogleDriveWebControllerTests` covering monthly budget spreadsheet identification, column matching, balance parsing, category aggregation, and controller error handling.
- **Google Drive Configuration Persistence & SQLite Caching**:
  - Implemented persistent SQLite database storage for user Google Drive connections and retrieved file metadata in `AppDbContext`.
  - Added `GoogleDriveConnection` entity for tracking connected drives, encrypted API credentials, synchronization timestamps, and health status.
  - Added `GoogleDriveCachedFile` entity for caching retrieved Drive file items, categories, sizes, and timestamps locally in SQLite.
  - Implemented automatic configuration and cache loading on subsequent logins to skip setup flows when a valid connection exists.
  - Supported multiple Google Drive connections per user with an intuitive "Add another Drive" switcher and management controls.
  - Added incremental cache synchronization logic to detect newly added, modified, or deleted files without re-downloading unchanged items.
  - Added manual synchronization ("Sync / Refresh") actions and endpoints to update local SQLite caches on demand.
  - Handled invalid and expired Google credentials with clear re-authentication alerts and modal key update actions.
  - Implemented `CredentialProtector` for AES encryption and secure masking of API keys and tokens in SQLite and UI views.
  - Added database migration `AddGoogleDrivePersistence` and updated SQLite schema models.
  - Added unit and integration tests in `GoogleDrivePersistenceTests` and `GoogleDriveWebControllerTests` covering first-time setup, subsequent login, multi-drive switching, caching, incremental sync, manual refresh, removal, re-authentication, and user isolation.
- **Google Drive Folder Explorer**:
  - Implemented Google Drive folder viewer allowing users to paste a Google Drive folder link (or raw folder ID) and browse all contained files in an interactive DataTables table.
  - Added `GoogleDriveHelper` in `PersonalFinance.Shared` for URL parsing (handling `/folders/`, `/u/0/folders/`, `/open?id=`, and raw IDs), friendly file category mapping, and human-readable byte formatting.
  - Added `GoogleDriveService` and `GoogleDriveController` in `PersonalFinance.ApiService` integrating with Google Drive API v3 with pagination and realistic demo fallback support.
  - Added `IGoogleDriveApi` Refit client with HTTP resilience handler.
  - Created MVC view `Views/GoogleDrive/Index.cshtml` with input form, sample link paste shortcut, metric cards, DataTables table (with custom ordering, responsive view, search, and pagination), and direct Google Drive file links.
  - Added xUnit unit test suite `PersonalFinance.Tests` covering URL parsing, byte formatting, MIME type resolution, and service handling.

### Changed
- **Streamlined Brevo Email Sender to Direct REST API**:
  - Removed legacy SMTP fallback pathways, `ISmtpClient` interface, and unused SMTP configuration options (`SmtpServer`, `Port`, `Login`, `Password`, `EnableSsl`) from `BrevoOptions` and `BrevoEmailSender`, standardizing all transactional email delivery strictly on Brevo's REST API endpoint.
- **Enforce Email Confirmation for User Registration**:
  - Configured ASP.NET Core Identity in `PersonalFinance.Web/Program.cs` with `options.SignIn.RequireConfirmedAccount = true` to require email and account verification before allowing user sign-in, mitigating spam account creation and unauthorized access.
  - Added test suite `IdentityConfigurationTests` verifying email confirmation enforcement and sign-in policy behavior for unconfirmed versus verified user accounts.
- **Default Application Route**: Configured the default MVC route in `PersonalFinance.Web/Program.cs` to route directly to `{controller=GoogleDrive}/{action=Index}/{id?}`, providing immediate access to the Google Drive file management portal upon landing.
- **Documentation & Architecture Overviews**: Updated `README.md` with comprehensive architecture overviews, data flow sequence diagrams, Google Drive analytics capabilities, caching workflows, credential encryption mechanisms, and end-to-end local development guides.
- **Google Drive Explorer UI/UX Redesign**:
  - Rebuilt `Views/GoogleDrive/Index.cshtml` into a modern, polished, intuitive file management interface inspired by Google Drive.
  - Introduced a streamlined search omnibox with inline quick actions ("Paste Sample", loading spinner, and collapsible API key drawer).
  - Added real-time category filter chips (All, Folders, Spreadsheets, Documents, PDFs, Images, Slides, Other) with dynamic count badges.
  - Implemented dual view modes: an interactive responsive DataTable view and a Google Drive-inspired card Grid view with smooth toggle support.
  - Upgraded file and folder indicators to crisp vector SVG iconography (matching Google Drive brand palettes) for all document types.
  - Added interactive clipboard copy actions with toast notifications for folder IDs and direct file links.
  - Enhanced empty states, loading states, and structured diagnostic troubleshooting cards.
  - Extended `site.css` with Google Drive design tokens, responsive cards, filter chips, and soft badges.
- **Google Drive Explorer Authentication & Authorization**:
  - Decorated `GoogleDriveController` in `PersonalFinance.Web` with `[Authorize]` attribute to enforce authentication before accessing the Google Drive explorer page or submitting queries.
  - Updated `Views/Shared/_Layout.cshtml` navigation bar to conditionally render the "Google Drive Files" menu link only for authenticated users.
- **Removed Items Page**:
  - Removed `ItemsController` and `Views/Items/Index.cshtml` from `PersonalFinance.Web`.
  - Removed the "Items Dashboard" navigation item from `Views/Shared/_Layout.cshtml`.
  - Cleaned up unused `IItemsApi` Refit client registration from `PersonalFinance.Web/Program.cs`.
- **Google Drive Explorer DataTable Sorting**:
  - Configured DataTable initialization in `Views/GoogleDrive/Index.cshtml` to sort files by Last Modified date descending by default, ensuring the most recently updated files are prioritized in view.
  - Added `orderBy=modifiedTime desc` parameter to Google Drive API v3 requests in `GoogleDriveService.cs`.
- **Google Drive Explorer Guidance & Diagnostics**:
  - Added clear instructions and callouts regarding the "Anyone with the link" (Viewer) folder sharing requirement across the web form, step-by-step modal guide, and `README.md`.
  - Enhanced `GoogleDriveService` with granular HTTP status code diagnostics (404, 403, 400) providing direct resolution steps when folder permissions are restricted or APIs are disabled.
  - Added an interactive error resolution card on the Google Drive view detailing the exact causes and steps to resolve folder permission and API key errors.
  - Added warning notices for empty folders and preview mode fallback states.
- **Serilog Console Theme**: Configured `AnsiConsoleTheme.Code` with formatted output templates across `PersonalFinance.ApiService` and `PersonalFinance.Web` for high-contrast, clear, and readable console log output.

### Fixed
- **Client-side Validation Assets**: Installed `jquery-validate` and `jquery-validation-unobtrusive` via LibMan into `PersonalFinance.Web/wwwroot/lib/`, resolving 404 errors for `jquery.validate.min.js` and `jquery.validate.unobtrusive.min.js` referenced in `_ValidationScriptsPartial.cshtml`.
- **Google Drive Timeout & Cancellation Handling**: Added specific exception handling for `TaskCanceledException`, `OperationCanceledException`, and `TimeoutException` in `GoogleDriveService` and `GoogleDriveController`, preventing unhandled cancel errors when network queries time out and providing clean fallback preview responses. Aligned HttpClient and resilience pipeline timeout configurations.

---

## [1.0.0] - 2026-09-20

### Added
- **Solution Architecture**: Created a modular multi-project .NET 10 solution:
  - `PersonalFinance.Shared`: Shared DTOs (`ItemDto`), API contracts (`IItemsApi`), exceptions (`ApiException`), and centralized version metadata (`AppVersion`).
  - `PersonalFinance.Data`: EF Core `AppDbContext`, ASP.NET Core Identity store, domain entities (`Item`), and database migrations.
  - `PersonalFinance.ApiService`: RESTful backend API with controllers (`ItemsController`), OpenAPI specification generation, Scalar interactive API documentation (`/scalar/v1`), and health checks (`/health`).
  - `PersonalFinance.Web`: ASP.NET Core MVC and Razor Pages frontend with ASP.NET Core Identity authentication, Refit type-safe HTTP client integration, resilience pipelines, DataTables, Bootstrap 5, Chart.js, and Leaflet.
- **SQLite Database Support**: Integrated `Microsoft.EntityFrameworkCore.Sqlite` for zero-configuration local database operations.
  - Added `DatabasePathHelper` to automatically resolve SQLite connection strings to a shared database file at the solution root (`PersonalFinance.db`), preventing split databases across projects during local development.
- **EF Core Code-First Support**:
  - Added `AppDbContextFactory` (`IDesignTimeDbContextFactory<AppDbContext>`) in `PersonalFinance.Data` for direct CLI migrations.
  - Generated `InitialCreate` migration establishing `Items` entity schema and ASP.NET Core Identity tables.
  - Added automatic database migration execution (`db.Database.Migrate()`) during development startup for both `ApiService` and `Web`.
- **Git Database Exclusion Rules**: Configured `.gitignore` to prevent committing SQLite database binaries and temporary lock files (`*.db`, `*.db-shm`, `*.db-wal`, `*.sqlite`, `*.sqlite3`, `PersonalFinance.db`).
- **Resilience & Observability**:
  - Configured Serilog structured logging with enrichers and console sinks.
  - Configured HTTP resilience pipelines with exponential retry and circuit-breaker patterns via `Microsoft.Extensions.Http.Resilience`.
- **Documentation**: Comprehensive `README.md` covering project architecture, SQLite setup, Git exclusion reasoning, EF Core Code-First workflow, and JetBrains Rider instructions.

### Changed
- Migrated database provider and connection strings from SQL Server (`(localdb)\mssqllocaldb`) to SQLite (`PersonalFinance.db`).
- Updated startup lifecycle in `PersonalFinance.ApiService` and `PersonalFinance.Web` from `Database.EnsureCreated()` to `Database.Migrate()` to support schema evolution via EF Core migrations.
