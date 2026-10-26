# Diagrams

A visual map of the Personal Finance App for engineers getting to know the codebase, and a reference for reviews.
Every diagram is [Mermaid](https://mermaid.js.org/) and renders on GitHub, in Rider and in VS Code. Each one says
which files it was drawn from, so it doubles as a reading list. Terms (Feature, Service, Entity, Account, Purge, ...)
are defined in [`GLOSSARY.md`](../GLOSSARY.md); the reasons behind the shapes are in [`adr/`](adr/).

**Last verified against `AppVersion` 5.13.0 (2026-10-24).** The diagrams are hand-maintained. When you add an
Entity, a Feature, an Infrastructure abstraction or a scheduled job, update the diagram that shows it and its row in
the [Feature map](#3-feature-map) (step 10 of the add-a-feature checklist in [`ARCHITECTURE.md`](ARCHITECTURE.md)).

## Index

| # | Diagram | Kind | Answers |
|---|---|---|---|
| [1](#1-runtime-context) | Runtime context | flowchart | What runs, what it talks to, which jobs exist |
| [2](#2-anatomy-of-a-feature) | Anatomy of a Feature (Feedback) | class | What a canonical Feature is made of, from browser to table |
| [3](#3-feature-map) | Feature map | table | For every Feature: controllers, Services, Entities, Refit Contract, Web controller, JS, how it deviates |
| [4](#4-data-model) | Data model | 4 ER diagrams | Every Entity, its columns, keys and relationships |
| [5](#5-request-flow-end-to-end) | Request flow, end to end | sequence | Headers, authentication, validation and both error paths |
| [6](#6-identity-and-rbac) | Identity and RBAC | flowchart | How an Account becomes a principal in each app, and where roles are checked |
| [7](#7-account-lifecycle) | Account lifecycle | state + sequence | Deletion Request, Grace Period, cancel, Purge |
| [8](#8-budget-data-source) | Budget data source | class + flowchart | The two budget sources behind one reader, and who consumes it |
| [9](#9-notifications) | Notifications | flowchart | Who raises them, where they are stored, how the bell reads them |
| [10](#10-research-pipeline) | Research pipeline | flowchart + sequence | Daily refresh from official sources, and the dashboard read path |
| [11](#11-unavailable-state-and-keeping-the-apiservice-awake) | Unavailable State | flowchart | What the Web shows when the ApiService fails, and the warm window |
| [12](#12-ai-assistant) | AI Assistant | sequence | Scope filter, quota, tools, OpenAI call, usage record |
| [13](#13-google-drive-and-spreadsheet-analysis) | Google Drive and Spreadsheet Analysis | class + sequence | The one cross-Feature dependency, and connect-then-import |
| [14](#14-startup-and-pipelines) | Startup and pipelines | 2 flowcharts | ApiService run modes, Web middleware order |
| [15](#15-scheduled-and-background-work) | Scheduled and background work | table | Everything that runs without a request |
| [16](#16-localization) | Localization | 3 flowcharts | How a request gets its language, where the Account's Language is stored, where strings come from, how translations are made |

Diagrams that live elsewhere and are not repeated here:

- Project dependencies and the short request flow: [`ARCHITECTURE.md`](ARCHITECTURE.md).
- Production system context, CI/CD, Bicep modules, rollback and the request path through Azure:
  [`deployment.md`](deployment.md) section 1.
- Release flow from merge to GitHub Release: [`DEVELOPMENT.md`](DEVELOPMENT.md).
- Custom domain and certificate flow: [`CUSTOM_DOMAIN.md`](CUSTOM_DOMAIN.md).
- The ASP.NET Core Identity tables: [`IDENTITY_SCHEMA.md`](IDENTITY_SCHEMA.md).

## 1. Runtime context

The logical shape at run time. Production resource names, ingress and the deploy pipeline are in
[`deployment.md`](deployment.md) 1.1. Locally, `PersonalFinance.AppHost` starts `web` and `apiservice` with Aspire
service discovery (`AppHost.cs`), and SQL Server runs in Docker (`compose.yaml`).

```mermaid
flowchart LR
    user(["Browser"])

    subgraph app["Personal Finance App"]
        web["PersonalFinance.Web<br/>MVC, Razor Pages, ASP.NET Core Identity"]
        api["PersonalFinance.ApiService<br/>REST controllers, Services, Validators"]
        db[("SQL Server / Azure SQL<br/>AppDbContext")]
        purge[["Purge job<br/>ApiService image, --purge-only<br/>daily 15:00 UTC"]]
        refresh[["Research refresh job<br/>ApiService image, --refresh-research<br/>daily 10:00 UTC"]]
    end

    subgraph ext["External services"]
        goauth["Google OAuth<br/>(sign-in)"]
        brevo["Brevo<br/>(transactional email)"]
        gdrive["Google Drive API v3<br/>(user's own API key)"]
        openai["OpenAI<br/>(gpt-4o-mini)"]
        sec["SEC EDGAR"]
        wb["World Bank API"]
        fed["Federal Reserve H.15<br/>BLS, U.S. Treasury"]
        rss["RSS feeds<br/>(news)"]
    end

    user -- "HTTPS" --> web
    web -- "Refit clients<br/>X-User-Id + X-Internal-Key" --> api
    web -- "Identity, consent,<br/>data-protection keys" --> db
    api -- "EF Core" --> db
    purge --> db
    refresh --> db
    refresh --> sec & wb & fed
    web --> goauth & brevo
    api --> gdrive & openai & rss
    api -- "company lookup<br/>(live, cached 24 h)" --> sec
```

Drawn from `Web/Infrastructure/InternalApiClientExtensions.cs`, `ApiService/Program.cs`, `infra/main.bicep` and the
Feature `Adapters/` folders.

## 2. Anatomy of a Feature

**Feedback** is the Feature to mirror ([`ARCHITECTURE.md`](ARCHITECTURE.md), "Adding a feature"). Its chain is the
canonical one from [ADR 0007](adr/0007-feature-folders-with-interface-only-dependencies.md): ApiService controller,
Validator, Service interface, `internal sealed` Service, `AppDbContext`, Entity; and on the Web side a thin MVC
controller that only knows the Refit Contract. Only the interface, the controller and the DTOs are `public`.

```mermaid
classDiagram
    direction LR

    namespace Web {
        class FeedbackController_Web["FeedbackController (Web)"] {
            +Index() ViewResult
            +Submit(FeedbackViewModel) IActionResult
        }
        class FeedbackViewModel {
            Models/Feedback
        }
        class UserIdForwardingHandler {
            <<DelegatingHandler>>
            adds X-User-Id and X-Internal-Key
        }
    }

    namespace Shared {
        class IFeedbackApi {
            <<Refit Contract>>
            +SubmitAsync(SubmitFeedbackRequest)
            +ListAsync(status)
            +ChangeStatusAsync(id, request)
        }
        class SubmitFeedbackRequest {
            DTOs/Feedback
        }
    }

    namespace ApiService {
        class FeedbackController_Api["FeedbackController (ApiService)"] {
            [Authorize] api/Feedback
            +Submit(SubmitFeedbackRequest)
            +List(status) RequireAdmin
            +ChangeStatus(id, request) RequireAdmin
        }
        class SubmitFeedbackRequestValidator {
            <<AbstractValidator>>
            Validators/
        }
        class IFeedbackService {
            <<interface>>
            +SubmitAsync(userId, request) SubmitFeedbackResult
            +ListAsync(status) List~FeedbackAdminItemDto~
            +ChangeStatusAsync(id, status) bool
        }
        class FeedbackService {
            <<internal sealed>>
        }
        class FeedbackFeature {
            <<static>>
            +AddFeedbackFeature()
        }
        class IRateLimiterService {
            <<Infrastructure, keyed Feedback>>
            +TryAllow(userId) bool
        }
    }

    namespace Data {
        class AppDbContext {
            DbSet~Feedback~ Feedback
        }
        class Feedback {
            +Category
            +Message
            +PageUrl
            +Status
        }
        class AuditableEntity {
            <<abstract>>
            +Id
            +UserId
            +CreatedAt
            +IsDeleted
            +DeletedAt
        }
    }

    FeedbackController_Web --> IFeedbackApi : injects
    FeedbackController_Web ..> FeedbackViewModel : builds
    IFeedbackApi ..> UserIdForwardingHandler : every call
    UserIdForwardingHandler ..> FeedbackController_Api : HTTP
    IFeedbackApi ..> SubmitFeedbackRequest
    FeedbackController_Api --> IFeedbackService : injects
    FeedbackController_Api --> SubmitFeedbackRequestValidator : IValidator~T~
    FeedbackService ..|> IFeedbackService
    FeedbackService --> AppDbContext
    FeedbackService --> IRateLimiterService : 5 per hour
    FeedbackFeature ..> FeedbackService : registers scoped
    AppDbContext --> Feedback
    Feedback --|> AuditableEntity
```

Drawn from `ApiService/Features/Feedback/`, `Web/Controllers/FeedbackController.cs`, `Shared/Contracts/IFeedbackApi.cs`
and `Data/Entities/Feedback.cs`. Two things make Feedback slightly more than minimal: the keyed rate limiter from
Infrastructure and a join on the Identity `Users` table for the admin inbox.

## 3. Feature map

One row per ApiService Feature, plus the Infrastructure-owned Notifications. ApiService routes are relative to the
ApiService root; a Web controller marked "JSON only" has no view and is called by the page's JavaScript at `/api/...`.
JavaScript paths are under `Web/wwwroot/js/`.

| Feature | ApiService controllers (route) | Service interfaces | Entities it owns (reads) | Refit Contract | Web controller | JavaScript | How it differs from section 2 |
|---|---|---|---|---|---|---|---|
| AccountLifecycle | `AccountDeletionController` (`api/account-deletion`), `DataExportController` (`api/data-export`) | `IAccountDeletionService`, `IAccountPurgeService`, `IDataExportService` | `DeletionRequest` (every user-owned Entity on purge and export) | `IAccountDeletionApi`, `IDataExportApi` | none: Identity Razor Pages under `Areas/Identity/Pages/Account/` | none | `AccountPurgeJob` runs the image with `--purge-only`; see [7](#7-account-lifecycle) |
| Admin | `AdminController` (`api/Admin`, RequireAdmin) | `IAdminService` | none (Identity tables) | `IAdminApi` | `Admin` (also injects `IFeedbackApi`) | none | `UserManager`/`RoleManager`, `AdminRoleLock`; no Validators |
| AiAssistant | `AiAssistantController` (`api/AiAssistant`) | `IAiAssistantService`, `IAiQuotaService`, `IAiScopeFilter` | `AiUsageRecord` (reads `NetWorthParameters`, `SavingsGoal`, `UserBudgetAllocation`, `UserPreference`) | `IAiAssistantApi` | `AiAssistant` (JSON only) | `core/ai-chat-widget.js`, `news/news-summary.js` | OpenAI through `IChatClient`; see [12](#12-ai-assistant) |
| Analysis | `SpreadsheetAnalysisController` (`api/SpreadsheetAnalysis`) | `ISpreadsheetAnalysisService`, `IGoogleSheetRemovalService` | `SpreadsheetBudgetSnapshot`, `SpreadsheetTransactionRecord` | `ISpreadsheetAnalysisApi` | `Analysis` (also injects `IBudgetTrackerApi`) | `analysis/` | Depends on GoogleDrive's `ISpreadsheetReader`; see [13](#13-google-drive-and-spreadsheet-analysis) |
| BudgetPlanner | `BudgetController` (`api/Budget`) | `IBudgetService` | `UserBudgetAllocation` | `IBudgetPlannerApi` | `Budget` | `budget/` | Raises Notifications; reads `IBudgetDataProvider` |
| BudgetTracker | `BudgetTrackerController` (`api/budget-tracker`) | `IMonthlyBudgetService`, `IBudgetTrackerRemovalService` | `BudgetCategory`, `BudgetTransaction`, `MonthlyBudgetPlan`, `MonthlyBudgetLine` | `IBudgetTrackerApi` | `BudgetTracker` | `budget-tracker/`, `core/data-source-toggle.js` | Six Validators; the controller also injects `IBudgetDataSourceService` |
| Community | `CommunityController` (`api/Community`) | `ICommunityService` | `CommunityStory`, `CommunityStoryLike`, `CommunityComment`, `CommunityCommentLike`, `CommunityPoll`, `CommunityPollOption`, `CommunityPollVote` | `ICommunityApi` | `Community` | `community.js` | No Validators: the Service throws `ArgumentException`, the controller returns 400 |
| Dashboard | `DashboardController` (`api/Dashboard`) | `IDashboardService` | none (reads `NetWorthParameters`, `SavingsGoal`, `UserBudgetAllocation`, `GoogleDriveConnection`) | `IDashboardApi` | `Dashboard` | `dashboard/` | Read-only aggregator over four Features plus `IBudgetDataProvider` |
| Feedback | `FeedbackController` (`api/Feedback`) | `IFeedbackService` | `Feedback` | `IFeedbackApi` | `Feedback`; inbox in `Admin` | none | The canonical example ([2](#2-anatomy-of-a-feature)) |
| GoogleDrive | `GoogleDriveController` (`api/GoogleDrive`) | `IDriveConnectionService`, `ISpreadsheetReader`, `IGoogleDriveFileSource` (internal) | `GoogleDriveConnection`, `GoogleDriveCachedFile` | `IGoogleDriveApi` | `GoogleDrive` | `google-drive/` | Typed `HttpClient`s to Google Drive; a 1,450-line static parser; see [13](#13-google-drive-and-spreadsheet-analysis) |
| NetWorth | `NetWorthController` (`api/NetWorth`) | `INetWorthParametersService` | `NetWorthParameters` | `INetWorthApi` | `Networth` | `networth/` | `calculate` is pure: a static Validator and the static `NetWorthProjectionService`, no Service, no database |
| Research | `ResearchController` (`api/Research`), `CompaniesController` (`api/Research/companies`) | `IResearchService`, `ICompanyProfileService`, nine `IResearchDatasetRefresher`, `ResearchSnapshotStore` | `ResearchSnapshot` (not user-owned) | `IResearchApi` | `Research` | `research/` (12 files) | The largest Feature: five external adapters, `Domain/` with 56 pure types, a refresh job; see [10](#10-research-pipeline) |
| SavingsGoals | `SavingsGoalsController` (`api/SavingsGoals`, Contributions under `{goalId}/contributions`) | `ISavingsGoalService` | `SavingsGoal`, `Contribution` | `ISavingsGoalsApi` | `SavingsGoals` | `savingsgoals/` | Validators for a new goal, a Contribution and a balance correction; the saved amount is the Contribution total, cached on the goal in the same save; raises each milestone Notification once |
| Search | `SearchController` (`api/Search`) | `ISearchService` | none (reads `BudgetTransaction`, `BudgetCategory`, `SpreadsheetTransactionRecord`, `SavingsGoal`) | `ISearchApi` | `Search` (JSON only) | `core/search.js` | Read-only; picks the source through `IBudgetDataSourceService` |
| Watchlist | `WatchlistController` (`api/Watchlist`), `NewsController` (`api/News`) | `IWatchlistService`, `INewsService`, `INewsFeedClient` (internal) | `SavedArticle` | `IWatchlistApi`, `INewsApi` | `Watchlist` (JSON only), `News` | `news/` | RSS adapter with `IMemoryCache` (5 minutes); `NewsService` has no database |
| Notifications (Infrastructure) | `NotificationController` (`api/Notification`) | `INotificationService` | `Notification` | `INotificationApi` | `Notification` (JSON only) | `core/notifications.js` | Shared by three Features; see [9](#9-notifications) |

Web controllers with no ApiService Feature: `Home` (landing page, `landing/skyline.js`), `InvestmentCalculator`
(client-side only, `investment-calculator/`) and `Legal`. Every Feature registers itself in
`ApiService/Features/<Feature>/<Feature>Feature.cs`, called in order from `ApiService/Program.cs`; every Refit Contract
is registered in `Web/Infrastructure/InternalApiClientExtensions.cs`.

## 4. Data model

Drawn from `Data/Entities/` and `AppDbContext.OnModelCreating` (all configuration is fluent, no attributes, no
`IEntityTypeConfiguration` classes, tables keep their `DbSet` names). The Identity tables are in
[`IDENTITY_SCHEMA.md`](IDENTITY_SCHEMA.md).

Reading key:

- **`AuditableEntity`** (abstract) gives every user-owned Entity `int Id`, `string UserId` (max 450), `CreatedAt`,
  `UpdatedAt?`, `IsDeleted`, `DeletedAt?` and `SoftDelete()`. The diagrams list only the Entity's own columns.
- **`UserId` is a logical link, not a foreign key.** No Entity has a database FK to `AspNetUsers`. After a Purge, kept
  rows carry `UserId = "deleted-account"` (`Shared/Constants/DeletedAccount.cs`).
- **Soft delete.** A global query filter `!IsDeleted` hides deleted rows of every `AuditableEntity` except
  `AiUsageRecord`, which is never soft-deleted. `CommunityPoll`, `CommunityPollOption`, `GoogleDriveCachedFile`,
  `ResearchSnapshot` and `DeletionRequest` are not auditable and are hard-deleted (or kept) as noted.
- Relationship labels give the FK column and the configured delete behaviour. Money columns are `decimal`; the
  precision is in the comment.

### 4.1 Budget Tracker, planner and net worth

```mermaid
erDiagram
    BudgetCategory {
        int Id PK
        string UserId "unique with Kind and Name, live rows only"
        string Name
        int Kind "BudgetCategoryKind: Expense=1, Income=2"
        int SortOrder
    }
    BudgetTransaction {
        int Id PK
        string UserId "indexed with Date"
        int CategoryId FK
        DateOnly Date
        decimal Amount "18,2"
        string Description
    }
    MonthlyBudgetPlan {
        int Id PK
        string UserId "unique with Year and Month, live rows only"
        int Year
        int Month
    }
    MonthlyBudgetLine {
        int Id PK
        string UserId
        int PlanId FK "unique with CategoryId, live rows only"
        int CategoryId FK
        decimal PlannedAmount "18,2"
        decimal ActualOverride "18,2, nullable"
    }
    UserBudgetSourcePreference {
        int Id PK
        string UserId UK "live rows only"
        int Source "BudgetDataSource: GoogleSheet=1, BudgetTracker=2"
    }
    UserBudgetAllocation {
        int Id PK
        string UserId "unique with Period"
        string Period "yyyy-MM"
        decimal MonthlyIncome "18,2"
        decimal HousingAmount "18,2, plus 9 more category amounts"
        DateTime UpdatedAtUtc
    }
    SavingsGoal {
        int Id PK
        string UserId
        string Name
        decimal TargetAmount "18,2"
        decimal CurrentAmount "18,2, cached total of its Contributions"
        string Description "nullable"
        DateTime TargetDate "nullable"
        bool IsCompleted "derived: CurrentAmount at or over TargetAmount"
        int HighestMilestoneNotified "0, 50, 75 or 100, never falls"
    }
    Contribution {
        int Id PK
        string UserId "indexed with SavingsGoalId and Date"
        int SavingsGoalId FK
        decimal Amount "18,2, signed, never zero"
        DateOnly Date
        string Note "nullable, max 200"
        int Kind "ContributionKind: Regular=0, Opening=1, Adjustment=2"
    }
    NetWorthParameters {
        int Id PK
        string UserId UK
        decimal StartingBalance "18,4"
        decimal AnnualSavings "18,4"
        decimal AnnualIncome "18,4"
        decimal AnnualSpending "18,4"
        int CurrentAge
        int RetirementAge
        decimal BaseRoi "18,4, plus ConservativeRoi, AggressiveRoi, AggressiveSavings"
        DateTime UpdatedAtUtc
    }

    BudgetCategory ||--o{ BudgetTransaction : "CategoryId, Restrict"
    BudgetCategory ||--o{ MonthlyBudgetLine : "CategoryId, Restrict"
    MonthlyBudgetPlan ||--o{ MonthlyBudgetLine : "PlanId, Cascade"
    SavingsGoal ||--o{ Contribution : "SavingsGoalId, Restrict"
```

`UserBudgetAllocation` and `NetWorthParameters` have no relationships; they are drawn here, with `SavingsGoal`, because
the Dashboard and the AI Assistant read them together with the budget data. Deleting a goal soft-deletes its
Contributions with it.

### 4.2 Google Drive and spreadsheet data

```mermaid
erDiagram
    GoogleDriveConnection {
        int Id PK
        string UserId "indexed with FolderId"
        string Name
        string FolderId
        string FolderUrl
        string EncryptedApiKey "nullable, CredentialProtector"
        string MaskedApiKey "nullable"
        bool IsValid
        string SyncStatus "Pending, Synced, Failed, NeedsReauth"
        string ErrorMessage "nullable"
        DateTime CreatedAtUtc
        DateTime LastSyncedAtUtc "nullable, cache fresh for 30 min"
    }
    GoogleDriveCachedFile {
        int Id PK
        int ConnectionId FK "nullable"
        string DriveFileId "indexed with ConnectionId"
        string Name
        string MimeType
        long Size "nullable"
        DateTime ModifiedTime "nullable"
        string WebViewLink "nullable"
        string FileType
        bool IsFolder
        bool IsTrashed
        DateTime LastFetchedUtc
    }
    SpreadsheetBudgetSnapshot {
        int Id PK
        string UserId "indexed with Year and MonthNumber"
        int ConnectionId FK
        string FileId
        string FileName
        int Year
        int MonthNumber
        string MonthName
        decimal BudgetedIncome "18,4"
        decimal ActualIncome "18,4"
        decimal BudgetedExpenses "18,4"
        decimal ActualExpenses "18,4"
        decimal StartingBalance "18,4, nullable"
        decimal EndingBalance "18,4, nullable"
        string CategoryBreakdownJson "max 10000"
        DateTime SyncedAtUtc
    }
    SpreadsheetTransactionRecord {
        int Id PK
        string UserId "indexed with Date, and with Category and Date"
        int SnapshotId FK
        int ConnectionId FK
        DateTime Date
        decimal Amount "18,4"
        string Description
        string Category
        string Type "Expense or Income"
    }

    GoogleDriveConnection ||--o{ GoogleDriveCachedFile : "ConnectionId, ClientSetNull"
    GoogleDriveConnection ||--o{ SpreadsheetBudgetSnapshot : "ConnectionId, Cascade"
    GoogleDriveConnection ||--o{ SpreadsheetTransactionRecord : "ConnectionId, Restrict"
    SpreadsheetBudgetSnapshot ||--o{ SpreadsheetTransactionRecord : "SnapshotId, Cascade"
```

Known quirk: `GoogleDriveConnection.BudgetSnapshots` and `.TransactionRecords` are navigation collections that
`OnModelCreating` never ties to the `ConnectionId` relationships above, so EF Core also created a second, optional
shadow FK column `GoogleDriveConnectionId` on both spreadsheet tables (visible in
`AzureSqlAppDbContextModelSnapshot.cs`). The code never sets it.

### 4.3 Community

```mermaid
erDiagram
    CommunityStory {
        int Id PK
        string UserId "deleted-account after a Purge"
        string AuthorName "Deleted User after a Purge"
        string Title
        string Summary
        string Body "max 10000"
        string Category "indexed"
        int Likes "denormalised counter"
        int CommentCount "counter only, no FK from comments"
        bool IsVisible
        bool IsFeatured "indexed"
        DateTime PublishedAtUtc "indexed"
        int ReadTimeMinutes
    }
    CommunityStoryLike {
        int Id PK
        string UserId "unique with StoryId"
        int StoryId FK
        DateTime CreatedAtUtc
    }
    CommunityComment {
        int Id PK
        string UserId
        string AuthorName
        string Text
        string Topic
        int PollId FK "nullable"
        string CaseStudyTitle "nullable"
        int Likes "denormalised counter"
        bool IsVisible
        DateTime CreatedAtUtc "indexed"
    }
    CommunityCommentLike {
        int Id PK
        string UserId "unique with CommentId"
        int CommentId FK
        DateTime CreatedAtUtc
    }
    CommunityPoll {
        int Id PK
        string PollCode UK
        string Question
        string Description "nullable"
        string Category
        bool IsActive
        DateTime CreatedAtUtc "indexed"
    }
    CommunityPollOption {
        int Id PK
        int PollId FK "nullable"
        string Text
        int Order
    }
    CommunityPollVote {
        int Id PK
        string UserId "unique with PollId"
        int PollId FK "nullable"
        int OptionId FK "nullable"
        DateTime CreatedAtUtc
    }

    CommunityStory ||--o{ CommunityStoryLike : "StoryId, Cascade"
    CommunityComment ||--o{ CommunityCommentLike : "CommentId, Cascade"
    CommunityPoll ||--o{ CommunityPollOption : "PollId, SetNull"
    CommunityPoll ||--o{ CommunityPollVote : "PollId, SetNull"
    CommunityPoll ||--o{ CommunityComment : "PollId, SetNull"
    CommunityPollOption ||--o{ CommunityPollVote : "OptionId, SetNull"
```

`CommunityPoll` and `CommunityPollOption` are seeded reference data (`SeedCommunityDataAsync`): no `UserId`, no soft
delete, untouched by the Purge. Stories and comments survive the Purge anonymised ([ADR
0005](adr/0005-account-purge-hard-deletes-after-grace-period.md)).

### 4.4 Account, consent and cross-cutting records

```mermaid
erDiagram
    AspNetUsers {
        string Id PK "IdentityUser, see IDENTITY_SCHEMA.md"
        string Email
        DateTimeOffset LockoutEnd "MaxValue while a Deletion Request is Pending"
        string SecurityStamp "rotated on request and cancel"
    }
    DeletionRequest {
        int Id PK
        string UserId "indexed; deleted-account after the Purge"
        DateTime RequestedAt
        DateTime PurgeAfter "RequestedAt + 30 days, indexed with Status"
        int Status "Pending=1, Cancelled=2, Purged=3; concurrency token"
        string CancelTokenHash UK "SHA-256; the raw token is never stored"
        DateTime CancelledAt "nullable"
        DateTime PurgedAt "nullable"
    }
    UserPreference {
        int Id PK
        string UserId UK "among non-deleted rows"
        string Language "en or vi; the culture cookie mirrors it"
    }
    UserConsent {
        int Id PK
        string UserId "indexed"
        string PolicyVersion "AppVersion at registration"
        bool AcceptedTerms
        bool AcceptedPrivacy
        string IpAddress "nullable"
    }
    Feedback {
        int Id PK
        string UserId "indexed"
        int Category "Bug=1, Idea=2, Other=3"
        string Message
        string PageUrl "nullable"
        int Status "New=1, Reviewed=2, Archived=3; indexed with CreatedAt"
    }
    Notification {
        int Id PK
        string UserId "indexed, and with IsRead"
        int Type "BudgetAlert=0, SavingsMilestone=1, SyncStatus=2"
        string Title "English snapshot"
        string Message "English snapshot"
        string TitleKey "nullable; rendered in the reader's language"
        string MessageKey "nullable"
        string Arguments "nullable JSON of named arguments"
        bool IsRead
        DateTime ReadAt "nullable"
        string SourceFeature "nullable: Budget, SavingsGoals, GoogleDrive"
    }
    AiUsageRecord {
        int Id PK
        string UserId "indexed; CreatedAt indexed; never soft-deleted"
        string Endpoint "chat or summarize"
        int PromptTokens
        int CompletionTokens
        int CachedPromptTokens
        decimal EstimatedCostUsd "18,6"
    }
    SavedArticle {
        int Id PK
        string UserId "unique with Url"
        string Title
        string Url
        string Source
        string Category
        string Notes "nullable"
        string ImageUrl "nullable"
        DateTime SavedAtUtc
    }
    ResearchSnapshot {
        int Id PK
        string Dataset UK "sectors, etfs, fund-overlap, macro, world, ..."
        string FirstPeriod
        string LastPeriod
        string Payload "JSON of the Shared snapshot DTO"
        DateOnly SourceAsOf
        DateTime BuiltAt
    }

    AspNetUsers ||..o| DeletionRequest : "UserId, logical"
    AspNetUsers ||..o| UserPreference : "UserId, logical"
    AspNetUsers ||..o{ UserConsent : "UserId, logical"
    AspNetUsers ||..o{ Feedback : "UserId, logical"
    AspNetUsers ||..o{ Notification : "UserId, logical"
    AspNetUsers ||..o{ AiUsageRecord : "UserId, logical"
    AspNetUsers ||..o{ SavedArticle : "UserId, logical"
```

`ResearchSnapshot` belongs to no Account: one row per dataset, replaced wholesale by the refresh job
([10](#10-research-pipeline)). `DeletionRequest` is the only row that outlives its Account.

## 5. Request flow, end to end

The short version is in [`ARCHITECTURE.md`](ARCHITECTURE.md). This one adds the two handlers that carry identity and
the two ways a failure reaches the browser.

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser (wwwroot/js or form)
    participant WC as Web controller
    participant RC as Refit Contract (Shared/Contracts)
    participant H as UserIdForwardingHandler (Web)
    participant AH as XUserIdAuthenticationHandler (ApiService)
    participant AC as ApiService controller
    participant V as Validator
    participant S as Service
    participant DB as AppDbContext / SQL

    B->>WC: GET view, or fetch /api/... with RequestVerificationToken
    WC->>RC: IFeatureApi.XxxAsync(dto)
    RC->>H: HTTP request (15 s timeout, standard resilience)
    H->>H: userId = User.FindFirstValue(NameIdentifier)
    H->>AH: + X-User-Id (signed-in only), + X-Internal-Key
    AH->>AH: FixedTimeEquals(X-Internal-Key, Internal:ApiKey) else NoResult
    AH->>DB: UserManager.GetRolesAsync(userId)
    AH-->>AC: ClaimsPrincipal: NameIdentifier, Name, Role per role
    AC->>AC: [Authorize] or RequireAdmin, then User.GetUserId()
    AC->>V: ValidateAsync(dto)
    alt invalid
        AC-->>RC: 400 ValidationProblem
    else valid
        AC->>S: XxxAsync(userId, dto)
        S->>DB: AsNoTracking query, or SaveChangesAsync
        DB-->>S: Entities
        S-->>AC: DTO (mapped by hand)
        AC-->>RC: 200 JSON, 404, or ProblemDetails
    end
    alt view action
        RC-->>WC: DTO, or ApiException / HttpRequestException / TimeoutException
        WC-->>B: View(model), or this.UnavailableView(...) on any failure
    else JSON action with [JsonApiErrors]
        RC-->>WC: DTO or exception
        WC-->>B: JSON, or an error body with the ApiException status (502 when unreachable)
    end
```

Drawn from `Web/Infrastructure/UserIdForwardingHandler.cs`, `ApiService/Infrastructure/XUserIdAuthenticationHandler.cs`,
`Web/Infrastructure/JsonApiErrorsAttribute.cs` and `Web/Infrastructure/UnavailableStateExtensions.cs`. The failure
branches are detailed in [11](#11-unavailable-state-and-keeping-the-apiservice-awake).

## 6. Identity and RBAC

Both apps share the Identity tables in `AppDbContext`, but build their principal differently: the Web from the cookie,
the ApiService from the forwarded header on every request ([ADR
0004](adr/0004-rbac-roles-from-database-and-policy-checks.md)). There is one Policy, `RequireAdmin`, and two Roles,
`Admin` and `User` (`Shared/Constants/AppPolicies.cs`, `AppRoles.cs`).

```mermaid
flowchart TB
    subgraph web["Web: cookie identity"]
        login["Sign in: Identity password, or Google OAuth"] --> cookie["Cookie principal<br/>NameIdentifier + Role claims<br/>(roles refresh with the security stamp, up to 30 min)"]
        cookie --> wpol{"Authorize Policy = RequireAdmin?"}
        wpol -- "not Admin" --> nf["AdminNotFoundAuthorizationResultHandler<br/>404, not 403"]
        wpol -- "allowed" --> wctrl["Web controller or Razor Page"]
        cookie -- "signed out, request is /api or JSON" --> ar["ApiAwareCookieRedirects<br/>401/403 instead of a login redirect"]
    end

    subgraph api["ApiService: XUserId scheme, per request"]
        hdr["X-User-Id + X-Internal-Key"] --> key{"key matches Internal:ApiKey?"}
        key -- "no, or no X-User-Id" --> none["NoResult: 401"]
        key -- "yes" --> roles["UserManager.GetRolesAsync<br/>AspNetUserRoles, no cache"] --> principal["ClaimsPrincipal"]
        principal --> apol{"RequireAdmin Policy?"}
        apol -- "fails" --> f403["403"]
        apol -- "allowed" --> actrl["Authorize controller<br/>User.GetUserId()"]
    end

    wctrl -- "Refit + UserIdForwardingHandler" --> hdr

    seed["At startup: RbacSeeder<br/>ensures Admin and User roles,<br/>grants Admin to Rbac:AdminEmail.<br/>DefaultRoleAssignment gives User on registration"] -.-> roles
```

Drawn from `Web/Program.cs`, `Web/Infrastructure/AdminNotFoundAuthorizationResultHandler.cs`,
`Web/Infrastructure/ApiAwareCookieRedirects.cs`, `ApiService/Infrastructure/XUserIdAuthenticationHandler.cs`,
`Data/RbacSeeder.cs` and `Data/DefaultRoleAssignment.cs`. Rules for new code are in `ARCHITECTURE.md`, "Identity".

## 7. Account lifecycle

[ADR 0005](adr/0005-account-purge-hard-deletes-after-grace-period.md). Owned by `ApiService/Features/AccountLifecycle/`
and the Identity Razor Pages `Manage/DeletePersonalData`, `DeletionRequested`, `CancelDeletion` and `Lockout`.

```mermaid
stateDiagram-v2
    [*] --> Active : Register (UserConsent recorded, User role)
    Active --> Pending : POST api/account-deletion
    Pending --> Cancelled : POST api/account-deletion/cancel with the token, before PurgeAfter
    Cancelled --> Active : sign in again
    Pending --> Purged : AccountPurgeJob finds PurgeAfter <= now
    Purged --> [*]

    note right of Pending
        Grace Period: 30 days.
        Every AuditableEntity row (except AiUsageRecord)
        is soft-deleted with one shared DeletedAt stamp.
        LockoutEnd = MaxValue: sign-in shows the Lockout page.
        Only the cancel link in the email works.
        Refused with 409: AlreadyPending, LastAdmin.
    end note

    note right of Cancelled
        Only rows with DeletedAt == RequestedAt are restored;
        rows the user deleted earlier stay deleted.
        The token is single-use (Status is a concurrency token).
    end note

    note left of Purged
        DeletionRequest row stays as an audit record
        with UserId = deleted-account.
        Stories and comments stay as "Deleted User".
    end note
```

What happens on each side of the Grace Period:

```mermaid
sequenceDiagram
    autonumber
    participant U as User
    participant P as DeletePersonalData page (Web)
    participant A as AccountDeletionController (ApiService)
    participant S as AccountDeletionService
    participant DB as AppDbContext
    participant E as BrevoEmailSender
    participant J as AccountPurgeJob (daily, 15:00 UTC)
    participant PS as AccountPurgeService

    U->>P: confirm password (or type DELETE for Google-only accounts)
    P->>A: IAccountDeletionApi.RequestAsync, POST api/account-deletion
    A->>S: RequestAsync(userId)
    S->>DB: guards: AccountNotFound 404, AlreadyPending 409, LastAdmin 409 (AdminRoleLock)
    S->>DB: SoftDelete every AuditableEntity row except AiUsageRecord, DeletedAt = stamp
    S->>DB: LockoutEnd = MaxValue, rotate SecurityStamp, add DeletionRequest(Pending, PurgeAfter = stamp + 30 d)
    S-->>P: DeletionRequestAcceptedDto(cancelToken, purgeAfterUtc)
    P->>E: "Your account is scheduled for deletion" with /Identity/Account/CancelDeletion?token=
    P->>U: SignOutAsync, redirect to DeletionRequested
    Note over J,PS: 30 days later, Container Apps Job runs the ApiService image with --purge-only
    J->>PS: PurgeDueAsync()
    PS->>DB: Pending and PurgeAfter <= now, ordered by PurgeAfter then Id
    loop each due request, one SaveChangesAsync each
        PS->>DB: UncountLikes, AnonymizeCommunityContent (Deleted User, self-deleted content removed)
        PS->>DB: RemoveCachedDriveFiles, RemoveRows for every AuditableEntity except Story and Comment
        PS->>DB: remove AspNetUserRoles, Claims, Logins, Tokens, then the AspNetUsers row
        PS->>DB: Status = Purged, PurgedAt = now, UserId = deleted-account
    end
    PS-->>J: AccountPurgeSummary(Purged, Skipped, Failed) -> exit code
```

Drawn from `AccountDeletionService.cs`, `AccountPurgeService.cs`, `AccountPurgeJob.cs`,
`Web/Areas/Identity/Pages/Account/Manage/DeletePersonalData.cshtml.cs` and `infra/modules/purge-job.bicep`. No email is
sent on cancel or on purge. `DataExportController` (`GET api/data-export`) is the third member of the lifecycle and
returns the Account's live rows as one JSON file.

## 8. Budget data source

[ADR 0002](adr/0002-budget-tracker-separate-from-spreadsheet-data.md): budget figures come from either an imported
Google Sheet or the built-in Budget Tracker, and every reader goes through one Infrastructure seam so it never knows
which. All classes are in `ApiService/Infrastructure/`.

```mermaid
classDiagram
    direction TB

    class IBudgetMonthReader {
        <<interface>>
        +GetMonthsAsync(userId, year) List~BudgetMonthData~
        +GetMonthAsync(userId, year, month) BudgetMonthData
        +GetTrendRecordsAsync(userId, year) List~BudgetTrendRecordData~
        +GetTransactionsAsync(userId, from, to, take) List~BudgetTransactionData~
        +GetAvailableYearsAsync(userId) List~int~
    }
    class IBudgetDataProvider {
        <<interface>>
        +GetActiveSourceAsync(userId) BudgetDataSource
    }
    class BudgetDataProvider {
        -cachedSources per user, cleared on PreferenceChanged
        -GetReaderAsync(userId) IBudgetMonthReader
    }
    class SheetBudgetMonthReader {
        reads SpreadsheetBudgetSnapshots
        reads SpreadsheetTransactionRecords
        parses CategoryBreakdownJson
    }
    class TrackerBudgetMonthReader {
        reads MonthlyBudgetPlans and Lines
        reads BudgetTransactions, BudgetCategories
        uses MonthlyBudgetCalculator.BuildRows
    }
    class IBudgetDataSourceService {
        <<interface>>
        +PreferenceChanged event
        +GetActiveSourceAsync(userId) BudgetDataSource
        +GetDataSourcePreferenceAsync(userId) BudgetDataSourceDto
        +SetDataSourcePreferenceAsync(userId, request) BudgetDataSourceDto
        +GetDataSummariesAsync(userId) List~BudgetDataSourceSummaryDto~
        +ForgetRemovedSourceAsync(userId, source, deletedAt)
    }
    class BudgetDataSourceService {
        reads UserBudgetSourcePreferences
    }
    class ProviderConsumers["Consumers of IBudgetDataProvider"] {
        DashboardService
        SpreadsheetAnalysisService
        AiAssistantService
        BudgetService, for alerts
    }
    class SourceConsumers["Consumers of IBudgetDataSourceService"] {
        BudgetTrackerController
        SpreadsheetAnalysisController
        SearchService
        GoogleSheetRemovalService
        BudgetTrackerRemovalService
    }

    IBudgetDataProvider --|> IBudgetMonthReader
    BudgetDataProvider ..|> IBudgetDataProvider
    SheetBudgetMonthReader ..|> IBudgetMonthReader
    TrackerBudgetMonthReader ..|> IBudgetMonthReader
    BudgetDataProvider --> SheetBudgetMonthReader
    BudgetDataProvider --> TrackerBudgetMonthReader
    BudgetDataProvider --> IBudgetDataSourceService : GetActiveSourceAsync, PreferenceChanged
    BudgetDataSourceService ..|> IBudgetDataSourceService
    ProviderConsumers ..> IBudgetDataProvider
    SourceConsumers ..> IBudgetDataSourceService
```

How the active source is chosen on each read:

```mermaid
flowchart LR
    q["GetActiveSourceAsync(userId)"] --> pref{"UserBudgetSourcePreference<br/>row for the user?"}
    pref -- "yes" --> use["that Source<br/>IsDefault = false"]
    pref -- "no" --> any{"any SpreadsheetBudgetSnapshot<br/>or SpreadsheetTransactionRecord?"}
    any -- "yes" --> gs["GoogleSheet<br/>IsDefault = true"]
    any -- "no" --> bt["BudgetTracker<br/>IsDefault = true"]
    use & gs & bt --> reader{"BudgetTracker?"}
    reader -- "yes" --> tr["TrackerBudgetMonthReader"]
    reader -- "no" --> sr["SheetBudgetMonthReader"]
```

Drawn from `IBudgetMonthReader.cs`, `BudgetDataProvider.cs`, `BudgetDataSourceService.cs` and
`InfrastructureServiceExtensions.cs`. The provider never falls back to the other source. Removing a source
(`GoogleSheetRemovalService`, `BudgetTrackerRemovalService`) soft-deletes its rows and calls `ForgetRemovedSourceAsync`.

## 9. Notifications

Notifications are Infrastructure because three Features raise them. The browser never polls: the bell loads the unread
count once per page and the list on click.

```mermaid
flowchart LR
    subgraph producers["Producers: ApiService Features"]
        bp["BudgetService.SaveBudgetAsync<br/>CheckBudgetAlerts: 80% warning, 100% exceeded"]
        sg["SavingsGoalService, on every Contribution change<br/>milestones at 50, 75, 100%, each once per goal"]
        sa["SpreadsheetAnalysisService.SyncSpreadsheetBudgetAsync<br/>sync completed or failed"]
    end

    ns["INotificationService (Infrastructure)<br/>Create, Get latest 50, UnreadCount,<br/>MarkAsRead, MarkAllAsRead"]
    ent[("Notification<br/>Type, Title, Message (English),<br/>TitleKey, MessageKey, Arguments, IsRead")]
    nc["NotificationController (ApiService)<br/>api/Notification"]
    napi["INotificationApi (Refit)"]
    wnc["NotificationController (Web)<br/>api/Notification, JsonApiErrors,<br/>antiforgery on the POSTs;<br/>NotificationText renders keys in the reader's language"]
    js["core/notifications.js<br/>unread-count once per page load,<br/>list on bell click, no polling"]

    bp -- "BudgetAlert" --> ns
    sg -- "SavingsMilestone" --> ns
    sa -- "SyncStatus" --> ns
    ns --> ent
    nc --> ns
    napi --> nc
    wnc --> napi
    js -- "fetch" --> wnc
```

Drawn from `ApiService/Infrastructure/INotificationService.cs`, `NotificationController.cs` (both apps) and
`Web/wwwroot/js/core/notifications.js`. The Web exposes no create endpoint; producers write through the Service, each
with a `TitleKey`, `MessageKey` and named `Arguments` beside the English text ([16](#16-localization)).

## 10. Research pipeline

The Research dashboard never calls a data source from a request. A daily job rebuilds one `ResearchSnapshot` row per
dataset from official public sources ([ADR 0012](adr/0012-research-data-from-official-public-sources.md)); the pages
read those rows. Security identifiers are used in memory during a refresh and never stored ([ADR
0010](adr/0010-research-never-stores-security-identifiers.md)).

```mermaid
flowchart LR
    job["ResearchRefreshJob<br/>ApiService image, --refresh-research<br/>Container Apps Job, daily 10:00 UTC"] --> svc["IResearchService.RefreshAsync(sources)"]

    subgraph refreshers["IResearchDatasetRefresher, in registration order; a failure keeps that dataset's previous snapshot"]
        direction TB
        r1["SectorsRefresher"]
        r2["FundsRefresher"]
        r3["FundOverlapRefresher"]
        r4["FundExposureRefresher"]
        r5["FundIndustriesRefresher"]
        r6["FundReturnsRefresher"]
        r7["FundFactsRefresher"]
        r8["MacroRefresher"]
        r9["WorldRefresher"]
    end

    subgraph adapters["Adapters: official public sources"]
        sec["ISecEdgarClient<br/>SEC EDGAR: XBRL frames, submissions, N-PORT<br/>SecRequestGate 5 requests/s"]
        fed["IFedRatesClient<br/>Federal Reserve H.15"]
        us["IUsAgencyClient<br/>BLS CPI-U, Treasury bill rates"]
        wb["IWorldBankClient<br/>World Bank: GDP, market cap"]
    end

    svc --> refreshers
    r1 & r2 & r7 --> sec
    r3 & r4 & r5 --> lfr["LatestFundReports<br/>latest N-PORT per fund"] --> sec
    r5 --> sec
    r6 --> sec & fed
    r8 --> fed & us
    r9 --> wb
    refreshers --> store["ResearchSnapshotStore.StoreAsync<br/>upsert one row per Dataset, only on success"]
    store --> snap[("ResearchSnapshot<br/>Dataset, Payload JSON, SourceAsOf, BuiltAt")]
```

The read path. Each tab fails on its own, and `IResearchApi` is the one Refit client registered without retries on 503,
because a 503 here means "never loaded", not "try again".

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser
    participant WC as ResearchController (Web)
    participant RA as IResearchApi (no retry on 503)
    participant AC as ResearchController (ApiService)
    participant RS as IResearchService
    participant ST as ResearchSnapshotStore
    participant DB as ResearchSnapshots

    B->>WC: GET /Research?tab=
    par eight datasets in parallel, Task.WhenAll
        WC->>RA: GetSectorsAsync ... GetFundExposureAsync
        RA->>AC: GET api/Research/sectors, funds, funds/overlap, ...
        AC->>RS: GetSectorsAsync() etc.
        RS->>ST: ReadAsync(dataset)
        ST->>DB: one row by Dataset, deserialise Payload
        alt snapshot exists
            AC-->>RA: 200 snapshot DTO
        else never loaded
            AC-->>RA: 503 ResearchUnavailableDto(Reason)
        end
    end
    WC-->>B: ResearchViewModel, a failed dataset shows _UnavailableState in its tab only
    B->>WC: later, on demand: FundIndustries (~600 KB), Suggest, Company, Releases
    WC->>RA: CompaniesController: live SEC lookups, 30 per minute per Account, cached 24 h
```

Drawn from `ApiService/Features/Research/ResearchFeature.cs`, `ResearchService.cs`, `ResearchSnapshotStore.cs`,
`ResearchRefreshJob.cs`, `Adapters/`, `Web/Controllers/ResearchController.cs` and
`infra/modules/research-refresh-job.bicep`. The Companies tab is the exception that calls SEC EDGAR at request time,
through `CompanyProfileService` with in-memory caches (`CompanyTickerIndexCache`, `KeptWhileUnreachableCache`).

## 11. Unavailable State and keeping the ApiService awake

The Unavailable State is a view pattern, not middleware: a controller catches any failure from its Refit call and
renders `Views/Shared/Unavailable.cshtml` (or the `_UnavailableState` partial inside a page) with a plain Retry link.
JSON actions get the same treatment from an exception filter. [ADR
0011](adr/0011-warm-window-instead-of-always-on-replicas.md) explains why the ApiService is sometimes asleep.

```mermaid
flowchart TB
    req["Web controller calls a Refit client"] --> ok{"Result?"}
    ok -- "2xx" --> good["View(model) or JSON"]
    ok -- "ApiException (4xx or 5xx)" --> kind{"Kind of action"}
    ok -- "HttpRequestException, TimeoutException,<br/>TaskCanceledException<br/>(ApiService asleep, 15 s timeout, resilience exhausted)" --> kind

    kind -- "view action" --> uv["this.UnavailableView(title, heading, retryController)<br/>Views/Shared/Unavailable.cshtml + _UnavailableState<br/>Retry is a plain GET"]
    kind -- "JSON action with JsonApiErrors" --> jf["JsonApiErrorsFilter<br/>ApiException: same status, user-safe message<br/>unreachable: 502 UnavailableMessage<br/>anything else: 500 UnexpectedMessage"]
    kind -- "Research tab" --> rt["that tab's _UnavailableState only;<br/>the other tabs render"]

    subgraph warm["Keeping the ApiService awake"]
        keda["KEDA cron rule cron-business-hours<br/>1 replica 08:00 to 22:00 America/Los_Angeles<br/>minReplicas 0 otherwise (about 25 s cold start)"]
        w1["ApiServiceStartupWarmer (IHostedService)<br/>GET /alive when a Web replica starts"]
        w2["ApiServiceWarmer from the Login page<br/>at most once per 2 minutes"]
    end
    warm -.-> req
```

Drawn from `Web/Infrastructure/UnavailableStateExtensions.cs`, `JsonApiErrorsAttribute.cs`, `ApiErrorMessages.cs`,
`ApiServiceWarmer.cs`, `ApiServiceStartupWarmer.cs` and `infra/modules/api-service.bicep`. Controllers that use the
pattern: Budget, Community, Networth, SavingsGoals, Admin (inbox), Dashboard (its own `Unavailable` view) and Research.

## 12. AI Assistant

The chat widget on every page. The ApiService guards the OpenAI call three times before it is made: scope, quota and
message size. Without an `OpenAI:ApiKey` no `IChatClient` is registered and the Service returns a fixed "unavailable"
reply. Setup is in [`AI_ASSISTANT_SETUP.md`](AI_ASSISTANT_SETUP.md).

```mermaid
sequenceDiagram
    autonumber
    participant JS as core/ai-chat-widget.js
    participant WC as AiAssistantController (Web, JSON)
    participant AC as AiAssistantController (ApiService)
    participant SF as IAiScopeFilter
    participant Q as IAiQuotaService
    participant S as AiAssistantService
    participant P as IBudgetDataProvider / AppDbContext
    participant O as IChatClient (OpenAI gpt-4o-mini)

    JS->>WC: POST /api/AiAssistant/chat: message, last 10 history items, antiforgery token
    WC->>AC: IAiAssistantApi.SendChatMessageAsync (30 s timeout)
    AC->>AC: empty message is 400, over 2000 characters is 400
    AC->>SF: Evaluate(message)
    alt greeting or off-topic
        SF-->>AC: canned reply, no model call
    else in scope
        AC->>Q: CheckQuotaAsync(userId, "chat")
        alt denied: 30 per day per Account, or 10 USD per month overall
            Q-->>AC: DenialReason, 429
        else allowed
            AC->>S: SendChatMessageAsync(userId, request)
            S->>P: financial snapshot: NetWorthParameters, projection, allocations, savings goals
            S->>P: UserPreference.Language (English when unset)
            S->>O: GetResponseAsync(system prompt with reply Language + snapshot + history, 4 tools, MaxOutputTokens 400)
            loop function calls, at most 3 iterations
                O-->>S: get_budget_summary, get_recent_transactions, get_savings_goals, get_net_worth_details
                S->>P: read
                S-->>O: tool result
            end
            O-->>S: reply and token usage
            S->>Q: RecordUsageAsync: AiUsageRecord with EstimatedCostUsd
            S-->>AC: AiChatResponseDto
        end
    end
    AC-->>WC: reply + Disclaimer (+ BudgetWarning above 90% of the monthly budget)
    WC-->>JS: JSON
```

Drawn from `ApiService/Features/AiAssistant/` and `Adapters/OpenAiChatClientRegistration.cs` (official OpenAI .NET
SDK wrapped by `Microsoft.Extensions.AI`). The `summarize` endpoint (news page) follows the same path with one more
guard: the unkeyed `IRateLimiterService`, 10 calls per minute per Account.

## 13. Google Drive and Spreadsheet Analysis

The one place where a Feature depends on another: Analysis imports spreadsheets through GoogleDrive's public
`ISpreadsheetReader`, interface only, as [ADR 0007](adr/0007-feature-folders-with-interface-only-dependencies.md)
requires. There is no OAuth: a connection stores the user's own Google API key, encrypted by
`Data/Helpers/CredentialProtector.cs`.

```mermaid
classDiagram
    direction LR

    namespace GoogleDrive {
        class GoogleDriveController {
            api/GoogleDrive
            +explore, connections CRUD, sync, reauth
            +spreadsheet-report, spreadsheet-transactions
        }
        class IDriveConnectionService {
            <<interface>>
            +GetFolderFilesAsync(request)
            +GetUserConnectionsAsync(userId)
            +GetConnectionFilesAsync(connectionId, userId, forceRefresh)
            +ConnectDriveAsync(userId, request)
            +SyncConnectionAsync(connectionId, userId, forceRefresh)
            +ReauthConnectionAsync(connectionId, userId, newApiKey)
            +DeleteConnectionAsync(connectionId, userId)
        }
        class DriveConnectionService {
            HttpClient to Drive v3 files
            CredentialProtector encrypt and mask
            file cache fresh for 30 min
        }
        class IGoogleDriveFileSource {
            <<internal interface>>
            +DownloadAsync(userId, fileId, fileName, connectionId) IAsyncEnumerable~SpreadsheetDownload~
        }
        class GoogleDriveFileSource {
            <<Adapter>>
            tries XLSX export, CSV export, then alt media
        }
        class ISpreadsheetReader {
            <<interface>>
            +GetSpreadsheetBudgetReportAsync(userId, fileId, fileName, connectionId) MonthlyBudgetReportDto
            +GetSpreadsheetTransactionsAsync(userId, fileId, fileName, connectionId) SpreadsheetTransactionsDto
        }
        class SpreadsheetReaderService {
            no DbContext
        }
        class BudgetSpreadsheetParser {
            <<static>>
            +ParseXlsxBudgetReport()
            +ParseSpreadsheetBudgetReport()
            +ParseXlsxTransactions()
            +ParseCsvTransactions()
        }
    }

    namespace Analysis {
        class SpreadsheetAnalysisController {
            api/SpreadsheetAnalysis
            +dashboard, sync, data, years, months
        }
        class ISpreadsheetAnalysisService {
            <<interface>>
            +GetSummaryStatisticsAsync(userId, year, month)
            +GetSpendingVelocityAsync(userId, year, month)
            +GetTrendDataAsync(userId, year)
            +GetCategoryInsightsAsync(userId, year, month)
            +GetBudgetVarianceAsync(userId, year, month)
            +SyncSpreadsheetBudgetAsync(userId, connectionId, fileId, fileName, overwrite) SyncSpreadsheetBudgetResult
        }
        class SpreadsheetAnalysisService {
            <<internal sealed>>
        }
        class IGoogleSheetRemovalService {
            <<interface>>
            +RemoveAsync(userId) BudgetDataSourceSummaryDto
        }
    }

    namespace Infrastructure {
        class IBudgetDataProvider
        class INotificationService
        class IBudgetDataSourceService
    }

    namespace Data {
        class GoogleDriveConnection
        class GoogleDriveCachedFile
        class SpreadsheetBudgetSnapshot
        class SpreadsheetTransactionRecord
    }

    GoogleDriveController --> IDriveConnectionService
    GoogleDriveController --> ISpreadsheetReader
    DriveConnectionService ..|> IDriveConnectionService
    DriveConnectionService --> GoogleDriveConnection
    DriveConnectionService --> GoogleDriveCachedFile
    GoogleDriveFileSource ..|> IGoogleDriveFileSource
    GoogleDriveFileSource --> GoogleDriveConnection : decrypts the key
    SpreadsheetReaderService ..|> ISpreadsheetReader
    SpreadsheetReaderService --> IGoogleDriveFileSource
    SpreadsheetReaderService --> BudgetSpreadsheetParser
    SpreadsheetAnalysisController --> ISpreadsheetAnalysisService
    SpreadsheetAnalysisController --> ISpreadsheetReader : cross-Feature, interface only
    SpreadsheetAnalysisController --> IBudgetDataSourceService
    SpreadsheetAnalysisController --> IGoogleSheetRemovalService
    SpreadsheetAnalysisService ..|> ISpreadsheetAnalysisService
    SpreadsheetAnalysisService --> ISpreadsheetReader : cross-Feature, interface only
    SpreadsheetAnalysisService --> IBudgetDataProvider
    SpreadsheetAnalysisService --> INotificationService : SyncStatus
    SpreadsheetAnalysisService --> SpreadsheetBudgetSnapshot
    SpreadsheetAnalysisService --> SpreadsheetTransactionRecord
```

Connecting a folder, then importing a sheet into budget snapshots. Imports are always user-triggered; nothing syncs on a
schedule.

```mermaid
sequenceDiagram
    autonumber
    participant U as User
    participant GW as GoogleDriveController (Web)
    participant GA as GoogleDriveController (ApiService)
    participant DC as DriveConnectionService
    participant G as Google Drive API v3
    participant AW as AnalysisController (Web)
    participant AA as SpreadsheetAnalysisController (ApiService)
    participant AS as SpreadsheetAnalysisService
    participant SR as SpreadsheetReaderService
    participant DB as AppDbContext

    U->>GW: folder URL + Google API key
    GW->>GA: POST api/GoogleDrive/connections
    GA->>DC: ConnectDriveAsync(userId, request)
    DC->>DB: GoogleDriveConnection: EncryptedApiKey, MaskedApiKey, SyncStatus = Pending
    GW->>GA: GET api/GoogleDrive/connections/id (file list)
    GA->>DC: GetConnectionFilesAsync(connectionId, userId, forceRefresh)
    alt cache older than 30 min, or forceRefresh
        DC->>G: files?q=parents in folder, pageSize 100, key
        G-->>DC: file list (401, 403 or 400 marks the connection NeedsReauth, IsValid = false)
        DC->>DB: upsert GoogleDriveCachedFile rows, SyncStatus = Synced, LastSyncedAtUtc
    end
    U->>AW: pick a sheet, Sync
    AW->>AA: POST api/SpreadsheetAnalysis/sync
    AA->>AS: SyncSpreadsheetBudgetAsync(userId, connectionId, fileId, fileName, overwrite)
    AS->>SR: GetSpreadsheetBudgetReportAsync
    SR->>G: export as XLSX, else CSV, else alt=media
    SR-->>AS: MonthlyBudgetReportDto (BudgetSpreadsheetParser)
    alt months already imported and overwrite = false
        AS-->>AW: HasConflict: SyncConflict view, then ConfirmOverwrite
    else no conflict, or overwrite
        AS->>DB: remove conflicting SpreadsheetBudgetSnapshot rows, add one per month
        AS->>SR: GetSpreadsheetTransactionsAsync
        AS->>DB: add SpreadsheetTransactionRecord rows linked by SnapshotId
        AS->>AS: INotificationService: SyncStatus completed or failed
    end
```

Drawn from `ApiService/Features/GoogleDrive/DriveConnectionService.cs`, `Adapters/GoogleDriveFileSource.cs`,
`SpreadsheetReaderService.cs`, `ApiService/Features/Analysis/SpreadsheetAnalysisService.cs` and the two Web controllers.
Removing the imported data (`IGoogleSheetRemovalService`) soft-deletes the snapshots and records but keeps the
connection.

## 14. Startup and pipelines

`Program.cs` in each app is a short list; the detail is in extension methods. The ApiService image is also the Purge
and Research jobs, chosen by a command-line flag.

```mermaid
flowchart TB
    start["ApiService Program.cs"] --> di["DI: ServiceDefaults (OpenTelemetry, health, resilience), Serilog,<br/>AppDbContext, Identity, XUserId authentication + RequireAdmin Policy,<br/>AddApiServiceInfrastructure() then 15 Add...Feature() calls,<br/>Controllers, FluentValidation (internal types), ProblemDetails, OpenAPI"]
    di --> pipe["Pipeline: ExceptionHandler, Serilog request logging, /health and /alive,<br/>/ version banner, OpenAPI + Scalar (Development or ApiDocs:Enabled),<br/>HttpsRedirection, Authentication, Authorization, MapControllers"]
    pipe --> mode{"args or environment"}
    mode -- "--purge-only<br/>PURGE_ONLY=true" --> purge["AccountPurgeJob.RunAsync<br/>migrate + seed, PurgeDueAsync, exit code"]
    mode -- "--refresh-research<br/>REFRESH_RESEARCH=true" --> research["ResearchRefreshJob.RunAsync<br/>migrate + seed, RefreshAsync, exit code"]
    mode -- "--migrate-only<br/>MIGRATE_ONLY=true" --> migrate["MigrateAndSeedDatabaseAsync, then exit<br/>(CI runs this before each deploy)"]
    mode -- "otherwise" --> run["ResearchFixtureSeeder (E2E only), then MigrateAndRunAsync:<br/>Migrate, SeedCommunityData, RbacSeeder, RunAsync"]
```

```mermaid
flowchart LR
    a["Serilog request logging"] --> b["/health, /alive"] --> c["ExceptionHandler + HSTS<br/>(outside Development)"] --> d["ForwardedHeaders"] --> e["HttpsRedirection"] --> f["RequestLocalization<br/>cookie, Accept-Language, en<br/>(formatting en-US or vi-VN)"] --> g["Routing"] --> h["Authentication<br/>Identity cookie, Google"] --> i["Authorization<br/>AdminNotFound handler"] --> j["Static assets"] --> k["MVC default route<br/>+ Razor Pages"] --> l["MigrateAndRunAsync"]
```

Drawn from `ApiService/Program.cs`, `Web/Program.cs`, `Data/DatabaseMigrationExtensions.cs` and
`ServiceDefaults/Extensions.cs`. Web DI, in order: ServiceDefaults and Serilog, `AppDbContext` and data protection,
Identity (cookie 14 days sliding, `ApiAwareCookieRedirects`, optional Google), the `RequireAdmin` Policy and the
404 handler, `AddInternalApiClients`, Brevo email, `IUserConsentService`, `TimeProvider`, forwarded headers,
`AddAppLocalization` ([16](#16-localization)), Razor Pages and MVC with a global antiforgery filter, view and
DataAnnotations localization.

## 15. Scheduled and background work

There are no `BackgroundService`s, timers or job libraries in either app. Everything that runs without a request is
listed here.

| What | Where | When | Purpose |
|---|---|---|---|
| Account Purge | Container Apps Job `infra/modules/purge-job.bicep`, ApiService image with `--purge-only` (`AccountPurgeJob`) | daily, cron `0 15 * * *` UTC, 900 s timeout | Hard-delete Accounts whose Grace Period ended ([7](#7-account-lifecycle)) |
| Research refresh | Container Apps Job `infra/modules/research-refresh-job.bicep`, ApiService image with `--refresh-research` (`ResearchRefreshJob`) | daily, cron `0 10 * * *` UTC, 1800 s timeout | Rebuild every `ResearchSnapshot` from official sources ([10](#10-research-pipeline)) |
| Migrations | ApiService image with `--migrate-only` (`Data/DatabaseMigrationExtensions.cs`) | each deploy, from `.github/workflows/deploy-azure.yml` | Apply EF Core migrations and seed, then exit |
| Warm window | KEDA cron rule `cron-business-hours` in `api-service.bicep` and `web-service.bicep` | 08:00 to 22:00 America/Los_Angeles, production only | Keep one replica of each app warm ([ADR 0011](adr/0011-warm-window-instead-of-always-on-replicas.md)) |
| `ApiServiceStartupWarmer` | `Web/Infrastructure/ApiServiceStartupWarmer.cs`, the only `IHostedService` | once per Web replica start | `GET /alive` on the ApiService so both wake together |
| `ApiServiceWarmer` | `Web/Infrastructure/ApiServiceWarmer.cs`, called from the Login page | at most once per 2 minutes | Same ping, before the first authenticated request |
| News auto-refresh | `Web/wwwroot/js/news/news.js` | every 5 minutes while the News page is open | Re-fetch `/News/Refresh`; the ApiService caches feeds for 5 minutes in `IMemoryCache` |
| SEC request gate | `ApiService/Features/Research/Adapters/SecRequestGate.cs` | during a refresh or company lookup | Throttle SEC EDGAR calls to 5 per second |

## 16. Localization

The Web app speaks English and Vietnamese ([ADR 0015](adr/0015-json-localization-with-custom-string-localizer.md),
[spec](specs/localization-and-multi-currency.md)). One flat JSON file per language holds every string; one in-house
localizer serves Razor, DataAnnotations, model binding, Identity errors and the JS island. The formatting culture
follows the Language (`en` → `en-US`, `vi` → `vi-VN`) for separators, dates and typed amounts; the currency symbol
always comes from the amount's currency (ADR 0016). ApiService is not localized and formats in `en-US`.

```mermaid
flowchart TB
    req["Request"] --> rl{"RequestLocalization<br/>(no query-string provider)"}
    rl -- "1. .AspNetCore.Culture cookie" --> ui["LanguageCultureProvider<br/>CurrentUICulture en or vi<br/>CurrentCulture en-US or vi-VN"]
    rl -- "2. Accept-Language" --> ui
    rl -- "3. otherwise" --> ui
    ui --> loc["IStringLocalizer (JsonStringLocalizer)<br/>uiCulture, parent, then en<br/>3401 LocalizationFallback, 3402 LocalizationNotFound"]
    cat["JsonLocalizationCatalog (singleton)<br/>Resources/i18n/en.json, vi.json<br/>loaded once at startup"] --> loc
    loc --> razor["Razor: @L[key], L.Named, L.NamedHtml, L.Plural<br/>(_ViewImports injects L)"]
    loc --> da["DataAnnotations ErrorMessage and Display(Name) keys<br/>ModelBindingMessagesSetup (11 messages)"]
    loc --> idn["LocalizedIdentityErrorDescriber<br/>Identity.Error.Code"]
    loc --> js["js-strings tag helper<br/>script data-i18n-strings JSON islands:<br/>the layout's Shell.Js. and the page's own"] --> pfi["core/i18n.js<br/>merges the islands; PFI18n.t, PFI18n.plural, html lang"]
    sw["_LanguageSwitcher<br/>top bar (anonymous) and account menu"] -- "POST /Language/Set + antiforgery" --> lc["LanguageController<br/>stores the choice when signed in,<br/>LanguageCookie.Write, LocalRedirect"]
    lc -. "cookie c=vi-VN, uic=vi" .-> rl
```

Money, numbers and typed amounts, on the server and in the browser:

```mermaid
flowchart LR
    cur["SupportedCurrencies<br/>(Shared/Constants)<br/>code, minor units, symbol"] --> mf["MoneyFormatter<br/>(Shared/Helpers)<br/>Format, FormatWhole, FormatAmount"]
    cul["CurrentCulture<br/>separators, symbol position"] --> mf
    mf --> views["Razor views, Web services,<br/>ApiService text"]
    ban["CurrencyFormatBanTests<br/>no 'C' format elsewhere"] -.-> views
    cul --> html["html data-culture"] --> pfi2["core/i18n.js<br/>PFI18n.formatMoney, formatNumber,<br/>formatDate, parseNumber"]
    pfi2 --> charts["page modules,<br/>Chart.js and D3 axes and tooltips"]
    pfi2 --> input["input data-money-input<br/>as-you-type grouping"]
    input -- "form post 1.234,56" --> bind["CultureDecimalModelBinder<br/>CultureDecimalParser (strict grouping)"]
    bind -- "1234.56, or a ModelState error" --> ctrl["Web controller"]
```

The Account's Language is stored, and the cookie only mirrors it:

```mermaid
flowchart LR
    reg["Register, ExternalLogin<br/>(new Account)"] -- "seed: request's Language" --> ups["IUserPreferenceService<br/>(Web/Services, AppDbContext)"]
    lc["LanguageController<br/>(signed in)"] -- "set" --> ups
    page["Account settings › Language<br/>Manage/Language"] -- "set + cookie" --> ups
    ups --> row[("UserPreference<br/>Language")]
    signin["OnSignedIn<br/>LanguageSignInEvents:<br/>password, Google, 2FA, refresh"] -- "read" --> ups
    signin -- "LanguageCookie.Write" --> cookie[".AspNetCore.Culture"]
    mail["AccountEmailService<br/>confirm, email change,<br/>reset password, deletion"] -- "read recipient's Language" --> ups
    mail -- "LanguageScope + EmailTemplateHelper" --> brevo["IEmailSender (Brevo)"]
    ies["IdentityEmailSender<br/>(IEmailSender&lt;IdentityUser&gt;)"] --> mail
```

```mermaid
flowchart LR
    en["en.json<br/>(written by hand)"] --> tr["npm run i18n:translate<br/>missing or stale keys only<br/>notranslate placeholders, glossary.vi.json"]
    lock["i18n.lock.json<br/>hash of the English per key"] <--> tr
    tr -- "textType=html, 429 back-off" --> az["Azure AI Translator F0"]
    az --> tr
    tr -- "placeholders verified" --> vi["vi.json<br/>commit: i18n: translate N keys (vi)"]
    vi --> ci["CI lint-js job: i18n-report<br/>missing, stale, unreferenced (reported)<br/>bad JSON, placeholder mismatch (fails)"]
    en --> ci
```

ApiService is still language-free, but its rejections now carry a localization key and typed arguments
([ADR 0017](adr/0017-api-errors-carry-localization-keys.md), [spec](specs/localized-api-errors.md)): a validator rule
sets `.WithErrorCode(ApiErrorKeys…)` and a service throws `ApiErrorException`, both rendered as one `ApiErrorDto`.
Web resolves each entry through the localizer and formats the arguments in the request culture; the English
`message` is only a fallback for a key missing from `en.json`. Web's own fallbacks are `Errors.*` keys.

```mermaid
flowchart LR
    src["Validator or service<br/>(ApiService Feature)"] --> raise["ApiErrorException<br/>or ValidationResult.ToApiError()<br/>key, English, field, ErrorArgs"]
    raise --> filt["ApiErrorExceptionFilter<br/>(ApiService/Infrastructure)<br/>the fitting 4xx"]
    filt -- "HTTP body" --> dto["ApiErrorDto<br/>errors: {key, message, field, args}"]
    dto --> aem["Web ApiErrorMessages<br/>FromApiException, ForUser, Fields"]
    aem --> loc["IStringLocalizer en.json, vi.json<br/>+ ApiErrorArgFormat<br/>Text, Number, Money (MoneyFormatter)"]
    loc --> out["page field errors<br/>or JSON {error}"]
```

Drawn from `Web/Infrastructure/Localization/`, `Shared/Helpers/MoneyFormatter.cs`, `Shared/Constants/SupportedCurrencies.cs`,
`Web/Services/UserPreferenceService.cs`, `AccountEmailService.cs`,
`Web/TagHelpers/JsStringsTagHelper.cs`, `Web/Controllers/LanguageController.cs`, `Web/Views/Shared/_LanguageSwitcher.cshtml`, `Web/wwwroot/js/core/i18n.js`,
`Web/tools/i18n-translate.js`, `Web/tools/i18n-report.js`, `ApiService/Infrastructure/ApiErrorException*.cs`,
`ErrorArgs.cs`, `ValidationResultExtensions.cs`, `Web/Infrastructure/ApiErrorMessages.cs`, `ApiErrorArgFormat.cs` and the `lint-js` job of `.github/workflows/deploy-azure.yml`.
A string that is not shaped like a key (English still on attributes of pages not yet translated) is shown as written,
without the 3402 warning.

## Keeping this page current

- Hand-edit the Mermaid; there is no generator. Render a changed block before committing: GitHub's preview, Rider's
  Markdown preview, or `npx @mermaid-js/mermaid-cli -i block.mmd -o block.svg` from a scratch folder (do not add it to
  `package.json`).
- A new Entity goes into the matching ER diagram in [4](#4-data-model) and, if user-owned, into the Purge list in
  `AccountPurgeService` automatically (it enumerates `AuditableEntity` types), so only the diagram needs you.
- A new Feature gets a row in [3](#3-feature-map). It needs its own diagram only when it deviates from
  [2](#2-anatomy-of-a-feature): external adapters, background jobs, cross-Feature dependencies.
- A new Infrastructure abstraction or scheduled job gets a diagram or a row in [15](#15-scheduled-and-background-work).
- Update the "Last verified against" version at the top when you have checked the whole page, not when you change one
  diagram.
