# 0007 – Feature Folders in ApiService and Web, with Interface-Only Dependencies Between Features

**Date:** 2026-10-22  
**Status:** Accepted (supersedes ADR 0001 §3); §1 and §5 superseded for Web by [ADR 0008](0008-web-uses-classic-mvc-layout.md)

## Context

ADR 0001 §3 organized ApiService by type (`Controllers/`, `Services/`, `Validators/`). By 4.2.1 `Services/` is one
flat folder of 64 files mixing interfaces, implementations, result types, exceptions, parsers and Google helpers.
`Shared/DTOs/` and the test project are already grouped by Feature; ApiService and Web are not. Following one Feature
end to end means opening seven or eight folders: the Feedback Feature alone is 23 files in 12 folders across four
projects. The project is expected to gain contributors, human and agent, who must be able to find and change a Feature
by rule rather than by memory.

The architecture review of 2026-10-22 also found that the hardest code to read is not misplaced but oversized: the
largest ApiService methods are 116 to 437 lines. A layout change does not fix that, so this decision pairs the layout
with an enforced complexity ceiling.

## Decision

1. **ApiService and Web are organized by Feature.** Each Feature (GLOSSARY.md) owns `Features/<Feature>/` in both
   projects: NetWorth, BudgetPlanner, BudgetTracker, Analysis, GoogleDrive, SavingsGoals, Community, Watchlist,
   AiAssistant, Feedback, Admin, AccountLifecycle, Search and Dashboard, plus three Web-only Features added when the
   Web move (#160) found pages that fit none of these: LandingPage, Legal (including Consent) and InvestmentCalculator.
   The same name is used in `Shared/DTOs/` and in the test project. `Infrastructure/` holds only code that two or
   more Features use (authentication handler, rate limiter, culture, DI helpers), or that serves the host rather than
   any Feature (the Identity area's email sender, authorization and cookie handlers); code used by one Feature lives
   in that Feature. `Data` and `Shared` keep their current layout.
2. **A Feature folder has a fixed anatomy.** At its root: the controller, `<Feature>Feature.cs` with
   `Add<Feature>Feature()`, and each service interface beside its implementation. Subfolders come only from a fixed
   set and exist only when needed: `Domain/` (pure logic with no EF Core or HTTP dependency), `Validators/`, and
   `Adapters/` (clients for Google Drive, OpenAI and other external systems). There is no `Interfaces/` folder.
3. **The seam is marked by visibility, not by folder.** A Feature's public types are its service interfaces, its
   controller and its DTOs in `Shared`. Every other type in the Feature is `internal sealed`. A Feature depends on
   another Feature only through that Feature's public interface or through `AppDbContext`; it never references another
   Feature's implementation types.
4. **Each Feature registers its own services.** `Program.cs` calls one `Add<Feature>Feature()` per Feature. Validators
   remain registered by FluentValidation's assembly scan.
5. **Web moves its C#, not its views.** Controllers, view models (`Models/`) and UI services move into
   `Features/<Feature>/`. `Views/<Controller>/` stays where Razor's convention finds it.
6. **The rules are enforced by tests and analyzers, not by review.** Three reflection-based xUnit tests check that every
   type in ApiService and Web lives in a `Features.<Feature>` or `Infrastructure` namespace, that each file's namespace
   matches its folder, and that constructor parameters crossing from one Feature into another are interfaces. Code
   metrics `CA1502` and `CA1505` run at warning severity (an error in CI) with thresholds in `CodeMetricsConfig.txt`,
   and `CA1852` enforces sealing. Methods that exceed the ceiling today are suppressed inline with a justification
   naming their follow-up; new code cannot regress.
7. **Everything else stays.** Four projects (ADR 0001, AGENTS.md), direct service injection (ADR 0001 §1), MVC
   controllers, DTOs as classes, and the Data Source provider (ADR 0002).

## Considered Options

- **Clean Architecture as projects (Domain, Application, Infrastructure):** rejected for now. Its one hard guarantee,
  the dependency direction, is already enforced by project references (`Web` references only `Shared`; `Data`
  references only `Shared`). Adding the projects would mean an abstraction over `AppDbContext`, which the EF Core team
  advises against, about ten more files per Feature, and a single move of every file in the solution. Revisit if a
  second host comes to share the application core or an EF-free domain grows large; the layer names used inside a
  Feature (`Domain/`, `Adapters/`) make that move a relocation rather than a redesign.
- **Keep type folders and only split the large classes:** rejected. It leaves tracing a Feature as a seven-folder walk.
- **A separate `Interfaces/` folder:** rejected. It is type-based sorting one level down. The one legitimate case,
  Refit contracts consumed by Web and implemented by generated code, already lives in `Shared/Contracts/`.
- **An architecture-test library (NetArchTest, MIT; ArchUnitNET, Apache 2.0):** rejected. Three rules do not justify a
  dependency, and the test project already has reflection-based convention tests to copy.
- **Razor views inside Feature folders:** rejected. It needs a custom view location expander and makes every
  `return View()` a thing to debug.
- **Minimal APIs:** rejected. The controllers are thin, validated and route-tested; a rewrite moves no logic.
- **`[Unreleased]`-style `no-release` label for the move PRs:** rejected. A refactor still deploys, and ADR 0006 says a
  Release means "this version is live".

## Consequences

- ADR 0001 §3 is superseded. Its §1, §2, §4 and §5 are unchanged.
- About 160 files in ApiService and Web change namespace. The move is done in mechanical PRs with no logic change, each
  a patch release: ApiService first, then Web, then the PR template and documentation sweep. Class-splitting from the
  2026-10-22 review follows in separate PRs.
- Making validators `internal` silently unregisters them unless `AddValidatorsFromAssemblyContaining<Program>()` is
  given `includeInternalTypes: true`. Controllers must stay `public` for MVC discovery.
- Applying rule 1 during the move (#159, #160): Notifications is raised by several Features and read from every page,
  so it lives in `Infrastructure/` with its controller. Controllers keep the names their routes and view folders come
  from, so the Landing Page's controller is `HomeController` and the Watchlist's news feed is `NewsController`;
  renaming them would change URLs and move views, which rule 5 rules out.
- Dashboard, Search and AI Assistant are Features that read other Features' data; rule 3 allows that through
  `AppDbContext` or the other Feature's interface. Helpers that several Features share, such as the Data Source
  provider, move to `Infrastructure/` under rule 1; their behaviour and ADR 0002 are unaffected.
- The complexity ceiling freezes today's largest methods behind suppressions; each suppression is a visible to-do.
- The Feedback Feature is the exemplar that `docs/ARCHITECTURE.md` points new contributors at. AGENTS.md's "Where
  things live" table, `docs/ARCHITECTURE.md` and the GLOSSARY.md `Service` entry change with the ApiService PR.
- `CODEOWNERS` is deferred until a second owner exists; a PR template carrying the Done checklist is added now.

## Related Decisions

- ADR 0001 – Direct service injection and the license policy (§3 superseded by this ADR).
- ADR 0002 – Budget Tracker data read through a source-selected provider.
- ADR 0006 – One Release per merged PR.
