---
sessionId: session-261016-062012-1xz5
---

# Project Architecture

Your solution (`PersonalFinance.sln`) is a **.NET 10 + Aspire** Clean Architecture solution with 7 projects:

| Project | Responsibility | Key Contents |
|---|---|---|
| `PersonalFinance.Data` | Data access layer | EF Core entities, `AppDbContext`, Azure SQL migrations |
| `PersonalFinance.Shared` | Cross-project contracts | DTOs, Refit API interfaces, shared helpers |
| `PersonalFinance.ApiService` | Backend API | Service interfaces, service implementations, API controllers, DI setup in `Program.cs` |
| `PersonalFinance.Web` | Frontend MVC | MVC controllers, Razor Views, ViewModels, JavaScript, CSS |
| `PersonalFinance.ServiceDefaults` | Aspire telemetry | Shared health checks, HTTP resilience |
| `PersonalFinance.AppHost` | Local orchestrator | Aspire local dev orchestration |
| `PersonalFinance.Tests` | xUnit tests | Unit and integration tests |

**Dependency flow:**
```
Web  ──depends on──>  ApiService  ──depends on──>  Data
   │                         │                         ▲
   └──── depends on ─────────┘                         │
        Shared  ──referenced by all layers─────────────┘
```

**Communication pattern:** Web talks to ApiService via **Refit** HTTP clients (not direct DI). Each Refit contract (e.g., `INetWorthApi`) maps to an API controller in ApiService (e.g., `NetWorthController`).

# Delivery Steps

###   Step 1: 1. Data Layer — Entity, DbContext, and Migration
Start at the bottom. Define your database model and schema before anything else depends on it.

###   Step 2: 2. Shared Layer — DTOs and Refit Contract
Define the contracts that bridge Web and ApiService. Create request/response DTOs and the Refit interface.

###   Step 3: 3. ApiService Layer — Interface, Service, Controller, and DI
Implement the backend business logic. Create the service interface, implementation, API controller, and register in Program.cs.

###   Step 4: 4. Web Layer — MVC Controller, ViewModel, View, and JavaScript
Build the frontend. Create the MVC controller calling the Refit client, the ViewModel with validation, the Razor View, and any JavaScript modules.

###   Step 5: 5. Tests and Version Bump
Add tests for the new feature and update the application version in AppVersion.cs.