# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [4.2.3] - 2026-10-22

### Added

- **Architecture Tests for ADR 0007**: a new Architecture test group guards the Feature layout while ApiService and Web are moved to it (#156).
  - Every ApiService and Web type lives in a `Features.<Feature>` or `Infrastructure` namespace; today's type-based namespaces are tolerated through an explicit allow-list that #157 to #160 shrink and #161 deletes.
  - Every source file declares the namespace its folder implies, and a constructor parameter that crosses from one Feature into another is an interface.
  - A composition test hosts ApiService as the deployed app does and resolves every public interface and every Validator.

### Changed

- **Internal Validators Are Registered**: FluentValidation's assembly scan now includes internal types, so a Validator can be `internal` without silently dropping out of DI; `ChangeFeedbackStatusRequestValidator` is the first.

## [4.2.2] - 2026-10-22

### Changed

- **Explicit Types in C#**: `var` is now allowed only when the type is written on the right-hand side (`new T(...)`, a cast, a conversion such as `.ToList()` or `T.Parse(...)`, an anonymous type), following Microsoft's C# coding conventions.
  - `.editorconfig` sets `csharp_style_var_for_built_in_types` and `csharp_style_var_elsewhere` to `false:warning`, so `IDE0008` fails the CI build; `dotnet format style PersonalFinance.sln` fixes a branch before the PR.
  - 188 C# files rewritten by `dotnet format`; no behaviour change.
  - `docs/CODING_STANDARDS.md` gains a "C# Style" section (explicit types, visibility and sealing, method shape, async and time, DTOs and records) and AGENTS.md points to it.
- **ADR 0007, Feature Folders**: ApiService and Web will be organized by Feature, with a fixed folder anatomy and interface-only dependencies between Features, superseding ADR 0001 §3. The move itself follows in later releases.
  - GLOSSARY.md defines `Feature` without folder details and adds `Account Lifecycle`.

## [4.2.1] - 2026-10-22

### Fixed

- **Code Formatting Drift**: `dotnet format whitespace --verify-no-changes` in CI reports no changes again.
  - Re-indented and re-wrapped nine files from the Data Export, Deletion Request, Purge, Search and Feedback work; whitespace only, no code changes.
  - `ChangeFeedbackStatusRequest.cs` and its validator are saved as UTF-8 without a BOM, as `.editorconfig` requires.

## [4.2.0] - 2026-10-22

### Added

- **Automatic GitHub Releases**: Every merge to `main` that bumps `AppVersion` now gets a GitHub Release once it is deployed and the smoke tests pass (ADR 0006).
  - A `create-release` job in `deploy-azure.yml` tags `v{AppVersion.Current}` on the merged commit. The Release notes are that version's `CHANGELOG.md` section followed by GitHub's generated list of merged PRs. A version with a suffix (e.g. `-rc.1`) is marked as a pre-release.
  - The job skips with a notice when the tag already exists, so a re-run or a merge without a bump never fails the deployment.
  - A new `release-check.yml` workflow fails a PR unless `AppVersion.Current` is higher than on `main`, matches `Major.Minor.Patch[-Suffix]`, and has a `## [x.y.z] - YYYY-MM-DD` section in `CHANGELOG.md`. Docs-only PRs and PRs labelled `no-release` pass.
  - `.github/scripts/release-version.sh` holds the shared version, comparison and notes logic.

### Changed

- **One CHANGELOG Section per Version**: Each PR now adds its own `## [x.y.z] - YYYY-MM-DD` section instead of writing under `## [Unreleased]`, which is gone. AGENTS.md, CONTRIBUTING.md and the coding standards describe the new rule.
  - Everything that had built up under `[Unreleased]` since 2.0.1 (versions 2.0.2 to 4.1.1) is now the `[4.1.1]` section.

## [4.1.1] - 2026-10-22

### Changed

- **Privacy Policy and Terms Describe Account Deletion**: The legal pages now say how deleting an account really works, instead of promising a soft delete processed within 30 days (#144).
  - Privacy Policy: data is hidden at once, the emailed link cancels it for 30 days, and everything is erased within 31 days of the request; Community stories and comments stay as "Deleted User"; only a record of when is kept; backups expire within 7 days.
  - Both pages point to the Personal data page, where users download their data and delete their account themselves; the Contact page stays for other requests.

- **Landing Page Redesign**: The homepage now explains what the app is for, financial independence and the age you can reach it, with a calmer financial look.
  - The hero shows a sample projection chart that crosses the FI target (25 × annual spending) at age 52, with a visible caption describing the figures.
  - A "How the plan works" section states the mission and the three rules behind every plan: FI target, growth, and FI age.
  - Net Worth, Budget and Savings Goals lead as the three core tools; the other six features move to an "Also included" list.
  - New navy, ledger-green and brass palette with IBM Plex Sans, scoped to the landing page; the signed-in app is unchanged.
  - Removed the fake browser window, all-caps eyebrow labels, entrance animations and decorative icons.
  - Sign in and Register use the same theme: ledger-green buttons, focus rings and links, navy text and IBM Plex Sans. The shared tokens live in `css/ledger.css`; the auth page styles move from `site.css` to `css/auth.css`.
  - The Sign in and Register brand link goes to the homepage instead of Google Drive, and Register repeats the "No bank logins" promise under its button.
  - Account settings (Profile, Email, Password, Two-factor authentication, Personal data) use the same theme inside the signed-in layout, with a section list beside the form that becomes a scrollable tab row on phones (`css/account.css`).
  - The other Identity pages (forgot and reset password, email confirmation, resend confirmation, external sign-in, lockout, sign out) share one themed shell, `Account/_AuthLayout.cshtml`; the shared form, link and button styles move to `css/ledger.css`.

- **Agent Guardrails From the Global Search Retro**: Checks and docs now catch the two kinds of mistake that let the Global Search bug ship.
  - `TestSupport/AzureSqlTranslation` translates an EF Core query to Azure SQL without a database. `CODING_STANDARDS.md` and the add-a-feature checklist require it for string, date and collation-dependent queries, which the InMemory provider runs in memory.
  - ESLint warns on string concatenation (`prefer-template`), `function` callbacks (`prefer-arrow-callback`), `.then()` chains and `innerHTML` assignments. The pre-commit hook fixes the first two in every file a commit touches.
  - AGENTS.md drops the JavaScript style rules ESLint already enforces.

- **Code-Review Follow-Ups**: The remaining code-review findings for the refactor are resolved, and the container images build again.
  - Docker builds copy `Directory.Build.props` (it holds `TargetFramework`); without it `dotnet restore` failed with NETSDK1013 for both images. A test checks both Dockerfiles.
  - The spreadsheet reader and Google Drive file source require the Account id; the parser no longer swallows unexpected errors.
  - `[JsonApiErrors]` gets its logger through DI, and Web page actions share one error-message mapping (`ApiErrorMessages.ForUser`).
  - Savings Goals, Net Worth parameters and Budget Planner categories each have one shared definition (list order, current record, category list); the Actual Savings Rate is `decimal`.
  - One antiforgery-token helper in `core/utils.js` replaces seven lookups.
  - New HTTP-level tests host the real ApiService pipeline (Microsoft.AspNetCore.Mvc.Testing, MIT) and check `X-Internal-Key` handling end to end.

- **Contributor Onboarding and Build Standards**: New contributors get a CONTRIBUTING guide, an accurate architecture guide, and shared build, style and analyzer settings.
  - Add `CONTRIBUTING.md` (clone, configure, run, verify, PR checklist) and `docs/ARCHITECTURE.md` (projects, request and identity flow, add-a-feature checklist).
  - Remove the stale `docs/CQRS_ARCHITECTURE.md` (MediatR, rejected by ADR 0001) and outdated SQLite instructions; document the `InMemory:` connection-string mode.
  - Add `PersonalFinance/Directory.Build.props` (shared target framework, nullable, `latest-recommended` analyzers as warnings) and a root `.editorconfig`; apply `dotnet format whitespace`.
  - Group `PersonalFinance.Tests` into feature folders; remove the unused `PersonalFinance.slnx`.
  - CI reports formatting drift with a non-blocking `dotnet format --verify-no-changes` step.

- **Startup Composition**: Web and ApiService `Program.cs` now read as a short list of what each app uses, with the details in named extension methods.
  - Web registers all ApiService Refit clients through `AddInternalApiClients`, which always forwards the Account; a test fails if a contract is not registered.
  - ApiService registers Services per feature (`FeatureServiceExtensions`); shared Serilog setup lives in ServiceDefaults (`AddAppLogging`); `MigrateAndRunAsync` handles startup migrations and `--migrate-only`.
  - Cookie redirect handling for API and fetch requests is one tested helper (`ApiAwareCookieRedirects`).
  - Refit clients no longer add a second standard resilience handler on top of the one `AddServiceDefaults()` already applies, so retries are no longer nested.
  - Removed the redundant `AddUserSecrets` call, which put user secrets above environment variables; configuration precedence is the ASP.NET Core default again.

- **Google Drive Ingestion Modules**: The 1,846-line `GoogleDriveHelper` in Shared is replaced by focused ApiService modules, so Web no longer carries the spreadsheet parser.
  - `GoogleDriveFileSource` downloads spreadsheets (export/download URLs, API key choice, Account-scoped connection lookup); `BudgetSpreadsheetParser` parses CSV/XLSX; `GoogleDriveMetadata` and `SampleSpreadsheets` hold the small helpers.
  - `SpreadsheetReaderService` tries each download in turn and keeps the first with data; its tests use a fake file source instead of matching Google URLs.
  - `GoogleDriveFileDto.IsMonthlyBudgetSpreadsheet` is set by ApiService, so the Google Drive page no longer calls a parser helper.

- **Web Error Handling and Feature Contracts**: Web JSON actions share one error translator, and each feature has its own Refit contract.
  - `[JsonApiErrors]` turns ApiService failures into `{ "error": ... }` with the right status code (ApiService's own, 502 when unreachable, 500 otherwise); it replaces 23 hand-written try/catch blocks (Budget Tracker, Community, AI Assistant, Google Drive).
  - `IPersonalFinanceApi` (four features in one contract, DTOs declared inline) is split into `ISavingsGoalsApi`, `IWatchlistApi`, `IBudgetPlannerApi` and `ISpreadsheetAnalysisApi`, with DTOs under `Shared/DTOs/<Feature>/`.
  - Removed the Web `PersonalFinanceController`: its savings-goal, budget and watchlist-check endpoints had no callers, and it skipped antiforgery validation. The News watchlist now uses `/api/Watchlist`, which validates the antiforgery token.

- **Net Worth Parameters and Savings Rates Defined Once**: The Net Worth parameters, their defaults, the projection table, and the two savings-rate figures each have a single definition.
  - "Savings Rate" (planned, Net Worth/Dashboard) and "Actual Savings Rate" (budget months, Budget Report/Analysis) are separate GLOSSARY terms, each calculated by `FinanceMetrics` and rounded to one decimal place.
  - `NetWorthParametersMapping` (ApiService) and `NetWorthViewModel.ToProjectionRequest/ApplySaved` (Web) replace eight hand-written copies of the ten-field mapping; a test fails if a parameter is missed.
  - `NetWorthDefaults` is the one source of default parameters for the page, the projection endpoint, and the JavaScript Reset fallback (which used slightly different numbers).
  - The Net Worth page renders the Savings Rate from the parameters instead of a hardcoded 29.5%, and the projection table only in JavaScript (the Razor copy repeated the highlight rules).
  - `BudgetPlannerCategories` lists the ten Budget Planner categories once for the Dashboard and the AI Assistant.
  - A failed Net Worth save no longer shows the exception message.
  - The Budget Report and Analysis label budget-month figures "Actual savings rate"; the Net Worth page loads its parameters with the same current-record rule as the Dashboard.

- **AI Assistant Reads Budget Data Through the Provider**: The AI Assistant's tools read budgets and transactions only through `IBudgetDataProvider` (ADR 0002), and it uses the same "current" Net Worth parameters and budget Period as the Dashboard.
  - Removed the legacy path that downloaded Google Drive spreadsheets live during a chat (about 380 lines).

- **AI Assistant Experience**: Redesigned the chat with a roomier indigo panel, focused starter cards, readable conversations, and an accessible multiline composer.
  - Preserve visible follow-up messages and restore failed questions for retry.
  - Add keyboard controls, loading feedback, safe response formatting, and mobile navigation clearance.

### Added

- **Purge**: Accounts whose 30-day Grace Period has ended are now erased permanently by a daily job (#143, ADR 0005).
  - A Container Apps Job (`infra/modules/purge-job.bicep`, default 15:00 UTC) runs the API image with `--purge-only`, because the API scales to zero. It costs seconds of compute a day, within the free grant.
  - One transaction per Account: every user-owned record (including ones deleted earlier, AI usage, consents and Feedback), cached Google Drive file listings, and the sign-in account with its roles, logins, claims and tokens are deleted.
  - Community stories and comments stay, shown as written by "Deleted User"; ones the Account had deleted itself are erased. The Account's likes and poll votes are deleted and like counts corrected.
  - Only the Deletion Request remains, with its request and purge times under a placeholder id.
  - Safe to run twice or overlapping: a request cancelled meanwhile is skipped. The job exits non-zero if any Account failed; those are retried the next day.

- **Deletion Request**: An Account can delete itself from Account settings > Personal data > Delete my account, with 30 days to cancel from an emailed link (#142, ADR 0005).
  - Confirmed with the password, or by typing DELETE for an Account that signs in with Google only. The last Admin cannot delete itself.
  - Confirming hides every user-owned record with one shared `DeletedAt` stamp, locks the Account out (password and Google sign-in) with a new security stamp, signs out, and emails a single-use cancel link.
  - The cancel link opens a page whose button restores the Account and exactly the records the request hid; records deleted earlier stay deleted. The link stops working once used or after 30 days.
  - The sign-in Lockout page now explains how to cancel a pending deletion. Permanent erasure after 30 days is the Purge (#143).
  - New `DeletionRequests` table (migration `AddDeletionRequest`), which keeps only the hash of the cancel token.

- **Data Export**: An Account can download all of its data as one JSON file from Account settings > Personal data (#141).
  - Covers the account and its consents, Net Worth, Budget Planner, Budget Tracker, synced spreadsheet data, Google Drive connections, savings goals, saved articles, Community posts, likes and votes, Feedback, notifications and AI Assistant usage.
  - Only live records: soft-deleted rows are left out. Secrets never appear (password hash, security stamp, Google Drive API keys).
  - Replaces the Identity UI default Personal data page and its download, which held only the sign-in fields plus the authenticator key; the old download address now redirects to Personal data. New `GET /api/data-export` endpoint and `IDataExportApi` contract.

- **About Page**: A public About page at `/about` explains what Personal Finance is, why it exists, who builds it and its core features, for signed-out and signed-in visitors.
  - Served by `HomeController`; it needs no sign-in and makes no API call.
  - Linked from the footer on every page and from the Landing Page hero.
  - A "Not financial advice" section links to the Disclaimer.

- **Send Feedback**: Signed-in Accounts can send Feedback (Bug, Idea or Other, up to 2,000 characters) from a "Send feedback" link in the sidebar, the footer and the About page.
  - The page the Account came from is captured automatically and shown on the form: its app-relative path and query string, at most 500 characters, and never the Send feedback page itself.
  - Feedback is stored against the Account with status New; it is user-owned and soft-deleted like other records.
  - New `Feedback` table (migration `AddFeedback`) with an index on status and created date for the Admin inbox.
  - `POST /api/feedback` requires a signed-in Account and validates the request with `SubmitFeedbackRequestValidator` (an empty or whitespace-only message is rejected) before `IFeedbackService` stores it. Feedback messages and limits have one source each (`FeedbackMessages`, `FeedbackLimits`).
  - The form keeps the user's input on validation errors, uses an anti-forgery token and Post/Redirect/Get, and shows a friendly message when ApiService fails.

- **Admin Feedback Inbox**: Admins read all Feedback in a Feedback inbox linked from the Admin Dashboard, newest first.
  - Each item shows its Category, status, message, the Account email, the page it was sent from and the date (UTC).
  - `GET /api/feedback` and the `Admin/Feedback` page both require the Admin Policy; soft-deleted Feedback is left out.
  - One `AsNoTracking` query joins the Account email, orders by date then id, and is checked against Azure SQL translation.
  - An empty inbox says so for the chosen filter (for example "No open Feedback"); a failed load shows the Unavailable State, whose Retry now reloads the same page and filter.

- **Feedback Rate Limit**: An Account can send at most 5 Feedback entries per hour; the 6th gets a friendly "try again later" message instead of being stored.
  - `POST /api/feedback` returns 429 and stores nothing; the limit is per Account, so other Accounts are unaffected.
  - Feedback has its own keyed `RateLimiterService` instance (5 per sliding hour, set in `FeedbackRateLimit`), so the AI Assistant's shared limiter keeps its limits.
  - Only valid submissions count toward the limit (the endpoint validates before the service is called), and the Web form keeps the user's input when it is reached.

- **Feedback Status Triage**: Admins mark Feedback as New, Reviewed or Archived from the Feedback inbox and filter the inbox by status.
  - The inbox opens on "Open" (New and Reviewed); Archived Feedback is hidden unless the Archived filter is chosen.
  - `GET /api/feedback?status=` filters the list; `PUT /api/feedback/{id}/status` changes the status, returns 404 for unknown or soft-deleted Feedback and 400 for an unknown status. Both require the Admin Policy.
  - The inbox's "Mark ..." buttons post with an anti-forgery token and return to the same filter with a confirmation or a friendly error.
  - The filtered inbox query is checked against Azure SQL translation.

- **Local SQL Server in Docker**: Developers can run the whole app against a local SQL Server 2022 container that uses the same provider and migrations as production Azure SQL.
  - `compose.yaml` at the repo root runs SQL Server 2022 Developer edition (`platform: linux/amd64`, so it runs under Rosetta on Apple Silicon) with a named data volume and a `sqlcmd` healthcheck; it stops with a clear message when `MSSQL_SA_PASSWORD` is missing.
  - `.env.example` lists `MSSQL_SA_PASSWORD` and `SQL_HOST_PORT`; the real `.env` stays gitignored.
  - `npm run db:up`, `db:down` and `db:reset` only call `docker compose`, so they work the same in PowerShell and zsh.
  - The development guide has a "Local SQL Server (Docker)" section and the settings needed to run the e2e suites with the projects started separately.
  - A unit test shows a local SQL Server connection string selects the same provider and migration set as Azure SQL.
  - The docs and agent rules treat local SQL Server as a supported development target; production stays Azure SQL, and only T-SQL and EF features both support may be used.
  - A new `sql-server-migrations` CI job starts SQL Server with `npm run db:up` and a random per-run `sa` password, runs the ApiService with `--migrate-only` twice (the second run must apply nothing), dumps the container and ApiService logs on failure and always runs `db:down`; the Azure deployment now waits for it.

- **AI Assistant Finance Scope Protection**: The AI assistant now rejects non-financial queries instantly with a canned refusal, saving OpenAI API tokens.
  - Deterministic keyword-based classifier (`AiScopeFilter`) checks each message before it reaches the LLM.
  - Off-topic queries (programming, trivia, creative writing, etc.) receive a static refusal with zero token cost.
  - Small talk greetings receive a brief acknowledgement guiding the user toward financial topics.
  - Prompt injection attempts are detected and refused.
  - All AI responses include a non-fiduciary disclaimer.
  - Input length is limited to 2,000 characters per message.

- **AI Assistant Token Usage Tracking and Spend Cap**: Every AI request now records its token counts and estimated cost in the database.
  - New `AiUsageRecord` entity tracks prompt tokens, completion tokens, and estimated cost per request.
  - Per-user daily message limit (30 messages/day) prevents abuse.
  - Global monthly budget ceiling ($10/month) with a hard kill switch — requests are rejected with HTTP 429 when limits are exceeded.
  - Budget warning at 90% of the monthly ceiling.
  - Cost calculation uses `gpt-4o-mini` pricing ($0.15/1M input, $0.075/1M cached, $0.60/1M output).

- **AI Assistant Compact System Prompt**: Replaced the monolithic full-transaction prompt with a compact, cacheable financial overview.
  - The system prompt is now stable across calls, enabling OpenAI prompt caching for lower costs.
  - Financial data (budget, transactions, savings goals, net worth) is available through on-demand tool calls.
  - Chat now runs through `Microsoft.Extensions.AI` `IChatClient` with `UseFunctionInvocation` (max 3 tool rounds); the four read-only tools (`get_budget_summary`, `get_recent_transactions`, `get_savings_goals`, `get_net_worth_details`) are bound to the authenticated user.
  - Token usage, including cached prompt tokens, is aggregated across tool rounds before it is recorded; client-supplied history is capped at 10 messages of 2,000 characters.
  - `max_completion_tokens` is applied to all requests (configurable via `OpenAI:MaxTokens`, default 400).

### Fixed

- **Google Sign-In Respects Lockout**: Signing in with Google to an existing, locked-out account by its email linked the Google login and signed in anyway; it now shows the Lockout page and links nothing.
- **Account Deletion No Longer Orphans Data**: The built-in Delete personal data page deleted only the sign-in Account and left all of its financial data behind; it is replaced by the Deletion Request.

- **Build warnings:** A clean build of the whole solution, tests included, now reports zero warnings, and CI fails on any new one (#125).
  - Budget period keys and counters are formatted and parsed with the invariant culture, and the savings goal target date is parsed as invariant ISO `yyyy-MM-dd`; relative-time labels and the figures in AI Assistant prompts use the app culture (en-US), so a host locale change no longer alters them.
  - Aspire is upgraded from 13.6.1 to 13.6.3, which removes the vulnerable MessagePack version (moderate severity, NU1902); the AppHost now resolves MessagePack 2.5.307.
  - Both full-solution Release builds in the deploy workflow pass `-p:EnforceWarningsAsErrors=true`, so a warning fails CI; a test checks the flag stays on.
  - The .NET SDK is pinned to 10.0.402 (latest patch) in a repo-root `global.json`, and every `setup-dotnet` step in CI installs it from that file, so newer analyzers (such as CA1873) can't break CI before they reach local builds; a test checks the workflow keeps using it.
  - The 35 Information and Debug log calls that SDK 10.0.402 flags (database migration, RBAC seeding, ApiService services and controllers, Web controllers, external login and the Brevo email sender) are wrapped in `IsEnabled` checks, so their arguments are only evaluated when the log is written (CA1873); messages and levels are unchanged.
  - The hardcoded AES key and static IV in credential protection are tracked in #133; CA5351 is suppressed on that line until then, so stored Google Drive connections keep working.

- **Startup Failure Exit Code**: The ApiService and Web app now exit with code 1 when startup fails, so a failed `--migrate-only` run no longer looks successful (#108).
  - The fatal "Application terminated unexpectedly." log line and the log flush are unchanged.
  - A clean shutdown, a successful `--migrate-only` run and EF design-time tooling (`dotnet ef`, which aborts the host) still exit 0; a cancellation counts as shutdown only after the host has started stopping.
  - The `sql-server-migrations` CI job now fails on the exit code and checks the `__EFMigrationsHistory` row count (the second run must not change it) instead of searching for exact log lines.

- **Stale DbContext Reference in the Development Guide**: The `dotnet ef database update` example named `SqlServerAppDbContext`, which does not exist; it now uses `AzureSqlAppDbContext`.

- **Sign In and Register Error Messages**: Field errors such as "The Email field is required." now appear under each field with a red border; they were always hidden because ASP.NET marks errors with `field-validation-error`, not Bootstrap's `.is-invalid`.

- **Signed-Out Header on Phones**: The Sign in and Create account buttons stay on one row inside the top bar instead of stacking and being cut off.
  - Below 480px the buttons are compact and the brand name is visually hidden (still read by screen readers).
  - "Log in" is renamed "Sign in" in the header, the Sign in page title and related links, matching the Sign in page itself.

- **Budget Categories in Global Search**: Global Search now finds Budget Categories by name from the user's current Data Source, replacing the "Budgets" group that only matched period strings like "2026-10" (#95).
  - Budget Tracker source: Budget Categories by name, excluding deleted ones; the result opens the Budget Tracker at the current month.
  - Google Sheet source: the distinct category names on synced transactions; the result opens the current month in Analysis.
  - At most 5 categories, ordered by name. The response group is now `categories` instead of `budgetItems`.

- **Global Search Dropdown**: The search dropdown now shows the Unavailable State with a retry when a search fails, instead of failing silently, and works fully from the keyboard and with screen readers (#94).
  - "No results for ..." appears only when a search succeeded with no matches.
  - Up/Down move through results, Enter opens the highlighted one, Esc closes the list and keeps the text.
  - The input is an ARIA combobox with a listbox of options; a polite live region announces the result count.
  - Only the latest query's response is shown; earlier requests are cancelled.
  - Results are built with `textContent`, and only same-site links are rendered. The module uses `async/await`.

- **Global Search Follows the Data Source**: Budget Tracker users now find their Budget Transactions in Global Search; transactions come only from the user's current Data Source (#93).
  - Budget Transactions match on description or Budget Category name. A deleted category still names the past transactions that used it.
  - Budget Tracker results open that month in the Budget Tracker. Google Sheet results still open that month in Analysis.
  - Switching the Data Source changes the results immediately. Savings Goals are searched for either source.
  - ADR 0002 §3 is amended, and **Global Search** is added to the glossary.

- **Global Search Returns Results**: The top-bar search now finds the signed-in Account's synced transactions and Savings Goals on Azure SQL; before, every search failed silently (#92).
  - Matching is a case-insensitive "contains" that EF Core translates to SQL; LIKE wildcards in the query (`%`, `_`, `[`) match literally.
  - The result groups are read one after another, because one `DbContext` does not allow concurrent queries.
  - Queries are trimmed, cut to 100 characters, and ignored below 2 characters. Results have deterministic order and caps: 10 newest transactions, 5 goals by name.
  - Transaction results show the date and link to that month in Analysis.
  - The Search DTOs are one shared set used by the API Contract and the Service.
  - A new test translates every search query to Azure SQL without a database, which the in-memory tests cannot do.

- **Unavailable State on Every Page**: Budget Planner, Net Worth, Savings Goals and Community show the Unavailable State when their data cannot be loaded, instead of defaults or an empty page.
  - Budget Planner and Net Worth no longer show default values after a failed load, so a user cannot save the defaults over their real data.
  - Savings Goals and Net Worth error messages no longer include exception text, and errors after a redirect now reach the page.
  - The Savings Goals validation summary was never displayed; it now is.
  - `GetBudgetData` answers with a JSON error instead of an empty budget; deleting Net Worth parameters reports a failure instead of ignoring it.

- **Dashboard After Sign-In**: The Dashboard no longer shows empty data when ApiService is still starting after being idle.
  - When the summary cannot be loaded, the Dashboard shows the Unavailable State ("We couldn't load your dashboard") with a Retry button, instead of empty sections and setup cards.
  - Opening the Login page sends a throttled, anonymous `/alive` ping that wakes ApiService while the user signs in.

- **Notification Bell, Global Search and Safe Error Messages**: The notification bell and global search work again, and Web never shows raw ApiService responses or exception text.
  - The app shell called `/api/Notification` and `/api/Search` on Web, which had no such endpoints (every page logged 404s); new Web controllers forward them to ApiService, and marking notifications read sends the antiforgery token.
  - The Budget Planner no longer shows the raw ApiService response body or the exception message when a save fails.
  - A test fails when JavaScript calls an `/api/` path that has no signed-in-only Web controller.

- **Budget Reports Show Only Real Spreadsheet Data**: When a Google Drive spreadsheet cannot be read, the Budget Report now explains why instead of showing a made-up 12-month budget, and a sync never saves invented months.
  - Removed the invented fallback report (ApiService reader, Web controller, and parser) that looked like real data and could be stored as snapshots, feeding Analysis, the Dashboard and the AI Assistant.
  - Months missing from a sheet stay empty instead of copying another month, and a month with totals but no category rows no longer gets an estimated category split.
  - `MonthlyBudgetReportDto.ErrorMessage` carries the reason; the Budget Report modal shows it.
  - Google Drive download URLs (which include the API key) are no longer written to the logs.

- **Account Isolation in ApiService**: Every ApiService endpoint now identifies the Account from authenticated claims, so a spoofed `X-User-Id` header or a foreign `connectionId` can no longer reach another Account's data.
  - Budget, Community, Dashboard, NetWorth, Notification, SavingsGoals, Search and Watchlist controllers read the raw header (skipping the `X-Internal-Key` check); they now require `[Authorize]` and use `User.GetUserId()`.
  - Google Drive endpoints no longer accept `userId` in the query string or body, and a spreadsheet `connectionId` only resolves for its owner, so another Account's stored API key cannot be used.
  - Web forwards the Account on the Google Drive client too, and no longer sends a placeholder `anonymous` id for signed-out visitors.
  - A convention test fails the build if an ApiService controller lacks `[Authorize]` or `[AllowAnonymous]`.

- **AI Assistant Ticket Completion (#77, #78, #79)**: Closed implementation gaps in finance scope filtering, quota enforcement, and financial tools.
  - Refuse off-topic and mixed requests before model calls using whole-word classification.
  - Track summary usage through the shared AI client, apply completion limits, and expose configurable daily/monthly quotas and budget warnings.
  - Preserve HTTP 429 messages through the Web proxy and display warnings and disclaimers with CSRF-protected requests.
  - Add stable compact financial snapshots, fetch detailed budget data on demand from the selected source, include goal and retirement projections, and limit tool invocation to three rounds.

- **Production Admin Provisioning**: The deploy now sets `Rbac__AdminEmail` (default `tngo0508@gmail.com`) and the shared `Internal__ApiKey` on the Web and ApiService container apps.
  - New Bicep parameters `rbacAdminEmail` and `internalApiKey`; the workflow reads `vars.RBAC_ADMIN_EMAIL` and `secrets.INTERNAL_API_KEY`.

- **Admin Authorization Hardening**: ApiService no longer grants role claims to a caller that only supplies `X-User-Id`.
  - New shared secret `Internal:ApiKey` (`Internal__ApiKey`): Web sends it as `X-Internal-Key`; when configured, ApiService rejects requests without it. Without it, roles are only attached in Development.
  - ApiService now calls `UseAuthentication()` before `UseAuthorization()`.
  - The last-Admin check and removal are serialized so concurrent removals cannot leave zero Admins.
  - The `User` role backfill runs once (when the role is first created) using a database-side query, so manual removals stay permanent.

### Added

- **Default User Role**: Every newly registered account is now automatically assigned the `User` role; `User` and `Admin` are the two built-in roles.
  - Applies to password and external-login registration; existing accounts are backfilled once, when the role is first created by the seeder.

- **Admin Role Management**: The Admin Dashboard can now assign and remove roles for accounts and add or delete roles.
  - New admin-only endpoints under `api/admin` (`users`, `roles`, `users/{id}/roles/{role}`) return 403 to non-admins.
  - Built-in roles cannot be deleted, a role with members cannot be deleted, and the last Admin or your own Admin role cannot be removed.
  - Role changes update the account's security stamp so the Web session picks up the change.
- **Role-Based Access Control and Admin Dashboard**: Added Identity roles with named policies and an admin-only Admin Dashboard.
  - `AppRoles` and `AppPolicies` (`RequireAdmin`) live in `PersonalFinance.Shared`; both apps enable `.AddRoles<IdentityRole>()` and register the policy.
  - On startup, `RbacSeeder` creates every role and grants Admin (add-only) to the confirmed account matching `Rbac:AdminEmail`; see `docs/DEVELOPMENT.md`.
  - ApiService `XUserIdAuthenticationHandler` loads roles from the database on every request; a client-supplied roles header is never trusted (ADR 0004).
  - Web `/Admin` returns 404 to non-admins; the Admin sidebar group and profile-dropdown link appear only for admins.
  - `GET api/admin/summary` (403 for non-admins) feeds the dashboard with account counts only.

### Security

- **External Login Email Confirmation**: Closed an account-takeover path where an email typed during external login was auto-confirmed and linked to an existing account.
  - A typed email now creates an unconfirmed account and sends the standard confirmation email; it is never linked to an existing account.

### Changed

- **Agent Guidelines and E2E Tooling**: Shrank `AGENTS.md` into a short, always-loaded rule set and moved the full standards to `docs/CODING_STANDARDS.md`.
  - `AGENTS.md` and `.junie/AGENTS.md` now list the verify commands (`dotnet test`, `npm run lint`), a "Where things live" map, search-exclusion hints and the done checklist; duplicated and unused sections (Git Worktrees) were removed.
  - Added `npm run e2e:preflight` (`e2e-tests/preflight.js`) that fails fast when the database, Web app or API is unreachable, and documented "Running the E2E tests locally" in `docs/DEVELOPMENT.md`.
  - Added `ModelValidationConventionTests`, which fails when `[Required]` is applied to a `bool` property.
- **README Architecture Section**: Rewrote the Architecture section to accurately reflect the current layered architecture.
  - Replaced "Clean Architecture" with "layered architecture" and added layers, dependency flow diagram, and key principles.
  - Documented direct service injection, type-based organization, soft deletes, and permissive license policy.
  - Added links to Architecture Decision Records (ADR-0001, ADR-0002).
  - Removed outdated references to SQLite, `GoogleDriveHelper`, and `CredentialProtector`.
  - Updated Tech Stack table to reflect Azure SQL Database as the sole database.

- **README**: Updated Quick Start section with user secrets setup commands and added new features (Budget Tracker, Dashboard & Navigation, Landing Page).
  - Added user secrets configuration commands in the Run Locally section.
  - Included important note directing users to DEVELOPMENT.md for complete setup instructions.
  - Added feature descriptions for Budget Tracker, Dashboard & Navigation, and Landing Page.

- **Development Documentation**: Enhanced the user secrets section in DEVELOPMENT.md with comprehensive management guidance.
  - Includes all `dotnet user-secrets` commands for both Web and ApiService projects with placeholder values.
  - Covers database connection strings, Brevo API key, Google OAuth credentials, Google Drive API key, and OpenAI API key.
  - Provides verification, reset, and single-secret removal commands.
  - Added a dedicated section explaining where user secrets are stored (`%APPDATA%\Microsoft\UserSecrets\`), how to find the SecretId from `.csproj` files, and how to view configured secrets.

### Added

- **Legal Pages and GDPR Compliance**: Added legal pages and consent features to reduce legal exposure from storing user financial data.
  - Added Privacy Policy, Terms of Service, Financial Disclaimer, Cookie Policy, and Contact pages.
  - Added two-column footer with legal links and app version display.
  - Added consent checkbox to registration form with policy version tracking (UserConsent entity).
  - Added GDPR cookie consent banner that defers third-party scripts (Google Fonts, Lucide icons) until user consent.
  - Cookie consent stored in a 180-day cookie with "Accept All" and "Essential Only" options.
- **Budget Tracker**: Added an in-app monthly budget tracker reproducing the Google "Monthly budget" sheet layout and
  behavior with Summary and Transactions tabs.
    - Users can enter planned amounts, actual overrides, and dated transactions (expenses and income) directly in the
      application, stored in Azure SQL Database.
    - Summary tab computes Planned, Actual, and Diff per category with totals and comparison bars (Diff for Expenses:
      Planned − Actual; Diff for Income: Actual − Planned).
    - Transactions tab displays side-by-side dated expenses and income entries with inline editing and confirmation on
      deletion.
    - Categories are seeded from defaults, customizable, reorderable, and soft-deleted/archived without losing past
      months' data.
- **Budget Data Source Switch & Active Source Routing**: Analysis and the Dashboard now read data from the user's active
  budget data source (Google Sheet or Budget Tracker).
    - Segmented data source control allows users to switch between Google Sheet and Budget Tracker with anti-forgery
      protection while preserving period/month query parameters.
    - Source-named empty states provide actionable links to add data or switch sources without silent fallbacks.
    - The Dashboard Monthly budget card shows planned vs actual income and expenses for the latest month of the active
      source, with the source toggle and a source-named empty state.
    - Recent activity labels the active source.
- **Budget data provider**: Added a unified user-selected read path for Google Sheet or Budget Tracker budget months and
  transactions.
    - Monthly sheet aggregation retains its snapshot count; trends retain individual sheet snapshots, while Tracker
      trends have one point per available month.
- **Landing Page**: Signed-out visitors at `/` now see a public Landing Page instead of being redirected to the login
  form; signed-in users at `/` are redirected to their Dashboard.
    - New anonymous `HomeController` is the default route; `/Dashboard` still requires sign-in and deep links return
      after login.
    - Hero with "Create free account" and "Sign in" calls to action, a static HTML/CSS product preview labelled
      "Sample data" (hidden from assistive technology), a nine-feature grid, a three-step "how it works", a privacy
      note, and a closing call to action. No testimonials, user counts, or ratings.
    - Page styles live in `landing.css` (`lp-*` prefix) on Indigo Ledger tokens; no chart library; entrance motion is
      disabled for reduced-motion users; single-column layout with full-width buttons on mobile.
    - Brand links in the sidebar and signed-out top bar, and the sign-out return URL, now point to `/`.
    - Unit tests for the Home controller and Playwright assertions for the Landing Page at `/`.
- **Dashboard Redesign**: The Dashboard now leads with the user's Net Worth and Progress to FI, followed by key
  figures, the planned budget breakdown, savings goals, recent activity, quick actions, and setup guidance.
    - Summary card shows Net Worth (from Net Worth settings), FI Target, and a Progress to FI bar with an "FI target
      reached" badge, or a prompt to add annual spending when it is zero.
    - Key figure tiles: Net Worth, Savings Rate (unavailable when annual income is zero), Budgeted income, and Goals
      funded (saved ÷ target across all goals, each goal capped at its target so an over-funded goal cannot mask
      unfunded ones, with "N of M complete").
    - Budget card shows all ten categories in dollars and percent, with "Unallocated" for any remainder, the period
      label, a colour-blind-checked donut, and a legend that doubles as the text alternative.
    - Goals card has accessible progress bars and completion badges; recent activity shows a type icon per item.
    - "Connect Google Drive" setup card appears only for users without a Google Drive connection; the setup section
      hides once every step is complete.
    - Quick actions redesigned as a keyboard-accessible grid, now including the Investment Calculator.
    - Dashboard summary API extended additively with FI, savings rate, budget allocation, goals overview, and Google
      Drive connection fields.
    - Net worth projection chart plots the Base, Conservative, and Aggressive scenarios from today to retirement age
      with a dashed FI Target line, year/age tooltips, and the age the FI target is reached, using the same
      calculation as the Net Worth page.
    - Projection chart axis always includes the FI Target line and any negative balances; Progress to FI is floored
      so it never reads 100% before the target is reached; an API timeout renders the empty Dashboard instead of an
      error page.
    - Polish and accessibility pass across the Landing Page and Dashboard: consolidated stylesheet, one short
      entrance fade and 150ms hover/focus transitions disabled under reduced motion (including chart draw-in
      animations), descriptive link text, AA-contrast goal badges, Dashboard content kept clear of the mobile bottom
      navigation, and Playwright checks at 1280px and 390px widths.
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

- **Budget Planner Flexible Allocations & Warning**: Replaced blocking allocation constraints with an informational warning when budget allocations exceed monthly income.
  - Allowed budget allocations to be saved whether under, equal to, or exceeding monthly income, empowering users to plan ahead flexibly without data loss.
  - Displayed a persistent warning banner upon saving when allocations exceed monthly income and updated UI remaining indicators.
- **Budget Planner Transportation Field Binding**: Fixed Transportation allocation resetting to an empty string on save due to a missing `asp-for` tag helper.
  - Added `asp-for="Transportation"` to the Transportation amount input field in `PersonalFinance.Web/Views/Budget/Index.cshtml`.
  - Added unit test asserting all 10 budget categories in `Index.cshtml` have correct `asp-for` bindings to prevent future regressions.
- **Cookie Banner `[hidden]` Safeguard**: Added `.cookie-banner[hidden] { display: none; }` so a future `display` rule on the banner can no longer override the `hidden` attribute and keep it permanently visible.
  - Added Playwright checks (Test 8b) for the banner: visible with no stored choice, hidden after "Accept All", and hidden on reload for "Accept All" and "Essential Only".
- **Contact Page Email**: The Contact page now uses `tngo0508@gmail.com` for both general and security inquiries instead of the unconfigured `support@`/`security@personalfinance.app` addresses.
- **Registration Consent and Cookie Banner**: Fixed the Terms/Privacy consent not being enforced on the server and the cookie banner reappearing for visitors who already accepted all cookies.
  - Registration now rejects submissions where the Terms of Service / Privacy Policy checkbox is unchecked (`[Required]` on a `bool` never fails).
  - The cookie banner is rendered hidden and only shown when no consent choice is stored.
  - Legal links now clear the ambient route area (`asp-area=""`) so they resolve on Identity pages, and `LegalController` is explicitly `[AllowAnonymous]`.
  - Registration E2E test now ticks the consent checkbox.
- **Budget Planner Exact Amounts**: The Budget Planner now stores each category's dollar Allocation Amount exactly as typed, so $700 stays $700.00 after saving and reloading.
  - Added per-category amount columns (`decimal(18,2)`) and migrated existing budgets from percentages.
  - Dropped the stored percentage columns; percentages are always derived from amounts.
  - JavaScript state uses integer cents to avoid floating-point drift.
  - Initial page data renders with invariant culture, so any server locale works.
- **Budget Planner Validation**: Save is only allowed when allocation amounts sum to Monthly Income exactly, with clear error messages.
  - Added a FluentValidation validator in the ApiService that returns 400 on validation failure.
  - Web controller maps API validation errors into ModelState and preserves user input.
  - Remaining shows both dollars and percent; the exact badge only appears at $0.00 remaining.
- **Budget Planner Responsive Layout**: Long numbers up to $10,000,000.00 now display in full without clipping at any screen width from 320px up.
  - Replaced fixed input widths with flexible min-width-based sizing.
  - Category grid uses shrinkable columns with an earlier single-column breakpoint.
  - Card headers, controls, and summary rows wrap neatly; the slider takes a full row on narrow cards.

### Changed

- **Budget Planner Contract**: The Budget allocations DTO now carries dollar amounts instead of percentages.
  - XML documentation clarifies that each property is a dollar amount.
  - Dashboard, AI Assistant, and Search all derive percentages from amounts.
- **Pre-commit Hook**: Added Husky pre-commit hook with lint-staged to auto-fix ESLint errors before commits.
  - Installed Husky 9 and lint-staged 15 as dev dependencies.
  - Staged JavaScript files are auto-formatted by ESLint before each commit.
  - Fixed `npm run lint` scripts to work on Windows (removed Linux-style env var syntax).

- **NetWorth Reset Button**: Reset now restores saved parameters as a frontend-only action instead of soft-deleting database records.
  - Reset restores the user's last saved values from a JavaScript cache populated on page load.
  - Reset is a dropdown button with "Reset to saved" (primary) and "Clear saved parameters" (destructive, with confirmation dialog).
  - Clear saved parameters reuses the existing `DeleteParameters` endpoint with a detailed confirmation warning.
  - Falls back to hardcoded defaults with a message when no saved parameters exist.
  - Randomize remains unchanged (already frontend-only).
  - Documented in ADR-0003.

- **ESLint Code Formatting**: Fixed 227 style and formatting violations across frontend JavaScript files.
    - Resolved `space-before-function-paren` and `object-curly-spacing` rule violations across all files under
      `PersonalFinance.Web/wwwroot/js/`.
    - Ensured static analysis passes with zero errors in CI/CD pipeline.

- **Investment Calculator Chart Rendering**: Prevented the Growth Over Time canvas from being rescaled by conflicting
  CSS, improving sharpness and label readability across screen sizes.
- **Budget Tracker Month/Year Selectors**: Month and year dropdowns now fill the navigation bar on mobile screens.
  - Selects flex to share the row between the arrow buttons; extra right padding keeps text clear of the arrow.
- **Budget Data Source Switch**: Fixed "Failed to switch budget data source" when choosing Budget Tracker on Analysis.
  - The Web Refit client serialized the `BudgetDataSource` enum as a camelCase string, which ApiService could not bind
    (HTTP 400); the enum now carries a `JsonStringEnumConverter` so both sides accept names and numbers.
- **Sync Year Detection**: Fixed a synced month (e.g. "Monthly budget - October 2026") being stored under a wrong year
  such as 2072.
  - The year in the file name is now authoritative; sheet cells are only a fallback and monetary amounts (e.g. `2072`)
    are no longer mistaken for years.
- **Dashboard Fabricated Net Worth Chart**: Removed the net worth "trend" chart, which plotted invented history (the
  current figure multiplied by 1.05–1.20 and labelled "1Y ago" to "Now"); no net worth history is stored.
  - The Dashboard is rebuilt on an Indigo Ledger structure: a greeting, then one Razor partial per section (summary,
    key figures, projection, budget, goals, activity, quick actions, setup cards) with a dedicated `dashboard.css`
    (`dash-` prefix) and a reusable setup-card component.
  - Chart data reaches the browser through a single JSON data island read by `js/dashboard/dashboard-core.js`; the
    budget donut moved to its own module and labels the Entertainment slice correctly instead of "Other".
  - The Web Dashboard controller now logs API failures (while still rendering an empty summary), and the Dashboard
    service uses no-tracking queries with deterministic tie-breakers for "latest" selections.

- **Navigation Bar in UI Test & Unauthenticated Layout**: Fixed UI test failure on login page asserting on navigation bar.
  - Added brand icon and name in topbar header for unauthenticated users in `_Layout.cshtml`.
  - Added full-width positioning rules in `sidebar.css` when sidebar is not present (`.no-sidebar`).
  - Updated `e2e-tests/ui-test.js` to assert on topbar/header for unauthenticated pages and sidebar navigation for authenticated pages.

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

- **Dashboard Monthly budget card**: Added a provider-backed card showing planned vs actual income and expenses for the
  latest month of the active source (Google Sheet or Budget Tracker), with a source toggle and source-named empty
  states. The Budget Planner card remains separate and independent.
- **App Shell Accent & Sidebar Persistence**: The sidebar, top bar, and mobile bottom navigation now use the Indigo
  Ledger brand accent, and the sidebar collapse preference is restored before first paint.
  - Replaced hard-coded `#58a6ff` accents with the indigo brand token (light indigo on the dark sidebar for contrast).
  - Collapse state is applied from `localStorage` in the document head to avoid layout flash; toggles expose
    `aria-expanded`, and Escape closes the mobile sidebar.
- **Architecture Alignment & ApiService Restructuring**: Aligned architecture documentation and codebase structure with direct service injection, permissive open-source licensing, and type-based organization.
  - Rewrote `docs/adr/0001-standardize-cqrs-feature-organization.md` to establish direct service injection via ASP.NET Core DI as the canonical pattern, documenting the explicit rejection of MediatR and AutoMapper due to commercial licensing changes and unnecessary mediator indirection.
  - Added a comprehensive License Policy to `AGENTS.md` and `.junie/AGENTS.md` enforcing strictly permissive open-source licenses (MIT, BSD 2-Clause/3-Clause, Apache 2.0) and explicitly banning commercial, dual-licensed, or copyleft dependencies.
  - Updated `GLOSSARY.md` to purge obsolete CQRS terms (`Command`, `Query`, `Handler`) and aligned the `ApiService Layer` and `Feature` definitions with direct service injection.
  - Restructured `PersonalFinance.ApiService` from 11 feature-nested directories (`AiAssistant/`, `Budget/`, `Community/`, `Dashboard/`, `GoogleDrive/`, `NetWorth/`, `Notification/`, `SavingsGoals/`, `Search/`, `SpreadsheetAnalysis/`, `Watchlist/`) into cohesive top-level type folders: `Controllers/`, `Services/`, and `Validators/`.
  - Standardized root namespaces across ApiService (`PersonalFinance.ApiService.Controllers`, `PersonalFinance.ApiService.Services`, `PersonalFinance.ApiService.Validators`) and updated service registrations in `Program.cs` and test suites (`PersonalFinance.Tests`).
  - Separated composite models previously bundled in `Validators/SpreadsheetAnalysisResults.cs` into individual, dedicated class files under `Services/`: `AnalysisDashboardResult.cs`, `SyncSpreadsheetBudgetResult.cs`, and `MonthConflictInfo.cs`.
  - Removed unused `PersonalFinance.ApiService.Validators` namespace imports across all 11 API controllers and associated service classes.
  - Incremented application version in `PersonalFinance.Shared/Constants/AppVersion.cs` to `1.20.1`.
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

## [2.0.1] - 2026-10-18

### Fixed

- **Currency Symbol in Azure Container Apps**: Amounts now render as `$1,234.56` instead of `¤1,234.56` when the app runs in the Azure Container Apps container.
  - Added a shared `AppCulture` helper and applied the `en-US` default culture at startup in both Web and ApiService, because the chiseled container image sets no locale and .NET falls back to the invariant culture, whose currency symbol is `¤`.
  - Locked Web request localization to `en-US` only, so a browser `Accept-Language` such as `de-DE` cannot change the currency symbol.
  - Added regression tests for the culture default and for backend text (dashboard activity, search results, savings-goal notifications). Amounts already stored with `¤` are not rewritten.

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

- [1.5.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.4.1...v1.5.0
- [1.4.1]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.4.0...v1.4.1
- [1.4.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.3.0...v1.4.0
- [1.3.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.2.0...v1.3.0
- [1.2.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.1.0...v1.2.0
- [1.1.0]: https://github.com/tngo0508/PersonalFinanceApp/compare/v1.0.0...v1.1.0
- [1.0.0]: https://github.com/tngo0508/PersonalFinanceApp/releases/tag/v1.0.0
