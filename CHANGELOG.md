# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- **Google Drive Folder Explorer**:
  - Implemented Google Drive folder viewer allowing users to paste a Google Drive folder link (or raw folder ID) and browse all contained files in an interactive DataTables table.
  - Added `GoogleDriveHelper` in `PersonalFinance.Shared` for URL parsing (handling `/folders/`, `/u/0/folders/`, `/open?id=`, and raw IDs), friendly file category mapping, and human-readable byte formatting.
  - Added `GoogleDriveService` and `GoogleDriveController` in `PersonalFinance.ApiService` integrating with Google Drive API v3 with pagination and realistic demo fallback support.
  - Added `IGoogleDriveApi` Refit client with HTTP resilience handler.
  - Created MVC view `Views/GoogleDrive/Index.cshtml` with input form, sample link paste shortcut, metric cards, DataTables table (with custom ordering, responsive view, search, and pagination), and direct Google Drive file links.
  - Added xUnit unit test suite `PersonalFinance.Tests` covering URL parsing, byte formatting, MIME type resolution, and service handling.

### Changed
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
