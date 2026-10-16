---
name: dotnet-feature-workflow
description: Implement and verify features or bug fixes in this .NET 10 Clean Architecture and Aspire solution. Use when changing Web, ApiService, Data, Shared, ServiceDefaults, AppHost, or the test project.
---

# .NET Feature Workflow

Use this skill for application changes that cross one or more solution projects.

## Repository boundaries

- `PersonalFinance.Web` owns MVC controllers, Razor views, Identity UI, view models, and UI-specific services.
- `PersonalFinance.ApiService` owns API endpoints, business logic, external-service coordination, and application
  services.
- `PersonalFinance.Data` owns EF Core entities, `AppDbContext`, migrations, and persistence configuration.
- `PersonalFinance.Shared` owns DTOs, Refit contracts, parsers, shared helpers, and cross-project contracts.
- `PersonalFinance.ServiceDefaults` owns shared Aspire telemetry, health checks, and HTTP resilience.
- `PersonalFinance.AppHost` is the local Aspire orchestrator; it is not production application logic.
- `PersonalFinance.Tests` contains xUnit unit and integration tests.

Keep controllers thin, use constructor injection, and preserve the dependency direction. Do not move business logic into
`PersonalFinance.Web` or UI concerns into `PersonalFinance.ApiService`.

## Worktree usage

Feature development should be done in the `dev` worktree located at `.worktrees/dev/`. The main repository stays on
`main` as a stable reference. For urgent production fixes, use the `hotfix` worktree at `.worktrees/hotfix/`.

## Required workflow

1. Work in the appropriate worktree (`dev` for features, `hotfix` for urgent fixes).
2. Inspect the relevant project files, registrations, contracts, existing feature pattern, and tests before editing.
3. Reuse existing services, DTOs, Refit interfaces, validators, and persistence patterns instead of creating duplicates.
4. Implement the smallest complete vertical slice, including DI registration, endpoint/controller integration, UI
   wiring, and error handling where applicable.
5. Use asynchronous APIs and pass cancellation tokens when the surrounding code supports them.
6. Add or update focused tests for changed behavior, including invalid input, missing records, and boundary cases.
7. Run the narrowest relevant tests first, then build or run the broader solution checks when the change warrants it.
8. Review the final changes for accidental edits, security issues, stale JavaScript handlers, and incomplete
   integration.
9. Update the application version in `PersonalFinance.Shared/Constants/AppVersion.cs` and add a changelog entry
   to `CHANGELOG.md` under the `[Unreleased]` section.

Never leave placeholder behavior, fake records, disabled tests, or unverified claims of success.