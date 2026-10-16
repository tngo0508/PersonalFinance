# AGENTS.md - .NET Core MVC Best Practices Guidelines

This document provides guidelines for AI agents working on this .NET Core MVC project, based on Microsoft's official best practices and architectural recommendations.

## Table of Contents
- [Quick Start](#quick-start)
- [Project Architecture](#project-architecture)
- [Backend/Frontend Code Separation](#backendfrontend-code-separation)
- [Git Worktrees](#git-worktrees)
- [Feature Implementation Protocol](#feature-implementation-protocol)
- [Version Management](#version-management)
- [Changelog Management](#changelog-management)
- [Naming Conventions](#naming-conventions)
- [JavaScript / ESLint](#javascript--eslint)
- [References](#references)

## Quick Start

Before working on the project, read these reference documents:

- **`GLOSSARY.md`** — Canonical definitions for architecture terms (Feature, Command, Query, Handler, Service, DTO, Entity) and domain concepts.
- **`docs/adr/`** — Architecture Decision Records explaining why key design choices were made.

## Project Architecture

### Layered Architecture
The project follows a layered architecture pattern with clear separation of concerns:

1. **UI Layer** (`PersonalFinance.Web`) - Entry point, handles HTTP requests/responses
2. **API Service Layer** (`PersonalFinance.ApiService`) - Business logic and service implementations
3. **Data Layer** (`PersonalFinance.Data`) - Data access, entities, and DbContext
4. **Shared Layer** (`PersonalFinance.Shared`) - DTOs, models, and shared utilities

### Key Principles
- **Dependency Inversion**: UI layer depends on interfaces defined in Application Core, not concrete implementations
- **Composition Root**: `Program.cs` is responsible for wiring up dependencies via DI
- **No Direct Instantiation**: Use dependency injection instead of `new` keyword for services
- **Interface Segregation**: Define interfaces in the layer that consumes them
- **CQRS with MediatR**: All ApiService features use CQRS — commands for actions, queries for data. See `docs/adr/0001-standardize-cqrs-feature-organization.md`.
- **Feature-Based Organization**: ApiService is organized by feature (e.g., `NetWorth/`, `Budget/`), not by type. Each feature is a vertical slice containing its controller, commands, queries, handlers, services, and validators.
- **Soft Deletes**: All user-owned entities inherit from `AuditableEntity` with soft-delete support. EF Core global query filters automatically exclude deleted records.

## Backend/Frontend Code Separation

### Strict Project Boundary
**`PersonalFinance.ApiService`** contains all backend code — business logic, service implementations, and data access coordination.  
**`PersonalFinance.Web`** contains all frontend code — controllers, views, Razor components, and UI-specific services.

When creating or modifying code, always place it in the correct project:

| Code Type | Project | Examples |
|---|---|---|
| Feature controllers (API) | `PersonalFinance.ApiService` | `NetWorth/NetWorthController.cs` |
| CQRS commands & queries | `PersonalFinance.ApiService` | `NetWorth/Commands/SaveParametersCommand.cs` |
| CQRS handlers | `PersonalFinance.ApiService` | `NetWorth/Commands/SaveParametersCommandHandler.cs` |
| Business logic services | `PersonalFinance.ApiService` | `NetWorth/Services/NetWorthParametersService.cs` |
| Validators | `PersonalFinance.ApiService` | `NetWorth/Validators/NetWorthValidators.cs` |
| MVC controllers (Web) | `PersonalFinance.Web` | `NetworthController.cs` |
| Views (Razor) | `PersonalFinance.Web` | `.cshtml` files |
| UI-specific services | `PersonalFinance.Web` | `UserIdForwardingHandler`, view model factories |
| Data entities | `PersonalFinance.Data` | `NetWorthParameters` entity |
| DbContext | `PersonalFinance.Data` | `ApplicationDbContext` |
| DTOs (by feature) | `PersonalFinance.Shared` | `DTOs/NetWorth/NetWorthParametersDto.cs` |
| API contracts | `PersonalFinance.Shared` | `Contracts/INetWorthApi.cs` |

### Rules for Code Placement
- **Never put business logic in Web controllers** — Controllers in `PersonalFinance.Web` should only handle HTTP concerns (request parsing, status codes, view selection) and delegate to services in `PersonalFinance.ApiService`.
- **Never put UI code in ApiService** — `PersonalFinance.ApiService` should have no dependency on MVC, Razor, or any UI framework.
- **Web depends on ApiService** — `PersonalFinance.Web` references `PersonalFinance.ApiService` to consume service interfaces, but never the reverse.
- **Shared for cross-cutting DTOs** — Data transfer objects used by both layers belong in `PersonalFinance.Shared`.
- **Data layer is independent** — `PersonalFinance.Data` contains only entities and DbContext; both `ApiService` and `Web` may reference it, but it should not reference either.

### Dependency Flow
```
PersonalFinance.Web  ──depends on──>  PersonalFinance.ApiService  ──depends on──>  PersonalFinance.Data
         │                                    │                                    ▲
         └──────── depends on ────────────────┘                                    │
              PersonalFinance.Shared  ──referenced by all layers───────────────────┘
```

## Git Worktrees

### Worktree Structure

The project uses git worktrees to enable parallel work on multiple branches simultaneously. All worktrees live in the
`.worktrees/` directory inside the repository root:

```
C:\workdir\repos\PersonalFinanceApp\          ← Main repo (main branch)
    .worktrees/
        ├── dev/                              ← Active development
        ├── release/                          ← Release preparation
        └── hotfix/                           ← Emergency fixes
```

### Worktree Purposes

| Worktree      | Branch    | Purpose                       | When to Use                                     |
|---------------|-----------|-------------------------------|-------------------------------------------------|
| **Main repo** | `main`    | Stable, production-ready code | Reference, PR reviews, stable baseline          |
| **dev**       | `dev`     | Active development            | Day-to-day feature work, integration testing    |
| **release**   | `release` | Release preparation           | Final testing, version bumping, deployment prep |
| **hotfix**    | `hotfix`  | Emergency fixes               | Urgent bug fixes that need immediate deployment |

### Rules for Working with Worktrees

- **Work in the appropriate worktree** — Use `dev` for feature development, `release` for release prep, and `hotfix` for
  urgent fixes.
- **Keep main repo clean** — The main repository should stay on `main` branch as a stable reference.
- **Never delete worktree directories manually** — Always use `git worktree remove` to clean up worktrees.
- **One branch per worktree** — Each worktree checks out exactly one branch.
- **Clean up after merging** — Remove worktrees that are no longer needed: `git worktree remove .worktrees/<name>`
- **The `.worktrees/` directory is gitignored** — It will not be tracked or committed.

### Quick Reference

```powershell
# List all worktrees
git worktree list

# Add a new worktree with a new branch
git worktree add .worktrees/<name> -b <branch-name>

# Remove a worktree (clean)
git worktree remove .worktrees/<name>

# Force remove (if dirty)
git worktree remove --force .worktrees/<name>

# Prune stale entries
git worktree prune
```

## Feature Implementation Protocol

### Core Principles

When instructed to implement a feature, follow this strict, end-to-end vertical-slice workflow. Do not skip steps, implement placeholder functionality, or leave unfinished integration work.

1. Inspect the existing codebase before making changes.
2. Follow established architectural patterns, naming conventions, and dependency injection registrations.
3. Implement the complete feature across all required layers.
4. Preserve existing functionality and maintain backward compatibility unless explicitly instructed otherwise.
5. Validate correctness, security, performance, accessibility, and user experience.
6. Run appropriate tests and builds before declaring the task complete.
7. Never claim that functionality works unless the relevant implementation and verification support that claim.

### 1. Repository Inspection and Implementation Planning

Before writing code:

- Inspect the solution structure, project references, existing controllers, services, handlers, entities, DbContext, migrations, ViewModels, Razor Views, JavaScript, and tests relevant to the feature.
- Read the existing `AGENTS.md`, project-specific instructions, and relevant configuration files.
- Identify the application's current architecture, including whether CQRS, MediatR, repositories, service layers, or other patterns are already established.
- Reuse existing components and abstractions when appropriate. Do not create duplicate services, entities, helpers, or infrastructure.
- Identify database changes, dependencies, API endpoints, UI changes, validation rules, authorization requirements, and testing requirements.
- Create a concise implementation plan for complex features before modifying files.

Do not assume a class, table, endpoint, package, or configuration exists without inspecting the repository.

### 2. Data Layer First — Azure SQL Database and Entity Framework Core

Implement and validate the data layer before building dependent application logic.

#### Database Architecture

- The application uses **Microsoft Azure SQL Database**, accessed through Entity Framework Core.
- Use the existing EF Core Code-First or database-first approach. Do not introduce a conflicting database management strategy.
- Use the existing Azure SQL Database connection configuration and dependency injection setup.
- Do not assume that local SQL Server, SQL Server Express, LocalDB, or a local database is the target database.
- Do not create a new database or change the configured Azure SQL server, database name, authentication method, or hosting configuration unless explicitly required.
- Never hardcode database credentials, passwords, access tokens, or connection strings containing secrets.
- Store secrets in appropriate configuration providers, such as environment variables, Azure Key Vault, or the existing managed identity configuration.
- Prefer Microsoft Entra ID authentication and managed identities for Azure-hosted applications when supported by the existing deployment architecture.
- Grant database identities only the permissions required by the application.
- Keep development, testing, staging, and production database configurations separate.

#### Entity and Schema Design

- **User-owned entities inherit from `AuditableEntity`** — All entities with a `UserId` field inherit from `AuditableEntity`, which provides `Id`, `UserId`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, and `DeletedAt`.
- **Soft deletes are mandatory** — Never hard-delete user-owned entities. Set `IsDeleted = true` and `DeletedAt = DateTime.UtcNow`. EF Core global query filters automatically exclude soft-deleted records.
- **System-defined entities** — Entities without `UserId` (e.g., `CommunityPoll`, `CommunityPollOption`) do NOT inherit from `AuditableEntity`.
- Follow the existing EF Core Code-First or database-first approach. Do not introduce a conflicting database management strategy.
- When Code-First migrations are used, create migrations for schema changes and inspect the generated migration before applying it.
- Define appropriate primary keys, foreign keys, indexes, unique constraints, nullability, maximum string lengths, precision, and delete behaviors.
- Use `decimal` with explicit precision for financial amounts. Never use `float` or `double` for money calculations.
- Store timestamps and dates using appropriate types and consistent time-zone conventions.
- Use `DateOnly` for date-only domain values when supported by the existing data model and provider.
- Avoid storing calculated values that can be derived reliably unless persistence is justified by the business requirements.
- Never modify production data or apply production migrations without explicit authorization.

#### Entity Framework Core and Schema Changes

- Use EF Core migrations when the project follows the Code-First approach.
- Inspect existing migrations and the current model before creating a new migration.
- Review generated migration scripts for destructive operations, data loss, unintended schema changes, and compatibility issues.
- Use explicit database types and precision where necessary, especially for financial values.
- Define appropriate primary keys, foreign keys, indexes, unique constraints, nullability, maximum string lengths, and delete behaviors.
- Use `decimal` with explicit precision for financial amounts. Never use `float` or `double` for money calculations.
- Ensure schema changes are compatible with Azure SQL Database and the configured EF Core SQL Server provider.
- Do not assume that every SQL Server feature, instance-level setting, or administrative operation is available in Azure SQL Database.
- Do not reset the database, drop tables, truncate production data, or apply destructive migrations without explicit authorization.
- Generate and inspect migration SQL before production deployment when appropriate.
- Follow the existing CI/CD process for applying migrations. Do not automatically run production migrations during application startup unless the project explicitly adopts that deployment strategy.

#### Query Correctness and Performance

- Use asynchronous EF Core operations such as `ToListAsync`, `SingleOrDefaultAsync`, and `SaveChangesAsync` where appropriate.
- Pass `CancellationToken` through asynchronous operations when supported by the existing application architecture.
- Use `AsNoTracking()` for read-only entity queries when change tracking is unnecessary.
- Project directly into DTOs or ViewModels when only selected fields are needed.
- Avoid N+1 queries, unnecessary `Include` chains, premature materialization, and loading entire tables into memory.
- Use deterministic ordering whenever selecting the first, last, newest, oldest, or top records. Include a stable tie-breaker when necessary.
- Use database-side filtering, sorting, aggregation, and pagination whenever practical.
- Use parameterized queries. Never concatenate user input into SQL statements.
- For date-bound queries, use correct date-range boundaries and indexes. For SQL Server, prefer sargable range predicates where practical instead of wrapping indexed columns in functions.
- Use `CAST(GETDATE() AS DATE)` only when appropriate for SQL Server-local date semantics. Do not assume the database server's local time zone matches the user's time zone.
- Consider transaction boundaries and concurrency when multiple related changes must succeed or fail together.
- Consider indexes, execution plans, query performance, connection limits, and database resource consumption when optimizing frequently executed queries.
- Avoid unnecessary round trips to Azure SQL Database, especially in loops.
- For user-specific dates, explicitly account for the intended time zone.
- Handle transient database failures using appropriate retry strategies where supported by the existing EF Core configuration.
- Do not blindly retry non-idempotent operations that could create duplicate records.

#### Azure SQL Database Connectivity and Operations

- Reuse the application's existing connection pooling and `DbContext` lifetime configuration.
- Do not create a new `DbContext` or database connection for every individual query without a valid architectural reason.
- Never log passwords, access tokens, connection strings containing secrets, or sensitive financial records.
- Diagnose connectivity failures by checking authentication, database permissions, firewall rules, private endpoints, DNS, networking, and connection configuration as appropriate.
- Do not disable firewall restrictions, TLS verification, or authentication protections as a workaround.
- Account for Azure SQL Database resource limits and possible throttling when designing expensive reports, bulk operations, and background jobs.
- Use appropriate pagination, batching, indexes, and query optimization for large datasets.
- Keep database administration and application data-access permissions separate where practical.

#### Migration Validation

- Inspect migration scripts for unintended data loss, destructive operations, and incorrect defaults.
- Verify model configuration and migration consistency.
- Follow the repository's existing migration and deployment workflow.
- Do not automatically reset databases, drop tables, or delete existing records to resolve migration errors.
- Verify that the EF Core model, migrations, and configured Azure SQL Database provider are consistent.
- Inspect generated SQL before applying important schema changes.
- Prefer testing migrations against a dedicated development or test database before staging or production.
- Clearly identify any migration that requires manual execution, elevated permissions, data backfills, or deployment coordination.

### 3. Application Layer — CQRS and MediatR

**All ApiService features use CQRS with MediatR.** There is no exception — every feature follows the same pattern. See `docs/adr/0001-standardize-cqrs-feature-organization.md` for the rationale.

#### Feature Folder Structure

Each feature lives in its own folder under `PersonalFinance.ApiService/`:

```
ApiService/
  NetWorth/
    NetWorthController.cs          ← API controller, injects IMediator
    Commands/
      SaveParametersCommand.cs     ← Command + handler in same file
      DeleteParametersCommand.cs
    Queries/
      LoadParametersQuery.cs       ← Query + handler in same file
    Services/
      NetWorthParametersService.cs ← Business logic and data access
    Validators/
      NetWorthValidators.cs        ← FluentValidation rules
```

Namespace convention: `PersonalFinance.ApiService.[Feature].Commands`, `PersonalFinance.ApiService.[Feature].Services`, etc.

#### Commands and Queries

Use clear, intention-revealing names, such as:

- `Create[Feature]Command`
- `Update[Feature]Command`
- `Delete[Feature]Command`
- `Get[Feature]Query`
- `Get[Feature]ByIdQuery`
- `[Feature]CommandHandler`
- `[Feature]QueryHandler`

Create only the operations required by the feature.

#### Handler Responsibilities

- **Handlers are thin coordinators** — they receive the command/query, call the appropriate service(s), map the result, and return the response.
- **Business logic lives in services** — handlers delegate to services; don't duplicate logic in handlers.
- Keep business logic out of MVC controllers.
- Inject required dependencies through constructors.
- Inject `DbContext`, external API clients, and other dependencies into the component that actually requires them.
- Keep handlers focused on one use case.
- Apply business rules, validation, authorization requirements, and transaction boundaries at the appropriate layer.
- Use asynchronous operations and propagate cancellation tokens.
- Return explicit, predictable results for success, validation failures, missing records, and conflicts.
- Avoid swallowing exceptions or returning success when an operation failed.
- Do not add unnecessary abstractions or repository layers when existing patterns already provide the required separation.

#### Validation and External Dependencies

- Validate inputs at the appropriate application boundary.
- Treat browser-side validation as a usability feature, not a security boundary.
- Validate external API responses and handle timeouts, cancellation, rate limits, and expected failures.
- Use `IHttpClientFactory` or the application's existing typed-client approach for HTTP integrations.
- Never hardcode API keys, credentials, connection strings, or other secrets.
- Use transactions only when required by the consistency requirements; do not wrap every query in a transaction.

### 4. Dependency Injection and Architecture

- Use constructor injection for application dependencies.
- Follow built-in .NET dependency injection lifetime rules.
- Register EF Core DbContext instances and request-scoped services as `Scoped` unless the existing design requires otherwise.
- Register stateless singleton services only when their dependencies are safe for singleton use.
- Use `Transient` for lightweight services when their lifecycle is appropriate; do not assume all handlers must be transient.
- Never inject a scoped service directly into a singleton.
- Avoid service locator patterns and unnecessary direct use of `IServiceProvider`.
- Ensure new services, handlers, validators, and integrations are registered correctly using the project's existing registration approach.
- Avoid circular dependencies and duplicated registrations.
- Do not introduce new NuGet packages unless necessary. Check the target framework, package compatibility, licensing implications, and existing alternatives first.

### 5. MVC Controllers and HTTP Endpoints

Keep controllers thin and consistent with the application's existing conventions.

#### ApiService Controllers (API endpoints)

- **Inject `IMediator`** — Never inject services directly. Send commands and queries through MediatR.
- Receive and bind HTTP input.
- Perform appropriate request-level validation.
- Dispatch commands and queries through MediatR.
- Translate application results into appropriate HTTP responses.
- Avoid embedding database queries, complex business rules, or substantial calculation logic in controllers.

#### Web Controllers (MVC views)

- **Call Refit APIs directly** — The Web layer does NOT use CQRS. Controllers call Refit API clients (e.g., `INetWorthApi`) to communicate with the ApiService.
- Keep Web controllers thin — they handle HTTP concerns, call APIs, prepare view models, and return views.
- Never inject ApiService services directly into Web controllers.

#### Input Validation

- Check `ModelState.IsValid` at the beginning of actions that accept model-bound input and require validation.
- Prefer strongly typed request models and ViewModels with appropriate validation attributes.
- Validate identifiers, numeric ranges, date ranges, enum values, and cross-field constraints.
- Treat all client input as untrusted.
- Handle missing records and invalid operations explicitly.
- Never rely exclusively on client-side validation.

#### HTTP and Security

- Apply `[ValidateAntiForgeryToken]` to state-changing MVC form actions, including POST actions, and preserve the application's established anti-forgery strategy.
- Ensure unsafe cookie-authenticated requests are protected against CSRF.
- Use appropriate HTTP verbs and response codes.
- Use POST/Redirect/GET for successful form submissions when appropriate.
- Enforce authentication and authorization at the endpoint and resource level.
- Never assume that hiding a button or link provides authorization.
- Avoid exposing internal exception messages, stack traces, database details, or sensitive information to users.
- Do not disable security protections merely to make an integration or test pass.

### 6. Presentation Layer — Razor Views and ViewModels

#### Strongly Typed ViewModels

- Pass dedicated ViewModels to Razor Views rather than exposing EF Core entities directly.
- Keep input ViewModels separate from output ViewModels when their validation or data requirements differ.
- Include only the data needed by the view.
- Map application results into presentation models at the appropriate boundary.
- Avoid over-posting by explicitly controlling which fields can be changed by the client.

#### Razor Conventions

- Use strongly typed Razor Views with `@model`.
- Prefer Tag Helpers such as `asp-for`, `asp-action`, `asp-controller`, and `asp-validation-for`.
- Use shared layouts, partial views, and reusable components where they reduce duplication.
- Preserve existing routing, layout conventions, and responsive behavior.
- Encode untrusted output by default. Do not use `Html.Raw` with untrusted content.
- Provide validation summaries and field-level error messages where appropriate.
- Ensure empty, loading, error, and success states are handled when relevant.

#### UI State and Financial Calculations

- Keep displayed values synchronized with the current user inputs and server-calculated results.
- When projection parameters change, ensure all dependent totals, charts, summaries, tables, and explanatory text update consistently.
- Avoid stale state, partial updates, duplicated calculations, and race conditions.
- Keep authoritative financial calculations in the appropriate server-side application or domain layer. Client-side calculations may support immediate feedback but must not be the sole source of truth for persisted or consequential results.
- Define assumptions clearly for investment returns, inflation, taxes, contribution timing, and retirement projections.
- Format currency, percentages, dates, and large numbers consistently.
- Do not silently replace missing financial data with fabricated values or misleading defaults.

### 7. JavaScript and Front-End Interactivity

- Use modern, block-scoped JavaScript.
- Never introduce `var`; use `const` by default and `let` when reassignment is necessary.
- Follow the project's existing JavaScript organization and module conventions.
- Avoid unnecessary global variables, duplicated event handlers, inline scripts, and direct DOM manipulation when an established component pattern exists.
- Initialize interactive components safely and prevent duplicate event registration.
- Handle asynchronous operations, request failures, loading states, and cancellation where appropriate.
- Prevent stale responses from overwriting newer user selections.
- Validate and encode data appropriately when passing values between Razor and JavaScript.
- Do not insert untrusted content using unsafe HTML APIs.
- Ensure interactive features work with keyboard navigation and accessible labels.
- Preserve responsive layouts and existing visual design conventions.
- Remove obsolete JavaScript and event handlers when replacing existing behavior.

### 8. User Experience and Professional Design

Every user-facing feature must be complete, coherent, and consistent with the rest of the application.

- Follow the existing design system, typography, spacing, color palette, and component conventions.
- Use clear labels, concise instructions, meaningful button text, and understandable error messages.
- Provide appropriate empty, loading, success, and failure states.
- Disable or otherwise protect against duplicate submissions when necessary.
- Use confirmation steps for destructive or consequential operations.
- Preserve user input when recoverable validation or server errors occur.
- Make tables, forms, filters, charts, and navigation usable on desktop and mobile.
- Ensure sufficient color contrast and accessible interaction states.
- Avoid decorative functionality that does not serve a clear user need.
- Never use fake records, mock responses, placeholder buttons, or hardcoded success messages as substitutes for required production functionality.
- If a real external integration is required but cannot be completed because credentials or access are unavailable, implement the supported integration path and clearly document what remains blocked.

### 9. Testing and Verification

Do not consider a feature complete merely because the code compiles.

#### Automated Verification

- Add or update unit tests for business rules, calculations, handlers, and validation.
- Add integration tests for important database operations, endpoint behavior, and persistence where appropriate.
- Test boundary conditions, invalid inputs, missing records, duplicate submissions, and relevant failure scenarios.
- For financial calculations, test representative values, zero values, negative values where permitted, rounding behavior, and edge cases.
- Follow the existing test framework and conventions.
- Avoid weakening, deleting, or skipping existing tests just to achieve a passing result.

#### Build and Runtime Checks

- Build the relevant solution and projects using the repository's target framework and configuration.
- Run relevant automated tests.
- Review compiler warnings, test failures, and runtime errors introduced by the changes.
- Verify that migrations and model configuration are consistent.
- Check routing, dependency injection, validation, authorization, and view rendering for the affected feature.
- Verify client-side behavior for interactive changes when browser testing is available.
- Do not claim a test passed, a migration succeeded, or an integration works unless it was actually verified.
- If verification is blocked by missing services, credentials, environment configuration, or unavailable tooling, explain the exact limitation.

### 10. Regression Prevention and Code Quality

- Keep changes focused on the requested feature.
- Preserve existing behavior unless a change is necessary and justified.
- Avoid unrelated refactoring, broad renaming, unnecessary package upgrades, and sweeping configuration changes.
- Follow the existing naming conventions, formatting, nullable-reference-type settings, and analyzer rules.
- Remove dead code, unused variables, obsolete event handlers, and temporary debugging statements introduced by the implementation.
- Do not leave `TODO`, `FIXME`, placeholder implementations, or incomplete branches for required functionality.
- Never overwrite user changes or revert unrelated work.
- Review the final diff for accidental modifications, security risks, missing registrations, and incomplete integration.

### 11. Feature Completion Checklist

Before declaring a feature complete, verify every applicable item:

- [ ] Existing architecture and relevant instructions were inspected.
- [ ] The implementation plan and dependencies were considered.
- [ ] Database schema and migrations are correct when required.
- [ ] Application logic and business rules are implemented.
- [ ] Required handlers, services, and dependency registrations are present.
- [ ] Controllers and endpoints are integrated and secured.
- [ ] ViewModels and Razor Views are correctly connected.
- [ ] JavaScript interactions and UI state synchronization work.
- [ ] Validation, error handling, and authorization are implemented.
- [ ] No required functionality depends on fake data or placeholder behavior.
- [ ] Automated tests were added or updated where appropriate.
- [ ] Relevant builds and tests were executed.
- [ ] Existing functionality and unrelated changes were preserved.
- [ ] Remaining limitations are documented accurately.

### 12. Final Implementation Report

After completing the work, provide a concise report containing:

1. **Implemented:** What functionality was delivered.
2. **Files changed:** The important files created or modified.
3. **Database changes:** Migrations, schema changes, or data changes, if applicable.
4. **Verification:** The exact build and test commands executed and their actual results.
5. **Security and edge cases:** Important protections and failure scenarios addressed.
6. **Outstanding issues:** Any incomplete work, environmental blockers, or manual deployment steps.

Be direct and factual. Distinguish implemented functionality from functionality that has only been planned or partially verified.

## Version Management

### Update Version After Task Completion

After completing a feature, bug fix, or significant change, **always update the application version** in `PersonalFinance.Shared/Constants/AppVersion.cs` before declaring the task complete.

### Version File Location
The version is defined in:
```
PersonalFinance.Shared/Constants/AppVersion.cs
```

### Semantic Versioning Rules (SemVer 2.0.0)
The file uses semantic versioning with these components:
- **Major**: Breaking changes that are not backwards-compatible
- **Minor**: New features or functionality that are backwards-compatible
- **Patch**: Bug fixes that are backwards-compatible
- **Suffix**: Optional pre-release tag (e.g., "preview.1", "alpha", "beta", "rc.1")

### When to Bump Each Version Component
- **Bump Patch** for bug fixes, small improvements, or minor UI tweaks
- **Bump Minor** for new features, new endpoints, or significant enhancements
- **Bump Major** for breaking changes, API changes, or database schema changes that require migration
- **Set Suffix** when working on pre-release features; remove it for stable releases

### Version Update Procedure
1. Open `PersonalFinance.Shared/Constants/AppVersion.cs`
2. Increment the appropriate version component (`Major`, `Minor`, or `Patch`)
3. Update the `Current` const string to match the new version (e.g., `"1.4.2"`)
4. If bumping Minor, reset Patch to 0
5. If bumping Major, reset Minor and Patch to 0
6. Update or remove the `Suffix` as needed

### Example
After adding a new feature to version `1.4.1`:
```csharp
public const string Current = "1.5.0";
public const int Major = 1;
public const int Minor = 5;  // Bumped from 4
public const int Patch = 0;  // Reset to 0
public const string? Suffix = null;
```

After fixing a bug in version `1.4.1`:
```csharp
public const string Current = "1.4.2";
public const int Major = 1;
public const int Minor = 4;
public const int Patch = 2;  // Bumped from 1
public const string? Suffix = null;
```

## Changelog Management

### Update Changelog After Task Completion

After completing a feature, bug fix, or significant change, **always update the CHANGELOG** in `CHANGELOG.md` before
declaring the task complete. This ensures every change is documented and traceable.

### Changelog File Location

The changelog is located at:

```
CHANGELOG.md
```

### Format

The project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format. All new entries go under the
`## [Unreleased]` section using the following categories:

- **Added** — New features
- **Changed** — Changes in existing functionality
- **Deprecated** — Features soon to be removed
- **Removed** — Features removed in this version
- **Fixed** — Bug fixes
- **Security** — Security-related fixes

### Changelog Entry Rules

- **Always add an entry** — Every feature, bug fix, or significant change must have a changelog entry.
- **Use the `[Unreleased]` section** — Add entries under `## [Unreleased]` until a release is made.
- **Bold the feature or component name** — Start each entry with a bold label describing what changed, followed by a
  colon and a brief description.
- **Include implementation details as sub-bullets** — For complex changes, add sub-bullets explaining key implementation
  details.
- **Be concise but informative** — One sentence describing the change, with optional sub-bullets for details.
- **Match the category to the change type** — Use "Added" for new features, "Fixed" for bugs, "Changed" for
  modifications, etc.

### Changelog Entry Procedure

1. Open `CHANGELOG.md`
2. Locate the `## [Unreleased]` section at the top
3. Find or create the appropriate category (`### Added`, `### Fixed`, `### Changed`, etc.)
4. Add a new bullet point describing the change
5. Add sub-bullets if the change requires implementation details

### Example

After adding a new watchlist feature:

```markdown
## [Unreleased]

### Added

- **Watchlist Feature**: Users can now create and manage watchlists to track assets of interest.
  - Added watchlist entity, service, controller, and UI views.
  - Supports adding, removing, and reordering assets within a watchlist.
```

After fixing a bug:

```markdown
## [Unreleased]

### Fixed

- **Chart Rendering on Mobile**: Fixed charts not displaying correctly on small screen sizes.
  - Replaced fixed panel dimensions with viewport-relative constraints.
  - Added responsive breakpoints for tablet and mobile devices.
```

## Naming Conventions

### Classes & Interfaces
- **PascalCase**: For class names (e.g., `EntityService`)
- **Interface Prefix**: Use `I` prefix for interfaces (e.g., `IEntityService`)
- **Suffixes**: Use appropriate suffixes (`Service`, `Controller`, `Dto`, `ViewModel`)

### Methods & Properties
- **PascalCase**: For public methods and properties
- **camelCase**: For private fields and local variables
- **Async Suffix**: Append `Async` to async methods

### Files & Folders
- **Match Class Name**: File name should match class name
- **Layer Folders**: Organize by layer (Controllers, Services, Models, etc.)
- **Feature Folders**: For larger features, use feature-based organization

## JavaScript / ESLint

### Overview
All client-side JavaScript lives under `PersonalFinance.Web/wwwroot/js/`. The project enforces code quality via ESLint, configured in `wwwroot/.eslintrc.js`. Always follow the ESLint rules when writing or modifying JavaScript.

### Frontend Best Practices

**Modern Syntax:**
- Never use `var` for variable declarations. Always use `const` for block-scoped variables that will not be reassigned, and `let` only when reassignment is required.
- Use arrow functions for callbacks and short, stateless functions.
- Prefer template literals over string concatenation for readable string building.

**Code Cleanliness:**
- Maximize readability and reduce cognitive complexity by avoiding deeply nested loops and conditional blocks.
- Extract complex logic into named, reusable functions.
- Leverage modern array methods (`map`, `filter`, `reduce`, `find`) over traditional `for` loops where appropriate.
- Keep functions short and focused on a single responsibility.

```javascript
// Good: Using modern array methods
const activeUsers = users.filter(u => u.isActive).map(u => u.name);

// Bad: Nested for loops
const activeUsers = [];
for (var i = 0; i < users.length; i++) {  // Also uses var
    if (users[i].isActive) {
        activeUsers.push(users[i].name);
    }
}
```

**Asynchronous Code:**
- Prefer `async/await` syntax over raw Promises or callbacks for asynchronous operations to maintain clean, readable execution flows.
- Always wrap `await` calls in `try/catch` blocks for proper error handling.
- Never use `.then()` chains when `async/await` is available.

```javascript
// Good: async/await with error handling
async function loadData() {
    try {
        const response = await fetch('/api/data');
        const data = await response.json();
        return data;
    } catch (error) {
        console.warn('Failed to load data:', error);
        throw error;
    }
}

// Bad: Promise chains
function loadData() {
    return fetch('/api/data')
        .then(response => response.json());
}
```

**DOM Manipulation:**
- Ensure all frontend scripts safely interact with the DOM, keeping logic modular and decoupled from the `.NET MVC` markup views where possible.
- Always check for element existence before manipulation to avoid null reference errors.
- Use `textContent` instead of `innerHTML` when inserting user-supplied data to prevent XSS attacks.
- Wait for DOM readiness before manipulating elements using the standard DOM ready pattern.

```javascript
// Good: Safe DOM interaction with null check
const element = document.getElementById('myElement');
if (element) {
    element.textContent = safeText;
}

// Bad: Assuming element exists
document.getElementById('myElement').innerHTML = userInput;
```

### ESLint Configuration
The project extends `eslint:recommended` with the following key rules:

**Variables:**
- Use `let` or `const`, never `var` (`no-var`, `prefer-const`)
- No unused variables (`no-unused-vars`) — prefix with `_` to intentionally ignore
- No undefined variables (`no-undef`)

**Code Style:**
- **4-space indentation**
- **Single quotes** for strings
- **Semicolons** required at statement ends
- **No trailing commas** (`comma-dangle: never`)
- **4-space indent** for blocks
- **Object curly spacing**: always spaced (`{ key: value }`)
- **Array bracket spacing**: never spaced (`[item1, item2]`)
- **No multiple empty lines** (max 1 consecutive blank line)

**Best Practices:**
- **Always use curly braces** for all control structures (`curly: all`)
- **No `console`** statements in production (`no-console: warn`)
- **No `debugger`**, `eval`, or `with` statements
- **No throwing literals** — always throw `Error` objects
- **Require `await`** in async functions (`require-await`)
- **No Yoda conditions** — write `x === 5`, not `5 === x`
- **No unused expressions**

**Potential Bugs:**
- Always use `isNaN()` for NaN checks (`use-isnan`)
- Always use valid `typeof` values (`valid-typeof`)
- No unsafe optional chaining, negation, or finally blocks
- No loss of precision, sparse arrays, or regex spaces

### Module Pattern
Use **IIFE (Immediately Invoked Function Expression)** modules with `'use strict'` to avoid polluting the global namespace:

```javascript
(function () {
    'use strict';

    // Private variables and functions
    function helper() { /* ... */ }

    // Expose public API via a namespace object
    window.MyModule = {
        init: function () { /* ... */ },
        doSomething: function () { /* ... */ }
    };

})();
```

### JSDoc Comments
Document all public functions and modules with JSDoc comments:

```javascript
/**
 * Initialize the net worth projection charts.
 * @param {Object} data - The projection data from the server.
 * @returns {void}
 */
function renderChart(data) {
    // ...
}
```

### DOM Ready Pattern
Always wait for DOM readiness before manipulating elements:

```javascript
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
} else {
    init();
}
```

### Security
- **Never use `innerHTML`** with user-supplied data — use `textContent` instead to prevent XSS
- **Sanitize input** before rendering to the DOM
- **Use CSRF tokens** for all form submissions (Razor provides `@Html.AntiForgeryToken()`)
- **Avoid `eval()`** and `new Function()` — they are disabled by ESLint

```javascript
// Good: Safe text rendering
element.textContent = userInput;

// Bad: XSS vulnerability
element.innerHTML = userInput;
```

### Global Libraries
The following libraries are available as global variables (declared in ESLint globals):
- **jQuery** (`$`, `jQuery`) — DOM manipulation and AJAX
- **Chart.js** (`Chart`) — Chart rendering
- **DataTables** (`DataTable`) — Table enhancement
- **Bootstrap** (`bootstrap`) — UI components

### File Organization
Organize JavaScript files by feature under `wwwroot/js/`:

```
wwwroot/js/
├── core/          # Core utilities and site-wide scripts
├── networth/      # Net worth feature modules
├── google-drive/  # Google Drive integration
├── news/          # News feature
└── community.js   # Community feature
```

### References
- [ESLint Documentation](https://eslint.org/docs/latest/)
- [ESLint Rules](https://eslint.org/docs/latest/rules/)
- [JavaScript Best Practices](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide)

## References

### Project Documentation
- **`GLOSSARY.md`** — Canonical definitions for architecture and domain terms.
- **`docs/adr/`** — Architecture Decision Records explaining key design choices.

### External Resources
- [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices)
- [ASP.NET Core MVC Overview](https://learn.microsoft.com/en-us/aspnet/core/mvc/overview)
- [Common Web Application Architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [ESLint Documentation](https://eslint.org/docs/latest/)
- [JavaScript Best Practices](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide)
- [ESLint Rules Reference](https://eslint.org/docs/latest/rules/)
