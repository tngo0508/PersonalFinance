# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

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
