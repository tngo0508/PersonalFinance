---
sessionId: session-261016-170344-oi7o
---

# Navbar Redesign Spec

## Problem Statement

The current navigation is a single horizontal top navbar with 8 flat items (Google Drive, Analysis, Net Worth, Finance
News, Budget Planner, Savings Goals, Investment Calculator, Community). This is cluttered, hard to scan, and doesn't
scale as new features are added. The brand logo links to Google Drive, which feels arbitrary — there's no home page or
Dashboard. On mobile, the hamburger menu shows all 8 items flat with no grouping. Users need an intuitive, scalable way
to navigate the web app.

## Solution

Replace the top navbar with a left sidebar navigation that groups items into three categories (Overview, My Finances,
Resources), add a Dashboard as the new home page, and support the experience with global search and a notification
system. On mobile, replace the hamburger menu with a bottom navigation bar. The AI Assistant remains as a floating
button, unchanged.

## User Stories

1. As a user, I want a Dashboard home page so I can see my financial overview at a glance
2. As a user, I want grouped navigation in a sidebar so I can find features quickly without scanning 8 flat items
3. As a user, I want to collapse the sidebar to icons-only so I can maximize content area on desktop
4. As a user, I want a bottom navigation bar on mobile so I can navigate easily with one hand
5. As a user, I want to search across my transactions, budgets, and savings goals so I can find data quickly
6. As a user, I want to be notified when my budget exceeds 80% or 100% so I can take action
7. As a user, I want to be notified when a savings goal reaches a milestone so I feel motivated
8. As a user, I want to be notified when Google Drive sync succeeds or fails so I know my data is current
9. As a user, I want the current page highlighted in the sidebar so I know where I am
10. As a user, I want the brand logo to link to the Dashboard so I can always return home
11. As an anonymous user, I want to see login and register options so I can create an account
12. As a user, I want empty states on the Dashboard so I understand what to do when I have no data
13. As a user, I want loading states on the Dashboard so I know data is being fetched
14. As a user, I want search results labeled by source so I know whether a result is a transaction, budget item, or
    savings goal
15. As a user, I want to mark notifications as read so I can track what I've addressed
16. As a user, I want the notification bell to show an unread count so I know how many alerts I have
17. As a user, I want all existing features to continue working so my workflow isn't disrupted
18. As a user, I want keyboard navigation in the sidebar so I can navigate without a mouse
19. As a user, I want the sidebar to be scrollable so I can access all items even if many are added
20. As a user, I want charts on the Dashboard to be interactive (zoom, tooltips) so I can explore my data

## Implementation Decisions

### Navigation Structure

- **Left sidebar** (240px expanded, 60px collapsed with toggle button) as the primary navigation on desktop
- **Three navigation groups**: Overview (Dashboard), My Finances (Net Worth, Budget, Savings Goals, Analysis), Resources
  (Investment Calculator, Finance News, Community, Google Drive)
- **Minimal top bar** containing global search, notification bell, and user profile dropdown
- **Mobile bottom nav** with 5 items (Dashboard, My Finances dropdown, Resources dropdown, Community, Profile) with
  Lucide icons and labels
- **AI Assistant** remains as a floating button in the bottom-right corner, unchanged

### Layout Redesign

- Replace `_Layout.cshtml` top navbar with a sidebar layout structure
- New `sidebar.css` for sidebar, top bar, and mobile nav styles
- New `sidebar.js` for sidebar toggle logic and mobile behavior
- Update `site.css` for layout adjustments (main content margin for sidebar)
- All existing routes, controllers, and views must continue to work — the layout change is cosmetic only

### Dashboard Feature

- New `DashboardController` in `PersonalFinance.Web/Controllers/` (MVC, calls `IDashboardApi` via Refit)
- New `DashboardService` in `PersonalFinance.ApiService/Dashboard/Services/` with caching and parallel data aggregation
- New API controller in `PersonalFinance.ApiService/Dashboard/` following CQRS pattern (per ADR-0001)
- Grid of cards layout: Net Worth Trend (wide), Budget Status, Savings Goals, Recent Activity, Quick Actions
- ApexCharts for modern, interactive charts (zoom, tooltips, animations)
- New `IDashboardApi` contract in `PersonalFinance.Shared/Contracts/`
- New DTOs in `PersonalFinance.Shared/DTOs/Dashboard/`: `DashboardSummaryDto`, `NetWorthSummaryDto`, `BudgetSummaryDto`,
  `SavingsGoalSummaryDto`, `RecentActivityDto`
- Brand logo link changed from Google Drive to Dashboard

### Notification Feature

- New `Notification` entity in `PersonalFinance.Data/Entities/` inheriting from `AuditableEntity`
- Three `NotificationType` values: `BudgetAlert`, `SavingsMilestone`, `SyncStatus`
- New `NotificationService` in `PersonalFinance.ApiService/Notification/Services/` with CRUD operations
- CQRS commands: `CreateNotificationCommand`, `MarkNotificationReadCommand`
- CQRS queries: `GetNotificationsQuery`, `GetUnreadCountQuery`
- New API controller in `PersonalFinance.ApiService/Notification/`
- New `INotificationApi` contract in `PersonalFinance.Shared/Contracts/`
- Notification triggers added to existing services: Budget (80%/100% alerts), Savings Goals (50%/75%/100% milestones),
  Google Drive sync (status)
- Notification bell UI in top bar with dropdown and unread count badge
- EF Core migration for the `Notification` table

### Search Feature

- New `SearchService` in `PersonalFinance.ApiService/Search/Services/` with unified search logic
- Single unified search endpoint querying across transactions, budgets, and savings goals
- CQRS query: `SearchQuery`
- New API controller in `PersonalFinance.ApiService/Search/`
- New `ISearchApi` contract in `PersonalFinance.Shared/Contracts/`
- New DTOs in `PersonalFinance.Shared/DTOs/Search/`: `SearchResultsDto`, `SearchResultDto`
- Search bar UI in top bar with debounced input and results dropdown with source labels
- Search across community content and news is out of scope for the first version

### Library Additions

- **Lucide Icons** (CDN) for all nav item icons (modern, clean icon set)
- **ApexCharts** (CDN) for Dashboard charts (more interactive than existing Chart.js)
- Chart.js remains for existing features; ApexCharts used only in Dashboard views to avoid conflicts

### Data Models

**Notification Entity** (inherits `AuditableEntity`):

- `Type` (enum: BudgetAlert, SavingsMilestone, SyncStatus)
- `Title` (string)
- `Message` (string)
- `IsRead` (bool)
- `ReadAt` (DateTime?)
- `SourceFeature` (string?, e.g., "Budget", "SavingsGoals", "GoogleDrive")

**Dashboard DTOs**:

- `DashboardSummaryDto` containing `NetWorthSummaryDto`, `BudgetSummaryDto`, `List<SavingsGoalSummaryDto>`,
  `List<RecentActivityDto>`

**Search DTOs**:

- `SearchResultsDto` containing `List<SearchResultDto>` for Transactions, BudgetItems, and SavingsGoals
- `SearchResultDto` with `Id`, `Title`, `Description`, `Source`, and `Url`

### Architecture

- New features follow the existing CQRS pattern (ADR-0001): commands, queries, handlers, services in feature-based
  folders
- Web layer calls Refit APIs directly (no CQRS in Web layer, per ADR-0001)
- DashboardService aggregates data from existing services (Net Worth, Budget, Savings Goals, Analysis) with caching
- Notification triggers integrated into existing services without breaking their current behavior
- All new services registered in `Program.cs`

### Risks and Mitigations

- **Layout breakage**: Modifying `_Layout.cshtml` affects all views. Mitigation: test all existing pages after changes,
  ensure CSS classes don't conflict with existing view styles
- **Route conflicts**: New Dashboard routes shouldn't conflict with existing controllers. Mitigation: use distinct route
  names
- **Performance**: Dashboard aggregates data from multiple services. Mitigation: caching in DashboardService, async
  parallel queries
- **Mobile compatibility**: Bottom nav needs to work with existing mobile views. Mitigation: responsive CSS with media
  queries, test on multiple breakpoints
- **Library conflicts**: ApexCharts alongside Chart.js. Mitigation: namespace isolation, only use ApexCharts in
  Dashboard views

## Testing Decisions

### Test Approach

- Test external behavior, not implementation details. Test through API endpoints and UI interactions rather than mocking
  internal service methods.
- The fewer seams across the codebase, the better — prefer testing through existing seams (API endpoints, e2e tests)
  over adding new test-only interfaces.

### Modules to Test

- **NotificationService**: CRUD operations, unread count calculation, mark as read logic
- **SearchService**: Query logic across transactions, budgets, and savings goals, filtering, and result formatting
- **DashboardService**: Data aggregation from multiple sources, caching behavior, empty state handling
- **API Endpoints**: New API controllers for Dashboard, Notification, and Search

### Prior Art

- Unit tests in `PersonalFinance.Tests/` (xUnit): `ApiServiceEndpointTests.cs`, `GoogleDrivePersistenceTests.cs`,
  `BrevoEmailSenderTests.cs`
- E2e UI tests in `e2e-tests/ui-test.js` (Playwright): tests navigation, layout, auth flow, and responsive behavior
- The new tests should follow the same patterns: xUnit for backend unit tests, Playwright for UI verification

### Test Changes

- Add unit tests for `DashboardService` data aggregation (xUnit)
- Add unit tests for `SearchService` query logic (xUnit)
- Add unit tests for `NotificationService` CRUD operations (xUnit)
- Add integration tests for new API endpoints (xUnit)
- Update `e2e-tests/ui-test.js` to test sidebar navigation, bottom nav, and Dashboard rendering (Playwright)
- Manual verification that all existing pages render with the new layout

## Out of Scope

- Search across community content and news articles (future enhancement)
- Notification types beyond budget alerts, savings milestones, and sync status
- Custom SVG icons (using Lucide Icons instead)
- Sidebar auto-hide behavior (slide on hover)
- Dashboard user preferences (customizable card layout)
- Real-time notification delivery (WebSockets/SSE) — notifications are fetched on page load and via polling
- Mobile hamburger menu — replaced entirely by bottom navigation bar

## Further Notes

- This redesign qualifies for ADR-0002: "Sidebar Navigation for Scalability" — it's a hard-to-reverse architectural
  change, surprising without context (why move from Bootstrap's default top nav?), and involves real trade-offs (more
  CSS complexity for better scalability).
- The `AppVersion.cs` Minor version should be bumped after implementation.
- The `CHANGELOG.md` should be updated under `[Unreleased]` → `### Added` with a Navbar Redesign entry.
- The existing e2e test (`ui-test.js`) checks for `nav.navbar` presence — this selector will need to be updated to match
  the new sidebar structure.

---

## Implementation Steps

### ✓ Step 1: Add Notification Entity and Database Migration

### ✓ Step 2: Notification Backend API and Refit Contract

### ✓ Step 3: Notification Triggers in Existing Services

### ✓ Step 4: Unified Search Backend API

### ✓ Step 5: Dashboard Backend API with Aggregation

### ✓ Step 6: Sidebar Layout, Top Bar, and Mobile Bottom Nav

### ✓ Step 7: Dashboard Views, Charts, and ApexCharts

### ✓ Step 8: Search and Notification UI in Top Bar

### ✓ Step 9: Final validation and cleanup