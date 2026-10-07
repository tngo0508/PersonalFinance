# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- **Architecture & Deployment Overview in `deployment.md`**:
  - Rewrote Section 1 as a learning-oriented architecture guide with Mermaid diagrams derived from `deploy-azure.yml` and `infra/*.bicep`: system context, solution project map, CI/CD job graph with a trigger matrix, `deploy-azure` sequence and Bicep module dependency graph, runtime request flow, configuration/secrets flow, and the startup/health/scale-to-zero lifecycle.
  - Added an Azure resource table (name patterns and cost model), an environment comparison (Aspire, Docker, Azure, tests), a secrets-to-environment-variable mapping, and a "Security Posture and Known Gaps" section (public unauthenticated API when `exposeApiPublicly=true`, SQL admin connection string, `AllowAllWindowsAzureIps` firewall rule, broad pipeline role scope).
- **`promote-latest` CI/CD Job**: Moves the `latest` GHCR tags to the deployed commit with `docker buildx imagetools create` only after `deploy-azure` and `smoke-test` succeed, so `latest` (the default image for `deploy.ps1` and `azd`) always points to a verified build.
- **`validate-infra` CI/CD Job**: Validates `infra/main.bicep` and `infra/local-dev.bicep` in a parallel job.
- **Local Development Azure SQL Provisioning**:
  - Added standalone `infra/local-dev.bicep` template provisioning only the Azure SQL server and Serverless Free Tier `PersonalFinance` database for running the app locally against Azure SQL.
  - Added `infra/deploy-local-sql.ps1` script that creates the `rg-personalfinance-dev` resource group, auto-detects the developer machine's public IP for the SQL firewall, deploys the template, and stores the resulting connection string in .NET User Secrets (`ConnectionStrings:DefaultConnection`) for `PersonalFinance.ApiService` and `PersonalFinance.Web`.
  - Added `allowedClientIpAddresses` parameter to `infra/modules/sql-database.bicep` creating per-IP server firewall rules (`AllowClientIp-{n}`).
- **Database Connection String & Secrets Management Documentation**:
  - Documented secure database connection string workflows across CI/CD, Infrastructure-as-Code (Bicep), design-time EF Core tools, and local development in `DEVELOPMENT.md` and `deployment.md`.
  - Added step-by-step instructions for `.NET User Secrets` configuration (`ConnectionStrings:DefaultConnection`) across `PersonalFinance.Web` and `PersonalFinance.ApiService` to prevent sensitive credentials from being committed to Git or stored in `appsettings.json`.
  - Documented dual CI/CD secret handling: Option A for automated Azure SQL Serverless Free Tier (`SQL_ADMIN_PASSWORD`) and Option B for external/custom SQL databases (`CUSTOM_CONNECTION_STRING`) via Bicep `@secure()` parameters and Azure Container Apps secret references (`secretRef: 'db-connection-string'`).
- **Google OAuth 2.0 External Authentication & Account Linking**:
  - Integrated Google OAuth 2.0 external authentication using `Microsoft.AspNetCore.Authentication.Google` (v10.0.0) with central package management in `Directory.Packages.props` and `PersonalFinance.Web.csproj`.
  - Configured Google authentication handler conditionally in `PersonalFinance.Web/Program.cs` supporting OpenID Connect scopes (`profile`, `email`) and claim action mappings (`urn:google:picture`, `urn:google:locale`, `urn:google:verified_email`) via `Authentication:Google` configuration and .NET User Secrets.
  - Implemented external login workflow in `Areas/Identity/Pages/Account/ExternalLogin.cshtml` and `ExternalLogin.cshtml.cs` with OAuth challenge handling, provider callback processing, automated user provisioning for verified email addresses, and secure account linking for existing confirmed accounts.
  - Added custom claim synchronization (`SynchronizeCustomClaimsAsync`) to persist and refresh Google profile picture URLs, given names, and locale preferences in Identity claims upon login and account linking.
  - Updated `_LoginPartial.cshtml` navigation partial to render user profile pictures and display names retrieved from external identity claims.
  - Modernized `Areas/Identity/Pages/Account/Login.cshtml` and `Register.cshtml` with accessible, branded Google sign-in and registration buttons dynamically populated from active authentication schemes.
  - Configured API-aware application cookie redirect handlers in `PersonalFinance.Web/Program.cs` returning HTTP `401 Unauthorized` and `403 Forbidden` status codes for unauthenticated API, JSON, and AJAX requests instead of HTML 302 redirects.
  - Added unit and integration test suite `ExternalAuthenticationTests.cs` in `PersonalFinance.Tests` covering scheme registration, option configuration, API-aware cookie redirect behaviors, user auto-provisioning, and secure account linking workflows.
- **Developer Guide & Development Notes (`DEVELOPMENT.md`)**:
  - Created a comprehensive `DEVELOPMENT.md` guide covering local prerequisites, architecture overview, .NET User Secrets management, Aspire dashboard configuration, SQLite database workflows, EF Core Code-First commands, testing guidelines, and troubleshooting solutions.
- **Secrets Management via .NET User Secrets**:
  - Migrated private API credentials (such as Brevo REST API keys and Google Drive keys) to `.NET User Secrets` (`secrets.json`) during local development to prevent committing sensitive keys to Git repositories.
- **.NET Aspire Orchestration & Service Defaults**:
  - Integrated .NET Aspire into the solution to manage multi-project lifecycle, service discovery, resilient communication, and observability.
  - Added `PersonalFinance.AppHost` project orchestrating `PersonalFinance.ApiService` and `PersonalFinance.Web` dependencies with `WithReference` and `WaitFor` definitions.
  - Added `PersonalFinance.ServiceDefaults` shared extension library configuring OpenTelemetry metrics (`AspNetCore`, `HttpClient`, `Runtime`), distributed tracing, OTLP exporters, default liveness/readiness health check endpoints (`/health` and `/alive`), service discovery, and standard HTTP resilience pipelines.
  - Configured `PersonalFinance.ApiService` and `PersonalFinance.Web` to reference `PersonalFinance.ServiceDefaults`, call `builder.AddServiceDefaults()`, and map endpoints with `app.MapDefaultEndpoints()`.
  - Configured `PersonalFinance.Web` Refit client to communicate with `ApiService` using Aspire service discovery (`https+http://apiservice`).
  - Added unit tests in `PersonalFinance.Tests` (`ServiceDefaultsTests`) verifying service registration for health checks, OpenTelemetry tracing, and metric providers.
  - Added Aspire package versions centrally to `Directory.Packages.props` and registered new projects in `PersonalFinance.sln` and `PersonalFinance.slnx`.
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
- **Faster Pull Request CI/CD Pipeline** (`.github/workflows/deploy-azure.yml`):
  - Container images now build as a parallel matrix (`api`, `web`) alongside `build-and-test` and `validate-infra` instead of sequentially after tests; `deploy-azure` still requires all three (`needs: [build-and-test, validate-infra, build-and-push-containers]`).
  - Pull requests read the Docker layer cache but no longer export it (the `mode=max` export took ~50s per image); `main` and manual runs still export it.
  - Added `concurrency` with `cancel-in-progress` for pull requests so a new push cancels the superseded run; runs on `main` queue rather than cancel, so deployments are never interrupted.
  - Added `paths-ignore` so changes limited to `**/*.md`, `.idea/`, `.junie/` and `.claude/` don't trigger the workflow.
  - Added NuGet package caching (`actions/cache` keyed on `Directory.Packages.props` and `*.csproj`) to `build-and-test`.
  - Images are pushed with the commit SHA tag only; `latest` is applied by `promote-latest`.
- **Dockerfiles**: Removed the redundant `dotnet build` step from the ApiService and Web Dockerfiles; `dotnet publish --no-restore` compiles once, saving a full compile per image.
- **Workflow Tests**: `GitHubActionsDeploymentWorkflowTests` now verifies that every job defines `timeout-minutes` (instead of asserting exactly four) and adds coverage for concurrency, `paths-ignore`, per-image cache scopes, deploy gating and `promote-latest`.
- **Monthly Budget Summary & Analytics Redesign**:
  - Redesigned the budget report modal in `Views/GoogleDrive/Index.cshtml` around a plain-language period summary (amount saved, savings rate, spending vs. plan, over-budget categories; best/toughest month and biggest expense in the year view).
  - Added a single period selector (Full year + months) with previous/next buttons and left/right arrow-key navigation.
  - Simplified KPI tiles (Income, Spending, Net saved, Savings rate) with whole-dollar amounts and "over/under plan" sub-lines; status is always shown as icon + label rather than color alone.
  - Replaced the category doughnut and rotated-label budget-vs-actual bar chart with per-category budget meters ("$X of $Y", "$N left/over", % used; within / near limit / over budget), with unbudgeted categories labeled "No budget set" instead of over budget.
  - Split year-view charts into "Income vs. spending by month" and "Net saved each month" (positive/negative bars instead of an overlaid line); clicking a month in either chart, or its table row, opens that month.
  - Adopted a validated, color-vision-deficiency-safe chart palette, hairline gridlines and compact currency axis ticks via new `bgt-*` styles in `wwwroot/css/site.css`.
  - Added dedicated loading and error states (with "Try again") so previous results are no longer shown while a new spreadsheet loads; the table view is collapsible.
- **Google Drive UI Storage Wording**: Replaced outdated SQLite references in `Views/GoogleDrive/Index.cshtml` and the `GoogleDriveController` connect message to reflect Azure SQL Database persistence via the API service, including a note about serverless auto-pause resume latency.
- **MVC & JavaScript Best Practices for the Google Drive Page**:
  - Moved ~700 lines of inline view script into `wwwroot/js/google-drive/explorer.js` and `wwwroot/js/google-drive/budget-report.js`, loaded with `asp-append-version` for cache busting and wrapped in strict-mode IIFEs to avoid global variables.
  - Removed Razor expressions from JavaScript; the report URL and connection ID are passed via `data-report-url` / `data-connection-id` attributes on `#monthlyBudgetModal`.
  - Modernized declarations from `var` to block-scoped `const` / `let` (applied with ESLint `no-var` and `prefer-const` autofixes).
  - Trimmed `Views/Shared/_Layout.cshtml` to load only jQuery, Bootstrap and `site.js` globally; removed unused Leaflet and Select2 assets from every page and moved DataTables and Chart.js into the Google Drive page's `Scripts` / new `Styles` sections.
- **Removed Boilerplate HomeController & Views**:
  - Removed obsolete template `HomeController`, default views (`Views/Home/Index.cshtml`, `Views/Home/Privacy.cshtml`), `ErrorViewModel`, and `Views/Shared/Error.cshtml`.
  - Updated `_Layout.cshtml` navigation and brand links to route directly to `GoogleDriveController`, removing dead links to `Home` and `Privacy`.
  - Configured standard `ProblemDetails` exception handling middleware in `PersonalFinance.Web/Program.cs`.
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
- **Docker Layer Cache Collisions in CI/CD**: The ApiService and Web builds shared the default GitHub Actions cache scope and overwrote each other's cache, causing `dotnet restore` layer misses; each image now uses its own scope (`scope=api` / `scope=web`).
- **Outdated Deployment Documentation**: Removed SQLite references from `deployment.md`, corrected notes that described the API as internal-only (CI deploys it with `exposeApiPublicly=true`), added the required `ConnectionStrings__DefaultConnection` to the local Docker run commands, replaced the `deploy.ps1` "SQLite" option with a custom connection string option, and documented that `-DeploySqlDatabase` must be passed explicitly (the script sends `$DeploySqlDatabase.IsPresent`, so its `= $true` default has no effect).
- **Budget Report Calculations & Rendering**:
  - Fixed the year-view "Avg / month" figures dividing by 12 regardless of how many months the spreadsheet contains.
  - Fixed budget charts rendering blank when report data arrived before the modal finished opening (charts now re-measure on `shown.bs.modal`).
  - Fixed stale responses from a previously opened spreadsheet overwriting the current report.
  - Print now outputs only the budget report with the details table expanded, instead of the whole page.
- **Local SQL Provisioning Script Parameter Quoting on Windows PowerShell**:
  - Fixed `infra/deploy-local-sql.ps1` failing with `Failed to parse string as JSON` because Windows PowerShell 5.1 strips embedded double quotes from native command arguments, corrupting the inline `allowedClientIpAddresses` JSON array (and passwords containing quotes or symbols).
  - Deployment parameters are now written to a temporary ARM parameters file passed via `--parameters @file`, which is deleted after deployment.
  - Added `az` exit code checking so failed deployments report a clear error instead of an empty provisioning state.
- **Post-Deployment Smoke Test Timeouts & Hang Prevention in CI/CD**:
  - Enforced connection (`--connect-timeout 5`) and maximum execution (`--max-time 10`) timeouts on all `curl` probes in `.github/workflows/deploy-azure.yml`, preventing post-deployment smoke tests from hanging indefinitely on unresolved sockets or cold-starting Azure Container Apps ingress.
  - Added default HTTP `000` status capture fallback when network requests fail or time out, preventing broken subshell evaluations.
  - Added URL normalization and trailing slash stripping (`${WEB_URL%/}`) with fail-fast validation when target deployment endpoints cannot be resolved from Azure outputs.
  - Configured job-level `timeout-minutes` limits across all workflow stages (`build-and-test: 15`, `build-and-push-containers: 20`, `deploy-azure: 25`, `smoke-test: 10`) to eliminate runaway workflow execution and conserve runner compute resources.
  - Added unit test coverage in `GitHubActionsDeploymentWorkflowTests.cs` verifying job timeouts and smoke test curl configuration.
- **Database Migration Target Host Logging & Container Apps Troubleshooting**:
  - Enhanced `DatabaseMigrationExtensions.cs` to explicitly log the target database host (`DataSource`) during startup migration initialization, making connection resolution and host configuration immediately visible in Azure Container Apps logs.
  - Added troubleshooting guidance to `deployment.md` for diagnosing `SocketException: Cannot assign requested address [::1]:1433` (Error Number: 10049) when container instances lack connection strings and fall back to local IPv6 loopbacks.
- **CI/CD Docker Image Tag Mismatch in GitHub Container Registry (GHCR)**:
  - Fixed `docker/metadata-action` tag format in `.github/workflows/deploy-azure.yml` by setting `prefix=` for `type=sha,format=short`, ensuring built container images are tagged with `<short_sha>` (e.g. `:3d8c356`) matching the parameters passed to Bicep rather than defaulting to `:sha-<short_sha>`.
  - Added troubleshooting guidance in `deployment.md` for resolving `MANIFEST_UNKNOWN` container image pull errors and configuring GitHub Container Registry package visibility settings.
- **Sign Out Redirect to Removed HomeController**: Updated `_LoginPartial.cshtml` sign-out form `asp-route-returnUrl` from obsolete `Url.Action("Index", "Home", new { area = "" })` to `Url.Action("Index", "GoogleDrive", new { area = "" })`, ensuring user sign-out redirects smoothly to the active landing controller.
- **Aspire Dashboard OTLP Endpoint Environment Variables**: Corrected misnamed dashboard environment variables in `PersonalFinance.AppHost/Properties/launchSettings.json` to standard `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL` and `DOTNET_RESOURCE_SERVICE_ENDPOINT_URL`, resolving startup exceptions on dashboard resource configuration.
- **Client-side Validation Assets**: Installed `jquery-validate` and `jquery-validation-unobtrusive` via LibMan into `PersonalFinance.Web/wwwroot/lib/`, resolving 404 errors for `jquery.validate.min.js` and `jquery.validate.unobtrusive.min.js` referenced in `_ValidationScriptsPartial.cshtml`.
- **Google Drive Timeout & Cancellation Handling**: Added specific exception handling for `TaskCanceledException`, `OperationCanceledException`, and `TimeoutException` in `GoogleDriveService` and `GoogleDriveController`, preventing unhandled cancel errors when network queries time out and providing clean fallback preview responses. Aligned HttpClient and resilience pipeline timeout configurations.

### Security
- **Global Antiforgery Validation**: Registered `AutoValidateAntiforgeryTokenAttribute` as a global MVC filter in `PersonalFinance.Web/Program.cs` so every unsafe (POST/PUT/PATCH/DELETE) action validates antiforgery tokens by default.
- **HTML Escaping of Spreadsheet Content**: Budget report category names, month names and data-source labels read from Google Sheets are now HTML-escaped before rendering, preventing script/HTML injection from spreadsheet content.

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
