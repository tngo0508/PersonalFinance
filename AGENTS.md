# AGENTS.md - Personal Finance (.NET MVC)

Short, always-loaded rules for AI agents. The full detail lives in [`docs/CODING_STANDARDS.md`](docs/CODING_STANDARDS.md). Read it before implementing a feature or reviewing a change.

## Verify your work

- **C#:** `dotnet test PersonalFinance.sln`
- **JavaScript:** `npm run lint` (auto-fix: `npm run lint:fix`). Never call `eslint` directly: the repo pins ESLint 8 inside this script, and a bare `eslint` (v9+) cannot read `.eslintrc.js`.
- **E2E (Playwright):** needs a running database, Web app and ApiService. Run `npm run e2e:preflight` first. Setup is in `docs/DEVELOPMENT.md` under "Running the E2E tests locally".
- Never claim something works unless you ran the check. If a check is blocked, say exactly why.

## Read first

- [`GLOSSARY.md`](GLOSSARY.md) — canonical terms (Service, DTO, Entity) and domain concepts.
- [`docs/adr/`](docs/adr/) — Architecture Decision Records.
- `PersonalFinance/tests/PersonalFinance.Tests/` — xUnit tests using `UseInMemoryDatabase`. Add tests here, following existing patterns (e.g. `DashboardServiceTests.cs`).

## Where things live

All paths are under `PersonalFinance/src/` unless noted.

| What | Where |
|---|---|
| API controllers, business services, validators | `PersonalFinance.ApiService/{Controllers,Services,Validators}` |
| MVC controllers, Razor views, UI services | `PersonalFinance.Web/{Controllers,Views,Services}` |
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
- **Services** own business logic and EF Core data access. Controllers do validation via `Validators/`, then delegate.
- Use constructor injection. No `new` for services. Register new services in `Program.cs`.
- Pass dedicated ViewModels to views, never EF entities.

## Data rules

- Azure SQL via EF Core Code-First. Never hardcode secrets; never assume a local SQL Server is the target.
- User-owned entities inherit `AuditableEntity` and are **soft-deleted** (`IsDeleted`, `DeletedAt`). Never hard-delete them.
- Money is `decimal` with explicit precision. Never `float`/`double`.
- Inspect every generated migration. Never reset, drop or apply production migrations without explicit authorization.
- Use async EF operations, `AsNoTracking()` for reads, deterministic ordering, and no N+1 queries.

## License policy

Only MIT, BSD (2/3-clause) and Apache 2.0 dependencies. Check the license before adding any NuGet/npm package. **MediatR and AutoMapper are banned** (use direct DI and manual mapping). No GPL/AGPL/copyleft, no paid or dual-licensed packages.

## Security and validation

- `[ValidateAntiForgeryToken]` on state-changing MVC form actions. Enforce authorization at the endpoint.
- Treat all client input as untrusted; validate server-side.
- **Never `[Required]` on a `bool`** — it always passes. Add an explicit `ModelState` error instead (a test enforces this).
- Never expose exception details to users or log secrets/financial records.

## JavaScript

- Modern JS only: `const`/`let`, never `var`; `async/await` with `try/catch`; no `.then()` chains.
- 4-space indent, single quotes, semicolons, no trailing commas, `function name ()` spacing, always use curly braces.
- Use IIFE modules with `'use strict'`; JSDoc public functions; wait for DOM ready; null-check elements.
- Use `textContent`, never `innerHTML`, for user data. No `eval`.
- Files live under `wwwroot/js/<feature>/`.
- When toggling the `hidden` attribute, make sure no CSS `display` rule overrides it (add `.x[hidden] { display: none; }`).

## Done checklist (every feature or fix)

1. Tests added or updated, `dotnet test` and `npm run lint` run.
2. **Bump `AppVersion`** in `PersonalFinance.Shared/Constants/AppVersion.cs` (SemVer: patch = fix, minor = feature, major = breaking or schema migration). Update `Current`, `Major`, `Minor`, `Patch` together.
3. **Add a `CHANGELOG.md` entry** under `## [Unreleased]` in the right category (Added, Changed, Fixed, ...). Start with a bold label, then a colon and one sentence; use sub-bullets for details.
4. Keep the change focused: no unrelated refactoring, no leftover TODOs or debug code.
5. Final report: what was implemented, files changed, DB changes, exact commands run and results, outstanding issues.

## Naming

PascalCase for types and public members, camelCase for private fields and locals, `I` prefix for interfaces, `Async` suffix on async methods, suffixes `Service`/`Controller`/`Dto`/`ViewModel`. File name matches the class name.

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues and are tracked on the **PersonalFinance Roadmap** project board. See `docs/agents/issue-tracker.md`.

### Triage labels

Five canonical labels (needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one GLOSSARY.md and docs/adr/ at the repo root. See `docs/agents/domain.md`.
