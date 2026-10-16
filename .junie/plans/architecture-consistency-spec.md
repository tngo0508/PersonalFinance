---
sessionId: session-261016-062843-1obq
---

# Spec

## Problem Statement

The ApiService layer of the Personal Finance application uses two inconsistent patterns for handling requests: some features (Budget, Community, SavingsGoals, Watchlist, SpreadsheetAnalysis) use CQRS with MediatR, while others (NetWorth, GoogleDrive, AI Assistant) use direct service calls from controllers. Additionally, code is organized by type (Controllers/, Services/, Features/, Validators/) rather than by feature, which means implementing a new feature requires touching 4+ folders across the project.

This inconsistency creates confusion for developers and AI agents: when adding a new feature, it's unclear which pattern to follow. The mixed patterns make refactoring harder, increase cognitive load, and make the codebase difficult to navigate and maintain. There is no AuditableEntity base class for user-owned entities, so soft deletes and audit fields are not available for financial data protection.

## Solution

Standardize the ApiService layer on a single, consistent architectural pattern:

1. **All features use CQRS with MediatR** — every feature follows the same command/query/handler pattern, with no exceptions.
2. **Feature-based folder organization** — each feature lives in its own folder with all related code co-located (controller, commands, queries, handlers, services, validators).
3. **Handlers are thin coordinators** — business logic remains in services; handlers delegate to services.
4. **AuditableEntity base class** — all user-owned entities inherit from AuditableEntity, providing soft deletes and audit fields.
5. **DTOs organized by feature** — DTOs in Shared are organized by feature subfolders for better navigation.
6. **Web layer remains simple** — no CQRS in the Web layer; controllers call Refit APIs directly.

The migration will be incremental: one feature at a time, starting with the simplest (AI Assistant), then NetWorth, then GoogleDrive. Existing features already using CQRS (Budget, Community, etc.) will be reorganized into feature folders.

## User Stories

1. As a developer, I want all ApiService features to follow the same CQRS pattern, so that I can predict where code belongs without checking multiple examples.

2. As a developer, I want feature-related code to be co-located in feature folders, so that I can find all code for a feature in one place instead of searching across multiple type-based folders.

3. As an AI agent, I want consistent patterns across all features, so that I can implement new features correctly without confusion about which pattern to use.

4. As a developer, I want handlers to be thin coordinators that delegate to services, so that business logic stays in tested service classes and migration risk is minimized.

5. As a user, I want soft deletes for my financial data, so that I can recover from accidental deletions.

6. As a developer, I want audit fields (CreatedAt, UpdatedAt) on all user-owned entities, so that I can trace when data was created or modified.

7. As a developer, I want DTOs organized by feature subfolders, so that I can quickly find the DTOs for a specific feature as the project grows.

8. As a developer, I want the Web layer to remain simple (no CQRS), so that the UI layer doesn't have unnecessary complexity from patterns that provide no benefit at the UI boundary.

9. As a developer, I want incremental migration of features, so that each feature can be tested and verified independently, reducing the risk of breaking multiple features at once.

10. As a developer, I want EF Core global query filters for soft deletes, so that I don't have to remember to filter soft-deleted records on every query.

11. As a maintainer, I want GLOSSARY.md and ADRs to document architectural decisions, so that new developers and AI agents can onboard without asking questions.

12. As a developer, I want namespace conventions to follow feature-based organization, so that namespaces clearly indicate which feature a class belongs to.

13. As a developer, I want the migration to start with the simplest feature (AI Assistant), so that I can build confidence in the pattern before tackling more complex features.

14. As a developer, I want existing API contracts in Shared to remain unchanged during migration, so that the Web layer continues working without modification during the transition.

15. As a user, I want system-defined entities (e.g., CommunityPoll) to remain simple without audit fields, so that the database isn't cluttered with unnecessary columns for shared data.

## Implementation Decisions

- **Migration order**: AI Assistant first (simplest, 2 endpoints), then NetWorth (moderate complexity, 4 endpoints), then GoogleDrive last (most complex, OAuth flow and many endpoints). Existing CQRS features (Budget, Community, SavingsGoals, Watchlist, SpreadsheetAnalysis) will be reorganized into feature folders.

- **Feature folder structure**: Each feature in ApiService will have a dedicated folder containing the API controller, Commands/ subfolder, Queries/ subfolder, Services/ subfolder, and Validators/ subfolder. The controller moves from `Controllers/` into the feature folder.

- **Handler responsibility**: Handlers receive commands or queries, call the appropriate service, map the result, and return the response. No business logic in handlers. Services retain their current business logic and data access code.

- **AuditableEntity**: Abstract base class providing Id (int), UserId (string), CreatedAt (DateTime), UpdatedAt (DateTime?), IsDeleted (bool), and DeletedAt (DateTime?). All entities with a UserId field inherit from this class. System-defined entities without UserId (CommunityPoll, CommunityPollOption) do not inherit.

- **Soft deletes**: Instead of hard deleting, user-owned entities are marked with IsDeleted = true and DeletedAt = DateTime.UtcNow. EF Core global query filters in AppDbContext.OnModelCreating() automatically exclude soft-deleted records from normal queries. Use .IgnoreQueryFilters() to include them.

- **DTO organization**: DTOs in PersonalFinance.Shared/DTOs/ will be moved into feature subfolders (e.g., DTOs/NetWorth/, DTOs/Community/, DTOs/GoogleDrive/). API contracts in Shared/Contracts/ remain flat.

- **Namespace convention**: `PersonalFinance.ApiService.[Feature].Commands`, `PersonalFinance.ApiService.[Feature].Queries`, `PersonalFinance.ApiService.[Feature].Services`, `PersonalFinance.ApiService.[Feature].Validators`.

- **Web layer unchanged**: Web MVC controllers continue to call Refit APIs directly. No CQRS adoption in the Web layer. No service injection from ApiService into Web controllers.

- **Backward compatibility**: API contracts (Refit interfaces) and DTOs remain unchanged in their public surface. The Web layer is not affected by the migration as API endpoints and response shapes stay the same.

- **Validation behavior**: The existing ValidationBehavior (FluentValidation pipeline behavior for MediatR) continues to be used for all commands. Features migrating to CQRS will get validators created or migrated.

- **Services retain interfaces**: Existing service interfaces (e.g., INetWorthParametersService, IAiAssistantService) remain in place. Handlers inject these interfaces. No removal of service interfaces.

- **X-User-Id header pattern unchanged**: The existing authentication pattern (X-User-Id header forwarded from Web to ApiService) remains in place. Controllers extract UserId from the header and pass it to commands/queries.

## Testing Decisions

- **Test external behavior, not implementation details**: Tests will validate that API endpoints return correct responses and services produce correct results. Tests will not verify internal handler implementation details.

- **Two-layer testing approach**:
  - **Service-layer unit tests**: Unit tests targeting services to verify business logic still works correctly after CQRS migration. These tests provide fast feedback and isolate business rules from the HTTP pipeline. Services are the highest seam where business logic lives.
  - **API integration tests**: Integration tests hitting API controllers via HTTP to verify the full request pipeline including MediatR dispatch, handler coordination, and service delegation. These tests validate that the CQRS migration didn't break the end-to-end flow.

- **Build verification after each migration step**: After each feature is migrated, the full solution will be built to ensure no broken references from folder reorganization. This is a gate before proceeding to the next feature.

- **Prior art**: The existing `PersonalFinance.Tests` project contains 13 test files covering API endpoints, database persistence, Google Drive integration, identity configuration, and infrastructure. Tests use xUnit and follow the existing patterns. Both unit tests and integration tests already exist in the project.

- **Modules to test**:
  - **Migrated features**: AI Assistant, NetWorth, and GoogleDrive will have both unit tests (service layer) and integration tests (API endpoints) verifying that commands and queries produce the same results as the previous direct service calls.
  - **AuditableEntity**: Unit tests will verify soft delete behavior (IsDeleted flag, DeletedAt timestamp) and global query filters (soft-deleted records excluded from queries, included with `.IgnoreQueryFilters()`).
  - **Reorganized features**: Existing CQRS features (Budget, Community, etc.) will have integration tests verifying that folder reorganization didn't break API endpoints.

## Out of Scope

- **Consolidating Web and ApiService into a single process**: The two-service split remains in place. This is a future consideration, not part of this migration.

- **CQRS in the Web layer**: The Web layer will not adopt MediatR. This is a deliberate decision to keep the UI layer simple.

- **Changing API contracts**: Refit interfaces and DTO public surfaces remain unchanged. No breaking changes to the API.

- **Authentication changes**: The X-User-Id header pattern remains. No migration to JWT or mutual TLS.

- **Feature removal**: No features are being removed (Community, AI Assistant, News, etc. all remain).

- **Database provider changes**: The dual-provider setup (SQLite for dev, Azure SQL for production) remains unchanged.

- **Auto-migration changes**: Auto-migration on startup behavior remains unchanged.

## Further Notes

- Documentation (GLOSSARY.md and ADR-0001) has already been created in a previous session and is up to date with these decisions.
- The AGENTS.md file has been updated with the new architectural patterns and guidelines.
- Version was bumped to 1.13.0 to reflect the architectural enhancement.
- The actual code migration is not yet implemented — this spec defines the plan for the migration work.

# Delivery Steps

### ✓ Step 1: Create AuditableEntity and configure soft deletes
Create the AuditableEntity base class and wire up EF Core global query filters.
### * Step 2: Migrate AI Assistant to CQRS and feature folder
- Create `AuditableEntity` abstract class in `PersonalFinance.Data` with Id, UserId, CreatedAt, UpdatedAt, IsDeleted, DeletedAt properties.
- Update all user-owned entities (NetWorthParameters, SavingsGoal, Item, SavedArticle, UserBudgetAllocation, SpreadsheetBudgetSnapshot, SpreadsheetTransactionRecord, GoogleDriveConnection, GoogleDriveCachedFile, CommunityStory, CommunityComment, CommunityCommentLike, CommunityPollVote, CommunityStoryLike) to inherit from AuditableEntity.
- Configure global query filters in `AppDbContext.OnModelCreating()` for all AuditableEntity types.
- Update `NetWorthParametersService.DeleteAsync` and other delete methods to use soft delete (set IsDeleted=true) instead of hard delete.
- Create and inspect EF Core migration for the new columns.
- Build the solution to verify no compilation errors.

### ✓ Step 2: Migrate AI Assistant to CQRS and feature folder
Migrate the AI Assistant feature from direct service calls to CQRS with MediatR, and move into a feature folder.
### * Step 3: Migrate NetWorth to CQRS and feature folder
- Create `ApiService/AiAssistant/` feature folder with Commands/, Queries/, Services/ subfolders.
- Create `ChatCommand` and `SummarizeArticleCommand` with handlers in the Commands/ folder.
- Move `AiAssistantController.cs` into the `AiAssistant/` folder.
- Move `AiAssistantService.cs` and `IAiAssistantService.cs` into `AiAssistant/Services/`.
- Update the controller to inject `IMediator` instead of `IAiAssistantService`.
- Handlers delegate to `IAiAssistantService` (business logic stays in the service).
- Update namespaces to follow `PersonalFinance.ApiService.AiAssistant.*` convention.
- Build and verify the solution compiles.

### ✓ Step 3: Migrate NetWorth to CQRS and feature folder
Migrate the NetWorth feature from direct service calls to CQRS with MediatR, and move into a feature folder.
### * Step 4: Migrate GoogleDrive to CQRS and feature folder
- Create `ApiService/NetWorth/` feature folder with Commands/, Queries/, Services/, Validators/ subfolders.
- Create `SaveParametersCommand`, `DeleteParametersCommand`, and `LoadParametersQuery` with handlers.
- Move `NetWorthController.cs` into the `NetWorth/` folder.
- Move `NetWorthParametersService.cs`, `INetWorthParametersService.cs`, and validators into the feature folder.
- Update the controller to inject `IMediator`.
- Handlers delegate to `INetWorthParametersService`.
- Update namespaces to follow `PersonalFinance.ApiService.NetWorth.*` convention.
- Build and verify the solution compiles.

### ✓ Step 4: Migrate GoogleDrive to CQRS and feature folder
Migrate the GoogleDrive feature from direct service calls to CQRS with MediatR, and move into a feature folder.
### ✓ Step 5: Reorganize existing CQRS features into feature folders and organize DTOs
Move existing CQRS features into feature-based folders and organize DTOs by feature. All steps complete.
- Create `ApiService/GoogleDrive/` feature folder with Commands/, Queries/, Services/ subfolders.
- Create commands and queries for all GoogleDrive endpoints (Explore, GetConnections, Connect, DeleteConnection, Sync, Reauth, etc.).
- Move `GoogleDriveController.cs` into the `GoogleDrive/` folder.
- Move `GoogleDriveService.cs` and `IGoogleDriveService.cs` into `GoogleDrive/Services/`.
- Update the controller to inject `IMediator`.
- Handlers delegate to `IGoogleDriveService`.
- Update namespaces to follow `PersonalFinance.ApiService.GoogleDrive.*` convention.
- Build and verify the solution compiles.

### ✓ Step 5: Reorganize existing CQRS features into feature folders and organize DTOs
Move existing CQRS features (Budget, Community, SavingsGoals, Watchlist, SpreadsheetAnalysis) into feature-based folders and organize DTOs by feature.
- Move Budget features from `Features/Budget/` into `ApiService/Budget/` folder structure with the controller.
- Move Community features into `ApiService/Community/` folder structure.
- Move SavingsGoals features into `ApiService/SavingsGoals/` folder structure.
- Move Watchlist features into `ApiService/Watchlist/` folder structure.
- Move SpreadsheetAnalysis features into `ApiService/SpreadsheetAnalysis/` folder structure.
- Move remaining controllers from `Controllers/` into their respective feature folders.
- Organize DTOs in `Shared/DTOs/` into feature subfolders (NetWorth/, Community/, GoogleDrive/, etc.).
- Update all namespace references and using statements across the project.
- Remove empty `Controllers/`, `Services/`, `Features/`, and `Validators/` folders.
- Build and verify the full solution compiles with no errors.