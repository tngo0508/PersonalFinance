# 0001 – Standardize ApiService on CQRS with Feature-Based Organization

**Date:** 2026-10-16  
**Status:** Accepted

## Context

The ApiService layer uses two inconsistent patterns for handling requests:

1. **CQRS with MediatR** — Used by Budget, Community, SavingsGoals, Watchlist, and SpreadsheetAnalysis. Commands and queries live in `Features/[Feature]/`, handlers coordinate business logic, and controllers inject `IMediator`.

2. **Direct service calls** — Used by NetWorth, GoogleDrive, and AI Assistant. Controllers inject service interfaces directly and call methods without MediatR.

Additionally, code is organized by **type** (Controllers/, Services/, Features/, Validators/) rather than by **feature**, which means implementing a new feature requires touching 4+ folders across the project.

This inconsistency creates confusion for developers and AI agents: when adding a new feature, it's unclear which pattern to follow. The mixed patterns also make refactoring harder and increase cognitive load.

## Decision

Standardize the ApiService layer on these patterns:

### 1. All features use CQRS with MediatR

Every feature in the ApiService will use commands and queries sent through MediatR. Controllers will inject `IMediator` and send intent-bearing objects, never call services directly.

**Migration path:** Incremental, one feature at a time:
- AI Assistant first (simplest, single endpoint)
- NetWorth second (moderate complexity)
- GoogleDrive last (most complex, OAuth flow)

### 2. Feature-based folder organization

Reorganize the ApiService by feature instead of by type:

```
ApiService/
  NetWorth/
    NetWorthController.cs
    Commands/
      SaveParametersCommand.cs
      DeleteParametersCommand.cs
    Queries/
      LoadParametersQuery.cs
    Services/
      NetWorthParametersService.cs
    Validators/
      NetWorthValidators.cs
  Budget/
  Community/
  ...
```

Namespace convention: `PersonalFinance.ApiService.[Feature].Commands`, `PersonalFinance.ApiService.[Feature].Services`, etc.

### 3. Handlers are thin coordinators

Handlers receive commands/queries, call the appropriate service(s), map results, and return responses. Business logic remains in services, not handlers. This minimizes migration risk and preserves existing tested code.

### 4. DTOs organized by feature within Shared

DTOs in `PersonalFinance.Shared/DTOs/` will be organized by feature subfolders:

```
Shared/DTOs/
  NetWorth/
    NetWorthParametersDto.cs
    NetWorthProjectionDto.cs
  Community/
    CommunityDto.cs
  ...
```

### 5. AuditableEntity base class for user-owned entities

All entities with a `UserId` field will inherit from `AuditableEntity`, which provides:
- `Id` (int), `UserId` (string)
- `CreatedAt` (DateTime), `UpdatedAt` (DateTime?)
- `IsDeleted` (bool), `DeletedAt` (DateTime?)

EF Core global query filters will automatically exclude soft-deleted records from normal queries.

### 6. Web layer remains simple

The Web MVC layer will NOT adopt CQRS. Web controllers are already thin — they call Refit APIs and return views. Adding MediatR at the UI layer adds unnecessary complexity.

## Consequences

**Positive:**
- **Predictability**: New developers and AI agents know exactly where to put code.
- **Consistency**: One pattern across all features reduces cognitive load.
- **Testability**: Handlers are isolated units; services are already tested.
- **Safety**: Soft deletes prevent accidental data loss; audit fields provide traceability.
- **Discoverability**: Feature folders keep related code together.

**Negative:**
- **Migration effort**: 3 features need to be migrated to CQRS, and all features need folder reorganization.
- **Indirection**: Adding MediatR to simple CRUD operations adds a layer of indirection.
- **Learning curve**: New contributors need to understand CQRS patterns.

**Mitigation:**
- Incremental migration reduces risk by verifying each feature independently.
- Handlers are thin coordinators, so the indirection is minimal.
- GLOSSARY.md and this ADR document the patterns for onboarding.

## Related Decisions

- GLOSSARY.md — Canonical definitions for architecture and domain terms.
- Future ADR may address whether to keep the two-service split (Web + ApiService) or consolidate into a single process.
