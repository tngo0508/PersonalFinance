# Coding Standards - .NET Core MVC Best Practices

Detailed standards for this .NET Core MVC project, based on Microsoft's official best practices and architectural recommendations. The short, always-loaded rules live in [`AGENTS.md`](../AGENTS.md); this file holds the full detail and is read when implementing a feature or reviewing a change.

## Table of Contents
- [Quick Start](#quick-start)
- [Project Architecture](#project-architecture)
- [License Policy](#license-policy)
- [Backend/Frontend Code Separation](#backendfrontend-code-separation)
- [Feature Implementation Protocol](#feature-implementation-protocol)
- [Version Management](#version-management)
- [Changelog Management](#changelog-management)
- [Naming Conventions](#naming-conventions)
- [C# Style](#c-style)
- [JavaScript / ESLint](#javascript--eslint)
- [References](#references)

## Quick Start

Before working on the project, read these reference documents:

- **`GLOSSARY.md`** — Canonical definitions for architecture terms (Service, DTO, Entity) and domain concepts.
- **`docs/adr/`** — Architecture Decision Records explaining why key design choices were made.
- **`PersonalFinance/tests/PersonalFinance.Tests/`** — Test project using xUnit with `UseInMemoryDatabase`. New features
  should add tests here following existing patterns (e.g., `DashboardServiceTests.cs`).

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
- **Direct Service Injection**: ApiService controllers inject service interfaces directly via DI without intermediate mediator libraries. See `docs/adr/0001-standardize-cqrs-feature-organization.md`.
- **Feature Folders**: ApiService and Web are organized by Feature (`Features/<Feature>/`), with code that two or more Features share in `Infrastructure/`. A Feature depends on another only through its interface. See `docs/adr/0007-feature-folders-with-interface-only-dependencies.md`.
- **Soft Deletes**: All user-owned entities inherit from `AuditableEntity` with soft-delete support. EF Core global query filters automatically exclude deleted records.

## License Policy

### Permissive Open-Source Only
This project strictly allows only **permissive open-source dependencies** with licenses:
- **MIT**
- **BSD** (2-Clause / 3-Clause)
- **Apache 2.0**

### Prohibited Packages & Licenses
- **No Commercial / Paid Licenses**: Dependencies requiring paid commercial licenses, dual licensing, or subscription tiers are strictly prohibited.
- **Banned Packages**:
  - **MediatR** (Lucky Penny Software / commercial license) — Prohibited. Use direct service injection via built-in .NET DI instead.
  - **AutoMapper** (Lucky Penny Software / commercial license) — Prohibited. Use explicit manual mapping methods or extension methods instead.
- **No GPL / AGPL / Copyleft** dependencies that impose licensing viral requirements on the solution.

### Rules for Package Management
1. **Check License Before Adding**: Always verify the license of any NuGet package before adding it to any project in the solution.
2. **Remove Paid Packages**: If an existing dependency changes its license to require payment or restrictive terms, it must be removed and replaced with a permissive open-source alternative or built-in .NET capability.

## Backend/Frontend Code Separation

### Strict Project Boundary
**`PersonalFinance.ApiService`** contains all backend code — business logic, service implementations, and data access coordination.  
**`PersonalFinance.Web`** contains all frontend code — controllers, views, Razor components, and UI-specific services.

When creating or modifying code, always place it in the correct project:

| Code Type | Project | Examples |
|---|---|---|
| API controllers | `PersonalFinance.ApiService` | `Features/NetWorth/NetWorthController.cs` |
| Business logic services | `PersonalFinance.ApiService` | `Features/NetWorth/NetWorthParametersService.cs` |
| Validators | `PersonalFinance.ApiService` | `Features/NetWorth/Validators/NetWorthProjectionValidator.cs` |
| Code two or more ApiService Features share | `PersonalFinance.ApiService` | `Infrastructure/RateLimiterService.cs` |
| MVC controllers and view models (Web) | `PersonalFinance.Web` | `Features/NetWorth/NetworthController.cs`, `Features/NetWorth/NetWorthViewModel.cs` |
| Views (Razor) | `PersonalFinance.Web` | `Views/<Controller>/*.cshtml` files |
| Code two or more Web Features share | `PersonalFinance.Web` | `Infrastructure/UserIdForwardingHandler.cs` |
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

- Production uses **Microsoft Azure SQL Database**, accessed through Entity Framework Core.
- Local development may use SQL Server 2022 in Docker (`compose.yaml`, `npm run db:up`); a native SQL Server or LocalDB also works. It uses the same EF Core provider, `AzureSqlAppDbContext` and migrations as production; only the connection string differs.
- Use only T-SQL and EF Core features that both Azure SQL Database and SQL Server support, so code that works locally also works in production.
- Never point local tooling (`dotnet ef`, e2e runs, user secrets) at the production database.
- Use the existing EF Core Code-First or database-first approach. Do not introduce a conflicting database management strategy.
- Use the existing Azure SQL Database connection configuration and dependency injection setup.
- Do not create a new database or change the configured Azure SQL server, database name, authentication method, or hosting configuration unless explicitly required.
- Never hardcode database credentials, passwords, access tokens, or connection strings containing secrets.
- Store secrets in appropriate configuration providers, such as environment variables, Azure Key Vault, or the existing managed identity configuration.
- Prefer Microsoft Entra ID authentication and managed identities for Azure-hosted applications when supported by the existing deployment architecture.
- Grant database identities only the permissions required by the application.
- Keep development, testing, staging, and production database configurations separate.

#### Entity and Schema Design

- **User-owned entities inherit from `AuditableEntity`** — All entities with a `UserId` field inherit from `AuditableEntity`, which provides `Id`, `UserId`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, and `DeletedAt`.
- **Soft deletes are mandatory** — Never hard-delete user-owned entities. Set `IsDeleted = true` and `DeletedAt = DateTime.UtcNow`. EF Core global query filters automatically exclude soft-deleted records. The one exception is the account Purge ([ADR 0005](adr/0005-account-purge-hard-deletes-after-grace-period.md)).
- **`DeletionRequest` is the one exception to the `UserId` rule** — It is not an `AuditableEntity`, because it is never soft-deleted and is the audit record that outlives the Purge (ADR 0005). The Purge clears its `UserId`.
- **System-defined entities** — Entities without `UserId` (e.g., `CommunityPoll`, `CommunityPollOption`) do NOT inherit from `AuditableEntity`.
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

### 3. Application Layer — Direct Service Injection and Feature Folders

**All ApiService features use Direct Service Injection.** Controllers inject service interfaces directly through built-in .NET dependency injection. There is no mediator indirection. See `docs/adr/0001-standardize-cqrs-feature-organization.md` for the rationale.

#### Feature Folder Structure

ApiService and Web are organized by Feature (`docs/adr/0007-feature-folders-with-interface-only-dependencies.md`). Each Feature in GLOSSARY.md owns `Features/<Feature>/`, under the same name used in `Shared/DTOs/` and the test project. The Feedback Feature is the exemplar:

```
ApiService/
  Features/
    Feedback/
      FeedbackController.cs          ← API controller, injects IFeedbackService
      FeedbackFeature.cs             ← AddFeedbackFeature(), called from Program.cs
      IFeedbackService.cs            ← Service interface, beside its implementation
      FeedbackService.cs             ← Business logic and data access (internal sealed)
      SubmitFeedbackResult.cs
      Validators/
        SubmitFeedbackRequestValidator.cs
  Infrastructure/                    ← Only what two or more Features share, or what serves the host
    RateLimiterService.cs
    NotificationService.cs
    ...
Web/
  Features/
    Feedback/
      FeedbackController.cs          ← MVC controller, calls the Refit client
      FeedbackViewModel.cs
  Infrastructure/
    UserIdForwardingHandler.cs
  Views/
    Feedback/                        ← Razor views stay where Razor's convention finds them
```

A Feature folder has a fixed anatomy:

- **At the root:** the controller, `<Feature>Feature.cs` with `Add<Feature>Feature()`, and each service interface beside its implementation.
- **Subfolders, only when needed:** `Domain/` (pure logic with no EF Core or HTTP dependency), `Validators/`, and `Adapters/` (clients for Google Drive, OpenAI and other external systems). There is no `Interfaces/` folder.
- **Visibility marks the seam:** a Feature's public types are its service interfaces, its controller and its DTOs in `Shared`. Every other type is `internal sealed`. A Feature depends on another Feature only through that Feature's interface or `AppDbContext`.
- **Namespaces follow folders:** `PersonalFinance.ApiService.Features.Feedback`, `PersonalFinance.ApiService.Features.Feedback.Validators`, `PersonalFinance.Web.Features.Feedback`, `PersonalFinance.ApiService.Infrastructure`.

`tests/PersonalFinance.Tests/Architecture/FeatureLayoutTests.cs` enforces the namespace, folder and cross-Feature rules, and `ApiServiceCompositionTests.cs` checks that every interface and Validator is registered. A new Feature is added to the `Features` list there and to GLOSSARY.md.

#### Service Responsibilities

- **Services own business logic and data access** — Services contain domain rules, database operations via EF Core, validation coordination, and transaction management.
- Keep business logic out of MVC and API controllers.
- Inject required dependencies (`ApplicationDbContext`, HTTP clients, etc.) through constructors.
- Keep services focused on clear domain boundaries.
- Use asynchronous operations and propagate cancellation tokens.
- Return explicit, predictable results for success, validation failures, missing records, and conflicts.
- Avoid swallowing exceptions or returning success when an operation failed.
- Do not add unnecessary mediator or CQRS abstractions.

#### Validation and External Dependencies

- Validate inputs at the appropriate application boundary using dedicated validators in the Feature's `Validators/` folder.
- Treat browser-side validation as a usability feature, not a security boundary.
- Validate external API responses and handle timeouts, cancellation, rate limits, and expected failures.
- Use `IHttpClientFactory` or the application's existing typed-client approach for HTTP integrations.
- Never hardcode API keys, credentials, connection strings, or other secrets.
- Use transactions only when required by consistency requirements; do not wrap every query in a transaction.

### 4. Dependency Injection and Architecture

- Use constructor injection for application dependencies.
- Follow built-in .NET dependency injection lifetime rules.
- Register EF Core DbContext instances and request-scoped services as `Scoped` unless the existing design requires otherwise.
- Register stateless singleton services only when their dependencies are safe for singleton use.
- Use `Transient` for lightweight services when their lifecycle is appropriate.
- Never inject a scoped service directly into a singleton.
- Avoid service locator patterns and unnecessary direct use of `IServiceProvider`.
- Ensure new services, validators, and integrations are registered correctly using the project's existing registration approach.
- Avoid circular dependencies and duplicated registrations.
- Strictly adhere to the License Policy — only MIT, BSD, or Apache 2.0 packages allowed. Check licenses before introducing any package.

### 5. MVC Controllers and HTTP Endpoints

Keep controllers thin and consistent with the application's existing conventions.

#### ApiService Controllers (API endpoints)

- **Inject Service Interfaces Directly** — Controllers inject service interfaces (e.g., `INetWorthParametersService`, `IBudgetService`) directly via constructor injection.
- Receive and bind HTTP input.
- Perform appropriate request-level validation using the Feature's validators in `Features/<Feature>/Validators/`.
- Delegate operations to injected domain services.
- Translate service results into appropriate HTTP responses (`Ok`, `BadRequest`, `NotFound`, `NoContent`).
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
- Never put `[Required]` on a `bool` — it is always satisfied, so an unchecked consent box passes. Enforce acceptance with an explicit `ModelState.AddModelError` check (`ModelValidationConventionTests` fails the build if this is violated).
- When showing/hiding elements with the HTML `hidden` attribute, make sure no author CSS sets `display` on the element without a matching `[hidden] { display: none; }` rule (see `.cookie-banner[hidden]` in `site.css`).

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
- A new or changed EF Core query that uses string, date, or collation-dependent operations has an Azure SQL translation guard: build it through the service's `IQueryable`-returning method and pass it to `AzureSqlTranslation.AssertTranslates` (`tests/PersonalFinance.Tests/TestSupport/`). The InMemory provider runs untranslatable queries in memory, so service tests alone pass queries that fail on Azure SQL.
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

The project follows the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format with one difference: there is no
`## [Unreleased]` section. Every merged PR is a release of its own
([ADR 0006](adr/0006-one-release-per-merged-pr.md)), so each PR adds a `## [x.y.z] - YYYY-MM-DD` section for the version
it bumps `AppVersion` to, dated the day you write it. After the merge is deployed and smoke-tested, CI publishes that
section as the GitHub Release `vx.y.z`. Entries use these categories:

- **Added** — New features
- **Changed** — Changes in existing functionality
- **Deprecated** — Features soon to be removed
- **Removed** — Features removed in this version
- **Fixed** — Bug fixes
- **Security** — Security-related fixes

### Changelog Entry Rules

- **Always add an entry** — Every feature, bug fix, or significant change must have a changelog entry.
- **One section per version** — Add a `## [x.y.z] - YYYY-MM-DD` section matching `AppVersion.Current`; never write
  under an older version's section. The `release-check` workflow fails the PR otherwise (docs-only PRs and PRs labelled
  `no-release` are exempt).
- **Bold the feature or component name** — Start each entry with a bold label describing what changed, followed by a
  colon and a brief description.
- **Include implementation details as sub-bullets** — For complex changes, add sub-bullets explaining key implementation
  details.
- **Be concise but informative** — One sentence describing the change, with optional sub-bullets for details.
- **Match the category to the change type** — Use "Added" for new features, "Fixed" for bugs, "Changed" for
  modifications, etc.

### Changelog Entry Procedure

1. Open `CHANGELOG.md`
2. Add a `## [x.y.z] - YYYY-MM-DD` heading for the new `AppVersion.Current` above the newest version section
3. Find or create the appropriate category (`### Added`, `### Fixed`, `### Changed`, etc.)
4. Add a new bullet point describing the change
5. Add sub-bullets if the change requires implementation details

### Example

After adding a new watchlist feature:

```markdown
## [4.3.0] - 2026-11-02

### Added

- **Watchlist Feature**: Users can now create and manage watchlists to track assets of interest.
  - Added watchlist entity, service, controller, and UI views.
  - Supports adding, removing, and reordering assets within a watchlist.
```

After fixing a bug:

```markdown
## [4.3.1] - 2026-11-03

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

## C# Style

These rules follow Microsoft's C# coding conventions and the `dotnet/runtime` coding style. `.editorconfig` enforces
them through `EnforceCodeStyleInBuild`: a rule at `warning` severity is a squiggle while you develop and a failed build
in CI, where warnings are errors. Before opening a PR, run `dotnet format style PersonalFinance.sln` and
`dotnet format whitespace PersonalFinance.sln` to fix a branch mechanically.

| Rule | Enforced by |
|---|---|
| Explicit types; `var` only when the type is on the right-hand side | `IDE0008` (warning) |
| File-scoped namespaces | `IDE0161` (warning) |
| `internal sealed` unless something needs the type public | `CA1852` (arrives with the ADR 0007 PRs) |
| One job per method, within the complexity ceiling | `CA1502`, `CA1505` (arrive with the ADR 0007 PRs) |
| Money is `decimal` | Code review; see Data Layer rules |

### Explicit types (IDE0007 / IDE0008)

Write the type unless it already appears on the right-hand side of the `=`. `var` is allowed for `new T(...)`, casts,
`as`, conversion methods named after their result (`.ToList()`, `XDocument.Load(...)`, `int.Parse(...)`), and
anonymous types, which require it. The analyzer decides what counts as apparent; if the build is green, the code is
within the rule. It is fine to write `var` while developing; `dotnet format style` rewrites it before the PR.

```csharp
// Allowed: the type is on the line
var service = new FeedbackService(context, limiter);
var rows = results.Select(r => new { r.Id, r.Name });

// Not allowed: the reader has to look elsewhere for the type
var inbox = await _feedbackApi.GetInboxAsync(ct);   // IReadOnlyList<FeedbackAdminItemDto> inbox = ...
foreach (var goal in goals)                         // foreach (SavingsGoal goal in goals)
var count = 0;                                      // int count = 0;
```

### Visibility and sealing

- A class is `internal sealed` unless something needs it public: a service interface, a controller (MVC discovery
  needs `public`), a DTO or an entity. `InternalsVisibleTo("PersonalFinance.Tests")` lets tests see internals.
- An interface lives in the same folder as the class that implements it. There is no `Interfaces/` folder
  ([ADR 0007](adr/0007-feature-folders-with-interface-only-dependencies.md)).

### Method shape

- One job per method, and the name says what it does. A public method should read as a list of steps whose private
  helpers carry the detail.
- Guard clauses and early returns instead of nested `if`. Use `ArgumentNullException.ThrowIfNull(x)` and
  `ArgumentException.ThrowIfNullOrWhiteSpace(s)` at the top of a method.
- No `#region`. If a class needs regions to be navigable, it needs splitting.

### Async and time

- `Async` suffix on every async method; accept and pass a `CancellationToken` wherever the surrounding code does.
- New code reads the clock through the injected `TimeProvider`, never `DateTime.UtcNow` or `DateTime.Now`, so tests
  can use `FakeTimeProvider`.

### DTOs and records

- Existing DTOs stay classes. A new DTO may be a `sealed record` with `init` properties when nothing model-binds it.
- No primary constructors for now: the solution has none, and mixing the two styles costs more than it saves.

### References

- [C# coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [.NET runtime C# coding style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)
- [Code-style rule options (`.editorconfig`)](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/code-style-rule-options)

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

The full rule set lives in `PersonalFinance.Web/wwwroot/.eslintrc.js`. Read it when you need to check a specific rule.

**Run ESLint:**

- **All platforms:** `npm run lint` (checks all files) or `npm run lint:fix` (auto-fixes formatting).

**Pre-commit hook:** The project uses Husky with lint-staged to auto-fix ESLint errors on staged JavaScript files before each commit. No manual linting is required before committing — the hook runs automatically. To bypass the hook in emergencies, use `git commit --no-verify`.

**Key rules to follow manually:**

- **4-space indentation**, single quotes, semicolons required
- **Space before function parens**: `function name ()` not `function name()`
- **Object curly spacing**: always spaced (`{ key: value }`)
- **Always use curly braces** for all control structures
- **No trailing commas**

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
