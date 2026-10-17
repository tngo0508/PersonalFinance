# Remove MediatR Dependency & Restructure ApiService

## Problem Statement

The project's ADR-0001 mandates CQRS with MediatR as the standard architectural pattern for the ApiService layer, but:

1. **MediatR now requires a paid license** (Lucky Penny Software changed from MIT to a commercial license). The project policy is strictly permissive open-source licenses only (MIT, BSD, Apache 2.0).
2. **AutoMapper has the same licensing concern** and must be avoided.
3. **The CQRS pattern was never actually implemented** — the codebase uses direct service injection. The ADR is aspirational and contradicts reality.
4. **The feature-based folder organization** (NetWorth/, Budget/, Community/) adds unnecessary nesting for a direct-service pattern. Type-based organization (Controllers/, Services/, Validators/) is simpler and more appropriate.

The documentation (ADR-0001, GLOSSARY.md, AGENTS.md) is inconsistent with the actual codebase and risks future developers or AI agents introducing licensed dependencies.

## Solution

Align the project documentation and structure with the actual architecture:

1. **Rewrite ADR-0001** to explicitly reject MediatR and CQRS, document the actual pattern (direct service injection), and add the license constraint as a guiding principle.
2. **Update GLOSSARY.md** to remove CQRS-specific terms (Command, Query, Handler) and simplify the ApiService layer description.
3. **Add a license policy to AGENTS.md** requiring strictly permissive open-source licenses (MIT, BSD, Apache 2.0) and explicitly banning MediatR and AutoMapper.
4. **Restructure ApiService folders** from feature-based to type-based organization.
5. **Update CHANGELOG.md** to document the architectural decision.

## User Stories

1. As a developer, I want the ADR to accurately describe the actual architecture, so that I don't waste time implementing a pattern that was never used.
2. As a developer, I want a clear license policy in AGENTS.md, so that I (and AI agents) never accidentally introduce a package with a paid license.
3. As a developer, I want the GLOSSARY to reflect the actual terms used in the codebase, so that I can onboard without confusion between aspirational and real patterns.
4. As a developer, I want the ApiService to use type-based folder organization, so that I can find all controllers, services, or validators in one place.
5. As a project maintainer, I want MediatR explicitly rejected in documentation, so that future refactoring discussions don't revisit a decided-against dependency.
6. As a project maintainer, I want AutoMapper explicitly rejected in documentation, so that mapping is done manually or with a license-free alternative.
7. As an AI agent working on this codebase, I want the license policy in AGENTS.md, so that I can enforce it before suggesting or adding NuGet packages.
8. As a code reviewer, I want the ADR to explain why CQRS was rejected, so that I understand the rationale if someone proposes it again.
9. As a developer, I want the folder restructuring to preserve all existing functionality, so that no features break during the reorganization.
10. As a developer, I want namespaces to be updated consistently after restructuring, so that the solution compiles without errors.

## Implementation Decisions

### ADR-0001 Rewrite

- Change the title and content to reflect the actual decision: reject MediatR, use direct service injection.
- Add the license constraint (MIT/BSD/Apache 2.0 only) as a core principle.
- Document why CQRS was rejected: MediatR licensing, unnecessary indirection for the project's scale, direct service injection is simpler and equally testable.
- Document the actual pattern: controllers inject service interfaces directly via DI, services contain business logic and data access, validators provide static validation rules.
- Update the folder organization section to describe type-based structure.
- Keep the AuditableEntity and soft-delete decisions, which are already implemented and correct.
- Keep the Web layer simplicity decision (no CQRS at Web layer), which remains valid.

### GLOSSARY.md Updates

- **Remove** the `Command` entry — no longer used.
- **Remove** the `Query` entry — no longer used.
- **Remove** the `Handler` entry — no longer used.
- **Update** the `ApiService Layer` entry to describe direct service injection instead of CQRS with MediatR.
- **Update** the `Feature` entry to reflect type-based organization instead of feature-based folders.
- Keep all other entries (Service, DTO, Entity, AuditableEntity, Soft Delete, domain terms) as they remain accurate.

### AGENTS.md License Policy

- Add a new section "License Policy" under the appropriate heading.
- State the requirement: only MIT, BSD, or Apache 2.0 licensed packages.
- Explicitly list banned packages: MediatR, AutoMapper.
- Add a rule: before adding a NuGet package, check its license.
- Add a rule: if a package changes its license to require payment, remove it and find an alternative.

### Folder Restructuring

Move files from feature-based to type-based organization:

**Current structure:**
```
ApiService/
  NetWorth/
    NetWorthController.cs
    Services/INetWorthParametersService.cs
    Services/NetWorthParametersService.cs
    Validators/NetWorthProjectionValidator.cs
  Budget/
    ...
  Community/
    ...
```

**Target structure:**
```
ApiService/
  Controllers/
    NetWorthController.cs
    BudgetController.cs
    CommunityController.cs
    ...
  Services/
    INetWorthParametersService.cs
    NetWorthParametersService.cs
    BudgetService.cs
    ...
  Validators/
    NetWorthProjectionValidator.cs
    ...
```

- Update all namespaces to match the new folder structure.
- Update all `using` statements in files that reference moved types.
- Update Program.cs service registrations if namespaces change.
- Ensure the solution compiles cleanly after restructuring.

### CHANGELOG.md Entry

- Add entry under `## [Unreleased]` → `### Changed`.
- Describe the architectural alignment: removed aspirational CQRS/MediatR references, documented actual direct service injection pattern, added license policy.

## Testing Decisions

### What Makes a Good Test

- **Build integrity**: The solution must compile with zero errors after every change. This is the primary verification for restructuring.
- **Behavioral equivalence**: No functional change is expected. API endpoints must return identical responses before and after restructuring.
- **Documentation consistency**: All three documents (ADR-0001, GLOSSARY.md, AGENTS.md) must agree on the architecture — no orphaned CQRS references.

### Modules to Verify

1. **ApiService build**: After restructuring, `dotnet build` must succeed with no errors.
2. **Namespace consistency**: All `using` statements must resolve correctly.
3. **Documentation cross-check**: Search for remaining references to "MediatR", "CQRS", "Command", "Query", "Handler" in documentation files to ensure they're removed or properly contextualized.

### Prior Art

- The project has no existing unit tests for the restructuring task, as it's a documentation and organization change.
- The build command (`dotnet build PersonalFinance.sln`) is the standard verification used in the project.

## Out of Scope

- **Actual code refactoring beyond folder moves**: No changes to service logic, controller behavior, or validation rules.
- **Adding new features**: This is purely an alignment task.
- **Database migrations**: No schema changes.
- **Web layer changes**: The Web layer is unaffected.
- **Removing existing packages**: MediatR and AutoMapper are not currently referenced, so no package removal is needed.
- **Implementing an alternative mediator**: No replacement for MediatR is needed since it was never used.

## Further Notes

- The ADR-0001 was created on 2026-10-16 and was aspirational from the start — the CQRS pattern was documented but never implemented. This rewrite corrects the record.
- The license policy is motivated by MediatR's license change from MIT to a commercial "Fair Code" license, and AutoMapper's similar concerns. This is a proactive guardrail.
- The folder restructuring is the only code change. All other changes are documentation-only.
- After restructuring, the AGENTS.md Backend/Frontend Code Separation section may need updates to reflect type-based organization instead of feature-based.
