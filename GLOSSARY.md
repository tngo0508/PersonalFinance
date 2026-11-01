# Glossary

Canonical definitions for the Personal Finance application domain and architecture.

## Architecture

### Feature
A vertical slice of functionality named after what a user would call it (Budget Tracker, Feedback, Savings Goals).
It spans everything that slice needs: entities, services, controllers, API contracts, DTOs, views and tests. In the
API, the DTOs and the tests, every part carries the Feature's name; in the web frontend a Feature is reached through
its controllers, which are named after their routes (the Budget Planner's is `Budget`, the Landing Page's is `Home`).
_Avoid_: Area (an ASP.NET Core term with its own meaning), component

### Service
A class that owns business logic and data access coordination for a feature. Injected directly into controllers via dependency injection, containing domain rules, validation, and database operations. A Service lives in its Feature's folder, `PersonalFinance.ApiService/Features/<Feature>/`, with its interface beside it; one that two or more Features use lives in `PersonalFinance.ApiService/Infrastructure/` (ADR 0007).

### Validator
A class that provides validation logic for requests and inputs. Validators live in their Feature's `PersonalFinance.ApiService/Features/<Feature>/Validators/` folder.

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
`PersonalFinance.Web` — the MVC frontend. Uses the classic MVC layout (ADR 0008): controllers in `Controllers/`, view models in `Models/<Controller>/`, Razor views in `Views/<Controller>/`, UI services in `Services/`, tag helpers in `TagHelpers/`, host plumbing in `Infrastructure/`, and the Identity Razor pages. Calls the ApiService via Refit HTTP clients. Controllers call APIs directly.

### ApiService Layer
`PersonalFinance.ApiService` — the REST API backend. Contains controllers, services, and validators organized by Feature in `Features/<Feature>/`, with code that two or more Features share in `Infrastructure/`. Uses direct service injection via ASP.NET Core dependency injection.

### Data Layer
`PersonalFinance.Data` — data access. Contains entities, DbContext, and database configuration. Independent of UI and API concerns.

### Shared Layer
`PersonalFinance.Shared` — cross-cutting types. Contains DTOs (organized by feature), API contracts, constants, and shared helpers. Referenced by all layers.

## Access Control

### Account
A person's sign-in identity in the application. Every new Account is assigned the `User` Role; an Account with no Role is still treated as an ordinary signed-in user.
_Avoid_: Member, customer

### Role
A named label assigned to an Account that determines which Policies it satisfies. Roles are additive; an Account may hold none, one, or several.
_Avoid_: Group, permission level

### Admin
The Role held by the operator of the application. Only Admins can reach admin pages and admin API endpoints.
_Avoid_: Owner (clashes with "user-owned" data), superuser, root

### Policy
A named access rule, such as "requires Admin", that pages and endpoints check instead of naming a Role directly.
_Avoid_: Rule, guard

### Idle Sign-out
Signing an Account out after 30 minutes with no user activity (keyboard, mouse, scroll or touch input) in any of its
open tabs, after a warning two minutes before. The same limit for every Account; requests the app makes on its own do
not count as activity.
_Avoid_: Auto logout, session timeout

### Deletion Request
An Account's confirmed request to be erased, which hides its data and starts the Grace Period.
_Avoid_: Account closure, deactivation

### Grace Period
The 30 days after a Deletion Request during which the Account can still cancel it.

### Purge
The permanent erasure of an Account and its data when its Grace Period ends. Its Guild Hall Strategies and Comments
stay, shown as written by "Former member".

### Data Export
A JSON file of the data an Account currently holds in the app, which the Account downloads for itself.

### Account Lifecycle
The stages an Account passes through after registration: active, then under a Deletion Request for its Grace Period
(locked out, able only to cancel), then Purged. Data Export, the Deletion Request and the Purge together make up the
lifecycle; it is the Feature that owns them.
_Avoid_: Account management (that is the settings pages), offboarding

## Domain

### Net Worth
The core financial tracking feature. Projects financial independence from user-entered parameters: a starting balance, annual income, spending and savings, current and retirement age, and three return-rate scenarios (Base, Conservative, Aggressive). The user's net worth is the starting balance they enter; it is not computed from individual assets and liabilities.

### FI Target
The portfolio value at which a user is considered financially independent: annual spending × 25 (the 4% rule).

### Progress to FI
Net worth as a percentage of the FI Target.

### Savings Rate
Planned annual savings as a percentage of annual income, from the user's Net Worth parameters
(`AnnualSavings / AnnualIncome`). Shown on the Net Worth page and the Dashboard. Calculated by
`FinanceMetrics.SavingsRatePercent`, rounded to one decimal place.
_Avoid_: using it for budget months; that is the Actual Savings Rate.

### Actual Savings Rate
The share of actual income left after actual expenses in budget months (`(Actual income − Actual expenses) / Actual
income`), from the Budget Tracker or synced Google Sheet data. Shown in the Budget Report and Analysis. Calculated by
`FinanceMetrics.ActualSavingsRatePercent`, rounded to one decimal place; 0 when there is no income. (Existing DTO
properties keep the name `SavingsRate` / `AverageSavingsRate` for the JavaScript that reads them.)
_Avoid_: calling it "Savings Rate" in new code or UI text without the "Actual" qualifier.

### Money Flow
The chart on the Analysis page showing one month of actual income moving into spending categories and savings.
_Avoid_: Sankey (a chart type, not the concept), cash flow (overloaded), Money Map

### Shortfall
The amount by which a month's actual expenses exceed its actual income. A month has either a Shortfall or savings,
never both.
_Avoid_: deficit, negative savings, overspend

### FI Breakdown
The panel on the Net Worth page showing how the Savings Rate, the FI Target, Progress to FI and the age at which the
user reaches FI are calculated from the values the user entered.
_Avoid_: plan graph, Plan View, dependency map

### Today's Money
A Net Worth projection shown with each year's balance divided by the inflation since now, so future amounts read in
today's prices and the FI Target keeps pace with them. The inflation rate is a projection input that is never saved.
_Avoid_: real terms, inflation-adjusted (in UI text)

### Lever
One single-number change to the user's Net Worth parameters (save more, spend less, a higher or lower return) that the
projection re-runs to show how it moves the age the user reaches the FI Target. Calculated in
`NetWorthProjectionService`; selecting one on the Net Worth page applies it.
_Avoid_: what-if, scenario (Base, Conservative and Aggressive are the scenarios)

### Landing Page
The public page served at `/` to signed-out visitors. It explains the product and leads to registration or sign-in.
_Avoid_: Home page (ambiguous with Dashboard)

### Headquarters
The city the product is run from, Stanton, California, as shown on the Landing Page. It names a city, never a street
address.
_Avoid_: HQ address, office, home

### Skyline
The 3D view of Headquarters behind the Landing Page hero, drawn from OpenStreetMap buildings.
_Avoid_: 3D map, city model, hero map

### Legal Pages
The public pages that state the Terms of Service, the Privacy Policy, the disclaimer, the cookie policy and how to
contact the operator.
_Avoid_: Policies (too narrow), static pages

### Consent
An Account's recorded acceptance of a given version of the Terms of Service and Privacy Policy, given at registration.
_Avoid_: Agreement, opt-in

### Investment Calculator
A signed-in tool that shows how an investment grows with compound interest from the values the user enters. It stores
nothing.
_Avoid_: Projection (that is Net Worth's), simulator

### Currency Converter
A signed-in tool that converts one amount between two Convertible Currencies at the latest stored Exchange Rate or the
rate of a past day, and shows the rate used, its date and its source. It stores nothing.
_Avoid_: Reporting Currency conversion (that is how totals are shown), exchange

### Dashboard
The signed-in user's overview page, summarizing net worth, budget, savings goals, and recent activity. Signed-in visitors to `/` are redirected here.
_Avoid_: Home page, home

### Unavailable State
What a page or section shows when its data could not be loaded. It tells the user their data is temporarily unreachable and offers a retry. It is never shown as an empty state, because an empty state means the user has no data yet.
_Avoid_: showing an empty state or setup prompts after a failed load

### Budget
Monthly budget allocation and tracking. Compares planned spending against actual spending from spreadsheet data.

### Budget Planner
The page where a user sets a yearly budget by allocating Monthly Income across 10 spending categories. Each category stores an **Allocation Amount** in dollars and cents; the percentage is always calculated from that amount. Save is only allowed when the allocation amounts add up to Monthly Income exactly.

### Allocation Amount
The dollar amount assigned to a budget category, stored to the cent. It is the source of truth for the Budget Planner. Percentages are always derived from this value.

### Budget Tracker

The in-app monthly budget page where the user enters plans, transactions, and overrides directly. It mirrors the layout
and rules of the Google monthly budget sheet, with a Summary view and a Transactions view. Its data is separate from
synced spreadsheet data.

### Planned

The amount the user intends to spend in a category, or earn in an income category, for a given month.

### Actual

The amount recorded for a category in a month. It is the sum of that category's transactions in the month, unless an
Actual Override is set.

### Actual Override

A manually entered Actual for a category in a month. It replaces the transaction sum for that category and month and can
be reverted to the transaction sum.

### Diff

The difference between Planned and Actual, always calculated and never entered. For expenses it is Planned minus Actual,
so a positive value means under budget. For income it is Actual minus Planned, so a positive value means more was earned
than planned.

### Budget Category

A user-defined grouping of budget entries, classified as an expense or an income category. Deleting a category hides it
from new months and entries, but its name remains visible in past months that used it.

### Budget Transaction

A dated expense or income entry with an amount, description, and category. Its month is determined by its date. Its type
follows the kind of its category.

### Data Source

The choice between the synced Google Sheet data and Budget Tracker data that Analysis and the Dashboard budget views
display. The source applies to a whole view and is never mixed within one month.

### Data Source Removal

The user deleting all of one Data Source's data: for the Google Sheet, every synced month; for the Budget Tracker, every
category, plan and transaction, returning it to its first-use state. It does not disconnect Google Drive, and
disconnecting Google Drive does not remove synced data. Afterwards the Data Source falls back to the source that still
has data.
_Avoid_: disconnect, reset, unlink (those mean other things)

### Global Search
The search in the top bar for signed-in users. It finds the user's own records by text: transactions and Budget
Categories from their current Data Source, and their Savings Goals. Selecting a result opens the page that holds it.
_Avoid_: page search, navigation search (finding app pages by name is a different feature)

### Guild Hall
The signed-in page where users trade Strategies, answer Polls and join Challenges under their Pseudonym. It never shows
money amounts and never ranks or compares users. In code it is the Community Feature.
_Avoid_: Community (the code name only), forum, social feed

### Pseudonym
The generated name an Account appears under everywhere in the Guild Hall. The Account can re-roll it, and its past posts
then show the new one. Real names and emails never appear in the Guild Hall.
_Avoid_: alias, handle, display name, username

### Strategy
A Guild Hall post about a practice an Account tried: what they tried, what happened and what they would keep doing, with
a category. Other users see it only after an Admin approves it.
_Avoid_: story, case study, tip

### Poll
A question with fixed options that an Admin writes and opens or closes in the Guild Hall, and that Accounts vote on.
_Avoid_: survey

### Comment
A short reply under a Strategy or a Poll. Other users see it at once; an Admin can hide it.

### Reaction
A supportive response to a Strategy or a Comment: Helpful or Cheering you on. There are no negative Reactions.
_Avoid_: like, upvote

### Challenge
A time-boxed habit an Admin defines, such as "cook at home five nights", that Accounts join. It is about behavior, never
an amount of money.
_Avoid_: goal (that is a Savings Goal), quest

### Check-in
An Account's private record that it kept a Challenge's practice for a period. Only the Account sees its own Check-ins;
the Guild Hall shows only how many people checked in.
_Avoid_: log, progress entry

### Feedback
A message a signed-in Account sends to the operator about the app, with a Feedback Category and the page it was sent
from. It belongs to the Account that sent it and only Admins read it. It is separate from the Contact page, which is for
support, data protection requests and security reports.
_Avoid_: Contact message, Ticket, Review

### Feedback Category
The kind of Feedback, chosen by the Account: Bug (something is broken), Idea (a suggestion) or Other.
_Avoid_: Type, topic

### Feedback Status
Where a piece of Feedback is in the Admin's triage: New (not yet read, the starting status), Reviewed (read) or
Archived (dealt with, and hidden from the Feedback inbox unless the Admin asks for it). An Admin can move Feedback
between any of the three, including back to New.
_Avoid_: State, resolution

### Google Drive Integration
Connection to Google Drive for spreadsheet synchronization. Manages OAuth connections, folder exploration, and file caching.

### Money Priorities
The signed-in page where a user with extra money each month answers a few questions and is shown which priority to put
it toward first.

### Priority Tree
The fixed, authored set of questions and outcomes that Money Priorities walks through, the same for every Account.
_Avoid_: decision tree (the chart type), plan

### Priority Walk
One saved pass through the Priority Tree: an Account's answers, the path they lead to and the priority at its end.
_Avoid_: session, result, assessment

### Purpose
What the user says they want their money to do, in their own words, entered on Money Priorities. Optional.
_Avoid_: goal (that is a Savings Goal), intent

### Purpose Note
The short AI-written text under a Walk's result that explains how the suggested priority serves the user's Purpose. It
never changes the suggestion.
_Avoid_: AI note, advice, recommendation

### Investment Scenario
A named, saved set of Investment Calculator inputs: the starting amount, monthly contribution, assumed return, years,
yearly contribution increase, inflation, fee and any applied what-if timing (DelayYears: the years before the plan
starts; StopAfterYear: the last year of contributions, or none), in one currency, with an optional note.
A Grow scenario keeps only the plan; a Goal scenario also keeps the goal target and whether the goal solves for the
monthly amount or the years. A user keeps at most 20. Loading one puts its inputs back in the calculator; the
projection is always worked out again, never stored. Information only: it is not advice and never moves money.
_Avoid_: plan (the calculator's current inputs), portfolio, preset

### Savings Goals
The Feature where a user sets a target amount, optionally with a target date, and records every movement of money
toward it as a Contribution. The page shows each goal's saved amount against its target. Information only: it never
moves money or gives advice.
_Avoid_: savings account, pot

### Contribution
One dated movement of money into or out of a Savings Goal: a signed amount (a deposit is positive, a withdrawal
negative, never zero), a date no later than today, an optional short note, and a kind. A Regular Contribution is one
the user recorded; the Opening Contribution is the goal's starting amount (at most one per goal); an Adjustment is
recorded by a balance correction and can only be deleted. It has no currency of its own: it is in its goal's currency.
_Avoid_: transaction (that is the Budget Tracker), deposit as the general term

### Saved amount
The total of a Savings Goal's Contributions. Never typed over: it changes only when a Contribution is added, edited
or deleted, and it never goes below zero. It is stored on the goal as a cached total (`CurrentAmount`) for the
Dashboard, Search, the AI Assistant and Data Export. The goal is Reached while it meets or passes the target.
_Avoid_: current amount, balance (except in "correct balance")

### Pace
What the Savings Goals page works out from a goal's Contribution log at today's date: its status against the target
date (Reached, No target date, Past due, Just started, Ahead, On track, Behind), what is still needed per month to
reach the target by that date, and when the recent rate (the last 90 days of Regular Contributions) would finish it.
Descriptive only: it never says what the user should do.
_Avoid_: forecast, plan, recommendation

### Watchlist
Item tracking and article watchlist for financial news and interests.

### Research
Signed-in pages of interactive visualisations of market data, for information only, opening on a search-first Home
(Sectors, World, Funds, the Fund Basket, each Tracked Fund and the Company Profile each have a page). It never gives
buy or sell recommendations and stores nothing.
_Avoid_: Explore, Markets, stock picker (distinct from News, which is headlines, and the Watchlist)

### Public Float
The market value of a company's shares held by outside investors, as the company reports it to the SEC once a year. The
Research Sectors page uses it to size companies because no displayable price data is available.
_Avoid_: market cap (that includes insider holdings and needs live prices), valuation

### Company Profile
The identity, reported annual financial statements and recent SEC filings of one SEC-registered company, as the
Research Company page shows them, for information only. Figures are read as the company reported them under US
GAAP in US dollars; nothing is converted, priced or scored. Its press releases are the announcements the company
attached to its SEC filings, never third-party coverage.
_Avoid_: stock page, ticker lookup, quote

### Tracked Fund
A fund, ETF or mutual fund, whose holdings Research reads, from a list the site owner keeps.
_Avoid_: watchlist (that is the Watchlist feature), portfolio

### Fund Basket
A set of one to five Tracked Funds, each with a weight, that a user picks in Research to view together. It is never
saved: it lives in the page's address (`?basket=VTI:60,BND:40`), and every Research link and search result carries it.
_Avoid_: portfolio (that implies holdings the user owns), watchlist (the Watchlist feature), allocation (a Budget term)

### Spreadsheet Analysis
Transaction and budget data analysis from Google Sheets. Provides dashboard views and sync operations.

### AI Assistant
AI-powered financial advice via OpenAI chat integration.

### Atelier
The Learning page: a public Library of Term Entries and a Journey of Lessons for signed-in users. Educational only; it
never gives personalised advice. In code it is the Learning Feature.
_Avoid_: academy, course page, Learning (the code name only)

### Term Entry
One reviewed, published explanation of a financial term in plain English, with its Vietnamese name, cited sources and,
where rules differ, notes for the US and Vietnam. It belongs to one Topic.
_Avoid_: article, post, definition

### Topic
The one subject group a Term Entry belongs to, such as Borrowing & credit or Tax.
_Avoid_: category (Budget Category and Feedback Category are different things), tag

### Approved Source
A website, law database or channel on the owner's allowlist that Atelier content may draw on, with its usage rule:
draft and cite, concept with a link, or link only.
_Avoid_: reference, provider

### Journey
The finite, ordered path of Lessons a signed-in user works through, one a day.
_Avoid_: course, track, curriculum

### Lesson
One day's step of the Journey: a short intro, two or three Term Entries, a Check and a link to try the idea in the app.
_Avoid_: module, chapter, unit

### Review
The flash cards from earlier Lessons that open a Lesson. The user marks each one Knew it or Not yet.
_Avoid_: quiz, flashcard deck

### Check
The short practice that ends a Lesson. Answering it finishes the Lesson, whatever the score.
_Avoid_: quiz, test, exam

### Keepsake
The object placed on the user's desk for each finished Lesson. It marks progress without points or streaks.
_Avoid_: badge, reward, trophy

## Localization and Currency

### Language
The user's chosen UI language (`en`, `vi`), stored on their preference Entity and mirrored into the culture cookie. It selects the translation file and, through its Culture, the number and date formats. Independent of the Reporting Currency.
_Avoid_: Locale (ambiguous between language and formatting), region

### Culture
The .NET culture the request runs in (`en-US`, `vi-VN`), derived from the Language. It decides separators, date formats and input parsing, never the currency symbol.

### Translation Key
The stable dotted identifier for one user-facing string (`SavingsGoals.Card.Remaining`), the same in Razor and JavaScript. The English text may change; the key does not. Plurals use `Key.one` / `Key.other`.
_Avoid_: Resource string, label id

### Currency Code
The ISO 4217 code (`USD`, `VND`) stored on every money-bearing row. It, not the Culture, decides the symbol and minor units when an amount is displayed.

### Reporting Currency
The one currency a user's totals are shown in, stored on their preference Entity (default `USD`). Converting into it never rewrites stored amounts.
_Avoid_: Base currency (that is the exchange-rate table's USD base), home currency, display currency

### Convertible Currency
A currency the currency converter can convert from or to, with a stored daily Exchange Rate. Every Supported Currency is a Convertible Currency; not every Convertible Currency can be stored on an amount.
_Avoid_: Quote currency (that is the quoted side of one Exchange Rate), supported currency

### Reporting Amount
An amount brought into the Reporting Currency by the ApiService's one converter and carried in a DTO beside the original amount and its Currency Code; null when no Exchange Rate exists, never zero. Totals sum Reporting Amounts and are rounded once; the page captions them with the rate used.
_Avoid_: Converted value, display amount

### Exchange Rate
A system-owned daily rate from USD to a quote currency on a given date, fetched by a refresher and never user-edited. Dated amounts convert at their own date's rate, balances at the latest.
