# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added

- **Navbar Redesign**: Replaced the cluttered top navbar with a left sidebar navigation, Dashboard home page, global
  search, and notification system.
    - Left sidebar (240px/60px collapsible) with three navigation groups: Overview, My Finances, Resources
    - New Dashboard home page with grid of cards: Net Worth trend, Budget status, Savings Goals, Recent Activity, Quick
      Actions
    - Global search across transactions, budgets, and savings goals with debounced input
    - Database-backed notification system with budget alerts, savings milestones, and sync status
    - Mobile bottom navigation bar with 5 items (Home, Finances, Tools, Community, Profile)
    - Lucide Icons for all navigation items, ApexCharts for Dashboard charts
    - New API endpoints: Dashboard summary, Search, Notification CRUD
- **Budget Chart Leader Lines**: Replaced the grid-based legend with labels drawn directly on the pie chart canvas.
  - Each label is connected to its corresponding chart slice by a colored leader line with a dot marker.
  - Labels show percentage only, keeping the chart area clean and uncluttered.
  - Overlapping labels are automatically suppressed to maintain clarity on smaller slices.

### Fixed

- **Investment Calculator Chart**: Fixed the Growth Over Time chart not rendering at all after the recent rework.
  - The interest earned area fill was missing a `moveTo` call to start the canvas path, causing the entire chart to fail
    drawing.
- **Budget Chart Leader Line Labels**: Fixed labels not being visible because they were clipped by the canvas edges.
  - Added layout padding to the Chart.js configuration so the chart draws smaller, leaving room for labels around the
    edges.
  - Increased the chart container canvas height to accommodate the extra space for labels.
- **Budget Chart Responsiveness**: Fixed leader line labels overflowing the chart container on smaller screens.
  - Label positions, font sizes, line widths, and dot sizes now scale proportionally with the chart's actual size.
  - Labels are hidden automatically when the chart is too small (radius < 80px) to prevent visual clutter.
  - CSS breakpoints now shrink the chart container consistently: 340px → 280px → 260px → 220px across screen sizes.
- **Budget Chart Mobile Sizing**: Fixed leader line labels still being cut off on small screens due to the chart card
  being too small.
  - Layout padding now adjusts dynamically based on the chart's actual width, reducing from 55px to 20px on smaller
    screens.
  - The chart container now uses full width on mobile instead of being constrained to 220px.
  - Increased canvas max-height across all breakpoints to give labels more vertical space.

### Changed

- **Investment Calculator Growth Chart**: Reworked the Growth Over Time chart with a clearer visual design.
  - Added a third area fill highlighting interest earned (the gap between contributions and total value).
  - Replaced the tiny on-canvas legend with a styled HTML legend below the chart showing final values.
  - The legend displays Total Value, Contributions, and Interest Earned with matching color indicators.
  - Improved end-point dots with larger, more visible markers.
- **Budget Category Color Boxes**: Added small colored squares next to each category allocation label.
  - Each color box matches the corresponding pie chart slice color, linking inputs to the visual breakdown.
  - Colors are set dynamically via JavaScript to stay consistent with the chart's color palette.
- **Budget Summary List**: Replaced the plain budget summary table with a visual list featuring mini progress bars.
  - Each category now displays a colored progress bar proportional to its allocation, making comparisons at a glance.
  - The list is more compact and visually connected to the pie chart above it.
  - Responsive breakpoints hide the percentage column on mobile to save space.

- **Removed MediatR Dependency**: Replaced MediatR CQRS mediator pattern with direct service calls across all API
  features to avoid licensing requirements introduced in MediatR v13+.
  - All API controllers now inject and call services directly instead of sending commands/queries through IMediator.
  - Removed all command, query, and handler classes (NetWorth, Budget, Community, SavingsGoals, Watchlist, GoogleDrive,
    SpreadsheetAnalysis, AiAssistant).
  - Removed MediatR pipeline ValidationBehavior and FluentValidation validators that targeted command classes.
  - Moved SpreadsheetAnalysis sync logic from command handler into SpreadsheetAnalysisService.
  - Removed MediatR and MediatR.Extensions.Microsoft.DependencyInjection NuGet packages.

### Fixed

- **Savings Goals Update Input Dollar Sign**: Fixed the dollar sign ($) overlapping the entered number in the Update
  form.
  - Bootstrap's `form-control` class was overriding the custom left padding on the input.
  - Increased CSS specificity by scoping the input selector under its wrapper parent class.
  - Increased left padding from 1.5rem to 1.75rem to give more room for the $ prefix.
- **Savings Goal Progress Bar Percentage**: Fixed the percentage text not showing inside the progress bar.
  - The progress track height was only 12px, which clipped the percentage label text.
  - Increased track height to 24px with proper padding so the percentage is visible.
- **Savings Goal Card Text Truncation**: Fixed text being shrunk or cut off in savings goal cards.
  - Added proper overflow handling and text-ellipsis to goal names and descriptions.
  - Increased line-height on amount display to prevent vertical clipping.
  - Ensured the header info container properly constrains flex children.
- **Savings Goals Create Modal**: Fixed the Create Goal button in the New Savings Goal modal rejecting valid dollar
  amounts.
  - The target amount input had `step="100"`, which only allowed values like 1, 101, 201, … — any other number was
    rejected.
  - The current amount input had `step="50"`, which only allowed multiples of 50.
  - Changed both inputs to `step="any"` so any dollar amount including cents can be entered.
- **Savings Goals Update Button**: Fixed the Update button on the Savings Goals page rejecting valid dollar amounts.
  - The update form's amount input had `step="50"`, which only allowed multiples of 50 (0, 50, 100, 150, …).
  - Changed `step` to `1` so any whole-dollar amount can be entered and saved.
- **News Save to Watchlist**: Fixed save button on news articles not working — clicking Save did nothing because the API
  controller routes no longer matched the Refit client endpoints.
  - The Refit client was updated to use simplified routes (`/api/Watchlist`) but the controller still expected
    `/api/Watchlist/watchlist`.
  - Fixed all watchlist controller action routes (POST, DELETE, check) to match the simplified Refit contract.
- **Budget Planner Save**: Fixed budget data being wiped after clicking Save — the DebtRepayment category value was
  never submitted because the JavaScript `capitalize()` helper only uppercased the first character, producing
  "Debtrepayment" instead of "DebtRepayment".
  - Replaced the `capitalize()` function with an explicit lookup table mapping all 10 camelCase category keys to their
    PascalCase C# property names.
  - The form now correctly submits all category values, allowing the 100% validation to pass and the budget to persist.
- **Savings Goals API Routes**: Fixed savings goals not persisting to the database due to incorrect API route templates.
  - API controller routes used `[HttpPost("savings-goals")]` but the Refit contract expected
    `[Post("/api/SavingsGoals")]`.
  - Fixed all three action routes (Create, Update, Delete) to match the controller's base route `/api/SavingsGoals`.
- **Budget Planner Form Validation**: Fixed browser validation error preventing budget save when dollar amount inputs
  produce non-half-step percentages.
  - Changed percentage input `step` attribute from `0.5` to `any` to allow any decimal value.
- **Budget Planner Data Persistence**: Fixed budget values resetting to $0 after saving due to broken API response
  deserialization.
  - `BudgetResponseDto.Allocations` was typed as `object?`, causing System.Text.Json to deserialize the API response
    as a generic dictionary instead of a typed object with decimal properties.
  - The reflection-based property mapping in `BudgetController.Index` checked for `decimal` property types, which
    never matched the deserialized dictionary, so all values remained at 0.
  - Changed `BudgetResponseDto.Allocations` to `BudgetAllocationsDto?` for strong typing.
  - Replaced reflection-based mapping with direct property assignment in both `Index` and `GetBudgetData` methods.
  - Refit's default JSON serializer already uses case-insensitive property matching, so lowercase API properties (e.g.,
    "housing") map correctly to PascalCase DTO properties (e.g., "Housing").
- **Budget Planner Dollar Amount Input**: Users can now enter dollar amounts directly in addition to using sliders and
  percentage inputs.
  - Added editable dollar amount input field next to each category's percentage input.
  - Dollar amounts sync bidirectionally with percentages based on monthly income.
  - All three input methods (slider, percentage, dollar amount) stay synchronized in real-time.
- **Budget Planner Responsive Layout**: Fixed the Budget Planner page layout breaking at medium screen widths.
  - Added responsive breakpoint at 1199px to tighten category card padding and input field widths.
  - Fixed summary table category labels overflowing and overlapping at narrow sidebar widths.
  - Reduced chart container and summary table size proportionally at medium screen sizes.
  - Category labels in the summary table now truncate with ellipsis when space is limited.
- **Budget Planner Percentage Input**: Fixed the percentage sign (%) overlapping with longer number values (e.g.,
  "11.6%")
  in category allocation inputs.
  - Increased the percentage input field width from 70px to 75px for more room.
  - Increased right padding from 1.5rem to 2rem to ensure the number never touches the % suffix.
  - Adjusted responsive breakpoints to maintain adequate input width at medium and tablet screen sizes.
- **Budget Planner Summary Table**: Fixed category names and amounts overlapping in the summary table.
  - Removed `display: flex` from the table cell, which broke the table layout model and caused content to overflow.
  - Added a flex wrapper span inside the table cell for the colored dot and category name, keeping the cell as a proper
    table element.
  - Applied text truncation with ellipsis to the category name span, with a tooltip showing the full name on hover.

### Fixed

- **Budget, Savings Goals, and Investment Calculator UI**: Fixed visual inconsistencies and layout issues across the
  three standalone finance pages.
  - Unified page header style across all three pages — removed card-style wrapper from Budget header to match Savings
    and Calculator.
  - Fixed pink rectangle rendering artifacts caused by malformed element structure.
  - Improved Savings Goals card grid layout — wider minimum card width (340px), better spacing, and flex column layout.
  - Enhanced Savings Goals progress bar visibility and styling with proper design system tokens.
  - Replaced hardcoded green colors with design system CSS variables (`--app-brand`, `--app-accent-positive`) for
    consistency.
  - Added monospace font (`IBM Plex Mono`) to financial values in Savings Goals and Investment Calculator for better
    readability.
  - Removed unnecessary hover transform animation from Savings Goals cards.

### Changed

- **Finance News UI Redesign**: Separated the mixed "Personal Finance Hub" into focused, standalone features.
  - Removed Budget Planner, Savings Goals, and Investment Calculator tabs from the Finance News page.
  - Finance News now only contains news articles and Watchlist (saved articles) — naturally related features.
  - Added Budget Planner, Savings Goals, and Investment Calculator as separate navigation menu items.
  - Created standalone `SavingsGoalsController` with CRUD operations via Refit API.
  - Created standalone `InvestmentCalculatorController` with client-side compound growth projection.
  - Split the monolithic `news.js` (1,271 lines) — extracted calculator logic to `investment-calculator/calculator.js`.
  - Split the monolithic `news.css` (1,362 lines) — created `savings-goals.css` and `investment-calculator.css`.
  - Each feature now has its own dedicated page, CSS, and JavaScript for cleaner maintenance.

### Changed

- **Complete Visual Redesign**: Replaced the generic Fluent UI / Bootstrap card-based design with a distinctive "Indigo
  Ledger" design system.
  - Replaced Communication Blue (#0078d4) brand color with indigo (#4F46E5) — unexpected for finance, memorable.
  - Added Inter font for UI text and IBM Plex Mono for all financial data values (currency, percentages, table columns).
  - Removed card shadows and decorative elevation — replaced with clean borders and horizontal rules.
  - Removed ALL-CAPS table headers — switched to sentence case throughout.
  - Redesigned Login and Register pages: removed centered card wrapper, replaced with clean left-aligned layout.
  - Updated all feature CSS files (networth.css, budget.css) to use the new indigo palette.
  - Reduced border radius from 8px to 3px for a sharper, more intentional aesthetic.
  - Added domain-specific accent colors: emerald for positive values, red for losses.

### Added

- **Budget Planner Page**: Full-featured budget allocation planner at `/Budget` with interactive pie chart, sliders,
  and summary table.
  - Created `BudgetViewModel` with 10 spending categories, validation, and computed allocation properties.
  - Implemented `BudgetController` with Index (load saved budget), Save (persist allocations), and GetBudgetData (JSON
    endpoint for AJAX updates).
  - Razor view with income input, category sliders synced with percentage inputs, real-time dollar amount display,
    doughnut chart powered by Chart.js, and a summary table.
  - CSS following Fluent UI design system with responsive layout for mobile and desktop.
  - JavaScript module for slider/input synchronization, chart updates, and remaining allocation indicator.

### Improved

- **Compound Growth Calculator Chart**: Completely redesigned the investment calculator chart for a polished,
  professional appearance.
  - Replaced jagged straight-line rendering with smooth Catmull-Rom spline curves.
  - Added gradient area fills (green for total value, indigo for contributions) instead of flat colors.
  - Added interactive hover tooltips that display year, projected value, and total contributions.
  - Improved axis labels with compact formatting ($10k, $100M) and nice round-number scaling.
  - Added endpoint indicator dots with glow effects and a clean inline legend.
  - Updated chart container styling with subtle shadow and rounded corners.

### Fixed

- **JavaScript Syntax Errors**: Fixed two JavaScript errors introduced by recent chart improvements.
  - Removed duplicate `const canvas` declaration in the chart tooltip function (`news.js`).
  - Removed broken Fluent UI `setTheme` import from `_Layout.cshtml` — the minified web-components bundle does not
    export `setTheme` (known upstream issue).
- **Compound Growth Calculator Chart Resolution**: Fixed the investment calculator chart rendering at fixed 400x280
  pixels regardless of container size.
  - Chart now dynamically sizes to fill its container width (300-800px range) with proportional height.
  - Added ResizeObserver to automatically redraw when the container or window resizes.
  - Tooltip bounds checking uses dynamic canvas width instead of hardcoded value.
- **Google Drive Sync Toast Readability**: Fixed the sync success toast appearing on a black background, making it hard
  to read after the Indigo Ledger redesign.
  - Replaced `bg-dark text-white` dark toast with a light theme toast using the new design system's CSS tokens.
- **CI/CD Smoke Test — /Budget 404**: Replaced placeholder Budget controller with full Budget Planner feature so the
  post-deployment smoke test probe for `/Budget` returns a redirect to login (auth required) instead of HTTP 404.
  - `BudgetController` with `[Authorize]` attribute serves the complete budget planner page.
  - `Views/Budget/Index.cshtml` provides interactive budget allocation form with chart and summary.
- **CI/CD Smoke Test Diagnostics**: Improved the post-deployment smoke-test script to emit richer diagnostics and be
  more tolerant of transient cold-starts.
  - Added deploy output logging (web_url, web_fqdn, api_fqdn) before running smoke tests so the target URL is visible in
    logs.
  - Each failed check now dumps response headers and the first 400 bytes of the response body to identify the root
    cause.
  - Increased the health endpoint readiness retry count from 30 to 60 (5-second intervals) to accommodate Azure
    Container Apps cold-starts.
  - Relaxed redirect checks to accept HTTP 200 (login page rendered) in addition to 301/302, as ASP.NET Core Identity
    may render the login page instead of redirecting.
  - Added failure detail dump for the health endpoint timeout case.

## [1.13.0] - 2026-10-16

### Added

- **Architecture Documentation**: Created `GLOSSARY.md` with canonical definitions for architecture terms (Feature, Command, Query, Handler, Service, DTO, Entity, AuditableEntity) and domain concepts.
- **Architecture Decision Records**: Created `docs/adr/0001-standardize-cqrs-feature-organization.md` documenting the decision to standardize on CQRS with MediatR and feature-based organization.

### Changed

- **AGENTS.md Guidelines**: Updated with new architectural patterns:
  - Added Quick Start section referencing GLOSSARY.md and ADRs.
  - Documented CQRS requirement for all ApiService features (no exceptions).
  - Added feature-based folder structure guidance with examples.
  - Updated code placement table with feature-based paths.
  - Added AuditableEntity and soft delete requirements for user-owned entities.
  - Clarified that Web layer does NOT use CQRS (calls Refit APIs directly).
  - Added handler responsibility guidelines (thin coordinators, services own logic).

## [1.8.8] - 2026-10-14

### Fixed

- **AI Assistant Workflow Options Unclickable**: Fixed the AI Assistant guided workflow options not responding to
  clicks.
  - Increased the chat panel and button z-index from 1060 to 1100 to ensure the panel renders above all page content.
  - Removed `isolation: isolate` from the chat panel that was interfering with the stacking context.
  - Cleaned up redundant `pointer-events: auto` declarations that were masking the underlying z-index conflict.

## [1.8.7] - 2026-10-14

### Fixed

- **AI Assistant Workflow Options Unclickable**: Fixed the AI Assistant guided workflow options not responding to
  clicks.
  - Added `isolation: isolate` to the chat panel to create a proper stacking context.
  - Added explicit `pointer-events: auto` to the messages container, workflow categories, and workflow item buttons.
  - Increased workflow item z-index to 2 to ensure they receive click events above all sibling elements.

## [1.8.6] - 2026-10-14

### Fixed

- **AI Assistant Chat Panel Layout**: Reverted the AI Assistant chat panel from unstable auto-height layout back to a
  stable fixed height of 520px.
  - Removed `height: auto` with min/max constraints that were causing layout instability.
  - Removed the tablet responsive breakpoint (768px) that was adding unnecessary complexity.
  - Simplified mobile breakpoint to use `height: calc(100vh - 100px)` for full-screen adaptation on small devices.

## [1.8.5] - 2026-10-14

### Fixed

- **AI Assistant Responsive UI**: Fixed the AI Assistant chat panel height and width to be fully responsive across all
  screen sizes.
  - Replaced fixed panel height (580px) with auto height using viewport-relative min/max constraints (min-height:
    400px, max-height: calc (100vh - 110px)).
  - Added tablet breakpoint (768px) with adjusted panel width and height for medium screens.
  - Improved mobile breakpoint (480px) with proper min-height and max-height for small screens.
  - Panel now adapts to available viewport space instead of using rigid fixed dimensions.

## [1.8.4] - 2026-10-14

### Fixed

- **AI Assistant Workflow Options Unclickable**: Fixed the AI Assistant guided workflow options being cut off and
  unclickable in the chat panel.
  - Increased the chat panel height from 460px to 580px to provide enough space for all workflow category cards.
  - Raised the panel and button z-index from 1050 to 1060 to ensure the panel stays above page content.

## [1.8.3] - 2026-10-14

### Fixed

- **AI Assistant Workflow Categories Cut Off**: Fixed the AI Assistant guided workflow categories being clipped and not
  fully visible in the chat panel.
  - Changed the chat messages container flex property from `flex: 1` to `flex: 1 1 0` to properly constrain the
    scrollable area within the fixed-height panel.
  - Hidden the unused `ai-chat-workflow` and `ai-chat-quick-actions` DOM elements that were consuming flex space and
    reducing the available height for the scrollable messages area.
  - Added `overflow-x: hidden` to prevent horizontal overflow.

## [1.8.2] - 2026-10-14

### Fixed

- **AI Assistant Clickability**: Fixed the AI Assistant guided workflow cards not being clickable.
  - Added `min-height: 0` to the chat messages container to fix flexbox overflow scrolling.
  - Added `position: relative` and `z-index: 1` to workflow item buttons to ensure they receive click events.
  - Added box-shadow to workflow category cards for better visual distinction.
  - Ensured scroll position resets to top when workflow cards are rendered.

## [1.8.1] - 2026-10-14

### Changed

- **AI Assistant Workflow Options**: Updated the AI Assistant guided workflow to include the options users need: Budget
  Analysis, Transaction Analysis, Budget Planner, and Net Worth Projection.
  - Replaced "Finance News" and "Financial Planning" categories with "Transaction Analysis" and "Budget Planner".
  - Added Transaction Analysis options: analyze transactions, find unusual spending, and category breakdown.
  - Added Budget Planner options: create a budget, apply 50/30/20 rule, and monthly budget review.
  - Renamed "Net Worth" category to "Net Worth Projection" with options: project net worth, retirement projection, and
    asset breakdown.
  - Updated contextual greetings to reflect the new workflow options.

## [1.8.0] - 2026-10-14

### Added

- **AI Assistant Guided Workflow**: Redesigned the AI Assistant chat widget with a guided workflow interface that helps
  users discover what the AI can do.
  - Added categorized workflow cards: Budget Analysis, Net Worth, Finance News, and Financial Planning.
  - Each category contains actionable options with descriptions (e.g., "Analyze my budget" → "Review spending patterns
    from Google Drive").
  - Contextual greetings adapt to the current page (Google Drive, Net Worth, News, Community).
  - Navigation indicators on options that link to specific pages (e.g., budget analysis links to Google Drive).
  - Chat resets to the guided workflow view when closed, so users always see the options on reopen.
  - Updated header with subtitle "Your personal finance guide" for clearer purpose.

### Changed

- **AI Assistant UI**: Replaced the generic welcome message and flat quick-action buttons with structured, categorized
  workflow cards featuring icons, labels, and descriptions.
- **AI Assistant CSS**: Added new styles for greeting bubbles, workflow category headers, clickable option items,
  hover/active states, focus-visible accessibility, and responsive mobile adjustments.

## [1.6.1] - 2026-10-14

### Fixed
- **External Login Authentication Error**: Replaced `RefreshSignInAsync` with `SignInAsync` in `ExternalLogin.cshtml.cs` to resolve the "RefreshSignInAsync prevented because the user is not currently authenticated" error during Google OAuth login callback.

## [1.5.0] - 2026-10-12

### Added
- **Finance News Aggregation**:
  - Added a News page with interactive finance news aggregation and display.
  - Implemented `NewsService` in `PersonalFinance.Web` for fetching and caching finance news articles.
  - Added `NewsController` with MVC actions and API integration.
  - Created `NewsViewModel` and supporting models (`NewsQueryOptions`, `NewsRefreshResponse`) for structured news data.
  - Added dedicated styling in `wwwroot/css/news.css` and client-side logic in `wwwroot/js/news/news.js`.

- **Community Features**:
  - Added community page with polls, comments, and stories for user engagement.
  - Implemented `CommunityController` in both `PersonalFinance.ApiService` (backend API) and `PersonalFinance.Web` (MVC frontend).
  - Added community data entities: `CommunityPoll`, `CommunityPollOption`, `CommunityPollVote`, `CommunityComment`, `CommunityCommentLike`, `CommunityStory`, and `CommunityStoryLike`.
  - Added `ICommunityApi` Refit client contract and `CommunityDto` for cross-layer communication.
  - Created `CommunityViewModel` for structured community data presentation.
  - Added dedicated styling in `wwwroot/css/community.css` and client-side logic in `wwwroot/js/community.js`.

- **Personal Finance Planning Entities**:
  - Added `SavingsGoal` entity for tracking user savings targets and progress.
  - Added `UserBudgetAllocation` entity for managing budget category allocations.
  - Added `SavedArticle` entity for bookmarking and saving articles.
  - Implemented `IPersonalFinanceApi` Refit client contract with comprehensive DTOs.
  - Added `PersonalFinanceController` in both ApiService and Web layers.

- **Database Migrations**:
  - Added migration `AddCommunityEntities` for community polls, comments, and related tables.
  - Added migration `UpdateNetWorthParametersDecimalPrecision` for improved financial calculation precision.
  - Added migration `AddCommunityStories` for community story entities.
  - Added migration `AddPersonalFinanceEntities` for savings goals, budget allocations, and saved articles.

- **Documentation**:
  - Added comprehensive `.NET Core MVC` best practices guidelines to `AGENTS.md` covering architecture, naming conventions, JavaScript/ESLint rules, and feature implementation protocol.
  - Added version management guidelines to `AGENTS.md` for consistent semantic versioning.
  - Reorganized project documentation under `docs/` directory with centralized `README.md`.

### Changed
- **Identity Dependency Refactoring**: Removed identity dependency and streamlined user scoping for simplified architecture.
- **OpenTelemetry & Security Dependencies**: Upgraded OpenTelemetry packages and added security-related dependencies in `Directory.Packages.props`.
- **Navigation Updates**: Added News and Community links to the main navigation in `_Layout.cshtml`.
- **Time Display**: Added `TimeAgoTagHelper` for relative time display across the application.

### Commits
- `1c01938` feat: add finance news aggregation feature
- `54bcc71` feat: add community features for polls, comments, and stories
- `a2ff9fe` feat: add new entities for savings goals, budget allocations, and saved articles
- `d8cffb8` feat: add community endpoints and identity integration
- `e218e02` refactor: remove identity dependency and streamline user scoping
- `1407da3` doc: add `.NET Core MVC` best practices guidelines
- `db7ff0d` chore: upgrade OpenTelemetry packages and add security-related dependencies
- `763680c` docs: reorganize project documentation under `docs` directory
- `73ebff5` docs: add version management guidelines to `AGENTS.md`
- `348b02a` chore: release version 1.5.0 with new features and updates
- [PR #23](https://github.com/tngo0508/PersonalFinanceApp/pull/23) — Redesign page
- [PR #24](https://github.com/tngo0508/PersonalFinanceApp/pull/24) — Update Aspire version

## [1.4.1] - 2026-10-12

### Changed
- **Aspire AppHost SDK Upgrade**: Upgraded Aspire AppHost SDK from 9.1.0 to 13.6.1.

### Commits
- `c4f6d14` Upgrade Aspire AppHost SDK from 9.1.0 to 13.6.1
- `2908c7e` chore: bump version to 1.4.1 and add changelog entry for Aspire SDK upgrade

## [1.4.0] - 2026-10-11

### Changed
- **Net Worth UI Refinements**:
  - Refactored Net Worth UI layout to a responsive two-column structure with sticky sidebar inputs for improved usability.
  - Reorganized sidebar into sticky top, scrollable middle, and sticky bottom sections with optimized compact mode and mobile-friendly behaviors.
  - Standardized CSS box-sizing and width across sidebar elements for consistent layout.
  - Removed independent scrolling and height constraints from sidebar content for a cleaner experience.
  - Simplified scenario row highlight styles and removed redundant currency symbols in metric outputs.
  - Improved code readability in Net Worth UI scripts by streamlining HTML content updates.

- **Fluent UI Design System Integration**:
  - Updated palettes, typography, and spacing to align with Fluent UI design guidelines.
  - Integrated modular design tokens for consistent theming across the application.
  - Reworked components (buttons, cards, modals, tables) with Fluent UI-inspired styling.
  - Added support for `Segoe UI` font and responsive typography.

- **Google Drive Sync**:
  - Enhanced Google Drive sync form with fallback spinner styling and improved error handling.
  - Refined DataTables pagination layout for better readability.

- **Navigation**:
  - Updated default route to use the Net Worth controller as the landing page.

### Commits
- `c952631` Refactor Net Worth UI layout and optimize input components
- `2d37e92` Refactor Net Worth sidebar layout and styles
- `80ed116` Standardize CSS box-sizing and width for Net Worth sidebar elements
- `2c1c240` Remove independent scrolling and height constraints from Net Worth sidebar content
- `505fc0d` Update styles to align with Fluent UI design system
- `7531917` update
- `ac8de80` Update default route to use Networth controller
- `34cb0ae` Enhance UI styles and improve Google Drive sync fallback behavior
- `7d33bff` Refine Net Worth UI styles and clarify metric outputs
- `da70916` Bump app version to 1.4.0 and update changelog for new features and improvements
- [PR #23](https://github.com/tngo0508/PersonalFinanceApp/pull/23) — Redesign page

## [1.3.0] - 2026-10-11

### Added
- **Net Worth Parameter Persistence**:
  - Added support for saving and retrieving Net Worth projection parameters so user settings persist across sessions.
  - Integrated parameter persistence with the API service (`PersonalFinance.ApiService`) for server-side storage and retrieval.

### Changed
- **Net Worth UI Improvements**:
  - Updated Net Worth UI with ROI badge updates and synchronized input controls for a more cohesive user experience.
  - Standardized currency symbol styling across the Net Worth UI for consistent formatting.
  - Refactored Net Worth scripts and adjusted layout script links for better code organization.

- **Code Quality**:
  - Enforced consistent function spacing and added ESLint configuration with shared utilities for improved JavaScript code quality.

- **Documentation**:
  - Updated `README.md` with refined formatting, removed emojis, and clarified contributing steps.
  - Updated `DEVELOPMENT.md` and `deployment.md` with comprehensive documentation improvements.

### Commits
- `2d086b5` Add support for saving and retrieving Net Worth projection parameters
- `ec6e5c7` Add net worth parameter persistence and API integration
- `930be05` Update net worth UI: add ROI badge updates and sync inputs
- `f6f0863` Standardize currency symbol styling across Net Worth UI
- `34885c2` Enforce consistent function spacing, add ESLint configuration, and integrate shared utilities
- `07bc3ba` Refactor Net Worth scripts and adjust layout script links
- `f99e62d` Update documentation: professional README, comprehensive DEVELOPMENT.md, and deployment.md
- `ec0313e` Update README.md: refine formatting, remove emojis, and clarify contributing steps
- `f847467` Bump version to 1.3.0 and update changelog
- [PR #22](https://github.com/tngo0508/PersonalFinanceApp/pull/22) — Persist Net Worth data

## [1.2.0] - 2026-10-11

### Added
- **Net Worth Projection & Financial Independence Planning**:
  - Added a Net Worth planning page with interactive projections for Financial Independence (FI) goals.
  - Parameters include current net worth, annual income, savings rate, investment return rate, inflation rate, and FI target.
  - Year-by-year projection table showing net worth growth, annual contributions, investment returns, inflation-adjusted values, and cumulative ROI.
  - Added preset scenarios (Conservative, Moderate, Aggressive) for quick setup.
  - Interactive slider controls for all input parameters with live preview updates.
  - Randomization functionality to explore Monte Carlo–style parameter variations.
  - Tooltips and a 4% rule explanation to guide users in setting realistic FI targets.
  - Input validation with reset functionality to restore default values.
  - Highlighted rows where cumulative ROI surpasses annual income, marking a key FI milestone.
  - Dark futuristic chart theme with enhanced interactions for Net Worth visualizations.
  - Refactored the feature to use the API service (`PersonalFinance.ApiService`) with shared DTOs (`NetWorthProjectionDto`) and server-side validation.
  - Added dedicated styling in `wwwroot/css/networth.css` and client-side logic in `wwwroot/js/networth.js`.
- **Enhanced FI Target Display**: Separate currency symbol and styled amount for improved readability of the FI target figure.
- **Google Drive Resilience & Error Handling**:
  - Added HTTP resilience handler for Google Drive connections, improving reliability under transient network failures.
  - Improved error handling for expired or invalid Google Drive credentials with clearer user-facing messages.
- **Google Drive UI Revamp**:
  - Improved accessibility, icons, and layout across the Google Drive explorer page.
  - Enhanced visual hierarchy and navigation for connected drives and file browsing.

### Changed
- **Dockerfile & Bicep Optimizations for Cost Efficiency and Performance**:
  - Optimized Dockerfiles for reduced image size and faster cold-start performance.
  - Updated Bicep configurations for enhanced scalability and resource efficiency.
  - Upgraded Azure SQL Database from Serverless Free Tier to Basic tier for predictable ~$5/month costs and consistent performance.
  - Updated Bicep tests and configuration to reflect the new Azure SQL Basic tier settings.
- **Monthly Budget Template Creation**:
  - Added support for creating Monthly Budget templates directly in Google Drive (PR #19).
  - Later removed the template creation feature in favor of the existing spreadsheet detection and parsing workflow, keeping the focus on analyzing user-provided budgets.

### Fixed
- **Net Worth Chart Rendering**: Removed custom glow effect plugin from Net Worth charts for streamlined and more performant rendering.
- **Net Worth Table Styling**: Simplified table row styling and hover effects for improved readability.

### Commits
- `c8aa286` Add non-blocking logging with Serilog.Async and configure log retention
- `8f13ab5` Add support for creating Monthly Budget templates in Google Drive
- `f582f26` Remove support for Monthly Budget template creation in Google Drive
- `4f7f808` Add Net Worth projection feature with FI planning
- `d64ee7d` Simplify Net Worth table row styling and hover effects
- `65a0eb6` Add tooltips and 4% rule explanation to Net Worth planning UI
- `bebeed4` Add input validation and reset functionality to Net Worth planning
- `254b9b0` Revamp Google Drive UI with improved accessibility, icons, and layout
- `d64b50f` Revamp Net Worth visualizations with dark futuristic theme and enhanced interactions
- `6eeac91` Remove custom glow effect plugin from Net Worth charts for streamlined rendering
- `40c8768` Add resilience handler and improve error handling for Google Drive connections
- `fa77938` Refactor Net Worth projection feature to use API service with validation and shared DTOs
- `97f5ae8` Highlight ROI surpassing annual income in Net Worth table
- `35c0d2c` Add randomization functionality for Net Worth planning parameters
- `ee0c01e` Add preset scenarios and slider controls to Net Worth planning UI
- `133fd08` Optimize Dockerfiles and Bicep configurations for cost efficiency and cold-start performance
- `b6fa7e2` Enhance FI Target display with separate currency symbol and styled amount
- `b9b86a7` Optimize Dockerfiles and Bicep configurations for enhanced scalability and performance
- `d6885de` Update Azure SQL Database to Basic tier for predictable ~$5/month costs
- `12df147` Update Bicep tests and configuration for Azure SQL Database Basic tier
- `f357a87` Bump version to 1.2.0 and update changelog
- [PR #18](https://github.com/tngo0508/PersonalFinanceApp/pull/18) — Log capabilities
- [PR #19](https://github.com/tngo0508/PersonalFinanceApp/pull/19) — Add monthly budget template
- [PR #20](https://github.com/tngo0508/PersonalFinanceApp/pull/20) — Net Worth page
- [PR #21](https://github.com/tngo0508/PersonalFinanceApp/pull/21) — Improve performance

## [1.1.0] - 2026-10-10

### Added
- **Spreadsheet Transactions Viewer**:
  - Added a **Transactions** action for every spreadsheet on the Google Drive page that opens the rows of the sheet's Transactions table in a searchable, sortable modal.
  - Added `GET /api/googledrive/spreadsheet-transactions` in `PersonalFinance.ApiService`, the `IGoogleDriveApi.GetSpreadsheetTransactionsAsync` Refit method, and the `SpreadsheetTransactions` action in the Web `GoogleDriveController`, returning the new `SpreadsheetTransactionsDto` / `SpreadsheetTransactionDto`.
  - Added `GoogleDriveHelper.ParseXlsxTransactions`, `ParseCsvTransactions` and `ParseTransactionRows`. A header row with `Date` and `Amount` starts a table, side-by-side tables are supported (Google's Monthly budget template puts Expenses and Income next to each other), the section label above each table sets the type, a `Type` column or signed amounts are used when there is no label, Excel serial dates are converted, and "Total…" rows are skipped. Sheets named like "Transactions" are read first.
  - Downloads reuse the budget report path (Google Sheets are exported as XLSX so every sheet is available, with CSV fallback). Failures return an error message instead of generated sample rows.
  - Added sample transactions for preview files, plus tests in `GoogleDriveHelperTests` and `GoogleDriveWebControllerTests`.
- **Budget Report Additions**:
  - Summary card with the period, status badge and plain-language takeaway; plan meters on the Income and Spending tiles; savings rate measured against a 20% savings goal.
  - "Where your income went" bar showing spent vs. saved per $100 earned, with a hatched segment when spending exceeds income.
  - Category sorting (largest, most over budget, A–Z) and an "Only over budget" filter.
  - **Export CSV** of the current view (months for the full year, categories for a month) and a **Transactions** link to the same file's transactions.
- **Transactions Insights**:
  - Highlights for the largest expense, top category share and most frequent category, plus a spending-over-time chart (daily, or monthly for ranges over two months) with hover tooltips.
  - Ranked top spending categories (the tail folds into "Other") that filter the table when selected.
  - Live totals for the filtered rows, **Export CSV** of the filtered rows, and a **Budget report** link for monthly budget sheets.
- **Architecture & Deployment Overview in `deployment.md`**:
  - Rewrote Section 1 as a learning-oriented architecture guide with Mermaid diagrams derived from `deploy-azure.yml` and `infra/*.bicep`: system context, solution project map, CI/CD job graph with a trigger matrix, `deploy-azure` sequence and Bicep module dependency graph, runtime request flow, configuration/secrets flow, and the startup/health/scale-to-zero lifecycle.
  - Added an Azure resource table (name patterns and cost model), an environment comparison (Aspire, Docker, Azure, tests), a secrets-to-environment-variable mapping, and a "Security Posture and Known Gaps" section (public unauthenticated API when `exposeApiPublicly=true`, SQL admin connection string, `AllowAllWindowsAzureIps` firewall rule, broad pipeline role scope).
- **`promote-latest` CI/CD Job**: Moves the `latest` GHCR tags to the deployed commit with `docker buildx imagetools create` only after `deploy-azure` and `smoke-test` succeed, so `latest` (the default image for `deploy.ps1` and `azd`) always points to a verified build.
- **`validate-infra` CI/CD Job**: Validates `infra/main.bicep` and `infra/local-dev.bicep` in a parallel job.
- **Local Development Azure SQL Provisioning**:
  - Added standalone `infra/local-dev.bicep` template provisioning only the Azure SQL server and Serverless Free Tier `PersonalFinance` database for running the app locally against Azure SQL.
  - Added `infra/deploy-local-sql.ps1` script that creates the `rg-personalfinance-dev` resource group, auto-detects the developer machine's public IP for the SQL firewall, deploys the template, and stores the resulting connection string in .NET User Secrets (`ConnectionStrings:DefaultConnection`) for `PersonalFinance.ApiService` and `PersonalFinance.Web`.
  - Added `allowedClientIpAddresses` parameter to `infra/modules/sql-database.bicep` creating per-IP server firewall rules (`AllowClientIp-{n}`).
- **Database Connection String & Secrets Management Documentation**:
  - Documented secure database connection string workflows across CI/CD, Infrastructure-as-Code (Bicep), design-time EF Core tools, and local development in `DEVELOPMENT.md` and `deployment.md`.
  - Added step-by-step instructions for `.NET User Secrets` configuration (`ConnectionStrings:DefaultConnection`) across `PersonalFinance.Web` and `PersonalFinance.ApiService` to prevent sensitive credentials from being committed to Git or stored in `appsettings.json`.
  - Documented dual CI/CD secret handling: Option A for automated Azure SQL Serverless Free Tier (`SQL_ADMIN_PASSWORD`) and Option B for external/custom SQL databases (`CUSTOM_CONNECTION_STRING`) via Bicep `@secure()` parameters and Azure Container Apps secret references (`secretRef: 'db-connection-string'`).
- **Google OAuth 2.0 External Authentication & Account Linking**:
  - Integrated Google OAuth 2.0 external authentication using `Microsoft.AspNetCore.Authentication.Google` (v10.0.0) with central package management in `Directory.Packages.props` and `PersonalFinance.Web.csproj`.
  - Configured Google authentication handler conditionally in `PersonalFinance.Web/Program.cs` supporting OpenID Connect scopes (`profile`, `email`) and claim action mappings (`urn:google:picture`, `urn:google:locale`, `urn:google:verified_email`) via `Authentication:Google` configuration and .NET User Secrets.
  - Implemented external login workflow in `Areas/Identity/Pages/Account/ExternalLogin.cshtml` and `ExternalLogin.cshtml.cs` with OAuth challenge handling, provider callback processing, automated user provisioning for verified email addresses, and secure account linking for existing confirmed accounts.
  - Added custom claim synchronization (`SynchronizeCustomClaimsAsync`) to persist and refresh Google profile picture URLs, given names, and locale preferences in Identity claims upon login and account linking.
  - Updated `_LoginPartial.cshtml` navigation partial to render user profile pictures and display names retrieved from external identity claims.
  - Modernized `Areas/Identity/Pages/Account/Login.cshtml` and `Register.cshtml` with accessible, branded Google sign-in and registration buttons dynamically populated from active authentication schemes.
  - Configured API-aware application cookie redirect handlers in `PersonalFinance.Web/Program.cs` returning HTTP `401 Unauthorized` and `403 Forbidden` status codes for unauthenticated API, JSON, and AJAX requests instead of HTML 302 redirects.
  - Added unit and integration test suite `ExternalAuthenticationTests.cs` in `PersonalFinance.Tests` covering scheme registration, option configuration, API-aware cookie redirect behaviors, user auto-provisioning, and secure account linking workflows.
- **Developer Guide & Development Notes (`DEVELOPMENT.md`)**:
  - Created a comprehensive `DEVELOPMENT.md` guide covering local prerequisites, architecture overview, .NET User Secrets management, Aspire dashboard configuration, SQLite database workflows, EF Core Code-First commands, testing guidelines, and troubleshooting solutions.
- **Secrets Management via .NET User Secrets**:
  - Migrated private API credentials (such as Brevo REST API keys and Google Drive keys) to `.NET User Secrets` (`secrets.json`) during local development to prevent committing sensitive keys to Git repositories.
- **.NET Aspire Orchestration & Service Defaults**:
  - Integrated .NET Aspire into the solution to manage multi-project lifecycle, service discovery, resilient communication, and observability.
  - Added `PersonalFinance.AppHost` project orchestrating `PersonalFinance.ApiService` and `PersonalFinance.Web` dependencies with `WithReference` and `WaitFor` definitions.
  - Added `PersonalFinance.ServiceDefaults` shared extension library configuring OpenTelemetry metrics (`AspNetCore`, `HttpClient`, `Runtime`), distributed tracing, OTLP exporters, default liveness/readiness health check endpoints (`/health` and `/alive`), service discovery, and standard HTTP resilience pipelines.
  - Configured `PersonalFinance.ApiService` and `PersonalFinance.Web` to reference `PersonalFinance.ServiceDefaults`, call `builder.AddServiceDefaults()`, and map endpoints with `app.MapDefaultEndpoints()`.
  - Configured `PersonalFinance.Web` Refit client to communicate with `ApiService` using Aspire service discovery (`https+http://apiservice`).
  - Added unit tests in `PersonalFinance.Tests` (`ServiceDefaultsTests`) verifying service registration for health checks, OpenTelemetry tracing, and metric providers.
  - Added Aspire package versions centrally to `Directory.Packages.props` and registered new projects in `PersonalFinance.sln` and `PersonalFinance.slnx`.
- **Custom HTML Email Templates & Identity Registration Scaffolding**:
  - Implemented `EmailTemplateHelper` in `PersonalFinance.Web/Services` providing responsive, branded HTML email templates with prominent call-to-action (CTA) confirmation buttons, cross-client email client styling, and fallback URL rendering.
  - Scaffolded Identity Razor pages `Register.cshtml`/`Register.cshtml.cs` and `ResendEmailConfirmation.cshtml`/`ResendEmailConfirmation.cshtml.cs` under `Areas/Identity/Pages/Account/` to integrate custom HTML confirmation email dispatch.
  - Added unit test suite `EmailTemplateHelperTests` validating URL and token interpolation, parameter HTML escaping, and button styling.
- **Modernized Identity Login UI**:
  - Scaffolded and customized `Areas/Identity/Pages/Account/Login.cshtml` and `Login.cshtml.cs` with a clean, responsive card-based layout featuring floating input labels, validation feedback, persistent login ("Remember me"), external login provider support, and direct navigation links to registration, password recovery, and email confirmation resend flows.
- **Brevo Transactional Email Sender (REST API)**:
  - Implemented `BrevoEmailSender` implementing ASP.NET Core Identity's `IEmailSender` interface to support asynchronous verification email dispatch via Brevo REST API (`https://api.brevo.com/v3/smtp/email`) for user registration, account confirmation, and password reset flows.
  - Added `BrevoOptions` strongly-typed configuration model supporting Brevo REST API keys (`ApiKey`) and sender identity (`SenderEmail`, `SenderName`).
  - Registered `BrevoOptions` and `BrevoEmailSender` via `AddHttpClient<IEmailSender, BrevoEmailSender>()` in `PersonalFinance.Web/Program.cs`.
  - Added unit test suite `BrevoEmailSenderTests` covering REST API payload dispatch, API error handling, missing credentials handling, HTML email construction, and dependency injection service resolution.
- **Multi-Sheet OpenXML Spreadsheet & Google Sheets Parsing**:
  - Implemented multi-sheet OpenXML (`.xlsx`) parsing in `GoogleDriveHelper` to support complex budget workbooks containing multiple sheets (e.g., `Summary` and `Transactions`).
  - Added direct `.xlsx` export integration in `GoogleDriveService` for Google Spreadsheets with automatic fallback to CSV format, preserving multi-sheet access across export types.
  - Added transaction table aggregation to parse individual line-item expenses from transactions sheets, dynamically mapping and rolling up category expense amounts into monthly budget totals.
  - Added intelligent filtering to exclude non-expense balance change records (such as "Increase in total savings", "Decrease in total savings", and "Change in total savings") from expense categorization and savings rate metrics.
  - Added test coverage in `GoogleDriveHelperTests` validating multi-sheet parsing, transaction aggregation, and balance delta exclusions.
- **Google Drive Monthly Budget Report & Analytics**:
  - Implemented Google Drive monthly budget spreadsheet detection, parsing, and financial analytics engine in `GoogleDriveHelper`.
  - Added support for parsing exported Google Sheets CSVs and local spreadsheet data with automatic detection of single-month and full-year layouts, planned vs. actual income and expenses, net savings, savings rate, and category breakdowns.
  - Added starting and ending balance detection and tracking across monthly budgets.
  - Added DTO models (`MonthlyBudgetReportDto`, `BudgetMonthSummaryDto`, `BudgetCategorySummaryDto`) in `PersonalFinance.Shared` representing structured budget reporting data.
  - Added `GET /api/googledrive/spreadsheet-report` endpoint in `PersonalFinance.ApiService` and Refit client method `IGoogleDriveApi.GetMonthlyBudgetReportAsync`.
  - Added `MonthlyBudgetReport` controller action in `PersonalFinance.Web`'s `GoogleDriveController` to fetch and deliver structured report data to the frontend.
  - Integrated interactive "Budget Report" modal in `Views/GoogleDrive/Index.cshtml` featuring KPI summary cards (Total Income, Total Expenses, Net Savings/Surplus, Savings Rate), Chart.js visualizations (bar/line comparison and category doughnut charts), month-by-month switcher with full-year overview, variance tracking progress bars, and printable report layout.
  - Added unit test suites in `GoogleDriveHelperTests` and `GoogleDriveWebControllerTests` covering monthly budget spreadsheet identification, column matching, balance parsing, category aggregation, and controller error handling.
- **Google Drive Configuration Persistence & SQLite Caching**:
  - Implemented persistent SQLite database storage for user Google Drive connections and retrieved file metadata in `AppDbContext`.
  - Added `GoogleDriveConnection` entity for tracking connected drives, encrypted API credentials, synchronization timestamps, and health status.
  - Added `GoogleDriveCachedFile` entity for caching retrieved Drive file items, categories, sizes, and timestamps locally in SQLite.
  - Implemented automatic configuration and cache loading on subsequent logins to skip setup flows when a valid connection exists.
  - Supported multiple Google Drive connections per user with an intuitive "Add another Drive" switcher and management controls.
  - Added incremental cache synchronization logic to detect newly added, modified, or deleted files without re-downloading unchanged items.
  - Added manual synchronization ("Sync / Refresh") actions and endpoints to update local SQLite caches on demand.
  - Handled invalid and expired Google credentials with clear re-authentication alerts and modal key update actions.
  - Implemented `CredentialProtector` for AES encryption and secure masking of API keys and tokens in SQLite and UI views.
  - Added database migration `AddGoogleDrivePersistence` and updated SQLite schema models.
  - Added unit and integration tests in `GoogleDrivePersistenceTests` and `GoogleDriveWebControllerTests` covering first-time setup, subsequent login, multi-drive switching, caching, incremental sync, manual refresh, removal, re-authentication, and user isolation.
- **Google Drive Folder Explorer**:
  - Implemented Google Drive folder viewer allowing users to paste a Google Drive folder link (or raw folder ID) and browse all contained files in an interactive DataTables table.
  - Added `GoogleDriveHelper` in `PersonalFinance.Shared` for URL parsing (handling `/folders/`, `/u/0/folders/`, `/open?id=`, and raw IDs), friendly file category mapping, and human-readable byte formatting.
  - Added `GoogleDriveService` and `GoogleDriveController` in `PersonalFinance.ApiService` integrating with Google Drive API v3 with pagination and realistic demo fallback support.
  - Added `IGoogleDriveApi` Refit client with HTTP resilience handler.
  - Created MVC view `Views/GoogleDrive/Index.cshtml` with input form, sample link paste shortcut, metric cards, DataTables table (with custom ordering, responsive view, search, and pagination), and direct Google Drive file links.
  - Added xUnit unit test suite `PersonalFinance.Tests` covering URL parsing, byte formatting, MIME type resolution, and service handling.

### Changed
- **Site Layout & Navigation Redesign** (`_Layout.cshtml`, `_LoginPartial.cshtml`, `site.css`):
  - New sticky header with a brand mark, active-page indicator, and an account menu (avatar or initial, name, email, Account settings, Sign out) replacing the "Hello …!" text and Sign out button; signed-out visitors get **Log in** and **Create account** buttons.
  - Added a "Skip to main content" link, page titles in the form "Page · PersonalFinance", a light page background with a width-limited container, and a slim footer that stays at the bottom without fixed positioning.
  - Unified Bootstrap buttons, links and focus rings on one brand blue.
- **Google Drive Page Redesign** (`Views/GoogleDrive/Index.cshtml`, `explorer.js`):
  - Two-pane layout: a "Your drives" sidebar with file counts and a needs-attention indicator (a drive picker dropdown on phones), and one drive header showing items, size and "Synced … ago" with **Sync now**, **Open in Drive** and a **⋯** menu for Copy folder ID, API key and Remove drive.
  - Added a Spreadsheets section with visible **Budget report** and **Transactions** actions. The file table is reduced to Name / Type / Modified / Size with a single **View** menu for spreadsheets, and one search box and type filter now drive both the list and grid views.
  - Sample-data and expired-key warnings moved into the drive header, success messages show as a toast, the connect form became two numbered steps with the API key under "Advanced", and user-facing wording no longer mentions internal storage details. The help dialog is now **Help & setup**.
  - Removed the four stat tiles, file IDs in rows, and the Created column.
- **Budget Report & Transactions Modals Redesign**: Shared header layout with file name, data source and actions. The transactions table shows categories as chips, marks income with a green "+", hides the redundant Type column, and moves search and filters into one toolbar. New charts use the same validated blue/orange series colors as the existing year charts.
- **Spreadsheet Source Lookup**: Extracted `GoogleDriveService.ResolveSpreadsheetSourceAsync` so the budget report and transactions share the connection, cached-file and API-key lookup.
- **Faster Pull Request CI/CD Pipeline** (`.github/workflows/deploy-azure.yml`):
  - Container images now build as a parallel matrix (`api`, `web`) alongside `build-and-test` and `validate-infra` instead of sequentially after tests; `deploy-azure` still requires all three (`needs: [build-and-test, validate-infra, build-and-push-containers]`).
  - Pull requests read the Docker layer cache but no longer export it (the `mode=max` export took ~50s per image); `main` and manual runs still export it.
  - Added `concurrency` with `cancel-in-progress` for pull requests so a new push cancels the superseded run; runs on `main` queue rather than cancel, so deployments are never interrupted.
  - Added `paths-ignore` so changes limited to `**/*.md`, `.idea/`, `.junie/` and `.claude/` don't trigger the workflow.
  - Added NuGet package caching (`actions/cache` keyed on `Directory.Packages.props` and `*.csproj`) to `build-and-test`.
  - Images are pushed with the commit SHA tag only; `latest` is applied by `promote-latest`.
- **Dockerfiles**: Removed the redundant `dotnet build` step from the ApiService and Web Dockerfiles; `dotnet publish --no-restore` compiles once, saving a full compile per image.
- **Workflow Tests**: `GitHubActionsDeploymentWorkflowTests` now verifies that every job defines `timeout-minutes` (instead of asserting exactly four) and adds coverage for concurrency, `paths-ignore`, per-image cache scopes, deploy gating and `promote-latest`.
- **Monthly Budget Summary & Analytics Redesign**:
  - Redesigned the budget report modal in `Views/GoogleDrive/Index.cshtml` around a plain-language period summary (amount saved, savings rate, spending vs. plan, over-budget categories; best/toughest month and biggest expense in the year view).
  - Added a single period selector (Full year + months) with previous/next buttons and left/right arrow-key navigation.
  - Simplified KPI tiles (Income, Spending, Net saved, Savings rate) with whole-dollar amounts and "over/under plan" sub-lines; status is always shown as icon + label rather than color alone.
  - Replaced the category doughnut and rotated-label budget-vs-actual bar chart with per-category budget meters ("$X of $Y", "$N left/over", % used; within / near limit / over budget), with unbudgeted categories labeled "No budget set" instead of over budget.
  - Split year-view charts into "Income vs. spending by month" and "Net saved each month" (positive/negative bars instead of an overlaid line); clicking a month in either chart, or its table row, opens that month.
  - Adopted a validated, color-vision-deficiency-safe chart palette, hairline gridlines and compact currency axis ticks via new `bgt-*` styles in `wwwroot/css/site.css`.
  - Added dedicated loading and error states (with "Try again") so previous results are no longer shown while a new spreadsheet loads; the table view is collapsible.
- **Google Drive UI Storage Wording**: Replaced outdated SQLite references in `Views/GoogleDrive/Index.cshtml` and the `GoogleDriveController` connect message to reflect Azure SQL Database persistence via the API service, including a note about serverless auto-pause resume latency.
- **MVC & JavaScript Best Practices for the Google Drive Page**:
  - Moved ~700 lines of inline view script into `wwwroot/js/google-drive/explorer.js` and `wwwroot/js/google-drive/budget-report.js`, loaded with `asp-append-version` for cache busting and wrapped in strict-mode IIFEs to avoid global variables.
  - Removed Razor expressions from JavaScript; the report URL and connection ID are passed via `data-report-url` / `data-connection-id` attributes on `#monthlyBudgetModal`.
  - Modernized declarations from `var` to block-scoped `const` / `let` (applied with ESLint `no-var` and `prefer-const` autofixes).
  - Trimmed `Views/Shared/_Layout.cshtml` to load only jQuery, Bootstrap and `site.js` globally; removed unused Leaflet and Select2 assets from every page and moved DataTables and Chart.js into the Google Drive page's `Scripts` / new `Styles` sections.
- **Removed Boilerplate HomeController & Views**:
  - Removed obsolete template `HomeController`, default views (`Views/Home/Index.cshtml`, `Views/Home/Privacy.cshtml`), `ErrorViewModel`, and `Views/Shared/Error.cshtml`.
  - Updated `_Layout.cshtml` navigation and brand links to route directly to `GoogleDriveController`, removing dead links to `Home` and `Privacy`.
  - Configured standard `ProblemDetails` exception handling middleware in `PersonalFinance.Web/Program.cs`.
- **Streamlined Brevo Email Sender to Direct REST API**:
  - Removed legacy SMTP fallback pathways, `ISmtpClient` interface, and unused SMTP configuration options (`SmtpServer`, `Port`, `Login`, `Password`, `EnableSsl`) from `BrevoOptions` and `BrevoEmailSender`, standardizing all transactional email delivery strictly on Brevo's REST API endpoint.
- **Enforce Email Confirmation for User Registration**:
  - Configured ASP.NET Core Identity in `PersonalFinance.Web/Program.cs` with `options.SignIn.RequireConfirmedAccount = true` to require email and account verification before allowing user sign-in, mitigating spam account creation and unauthorized access.
  - Added test suite `IdentityConfigurationTests` verifying email confirmation enforcement and sign-in policy behavior for unconfirmed versus verified user accounts.
- **Default Application Route**: Configured the default MVC route in `PersonalFinance.Web/Program.cs` to route directly to `{controller=GoogleDrive}/{action=Index}/{id?}`, providing immediate access to the Google Drive file management portal upon landing.
- **Documentation & Architecture Overviews**: Updated `README.md` with comprehensive architecture overviews, data flow sequence diagrams, Google Drive analytics capabilities, caching workflows, credential encryption mechanisms, and end-to-end local development guides.
- **Google Drive Explorer UI/UX Redesign**:
  - Rebuilt `Views/GoogleDrive/Index.cshtml` into a modern, polished, intuitive file management interface inspired by Google Drive.
  - Introduced a streamlined search omnibox with inline quick actions ("Paste Sample", loading spinner, and collapsible API key drawer).
  - Added real-time category filter chips (All, Folders, Spreadsheets, Documents, PDFs, Images, Slides, Other) with dynamic count badges.
  - Implemented dual view modes: an interactive responsive DataTable view and a Google Drive-inspired card Grid view with smooth toggle support.
  - Upgraded file and folder indicators to crisp vector SVG iconography (matching Google Drive brand palettes) for all document types.
  - Added interactive clipboard copy actions with toast notifications for folder IDs and direct file links.
  - Enhanced empty states, loading states, and structured diagnostic troubleshooting cards.
  - Extended `site.css` with Google Drive design tokens, responsive cards, filter chips, and soft badges.
- **Google Drive Explorer Authentication & Authorization**:
  - Decorated `GoogleDriveController` in `PersonalFinance.Web` with `[Authorize]` attribute to enforce authentication before accessing the Google Drive explorer page or submitting queries.
  - Updated `Views/Shared/_Layout.cshtml` navigation bar to conditionally render the "Google Drive Files" menu link only for authenticated users.
- **Removed Items Page**:
  - Removed `ItemsController` and `Views/Items/Index.cshtml` from `PersonalFinance.Web`.
  - Removed the "Items Dashboard" navigation item from `Views/Shared/_Layout.cshtml`.
  - Cleaned up unused `IItemsApi` Refit client registration from `PersonalFinance.Web/Program.cs`.
- **Google Drive Explorer DataTable Sorting**:
  - Configured DataTable initialization in `Views/GoogleDrive/Index.cshtml` to sort files by Last Modified date descending by default, ensuring the most recently updated files are prioritized in view.
  - Added `orderBy=modifiedTime desc` parameter to Google Drive API v3 requests in `GoogleDriveService.cs`.
- **Google Drive Explorer Guidance & Diagnostics**:
  - Added clear instructions and callouts regarding the "Anyone with the link" (Viewer) folder sharing requirement across the web form, step-by-step modal guide, and `README.md`.
  - Enhanced `GoogleDriveService` with granular HTTP status code diagnostics (404, 403, 400) providing direct resolution steps when folder permissions are restricted or APIs are disabled.
  - Added an interactive error resolution card on the Google Drive view detailing the exact causes and steps to resolve folder permission and API key errors.
  - Added warning notices for empty folders and preview mode fallback states.
- **Serilog Console Theme**: Configured `AnsiConsoleTheme.Code` with formatted output templates across `PersonalFinance.ApiService` and `PersonalFinance.Web` for high-contrast, clear, and readable console log output.

### Fixed
- **Google Drive Page Layout Issues**: The file table no longer overflows on phones, DataTables sort arrows no longer overlap column headers, and files without a size show "—" consistently.
- **Floating-Label Placeholders on Login/Register**: Removed leftover template CSS that displayed placeholder text (e.g. "name@example.com") next to the floating field labels.
- **Unused Layout Stylesheet**: Removed `Views/Shared/_Layout.cshtml.css`, whose scoped styles were never loaded because the CSS isolation bundle isn't referenced.
- **Docker Layer Cache Collisions in CI/CD**: The ApiService and Web builds shared the default GitHub Actions cache scope and overwrote each other's cache, causing `dotnet restore` layer misses; each image now uses its own scope (`scope=api` / `scope=web`).
- **Outdated Deployment Documentation**: Removed SQLite references from `deployment.md`, corrected notes that described the API as internal-only (CI deploys it with `exposeApiPublicly=true`), added the required `ConnectionStrings__DefaultConnection` to the local Docker run commands, replaced the `deploy.ps1` "SQLite" option with a custom connection string option, and documented that `-DeploySqlDatabase` must be passed explicitly (the script sends `$DeploySqlDatabase.IsPresent`, so its `= $true` default has no effect).
- **Budget Report Calculations & Rendering**:
  - Fixed the year-view "Avg / month" figures dividing by 12 regardless of how many months the spreadsheet contains.
  - Fixed budget charts rendering blank when report data arrived before the modal finished opening (charts now re-measure on `shown.bs.modal`).
  - Fixed stale responses from a previously opened spreadsheet overwriting the current report.
  - Print now outputs only the budget report with the details table expanded, instead of the whole page.
- **Local SQL Provisioning Script Parameter Quoting on Windows PowerShell**:
  - Fixed `infra/deploy-local-sql.ps1` failing with `Failed to parse string as JSON` because Windows PowerShell 5.1 strips embedded double quotes from native command arguments, corrupting the inline `allowedClientIpAddresses` JSON array (and passwords containing quotes or symbols).
  - Deployment parameters are now written to a temporary ARM parameters file passed via `--parameters @file`, which is deleted after deployment.
  - Added `az` exit code checking so failed deployments report a clear error instead of an empty provisioning state.
- **Post-Deployment Smoke Test Timeouts & Hang Prevention in CI/CD**:
  - Enforced connection (`--connect-timeout 5`) and maximum execution (`--max-time 10`) timeouts on all `curl` probes in `.github/workflows/deploy-azure.yml`, preventing post-deployment smoke tests from hanging indefinitely on unresolved sockets or cold-starting Azure Container Apps ingress.
  - Added default HTTP `000` status capture fallback when network requests fail or time out, preventing broken subshell evaluations.
  - Added URL normalization and trailing slash stripping (`${WEB_URL%/}`) with fail-fast validation when target deployment endpoints cannot be resolved from Azure outputs.
  - Configured job-level `timeout-minutes` limits across all workflow stages (`build-and-test: 15`, `build-and-push-containers: 20`, `deploy-azure: 25`, `smoke-test: 10`) to eliminate runaway workflow execution and conserve runner compute resources.
  - Added unit test coverage in `GitHubActionsDeploymentWorkflowTests.cs` verifying job timeouts and smoke test curl configuration.
- **Database Migration Target Host Logging & Container Apps Troubleshooting**:
  - Enhanced `DatabaseMigrationExtensions.cs` to explicitly log the target database host (`DataSource`) during startup migration initialization, making connection resolution and host configuration immediately visible in Azure Container Apps logs.
  - Added troubleshooting guidance to `deployment.md` for diagnosing `SocketException: Cannot assign requested address [::1]:1433` (Error Number: 10049) when container instances lack connection strings and fall back to local IPv6 loopbacks.
- **CI/CD Docker Image Tag Mismatch in GitHub Container Registry (GHCR)**:
  - Fixed `docker/metadata-action` tag format in `.github/workflows/deploy-azure.yml` by setting `prefix=` for `type=sha,format=short`, ensuring built container images are tagged with `<short_sha>` (e.g. `:3d8c356`) matching the parameters passed to Bicep rather than defaulting to `:sha-<short_sha>`.
  - Added troubleshooting guidance in `deployment.md` for resolving `MANIFEST_UNKNOWN` container image pull errors and configuring GitHub Container Registry package visibility settings.
- **Sign Out Redirect to Removed HomeController**: Updated `_LoginPartial.cshtml` sign-out form `asp-route-returnUrl` from obsolete `Url.Action("Index", "Home", new { area = "" })` to `Url.Action("Index", "GoogleDrive", new { area = "" })`, ensuring user sign-out redirects smoothly to the active landing controller.
- **Aspire Dashboard OTLP Endpoint Environment Variables**: Corrected misnamed dashboard environment variables in `PersonalFinance.AppHost/Properties/launchSettings.json` to standard `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL` and `DOTNET_RESOURCE_SERVICE_ENDPOINT_URL`, resolving startup exceptions on dashboard resource configuration.
- **Client-side Validation Assets**: Installed `jquery-validate` and `jquery-validation-unobtrusive` via LibMan into `PersonalFinance.Web/wwwroot/lib/`, resolving 404 errors for `jquery.validate.min.js` and `jquery.validate.unobtrusive.min.js` referenced in `_ValidationScriptsPartial.cshtml`.
- **Google Drive Timeout & Cancellation Handling**: Added specific exception handling for `TaskCanceledException`, `OperationCanceledException`, and `TimeoutException` in `GoogleDriveService` and `GoogleDriveController`, preventing unhandled cancel errors when network queries time out and providing clean fallback preview responses. Aligned HttpClient and resilience pipeline timeout configurations.

### Security
- **Global Antiforgery Validation**: Registered `AutoValidateAntiforgeryTokenAttribute` as a global MVC filter in `PersonalFinance.Web/Program.cs` so every unsafe (POST/PUT/PATCH/DELETE) action validates antiforgery tokens by default.
- **HTML Escaping of Spreadsheet Content**: Budget report category names, month names and data-source labels read from Google Sheets are now HTML-escaped before rendering, preventing script/HTML injection from spreadsheet content.

### Commits
- `ee90f48` Release v1.1.0: Add spreadsheet transactions viewer, budget report enhancements, and UI redesign
- `fcd6322` Refactor budget and transactions modals for usability and consistency
- `9e95faa` Revamp layout and styling for a modernized UI/UX
- `f2503dd` Streamline Google Drive Manager UI and enhance folder connection workflow
- `2614c81` Add support for reading spreadsheet transactions from Google Drive
- `7f5d50f` Optimize CI/CD pipeline for speed and parallelism, add `promote-latest` job, and refine Docker builds
- `0b00771` Comprehensive documentation of architecture, deployment strategy, CI/CD pipelines, and Azure infrastructure setup
- `5b5dc3b` Update changelog: redesign budget analytics, improve Google Drive UI, and enhance security
- `ab29c58` Enable global anti-forgery validation and improve modal functionality
- `5b4841f` Redesign budget modal with improved layout and user guidance
- `2445956` Switch Google Drive storage from SQLite to Azure SQL
- `fb48353` Fix `deploy-local-sql.ps1` parameter quoting issue on Windows PowerShell
- `59296c2` Add local development provisioning for Azure SQL Serverless DB
- [PR #14](https://github.com/tngo0508/PersonalFinanceApp/pull/14) — Local dev provisioning
- [PR #15](https://github.com/tngo0508/PersonalFinanceApp/pull/15) — Update verbiage
- [PR #16](https://github.com/tngo0508/PersonalFinanceApp/pull/16) — Update deployment doc
- [PR #17](https://github.com/tngo0508/PersonalFinanceApp/pull/17) — Display transaction details

---

## [1.0.0] - 2026-09-20

### Added
- **Solution Architecture**: Created a modular multi-project .NET 10 solution:
  - `PersonalFinance.Shared`: Shared DTOs (`ItemDto`), API contracts (`IItemsApi`), exceptions (`ApiException`), and centralized version metadata (`AppVersion`).
  - `PersonalFinance.Data`: EF Core `AppDbContext`, ASP.NET Core Identity store, domain entities (`Item`), and database migrations.
  - `PersonalFinance.ApiService`: RESTful backend API with controllers (`ItemsController`), OpenAPI specification generation, Scalar interactive API documentation (`/scalar/v1`), and health checks (`/health`).
  - `PersonalFinance.Web`: ASP.NET Core MVC and Razor Pages frontend with ASP.NET Core Identity authentication, Refit type-safe HTTP client integration, resilience pipelines, DataTables, Bootstrap 5, Chart.js, and Leaflet.
- **SQLite Database Support**: Integrated `Microsoft.EntityFrameworkCore.Sqlite` for zero-configuration local database operations.
  - Added `DatabasePathHelper` to automatically resolve SQLite connection strings to a shared database file at the solution root (`PersonalFinance.db`), preventing split databases across projects during local development.
- **EF Core Code-First Support**:
  - Added `AppDbContextFactory` (`IDesignTimeDbContextFactory<AppDbContext>`) in `PersonalFinance.Data` for direct CLI migrations.
  - Generated `InitialCreate` migration establishing `Items` entity schema and ASP.NET Core Identity tables.
  - Added automatic database migration execution (`db.Database.Migrate()`) during development startup for both `ApiService` and `Web`.
- **Git Database Exclusion Rules**: Configured `.gitignore` to prevent committing SQLite database binaries and temporary lock files (`*.db`, `*.db-shm`, `*.db-wal`, `*.sqlite`, `*.sqlite3`, `PersonalFinance.db`).
- **Resilience & Observability**:
  - Configured Serilog structured logging with enrichers and console sinks.
  - Configured HTTP resilience pipelines with exponential retry and circuit-breaker patterns via `Microsoft.Extensions.Http.Resilience`.
- **Documentation**: Comprehensive `README.md` covering project architecture, SQLite setup, Git exclusion reasoning, EF Core Code-First workflow, and JetBrains Rider instructions.

### Changed
- Migrated database provider and connection strings from SQL Server (`(localdb)\mssqllocaldb`) to SQLite (`PersonalFinance.db`).
- Updated startup lifecycle in `PersonalFinance.ApiService` and `PersonalFinance.Web` from `Database.EnsureCreated()` to `Database.Migrate()` to support schema evolution via EF Core migrations.

### Commits
- `1d23a35` Switch database provider from SQL Server to SQLite.
- `3cffe38` Switch database structure to support SQLite with EF Core migrations. Add shared `DatabasePathHelper` for consistent solution-wide database paths.
- `72b6346` initial commit
- `e8c061a` Initial commit

---

## Links

- [Unreleased]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.5.0...HEAD
- [1.5.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.4.1...v1.5.0
- [1.4.1]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.4.0...v1.4.1
- [1.4.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.3.0...v1.4.0
- [1.3.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.2.0...v1.3.0
- [1.2.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.1.0...v1.2.0
- [1.1.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.0.0...v1.1.0
- [1.0.0]: https://github.com/tngo0508/PersonalFinanceApp/releases/tag/v1.0.0
