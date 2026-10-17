# Glossary

Canonical definitions for the Personal Finance application domain and architecture.

## Architecture

### Feature
A vertical slice of functionality spanning entities, domain services, controllers, API contracts, DTOs, and views. Code is organized by type in the ApiService (`Controllers/`, `Services/`, `Validators/`) and Web layer, with feature-specific DTOs organized under `PersonalFinance.Shared/DTOs/`.

### Service
A class that owns business logic and data access coordination for a feature. Injected directly into controllers via dependency injection, containing domain rules, validation, and database operations. Services live in `PersonalFinance.ApiService/Services/`.

### Validator
A class that provides validation logic for requests and inputs. Validators live in `PersonalFinance.ApiService/Validators/`.

### API Contract
A Refit interface that defines the HTTP API between the Web layer and the ApiService layer. Contracts live in `PersonalFinance.Shared/Contracts/` and are shared by both layers. Examples: `INetWorthApi`, `ICommunityApi`.

### DTO
Data Transfer Object used to exchange data across layer boundaries. DTOs live in `PersonalFinance.Shared/DTOs/`, organized by feature subfolders (e.g., `DTOs/NetWorth/`, `DTOs/Community/`). DTOs are plain data carriers with no business logic.

### Entity
A domain object persisted to the database via EF Core. Entities live in `PersonalFinance.Data/Entities/`. User-owned entities inherit from `AuditableEntity` and include audit fields and soft-delete support.

### AuditableEntity
Abstract base class for user-owned entities. Provides: `Id` (int), `UserId` (string), `CreatedAt` (DateTime), `UpdatedAt` (DateTime?), `IsDeleted` (bool), and `DeletedAt` (DateTime?). All entities with a `UserId` inherit from this class.

### Soft Delete
A pattern where entities are marked as deleted (`IsDeleted = true`) instead of being removed from the database. EF Core global query filters automatically exclude soft-deleted records from normal queries. Use `.IgnoreQueryFilters()` to include them.

## Layers

### Web Layer
`PersonalFinance.Web` — the MVC frontend. Contains controllers, views, Razor pages, and UI-specific services. Calls the ApiService via Refit HTTP clients. Controllers call APIs directly.

### ApiService Layer
`PersonalFinance.ApiService` — the REST API backend. Contains controllers, services, and validators organized by type in `Controllers/`, `Services/`, and `Validators/`. Uses direct service injection via ASP.NET Core dependency injection.

### Data Layer
`PersonalFinance.Data` — data access. Contains entities, DbContext, and database configuration. Independent of UI and API concerns.

### Shared Layer
`PersonalFinance.Shared` — cross-cutting types. Contains DTOs (organized by feature), API contracts, constants, and shared helpers. Referenced by all layers.

## Domain

### Net Worth
The core financial tracking feature. Projects financial independence from user-entered parameters: a starting balance, annual income, spending and savings, current and retirement age, and three return-rate scenarios (Base, Conservative, Aggressive). The user's net worth is the starting balance they enter; it is not computed from individual assets and liabilities.

### FI Target
The portfolio value at which a user is considered financially independent: annual spending × 25 (the 4% rule).

### Progress to FI
Net worth as a percentage of the FI Target.

### Savings Rate
Annual savings as a percentage of annual income.

### Landing Page
The public page served at `/` to signed-out visitors. It explains the product and leads to registration or sign-in.
_Avoid_: Home page (ambiguous with Dashboard)

### Dashboard
The signed-in user's overview page, summarizing net worth, budget, savings goals, and recent activity. Signed-in visitors to `/` are redirected here.
_Avoid_: Home page, home

### Budget
Monthly budget allocation and tracking. Compares planned spending against actual spending from spreadsheet data.

### Community
Social features including polls, stories, comments, and likes. System-defined polls are shared across users; user-generated content (stories, comments) is user-owned.

### Google Drive Integration
Connection to Google Drive for spreadsheet synchronization. Manages OAuth connections, folder exploration, and file caching.

### Savings Goals
Target savings goals tracking with progress monitoring.

### Watchlist
Item tracking and article watchlist for financial news and interests.

### Spreadsheet Analysis
Transaction and budget data analysis from Google Sheets. Provides dashboard views and sync operations.

### AI Assistant
AI-powered financial advice via OpenAI chat integration.
