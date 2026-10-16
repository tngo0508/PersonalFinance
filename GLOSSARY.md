# Glossary

Canonical definitions for the Personal Finance application domain and architecture.

## Architecture

### Feature
A vertical slice of functionality that spans all layers: entity, service, commands/queries, handlers, controller, API contract, DTOs, and views. Features are organized in dedicated folders (e.g., `NetWorth/`, `Budget/`, `Community/`). Each feature is self-contained and can be developed, tested, and migrated independently.

### Command
An intent-bearing object that represents an action to be performed (e.g., `SaveParametersCommand`, `DeleteConnectionCommand`). Commands are sent through MediatR and handled by a dedicated handler. Commands live in `Features/[Feature]/Commands/`.

### Query
An intent-bearing object that represents a request for data (e.g., `LoadParametersQuery`, `GetConnectionsQuery`). Queries are sent through MediatR and handled by a dedicated handler. Queries live in `Features/[Feature]/Queries/`.

### Handler
A class that processes a command or query. Handlers are thin coordinators: they receive the request, call the appropriate service(s), map the result, and return the response. Business logic lives in services, not handlers. Handlers live alongside their commands/queries in `Features/[Feature]/`.

### Service
A class that owns business logic and data access coordination for a feature. Services are injected into handlers and contain the domain rules, validation, and database operations. Services live in `Features/[Feature]/Services/`.

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
`PersonalFinance.Web` — the MVC frontend. Contains controllers, views, Razor pages, and UI-specific services. Calls the ApiService via Refit HTTP clients. Does NOT use CQRS; controllers call APIs directly.

### ApiService Layer
`PersonalFinance.ApiService` — the REST API backend. Contains feature folders with commands, queries, handlers, and services. Uses CQRS with MediatR for all features. Organized by feature, not by type.

### Data Layer
`PersonalFinance.Data` — data access. Contains entities, DbContext, and database configuration. Independent of UI and API concerns.

### Shared Layer
`PersonalFinance.Shared` — cross-cutting types. Contains DTOs (organized by feature), API contracts, constants, and shared helpers. Referenced by all layers.

## Domain

### Net Worth
The core financial tracking feature. Tracks assets, liabilities, and projects financial independence based on user-defined parameters (current net worth, monthly savings, investment return rate, inflation rate).

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
