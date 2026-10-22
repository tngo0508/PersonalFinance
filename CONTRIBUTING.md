# Contributing

This guide gets you from a fresh clone to a merged pull request. Deeper material lives in:

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md): how the projects fit together and where new code goes.
- [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md): secrets, EF Core migrations, OAuth, e2e tests, troubleshooting.
- [`docs/CODING_STANDARDS.md`](docs/CODING_STANDARDS.md): full coding rules.
- [`GLOSSARY.md`](GLOSSARY.md) and [`docs/adr/`](docs/adr/): domain terms and past decisions.

## 1. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0): the version pinned in [`global.json`](global.json) (10.0.402 or a later 10.0.4xx patch). CI uses the same file.
- [Node.js](https://nodejs.org/) 20+ (ESLint, pre-commit hook, Playwright e2e tests)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) for the local SQL Server database, or the in-memory mode below to try the app without one
- An IDE that reads `.editorconfig`: Rider, Visual Studio, or VS Code with C# Dev Kit
- Optional: `dotnet tool install --global dotnet-ef` for migrations

## 2. Clone and configure

```bash
git clone https://github.com/tngo0508/PersonalFinance.git
cd PersonalFinance
npm install            # installs the Husky pre-commit hook and Playwright

# Local SQL Server in Docker: copy .env.example to .env and set MSSQL_SA_PASSWORD first
npm run db:up

# Minimum secrets to start the app (see docs/DEVELOPMENT.md section 3 for every key)
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' '<connection string>' --project PersonalFinance/src/PersonalFinance.Web
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' '<connection string>' --project PersonalFinance/src/PersonalFinance.ApiService
```

The local container's connection string, the password rules and the Apple Silicon setting are in "Local SQL Server
(Docker)" in `docs/DEVELOPMENT.md`. A dev Azure SQL database is an alternative documented there too.

**No database yet?** Use `InMemory:Dev` as the connection string for both projects. This is the mode CI uses for the
Playwright e2e run (`DatabaseProviderHelper` detects the `InMemory:` prefix). Data is lost on restart, and Web and
ApiService each keep their own store, so it is for exploring the UI, not for testing data flows end to end.

Never commit secrets. Google OAuth, Brevo, Google Drive and OpenAI keys are only needed for the features that use them;
see `docs/DEVELOPMENT.md`.

## 3. Run

```bash
dotnet run --project PersonalFinance/src/PersonalFinance.AppHost
```

Aspire starts Web (`https://localhost:7200`), ApiService (`https://localhost:7100`, API docs at `/scalar/v1`) and prints
the dashboard URL.

## 4. Verify your change

Run these before every push; CI runs the same checks.

```bash
dotnet build PersonalFinance.sln     # analyzers report warnings; do not add new ones
dotnet test PersonalFinance.sln      # xUnit, EF Core InMemory, no database needed
npm run lint                         # ESLint 8 for wwwroot/js (auto-fix: npm run lint:fix)
```

UI changes should also pass the Playwright e2e tests: `npm run e2e:preflight`, then follow "Running the E2E tests
locally" in `docs/DEVELOPMENT.md`.

Code style is defined in `.editorconfig` and checked during build. To fix formatting:
`dotnet format PersonalFinance.sln`.

## 5. Where things go

| Change | Location |
|---|---|
| Business logic, EF Core queries | `PersonalFinance.ApiService/Services/` |
| Request validation | `PersonalFinance.ApiService/Validators/` (FluentValidation) |
| REST endpoint | `PersonalFinance.ApiService/Controllers/` |
| DTOs and Refit Contracts | `PersonalFinance.Shared/DTOs/<Feature>/`, `PersonalFinance.Shared/Contracts/` |
| Entities and migrations | `PersonalFinance.Data/Entities/`, `PersonalFinance.Data/Migrations/AzureSql/` |
| Pages, view models, JavaScript | `PersonalFinance.Web/{Controllers,Models,Views}`, `wwwroot/js/<feature>/` |
| Tests | `PersonalFinance/tests/PersonalFinance.Tests/<Feature>/` |

The step-by-step feature checklist is in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md#adding-a-feature-checklist).

## 6. Pull request checklist

1. Branch from `main` (`feat/...`, `fix/...`, `chore/...`, `docs/...`).
2. Add or update tests; `dotnet test` and `npm run lint` pass.
3. Bump the version in `PersonalFinance/src/PersonalFinance.Shared/Constants/AppVersion.cs`: update `Current`,
   `Major`, `Minor` and `Patch` together (patch = fix, minor = feature, major = breaking change or schema migration).
4. Add a `CHANGELOG.md` entry under `## [Unreleased]` in the right category (Added, Changed, Fixed, ...): a bold label,
   a colon, one sentence, and sub-bullets for details.
5. Keep the change focused: no unrelated refactoring, leftover TODOs or debug code.
6. Use [Conventional Commits](https://www.conventionalcommits.org/) for commit messages (`feat:`, `fix:`, `docs:`,
   `refactor:`, `test:`, `chore:`).
7. In the PR description, list what changed, any database changes, and the exact commands you ran with their results.

## Non-negotiables

- Only MIT, BSD or Apache 2.0 dependencies. MediatR and AutoMapper are banned ([ADR 0001](docs/adr/0001-standardize-cqrs-feature-organization.md)).
- Money is `decimal`, never `float` or `double`.
- User-owned Entities are soft-deleted, never hard-deleted.
- Never `[Required]` on a `bool`; add an explicit `ModelState` error instead.
- `[ValidateAntiForgeryToken]` on state-changing MVC actions; authorize every endpoint.
- Never expose exception details to users or log secrets or financial records.
