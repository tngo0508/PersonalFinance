# 0001 – Standardize ApiService on Direct Service Injection and Permissive Open-Source Licensing

**Date:** 2026-10-16  
**Status:** Accepted

## Context

The architecture previously contemplated standardizing the ApiService layer on CQRS with MediatR. However, key factors led to revisiting and rejecting that approach:

1. **Licensing constraints** — MediatR and AutoMapper transitioned to commercial licensing models (Lucky Penny Software / paid licensing). The project enforces a strict policy that all dependencies must use permissive open-source licenses (MIT, BSD, Apache 2.0).
2. **Unnecessary indirection** — MediatR and CQRS command/query handlers introduce unnecessary layers of indirection and boilerplate for the project's scale without clear benefits.
3. **Architecture reality** — The codebase never actually adopted CQRS or MediatR; all controllers inject domain service interfaces directly through ASP.NET Core dependency injection.
4. **Folder layout** — Type-based organization (`Controllers/`, `Services/`, `Validators/`) provides a clean, discoverable structure for a direct-service architecture without excessive nested subdirectories.

## Decision

Standardize the ApiService layer on these patterns:

### 1. Reject MediatR and CQRS in favor of Direct Service Injection

All ApiService features use direct service injection via ASP.NET Core built-in dependency injection:
- API controllers inject service interfaces directly (e.g., `INetWorthParametersService`, `IBudgetService`).
- Services contain business logic, domain rules, and data access coordination.
- Controllers remain thin coordinators responsible for request binding, parameter validation, and mapping responses.
- No mediator library or CQRS indirection is used.

### 2. Permissive Open-Source License Policy (MIT, BSD, Apache 2.0 Only)

The project strictly prohibits dependencies with paid, commercial, or copyleft licenses. Specifically:
- **MediatR** is banned due to commercial licensing changes; built-in .NET dependency injection is used instead.
- **AutoMapper** is banned due to commercial licensing changes; manual mapping methods and extensions are used instead.
- All new NuGet packages must be verified for MIT, BSD, or Apache 2.0 compliance before adoption.

### 3. Type-Based Folder Organization

The ApiService project is organized by type in top-level folders:
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
    IBudgetService.cs
    BudgetService.cs
    ...
  Validators/
    NetWorthProjectionValidator.cs
    ...
```

Namespace conventions:
- `PersonalFinance.ApiService.Controllers`
- `PersonalFinance.ApiService.Services`
- `PersonalFinance.ApiService.Validators`

### 4. DTOs Organized by Feature within Shared

Data transfer objects in `PersonalFinance.Shared/DTOs/` remain organized by feature subfolders (`DTOs/NetWorth/`, `DTOs/Community/`, etc.) for clear cross-project contracts between ApiService and Web.

### 5. AuditableEntity Base Class for User-Owned Entities

All entities with a `UserId` field inherit from `AuditableEntity`, which provides:
- `Id` (int), `UserId` (string)
- `CreatedAt` (DateTime), `UpdatedAt` (DateTime?)
- `IsDeleted` (bool), `DeletedAt` (DateTime?)

EF Core global query filters automatically exclude soft-deleted records from normal queries.

### 6. Web Layer Simplicity

The Web MVC layer remains simple. Controllers call Refit API contracts (e.g., `INetWorthApi`) to communicate with ApiService. No mediator or CQRS pattern is introduced in the UI layer.

## Consequences

**Positive:**
- **Zero licensing risk**: Guarantees no hidden licensing costs or legal compliance issues from third-party libraries.
- **Simplicity and transparency**: Direct service injection is idiomatic .NET, easy to navigate, debug, and test.
- **Codebase consistency**: Project documentation now accurately mirrors the actual implementation.
- **Fast onboarding**: Developers and AI agents follow standard .NET dependency injection without bespoke mediator layers.

**Negative:**
- Reorganizing files into type-based folders requires updating namespaces and using statements across ApiService.

## Related Decisions

- GLOSSARY.md — Canonical definitions for architecture and domain terms.
- AGENTS.md — Agent guidelines and license policy enforcement.
