# AGENTS.md - Personal Finance (.NET MVC)

Short, always-loaded rules for AI agents. The full detail lives in [`docs/CODING_STANDARDS.md`](docs/CODING_STANDARDS.md). Read it before implementing a feature or reviewing a change.

## Verify your work

- **C#:** `dotnet test PersonalFinance.sln`
- **JavaScript:** `npm run lint` (auto-fix: `npm run lint:fix`). Never call `eslint` directly: the repo pins ESLint 8 inside this script, and a bare `eslint` (v9+) cannot read `.eslintrc.js`.
- **E2E (Playwright):** needs a running database, Web app and ApiService. Run `npm run e2e:preflight` first. Setup is in `docs/DEVELOPMENT.md` under "Running the E2E tests locally".
- Never claim something works unless you ran the check. If a check is blocked, say exactly why.

## Read first

- [`GLOSSARY.md`](GLOSSARY.md) — canonical terms (Service, DTO, Entity) and domain concepts.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — request flow, identity flow, and the add-a-feature checklist.
- [`docs/adr/`](docs/adr/) — Architecture Decision Records.
- `PersonalFinance/tests/PersonalFinance.Tests/` — xUnit tests using `UseInMemoryDatabase`. Add tests here, following existing patterns (e.g. `DashboardServiceTests.cs`). InMemory runs queries Azure SQL can't: see step 8 of the add-a-feature checklist in `docs/ARCHITECTURE.md`.

## Where things live

All paths are under `PersonalFinance/src/` unless noted.

| What | Where |
|---|---|
| A Feature's API controller, services, validators, DI registration ([ADR 0007](docs/adr/0007-feature-folders-with-interface-only-dependencies.md)) | `PersonalFinance.ApiService/Features/<Feature>/` |
| What two or more Features share: the Data Source provider (ADR 0002), the projection and mapping helpers, auth, rate limiting, Notifications | `PersonalFinance.ApiService/Infrastructure/` |
| Web MVC: controllers, view models (named after the controller), views ([ADR 0008](docs/adr/0008-web-uses-classic-mvc-layout.md)) | `PersonalFinance.Web/Controllers/`, `Models/<Controller>/`, `Views/<Controller>/` |
| Web UI services and tag helpers | `PersonalFinance.Web/Services/`, `TagHelpers/` |
| Web host plumbing: the user-id forwarding handler, Refit client registration, Unavailable State, email | `PersonalFinance.Web/Infrastructure/` |
| Legal pages (privacy, terms, disclaimer, cookies, contact) | `PersonalFinance.Web/Controllers/LegalController.cs`, `Views/Legal/` |
| Footer, cookie banner, layout | `PersonalFinance.Web/Views/Shared/` (`_Layout`, `_CookieBanner`) |
| Registration + consent | `PersonalFinance.Web/Areas/Identity/Pages/Account/Register*`, `Services/UserConsentService.cs` |
| Client JavaScript (by feature) | `PersonalFinance.Web/wwwroot/js/{core,networth,budget,dashboard,...}` |
| Global CSS | `PersonalFinance.Web/wwwroot/css/site.css` |
| Entities, DbContext, migrations | `PersonalFinance.Data/{Entities,Migrations/AzureSql}` |
| DTOs, API contracts, `AppVersion` | `PersonalFinance.Shared/{DTOs,Contracts,Constants}` |
| E2E tests (repo root) | `e2e-tests/` |

**Searching:** exclude vendored assets, e.g. `PersonalFinance.Web/wwwroot/lib/` (Bootstrap, jQuery, Chart.js, ...) and `node_modules/`. They flood results.

## Architecture rules

- **Layers:** `Web` -> `ApiService` -> `Data`; `Shared` is referenced by all. `Data` references neither `Web` nor `ApiService`. `ApiService` has no MVC/Razor dependency.
- **Web controllers** stay thin: call Refit API clients (e.g. `INetWorthApi`), build view models, return views. They never hold business logic and never inject ApiService services.
- **ApiService controllers** inject service interfaces directly via DI. No MediatR, no mediator layer (ADR 0001).
- **Services** own business logic and EF Core data access. Controllers do validation via the Feature's `Validators/`, then delegate.
- Use constructor injection. No `new` for services. Register new services in `Program.cs`.
- Pass dedicated ViewModels to views, never EF entities.

## Data rules

- EF Core Code-First. Production is Azure SQL; local development may use SQL Server in Docker (`compose.yaml`, `npm run db:up`). Use only T-SQL and EF features both support. Never point local tooling (`dotnet ef`, e2e runs, user secrets) at production. Never hardcode secrets.
- User-owned entities inherit `AuditableEntity` and are **soft-deleted** (`IsDeleted`, `DeletedAt`). Never hard-delete them, except in the account Purge ([ADR 0005](docs/adr/0005-account-purge-hard-deletes-after-grace-period.md)).
- Money is `decimal` with explicit precision. Never `float`/`double`.
- Inspect every generated migration. Never reset, drop or apply production migrations without explicit authorization.
- Use async EF operations, `AsNoTracking()` for reads, deterministic ordering, and no N+1 queries.

## License policy

Only MIT, BSD (2/3-clause), Apache 2.0 and ISC dependencies ([ADR 0009](docs/adr/0009-adopt-d3-for-bespoke-interactive-visualisations.md)). Check the license before adding any NuGet/npm/libman package. **MediatR and AutoMapper are banned** (use direct DI and manual mapping). No GPL/AGPL/copyleft, no paid or dual-licensed packages.

## Security and validation

- `[ValidateAntiForgeryToken]` on state-changing MVC form actions. Enforce authorization at the endpoint.
- Treat all client input as untrusted; validate server-side.
- **Never `[Required]` on a `bool`** — it always passes. Add an explicit `ModelState` error instead (a test enforces this).
- Never expose exception details to users or log secrets/financial records.

## JavaScript

- `npm run lint` enforces syntax and style. Clear its warnings in every file you touch.
- Use IIFE modules with `'use strict'`; JSDoc public functions; wait for DOM ready; null-check elements.
- Files live under `wwwroot/js/<feature>/`.
- When toggling the `hidden` attribute, make sure no CSS `display` rule overrides it (add `.x[hidden] { display: none; }`).

## Done checklist (every feature or fix)

1. Tests added or updated, `dotnet test` and `npm run lint` run.
2. **Bump `AppVersion`** in `PersonalFinance.Shared/Constants/AppVersion.cs` (SemVer: patch = fix, minor = feature, major = breaking or schema migration). Update `Current`, `Major`, `Minor`, `Patch` together.
3. **Add a `CHANGELOG.md` section** `## [x.y.z] - YYYY-MM-DD` at the top for the new version (there is no `[Unreleased]`), with entries in the right category (Added, Changed, Fixed, ...). Start each with a bold label, then a colon and one sentence; use sub-bullets for details. After the deploy, CI publishes it as the GitHub Release `vx.y.z` ([ADR 0006](docs/adr/0006-one-release-per-merged-pr.md)); `release-check` fails the PR without the bump and section, unless it is docs-only or labelled `no-release`.
4. Keep the change focused: no unrelated refactoring, no leftover TODOs or debug code.
5. Final report: what was implemented, files changed, DB changes, exact commands run and results, outstanding issues.

## C# style

PascalCase for types and public members, camelCase for private fields and locals, `I` prefix for interfaces, `Async` suffix on async methods, suffixes `Service`/`Controller`/`Dto`/`ViewModel`. File name matches the class name.

Explicit types: write `var` only when the type is on the right-hand side (`new T(...)`, a cast, `.ToList()`, `T.Parse(...)`, an anonymous type). `IDE0008` fails the CI build otherwise; `dotnet format style PersonalFinance.sln` fixes a branch. The full rule set is the "C# Style" section of `docs/CODING_STANDARDS.md`.

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues and are tracked on the **PersonalFinance Roadmap** project board. See `docs/agents/issue-tracker.md`.

### Triage labels

Five canonical labels (needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one GLOSSARY.md and docs/adr/ at the repo root. See `docs/agents/domain.md`.
