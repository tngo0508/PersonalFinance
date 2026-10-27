# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [10.1.0] - 2026-10-26

### Added

- **Totals in your Reporting Currency**: every page brings amounts saved in another currency into the currency chosen in Account settings › Currency, server-side, at the daily exchange rates (slice C2 of the localization and multi-currency spec, #411, ADR 0016).
  - Dated amounts (Budget Tracker transactions and monthly lines, synced spreadsheet months) use their own day's rate; balances (Savings Goals, the Budget Planner, Net Worth settings, the Dashboard's figures) use the latest. The Net Worth projection converts its inputs once, then projects in the Reporting Currency.
  - A day without a rate uses the nearest earlier one; when that is more than three days older the page says "rate as of {date}". With no rate at all the amounts stay in their own currency under "conversion unavailable", never as zero. Each total is rounded once.
  - Each amount keeps its own currency beside the converted figure, and a converted total carries the caption "converted at {rate} on {date}".
- **Reporting Currency setting**: Account settings › Currency chooses the currency totals are shown in, independent of the language. Changing it never rewrites a saved amount.
- **Per-amount currency selectors**: a new Savings Goal, a Budget Tracker transaction or override picks USD or VND per row; the Budget Planner, Net Worth settings and a Money Priorities walk pick it once; connecting a Google Drive folder asks for its spreadsheets' currency. Every selector defaults to the Reporting Currency.
- **Playwright smoke test**: a đồng goal inside a dollar total with its conversion caption, then the Reporting Currency switched to đồng (Test 9f). The E2E fixtures seed one exchange rate so the caption shows a rate.

### Changed

- **Rates are cached**: the ApiService keeps the exchange rates it has used in memory for an hour, keyed by currency and day, and the daily rate refresh drops them as soon as it writes new rows. A cold read is one query per currency and date range.
- **Search results, Dashboard activity, Purpose Note facts and milestone Notifications** name an amount in its own currency instead of always in US dollars.

## [10.0.0] - 2026-10-27

### Removed

- **All Community data**: a migration hard-deletes every poll, option, vote, story, comment and like, real users' content included, so the Guild Hall starts empty (#422, authorized on #396, spec #420). This is a deliberate exception to soft deletes, and rolling the migration back does not restore the rows.
- **Made-up Community content**: new databases are no longer seeded with three polls and three stories by invented authors with invented like and comment counts.
- **Community Quick Stats**: the four site-wide counters (one of which counted closed polls as active), their API data and their translations.
- **Comment topic**: comments no longer carry a free-text topic, in the API, on the page or in the Data Export.

### Changed

- **Community counts come from real rows**: story likes, comment likes and story comment counts are counted from the like and comment records instead of stored counters, so they can no longer drift or be set by hand. The Purge no longer adjusts counters.
- **Comments link to their story by id**: a comment on a story stores the story's id (a nullable foreign key) instead of a copy of its title. The Data Export's comments carry `StoryId` instead of `CaseStudyTitle`.

### Added

- **Guild Hall vocabulary and ADR 0018**: the glossary defines Guild Hall, Pseudonym, Strategy, Poll, Comment, Reaction, Challenge and Check-in, and ADR 0018 records that the Guild Hall is pseudonymous and never shows money amounts.

## [9.1.0] - 2026-10-27

### Changed

- **The Dashboard clock and calendar put a value on your time**: the Today panel is now set like a cheque, with the clock as the stub and the month as a ledger page, in the app's money typeface.
  - The clock shows the seconds and your time zone, plus a tape of the day's 24 hours that fills as the day passes.
  - When your Net Worth settings have a positive net worth and base return, a live figure shows what compounding has earned since midnight, with the daily and hourly rate and the figures it is estimated from. When the Base projection reaches FI, a line says about how many days away it is.
  - The calendar shows how much of the month is gone. When the monthly budget is for the current month, it also sets spending against that pace: a brass marker on the spending bar, an "On pace", "Ahead of pace" or "Over budget" label, and what is left to spend per day. Weekends are set lighter, and today is ringed in brass.

## [9.0.0] - 2026-10-27

### Added

- **Every amount carries its currency**: the eight money-bearing Entities gain a `CurrencyCode` (`char(3)`, ISO 4217, default `USD`), one per row and never per field (slice C1 of the localization and multi-currency spec, #410, ADR 0016).
  - Document-level on `NetWorthParameters`, `UserBudgetAllocation` and `PriorityWalk`; row-level on `BudgetTransaction`, `MonthlyBudgetLine`, `SavingsGoal`, `SpreadsheetBudgetSnapshot` and `SpreadsheetTransactionRecord`. Every row saved before this release is US dollars.
  - Each amount is rounded to its currency's minor units on write, halves away from zero, so VND is stored in whole đồng. Columns stay `decimal`.
  - Stored amounts still show in US dollars exactly as before: conversion and the currency selector come in slice C2.
- **Reporting Currency**: the user preference Entity gains `ReportingCurrency`, default `USD`. Changing it never rewrites a stored amount.
- **Google Drive connections have a currency**: chosen when the user connects, the Account's Reporting Currency by default; every imported spreadsheet row inherits it.
- **Exchange rates**: a new `ExchangeRate` table (system data, USD base only, unique per pair and day, rate `decimal(18,8)`) filled by `FxRatesRefresher` in the daily Research refresh job.
  - Frankfurter's blended rate is the primary source, read from the day after its latest stored rate (back to 2015 on the first run); the fawazahmed0 currency-api (jsDelivr, then its Cloudflare mirror) gives the latest rate when Frankfurter fails. Neither needs a key.
  - It runs even when the SEC contact is not configured, and rows are only added, never rewritten.

### Changed

- **Money requests accept a currency**: the Budget Tracker transaction and line requests, the Budget Planner save, the Money Priorities submit, the new Savings Goal, the Net Worth parameters and the Google Drive connect take an optional `CurrencyCode`.
  - A code that is not in `SupportedCurrencies` is rejected under the Feature's new `CurrencyUnsupported` key; no code keeps the row's currency, or makes a new row US dollars.
  - A Savings Goal's currency is set when it is created; its Contributions and later edits use it.
  - A Priority Walk in another currency is a new Walk, and a Purpose Note is reused only between Walks in the same currency.

## [8.9.0] - 2026-10-26

### Added

- **Clock and calendar on the Dashboard**: the header shows the time and the current month in your own time zone and language, so you can see at a glance how much of the budget month is left (#412).
  - Days already behind you this month are shaded, today is set in ink, and days with recent activity carry a dot; a line under the clock says how many days are left in the month.
  - The week starts on the culture's first weekday (Sunday in English, Monday in Vietnamese). The clock updates on the minute and the month redraws when the day changes.

## [8.8.0] - 2026-10-26

### Added

- **Money Priorities path and Priority Tree in Vietnamese**: the "Your path" sentences, the diagram labels and the diagrams' accessible title and descriptions show in the reader's language (#382).
  - Each question, answer and leaf has a resource key by its node id or edge (`MoneyPriorities.Node.Q1`, `MoneyPriorities.Edge.Q1.L1`, `MoneyPriorities.Leaf.L1`), so a saved Walk shows the current wording of its nodes; one Web has no key for shows the ApiService's English.
  - A test checks that every node and answer of every authored tree version has an English entry matching the tree and a Vietnamese entry, so a new tree version cannot ship untranslated.

### Changed

- **Web builds the Priority Tree diagrams**: the ApiService sends the tree (node ids, kinds, edges, English labels) and the path's node ids instead of finished Mermaid text and English sentences; Web writes the diagram texts and the path sentences in the request's language. The ApiService stays language-free (ADR 0017).
  - `PriorityDiagramText` and its palette moved from the ApiService to `PersonalFinance.Web/Models/MoneyPriorities/`; a translated label is kept on one line and its quotes escaped, so it cannot add a Mermaid statement.
  - The page's result JSON now nests the Walk under `walk`, beside `pathSentences`, `wholeTreeDiagram` and `pathViewDiagram`.

## [8.7.0] - 2026-10-26

### Added

- **AI Assistant text in the user's Language**: everything the AI Assistant shows around a chat reply now follows the stored Language, not only the reply itself (#381).
  - Chat widget: the launcher, header, buttons, placeholder, input hint, greetings, quick-action chips (and the message each sends), status lines and footer disclaimer.
  - Fixed replies: the no-API-key, failed-call, empty-reply, off-topic, greeting and too-long chat replies, and the no-key and failed News summary messages, carry a keyed `FixedText` entry (`ResultMessageKeys.AiAssistant`) that Web resolves. Web also translates the disclaimer and the monthly budget warning.
  - News summaries are written in the user's Language, whatever language the article is in. The headline and link stay as sourced.
  - New Purpose Notes are written in the user's Language. A saved note keeps the language it was written in and is not rewritten when the Language changes.
- **Glossary terms**: "AI assistant" for the Vietnamese translation.

### Fixed

- **Vietnamese chat messages refused as off-topic**: the AI scope filter knew only English finance and greeting words, so a message typed in Vietnamese, and now the translated quick actions, got the off-topic refusal without reaching the model. It now also matches Vietnamese finance terms and greetings, on Unicode-normalized (NFC) input.

### Changed

- **Chat widget DOM**: the widget builds its elements with `textContent` instead of `innerHTML`.

## [8.6.0] - 2026-10-26

### Added

- **Google Drive and Analysis messages in Vietnamese**: the error and warning text that Google Drive and Analysis send inside a successful result is keyed, so it shows in the reader's language (#405).
  - Google Drive: the expired-credentials error, every sample-files warning (no API key, timeout, HTTP 400, 403, 404 and other statuses, unreadable answer, cannot connect, unreachable), the empty-folder warning, and why a budget report or transaction log could not be read. The HTTP status, and the sheet and column names the parser looks for (Summary, Transactions, Date, Amount), are arguments, so they stay as the spreadsheet must spell them.
  - Analysis: why a spreadsheet sync failed (connection not found, could not be read, no budget data, months already saved, sync failed), or the sheet's own reason when it gave one.
  - The English fields stay: each DTO gets an `Error` (and, for folder results, a `Warning`) entry beside them, with keys in the new `ResultMessageKeys`. Web resolves the entry before a page, a redirect message or the page's JavaScript sees the text.
  - The stored credentials message needs no schema change: its key comes from the connection's `NeedsReauth` status.
- **Key coverage test for result messages**: every `ResultMessageKeys` key has an English and a Vietnamese entry, follows the `<Feature>.Result.<Rule>` naming rule, and is declared once.

## [8.5.0] - 2026-10-26

### Added

- **AI Assistant, Research, Analysis and Google Drive errors in Vietnamese**: every rejection these Features return carries a localization key, so every Feature but Admin now answers rejected requests in the reader's language (slice E3b of the localized API errors spec, closing #385).
  - AI Assistant: the empty and too-long chat message, the missing article title, the sign-in, daily-limit and monthly-budget quota refusals (429) and the summary rate limit are keys; the daily limit is a number argument.
  - Research: the company search and ticker rules carry their limits as arguments; "too many lookups" (429) is a key.
  - Analysis and Google Drive: the missing file, missing or invalid folder link and "connection not found" (404) are keys; a rejection is never sent inside a result body any more.
- **The Purpose Note says which AI limit was reached**: its quota refusal carries the quota's keyed reason, so the note names the daily limit (with its number) or the monthly budget instead of a general "try again later".

### Changed

- **Unkeyed error bodies show a status message**: Web no longer reads text out of an unkeyed ApiService error body; it shows the generic message of the status code (invalid request, not found, conflict) in the reader's language.
  - The Admin area stays English: Web's Admin controller reads its own `AdminErrorDto` reason.
  - Web's AI endpoints answer their own "message is required" and "title is required" checks in the reader's language.
- **Architecture guard covers every Feature but Admin**: `ApiErrorArchitectureTests` scans every folder under `Features/` except Admin, so a new Feature is checked from its first commit.

### Removed

- **Legacy error parsing**: the best-effort reading of `{ "error" }`, `{ "errors": [string] }`, `detail` and `title` bodies is gone from `ApiErrorMessages`.

## [8.4.0] - 2026-10-26

### Added

- **Feedback, account deletion, Savings Goals, Watchlist and Net Worth errors in Vietnamese**: every rejection these Features return carries a localization key, so their validation messages and refusals answer in the reader's language, with limits formatted in the reader's culture (slice E3a of the localized API errors spec, #385).
  - Feedback: the five submit rules, the status rule and the rate limit (429) are keys; the message-length limit is an argument instead of a positional format.
  - Account deletion: "only Admin" and "already scheduled" (409) and the invalid cancel link are keys; a malformed cancel token is one error instead of up to three.
  - Savings Goals: the goal, Contribution and balance-correction rules are keys with the largest amount as a money argument; "below zero" (409) and "an adjustment can only be deleted" are keys.
  - Watchlist: every save rule has its own key and message, replacing FluentValidation's default English.
  - Net Worth: the projection validator returns a keyed entry naming the field, reusing the form's own `NetWorth.Validation.*` messages, plus a new key for "retirement age must be greater than current age".
- **Key coverage test for the five Features**: every `ApiErrorKeys` key of each Feature is produced by a real rejection, and every rejection resolves in English with no placeholder left; `ApiErrorArchitectureTests` now scans these Features too.

## [8.3.0] - 2026-10-26

### Added

- **Budget Tracker and Budget Planner errors in Vietnamese**: every rejection the two Features return carries a localization key, so the Budget Tracker's category, transaction, planned-amount and month errors and the Budget Planner's income and allocation errors answer in the reader's language, with limits formatted in the reader's culture (slice E2 of the localized API errors spec, #385).
  - The six Budget Tracker validators and the Budget Planner validator set `.WithErrorCode(ApiErrorKeys…)` on every rule and pass their bounds as typed arguments; `MonthlyBudgetService` throws `ApiErrorException` for a missing category or transaction (404), a duplicate category (409), a kind change and an archived category; the "Invalid year or month." route check is a key.
  - The Budget Planner's allocation rules are one shared helper per field instead of ten repeated blocks.
- **Wire-format guard for keyed errors**: a `WebApplicationFactory` test calls the real ApiService pipeline and asserts a service rejection arrives as a camelCase `ApiErrorDto` with its status code, so the global `ApiErrorExceptionFilter` registration in `Program.cs` cannot be dropped unnoticed.
- **Guard tests cover the budget Features**: `ApiErrorArchitectureTests` scans Budget Tracker and Budget Planner for literal error bodies and unkeyed rejections, and each Feature has a test that resolves every validator failure in English with no placeholder left.

### Removed

- **`DuplicateCategoryException`**: the Budget Tracker's duplicate-category rejection is an `ApiErrorException` with the `BudgetTracker.Error.DuplicateCategory` key; `BudgetTrackerController` no longer catches and rewraps service exceptions, and `BudgetController` no longer returns the `{ ErrorCode, ErrorMessage }` shape.
## [8.2.0] - 2026-10-26

### Added

- **Savings Goals show their Pace**: each card says in words where the goal stands against its target date, what is still needed per month, and when the recent pace would reach it (slice 2 of spec #395).
  - Statuses, first that applies: Reached, No target date, Past due, Just started (under 30 days old), then Ahead, On track or Behind against the line from the goal's start to its date, with a band of 5% of the target.
  - The projection uses the net of the last 90 days' Contributions the user recorded; the opening amount and balance corrections are left out. Without enough history, or with no recent saving, the card says so instead.
  - Each card shows what was saved this month.
  - The wording describes and never tells the user what to do.
- **A summary above the goals**: saved in active goals, saved this month, how many dated goals are on track, and the next target date with its goal's name.

## [8.1.0] - 2026-10-25

### Added

- **API errors carry localization keys**: every rejection from a converted Feature arrives as one `ApiErrorDto`, a list of errors each with a key, an English message, an optional field and typed arguments, so Web shows it in the reader's language (slice E1 of the localized API errors spec, #385).
  - `ApiErrorKeys` in Shared names every key; each is a key of `en.json`.
  - A service throws `ApiErrorException` and one filter in ApiService renders it as the fitting 4xx; validators set `.WithErrorCode(ApiErrorKeys…)` and `ToApiError()` turns their result into the same shape. Arguments go through `ErrorArgs` as `Text`, `Number` or `Money`.
  - ADR 0017 and the spec `docs/specs/localized-api-errors.md` record the contract and the slices that convert the other Features.
- **Money Priorities and Community errors in Vietnamese**: the Money Priorities form's field errors, its AI-note quota message and Community's poll, comment and story rejections answer in the reader's language, with limits formatted in the reader's culture (`1.000.000 $`).
- **Guard tests for keyed errors**: one test fails when an `ApiErrorKeys` constant has no `en.json` entry, and another when Money Priorities or Community returns a `BadRequest` with a literal, throws an unkeyed rejection or gives `.WithErrorCode` a literal.

### Changed

- **`ApiErrorMessages` resolves through the localizer**: Web looks each key up in `en.json` / `vi.json` and formats the arguments in the request culture, money through `MoneyFormatter`; the English message is shown only for a key Web does not know.
  - Bodies without keys, from Features not yet converted, are still read as before.
- **Web's error fallbacks are keys**: the unavailable, unexpected, server, not found, conflict, invalid request and generic messages are `Errors.*` keys, translated like any other text, and callers pass their own fallback as already-localized text.
- **ASP.NET validation responses show one message**: a `ProblemDetails` body is shown as its status code's message (invalid request, not found, conflict or a generic error), never parsed for text.

### Removed

- **`MoneyPrioritiesValidationErrorsDto`**: Money Priorities returns `ApiErrorDto`, and Web reads the field errors from it.
- **The English-matching table in `MoneyPrioritiesText`**: the page no longer swaps translations in by matching ApiService's English text; the id-keyed lookups stay.
- **`CommunityVoteResultDto`**: a rejected vote throws `ApiErrorException` instead of returning an error message, and `VoteAsync` returns the response.
## [8.0.0] - 2026-10-25

### Added

- **Savings Goals keep a Contribution log**: each goal records every deposit and withdrawal with its date and an optional note, and its saved amount is their total instead of a number typed over (slice 1 of spec #395).
  - An "Add Contribution" form (amount, deposit or withdrawal, optional date, optional note) replaces the card's Update input. The date defaults to today and can't be in the future or before 2000.
  - Each card has a collapsible history, newest first, where a Contribution can be edited or deleted.
  - "Correct balance" sets the saved amount to the figure the bank shows and records the difference as a balance correction in the history.
  - No change can take a goal's saved amount below zero; the page says so in the reader's language.
  - Data Export lists each goal's Contributions; deleting an Account hides them with the goals and the Purge erases them.

### Changed

- **A goal shows as reached on its own**: Reached is set whenever the saved amount meets or passes the target, and cleared when a withdrawal takes it back below.
- **Each milestone Notification goes out once per goal**: the 50%, 75% and 100% Notifications no longer repeat after a withdrawal and a re-deposit, and a goal created part-way gets none for the milestones it starts past.
- **The goal update API no longer takes the saved amount**: `UpdateSavingsGoalRequestDto` drops `CurrentAmount` and `IsCompleted`; the amount changes only through the new Contribution endpoints under `api/SavingsGoals/{goalId}`. Web is the only client.

### Database

- **New `Contributions` table and `SavingsGoals.HighestMilestoneNotified` column** (migration `AddContributions`).
  - Every goal with a saved amount gets one Opening Contribution of that amount, dated the goal's creation, so nothing entered before is lost.
  - Milestones the goal has already passed are marked as notified, and Reached is set from the saved amount.

## [7.13.2] - 2026-10-25

### Fixed

- **A new savings goal shows its progress bar**: a goal with nothing saved yet showed a dark blob with a clipped "0%" instead of an empty bar.
  - The percentage now sits beside the track instead of inside the fill, so a narrow fill can't hide it, and the track has a visible border.
  - Screen readers read the progress from the track, as on the Dashboard.

## [7.13.1] - 2026-10-25

### Fixed

- **Tests pass on any machine locale**: the tests that read the machine's culture now set the culture they expect, so the suite passes on a non-English or Windows machine as it does in CI (#384).
  - `FormatBytes` runs in ApiService's pinned `en-US`, and the plural test sets the language's formatting culture as a request does; no product code was wrong.
  - The Dashboard rendering test compares with LF line endings, so a Windows checkout (CRLF views) passes too.

## [7.13.0] - 2026-10-25

### Added

- **Research and Finance News in Vietnamese**: the Research page (every tab, its charts, tooltips, tables, screen-reader text and the Fund Basket) and the Finance News page are translated (slice L3 of the localization spec, #359).
  - Company, fund, country and industry names, headlines, sources and topics stay as the data gives them; the regions and fund roles from ApiService stay in English for now.
  - Dates, counts and percentages follow the reader's culture (`31 tháng 12 2025`, `12,3%`), and country names in the basket views follow the page language.
  - The tabs' Unavailable State, the Companies tab's "Enter a ticker or company name" error and the News load error answer in the reader's language.

### Fixed

- **Finance News times**: "Last updated" shows the refresh time again, and each article's age is drawn by the page in the reader's language; the shared relative-time tag replaced the whole text of each, leaving only "Just now" or "2d ago" and dropping the clock icon.
  - The refresh time and each article's hover date use the culture's short date and time (`12/31/2025 at 2:05 PM`, `31/12/2025 lúc 14:05`).

## [7.12.0] - 2026-10-25

### Added

- **Analysis and Google Drive in Vietnamese**: both pages, their dialogs and their charts are translated (slice L3 of the localization spec, #365).
  - Analysis: summary cards, Spending Velocity, Money Flow, category and variance tables, status badges, Data Source Removal and the sync-conflict dialog.
  - Google Drive: the drive list, connect form, file list and filters, the budget report and transactions dialogs, CSV export headers, and the help and new-budget guides.
  - Month names, dates and percentages follow the reader's culture. File-type labels are translated; the type filter still matches the type the API sends.
  - Names the app matches stay in English: the "Monthly budget" file name prefix, the template's Summary and Transactions tabs, and the Date and Amount columns.

### Fixed

- **Drive sync failures no longer show the exception text**: the sync-failed Notification and the Analysis error alert say only that the spreadsheet could not be synced or read, and the exception is logged instead.
  - The Notification is translated; Notifications already stored show the new message too, since the message comes from its key.

## [7.11.0] - 2026-10-25

### Added

- **Net Worth and the Investment Calculator in Vietnamese**: both pages, their charts, the FI Breakdown and the help dialog follow the UI language (slice L3 of the localization spec, #363).
  - 160 keys under `NetWorth.` and `InvestmentCalculator.`; the pages' scripts read theirs from `<js-strings>` islands.
  - The FI Breakdown takes its wording from the localizer, so the live cards after each recalculation match the server's.
  - Save, clear and Unavailable State messages and the parameter validation messages are translated.

### Changed

- **Net Worth amounts use the shared money formatter**: the FI Target and every tile, table cell and chart label format in the reader's culture (`1.250.000 $` in Vietnamese); the hand-built `"$" + N0` is gone.
  - The Base Final card shows the retirement age after a recalculation too, as it does on first render.

### Fixed

- **Net Worth reads its fields in the reader's culture**: under Vietnamese, `180.000` is read as 180,000 and `7,5` as 7.5 for the projection and its range checks, instead of 180 and 75.

## [7.10.0] - 2026-10-25

### Added

- **Money Priorities in Vietnamese**: the page, its form, result card, Earlier results, Purpose Note area and Priority Tree controls read in the user's Language (slice L3 of the localization spec, #364).
  - The leaf labels, the estimate and AI disclaimers, the Purpose Note messages and the submit validation messages the ApiService sends are swapped for their translation by the Web app; text it does not recognize shows as sent.
  - Months and years use plural forms; dates, amounts and rates use the page culture.
  - Still in English: the "Your path" sentences and the Priority Tree diagram labels (written by the ApiService), the AI quota message and the AI-written Purpose Note itself.

### Changed

- **Purpose Note facts use the shared money formatter**: the AI prompt's dollar figures go through `MoneyFormatter` instead of a hand-built `$` and `N0`; the prompt still reads in English.

## [7.9.0] - 2026-10-25

### Added

- **Dashboard in Vietnamese**: every section of the Dashboard, its charts and its Unavailable State read in the chosen language (slice L3 of the localization spec, #360).
  - Amounts, percentages (`12,5%`) and month labels (`Tháng 10 năm 2026`) follow the reader's culture.
  - Chart series, axis, tooltips and the FI Target line come from a `Dashboard.Js.` strings island; the budget donut takes its slice names from the translated legend.
  - Recent activity descriptions are worded in the reader's language from new raw fields on `RecentActivityDto` (`Category`, `Amount`, `TargetAmount`, `SavedAmount`); `Description` stays as the English fallback.
  - Budget Planner category names are shared keys under `Common.BudgetCategory.`.

### Changed

- **Relative times are translated everywhere**: the `<time datetime>` tag helper ("5d ago", "Just now") now uses `Common.TimeAgo.` keys, and dates older than a week use the language's date format, on every page that shows them.

## [7.8.0] - 2026-10-25

### Added

- **Budget Tracker in Vietnamese**: the page, its dialogs, its script messages and its error responses are translated (slice L3 of the localization spec, #362).
  - 131 `BudgetTracker.*` keys; the script reads its strings from the page's `BudgetTracker.Js.` island.
  - The monthly sheet carries `PreviousPlanYear` and `PreviousPlanMonth` in place of the English `PreviousMonthLabel`, so "Copy from September 2026" names the month in the reader's language.

### Changed

- **Budget Tracker dates and amounts follow the reader's culture**: transaction dates show as `10/25/2026` or `25/10/2026` instead of `2026-10-25`, and the initial `$0.00` placeholders and the `($)` field labels come from the money formatter and the currency.

## [7.7.0] - 2026-10-25

### Added

- **Budget Planner in Vietnamese**: the Budget Planner page reads in the chosen language (slice L3 of the localization spec, #361).
  - Headings, category labels, ARIA labels, the chart's labels and tooltip, the remaining/over-budget label, field names and validation messages.
  - The save confirmation, the over-income warning, the save failure and the Unavailable State.
  - Percentages use the reader's number format (`12,5%` in Vietnamese); the money symbols come from the money formatter, not from the page.

## [7.6.1] - 2026-10-26

### Fixed

- **Net Worth projection with Vietnamese number formats**: the Net Worth page now reads its own fields in the page culture, so `180.000` is 180,000 and `7,5` is 7.5 under Vietnamese; before, they were read as 180 and 75, which gave a wrong projection and rejected valid return rates.

## [7.6.0] - 2026-10-25

### Added

- **Vietnamese number and date formats**: a Vietnamese reader now sees `1.234,56` and `25/10/2026`, and types amounts the same way (slice L2 of the localization spec, #353).
  - The formatting culture follows the language (`vi` → `vi-VN`, `en` → `en-US`), from the cookie or the browser.
  - Account settings › Language says so in one line.
  - Account emails write their dates in the recipient's culture.
- **One money formatter for server and browser**: the currency symbol and decimals come from the amount's currency, the separators from the culture (`$1,234.56`; `1.234,56 $`; `1.234.567 ₫`).
  - `SupportedCurrencies` constant (USD, VND; only USD is used yet) and `MoneyFormatter` in Shared; `PFI18n.formatMoney`, `formatNumber`, `formatDate` and `parseNumber` in `core/i18n.js`.
  - Every `"C"` / `"C0"` format and every hardcoded `'en-US'` in page scripts is replaced, chart axes and tooltips included; a test bans `"C"` formats.

### Changed

- **Amounts are read in the reader's culture, strictly**: a posted amount must group digits in threes and use the culture's decimal separator, so `1234.56` typed under Vietnamese is refused instead of being saved as 123,456.
  - Budget Planner and Savings Goals money fields group digits as you type.
  - Savings Goals no longer saves an amount it could not read as 0; it shows why instead.

## [7.5.0] - 2026-10-25

### Added

- **Sign-in and Account settings in Vietnamese**: every Identity page is translated, from sign in, register and password reset to the Account settings pages and their navigation (slice L3 of the localization spec, #355).
  - Titles, labels, help text, buttons, placeholders, validation and status messages all come from the shared resource files; English reads as before.
  - Status messages are stored as keys and translated when shown, and error messages keep their red style in every language.
  - The Account emails were already translated in 7.0.0. The deletion date, in the email and on the page, keeps its English format until the shared date and number formatting lands (slice L2).

## [7.4.0] - 2026-10-25

### Added

- **App shell in Vietnamese**: the navigation, top bar, footer, mobile bottom nav, account menu, cookie banner, data source toggle, Unavailable State and Send feedback page follow the chosen language (slice L3 of the localization spec, #357).
  - The top-bar search, the notifications dropdown and the data source toggle's error message are translated too.
  - The Send feedback form's labels, categories, confirmation and validation messages are translated; messages ApiService returns stay in English.

### Changed

- **Several JS string islands per page**: `<js-strings>` islands are marked with `data-i18n-strings` instead of an id, and `core/i18n.js` merges every island, so the layout's `Shell.Js.` strings sit beside a page's own.
- **Notifications dropdown built with DOM nodes**: the dropdown no longer assigns `innerHTML`; its markup and behaviour are unchanged.

## [7.3.0] - 2026-10-25

### Added

- **Community page in Vietnamese**: the Community page's own wording (headings, stats, buttons, the story form, toasts and error messages) follows the chosen language (slice L3 of the localization spec, #358).
  - Polls, stories, comments and author names are user content and stay as written; they are never translated.
  - Poll and story category labels are translated; the stored category value does not change.
  - Vote counts use plural forms, and "5m ago"-style times are translated.

## [7.2.0] - 2026-10-25

### Added

- **Landing Page and About in Vietnamese**: the Landing Page and the About page are shown in the visitor's language, from the language cookie or the browser's `Accept-Language` (slice L3 of the localization spec, #356).
  - The sample plan keeps its US dollar figures until amounts are formatted per language.
- **English-only notice on the Legal Pages**: Privacy, Terms, Disclaimer, Cookies and Contact keep their English text, now under a translated title and the notice "Legal text is provided in English" for visitors using another language.
  - The English text is tagged `lang="en"`, so screen readers read it as English.

## [7.1.0] - 2026-10-25

### Added

- **AI Assistant replies in your language**: the assistant answers in the Language saved on the Account, English when none is saved (#354).

## [7.0.0] - 2026-10-25

### Added

- **Language saved to the Account**: the language an Account chooses is stored and follows it to every browser and device it signs in on (slice L1c of the localization spec, #295).
  - New Account settings › Language page; the account-menu switcher saves the choice too.
  - Registering, with a password or with Google, saves the language the visitor was using.
  - Signing in writes the language cookie from the stored choice; an Account that never chose keeps its browser's language.
  - The Data Export includes the chosen language.
- **Emails in the Account's language**: the confirm-email, change-email, reset-password and deletion emails are written in the recipient's stored language, in the app's branded layout.
  - Password-reset and change-email emails used the plain Identity UI text before; they now share the confirmation email's layout.

### Changed

- **Notifications in the reader's language**: savings milestones, budget alerts and Google Drive sync results are stored as a key with arguments and shown in the language of whoever reads them, even after switching. Notifications from before this release keep their English text.
- **Database migration `AddUserPreferenceAndNotificationKeys`**: a new `UserPreferences` table, and `TitleKey`, `MessageKey` and `Arguments` columns on `Notifications`. Additive only; nothing existing is changed or removed.

## [6.3.1] - 2026-10-25

### Changed

- **Identity pages owned by the app**: the 22 sign-in and Account settings pages still served precompiled by the ASP.NET Core Identity UI library are now scaffolded into `Areas/Identity/Pages`, so they can be translated (slice L1b of the localization spec, #295).
  - Forgot and reset password, confirm email and email change, two-step sign-in and recovery codes, sign out, access denied, and Account settings' Profile, Email, Password, Set password, External logins and two-factor pages.
  - The pages render the same HTML as before: the views are the library's own Razor files for Identity UI 10.0.0, and the page models keep its anonymous pages, emails, log messages and event IDs.
  - A test pins that every library page is in the project, which pages allow anonymous visitors, and that the confirm-account link is never shown while a real email sender is configured.

## [6.3.0] - 2026-10-25

### Added

- **Vietnamese, starting with Savings Goals**: the app can now show its text in Vietnamese, and the Savings Goals page is the first page translated end to end (slice L1a of the localization spec, #295).
  - Visitors get the language their browser asks for (`Accept-Language`) when it is English or Vietnamese, otherwise English.
  - An EN | VI switcher in the top bar for signed-out pages, and a Language section in the account menu, remember the choice in a cookie for a year. The choice is not yet saved to the Account.
  - Validation, model-binding and ASP.NET Core Identity error messages follow the language; untranslated text falls back to English.
  - Numbers, dates and the currency symbol still use the US format.
- **JSON localizer**: an in-house `IStringLocalizer` reads `Resources/i18n/<language>.json` once at startup and falls back to English per key, logging events 3401 and 3402 (ADR 0015).
  - `@L["Key"]`, `L.Named`, `L.NamedHtml` and `L.Plural` in Razor; `<js-strings prefix="…">` and `core/i18n.js` (`PFI18n.t`, `PFI18n.plural`) for JavaScript.
- **Translation tooling**: `npm run i18n:translate` drafts missing and stale keys with Azure AI Translator, and CI's new `i18n-report` step lists missing, stale and unreferenced keys.
  - `i18n.lock.json` records which English each translation came from, so only changed keys are re-sent and hand corrections survive.
  - CI fails only on malformed resource JSON or a translation whose placeholders differ from the English.

### Changed

- **Cookie Policy**: lists the language cookie among the essential cookies.

## [6.2.2] - 2026-10-25

### Changed

- **Browser tab title and icon**: the site now has a ledger-bars favicon in the brand green and brass, and the landing page tab reads "PersonalFinance — Plan for enough".
  - Ships `favicon.svg` with `favicon.ico` (16, 32, 48 px), `favicon-32x32.png` and a 180 px `apple-touch-icon.png` fallback, all linked from `_Layout.cshtml`.
  - Inner pages keep the "Page · PersonalFinance" format; a view can set `ViewData["DocumentTitle"]` to replace the whole title.

## [6.2.1] - 2026-10-25

### Fixed

- **Custom domain HTTPS on deploy**: the deploy and rollback workflows now pass the `CUSTOM_DOMAIN_CERTIFICATE_ID` repository variable to the Bicep, so a deploy keeps the custom domain's managed certificate bound instead of rebinding the domain with HTTPS off.

### Changed

- **Custom domain runbook**: `docs/CUSTOM_DOMAIN.md` now sets up a subdomain such as `www` with CNAME records and CNAME validation, keeps the apex as an alternative, and covers switching hostnames and PowerShell quoting.
  - A production setup log lists every command used for `www.personalfinancex.com`, ready to run in PowerShell.

---

## [6.2.0] - 2026-10-25

### Added

- **Purpose Note**: when you give a Purpose on the Money Priorities page, the AI Assistant explains in a short plain-text note under the result how the suggested priority serves it.
  - The note loads after the result, so the result never waits for it, and shows the AI disclaimer.
  - A Walk identical to an earlier one reuses that Walk's note instead of asking again.
  - Distinct states cover no Purpose (an invitation to add one), not available right now, the AI quota reached, and a refused Purpose.
  - Usage counts against the AI quota and is recorded as `money-priorities-note`.
  - Only the Purpose, the answers, the path, the suggested priority and whole-dollar figures are sent to OpenAI.
- **Privacy page OpenAI disclosure**: the Privacy page says what Money Priorities sends to OpenAI for a Purpose Note, and that nothing is sent without a Purpose.

---

## [6.1.0] - 2026-10-25

### Added

- **Money Priorities page**: a form that asks how much you can put aside each month and the questions the Priority Tree needs, showing each follow-up question only when your answers call for it.
  - The result card shows the recommended next step with its figures, "Your path" in plain sentences, the stated assumptions and the estimate disclaimer, and a footnote on IRA eligibility.
- **Priority Tree diagram**: the tree is drawn with Mermaid, with your path highlighted after a submit, and switches between the whole tree and a path-only view to stay readable on narrow screens.
- **Earlier results**: your 10 most recent Priority Walks are listed on the page, and you can view or delete each one.
- **Navigation and Privacy page**: Money Priorities appears in the sidebar under My Finances, and the Privacy page lists the answers and Purpose it stores.
- **E2E walkthrough**: a Playwright walkthrough submits three Walks that reach different leaves, checks Earlier results and the path view.
- **ADR 0014 and Mermaid 11.17.2**: the decision to draw authored diagrams with Mermaid, with `mermaid@11.17.2` vendored through libman.

### Fixed

- **Age range sent as a number**: the page sends the age range to the API as its numeric value, which the API expects.

---

## [6.0.0] - 2026-10-25

### Added

- **Money Priorities foundations**: the data, Priority Tree, math and API that the Money Priorities page will use; there is no user-visible page yet.
- **Priority Walk table**: a new `PriorityWalks` table stores each Priority Walk's answers, Purpose, path and leaf, so this release changes the schema (hence the major version).
  - Migration `20261025060333_AddPriorityWalk`. Walks are soft-deleted like other user-owned data; dollar figures are never stored, they are recomputed from the answers.
- **Priority Walks in the Data Export**: the account data export has a Priority Walks section with each Walk's answers, path and Purpose Note.
- **Priority Tree version 1 and path evaluation**: the authored tree of five questions and seven leaves, and a pure evaluator that walks it from a set of answers and clears every answer off the path.
  - The submit validator and the evaluator share one source for which questions a path asks, and a test checks they agree for every leaf.
- **IRS 2026 contribution limits**: a table of the 401(k) employee deferral and IRA limits by age range (including the age 50 and 60–63 catch-ups) and the Roth IRA phase-out ranges, looked up by tax year.
- **Leaf math in decimal only**: debt payoff, emergency fund gap, employer match, the IRA-then-401(k) split and compound growth under the Cash, Conservative, Base and Aggressive scenarios, with no floating point anywhere.
  - Golden-value tests pin each calculation to fixed expected figures, and a test fails if the Domain uses `double`, `float`, `Math.Pow` or `Math.Log`.
- **Mermaid diagram texts generated in C#**: the whole tree, the tree with a Walk's path highlighted, the path view, and plain-language sentences that describe the path.
- **Money Priorities API and Refit contract**: authenticated endpoints under `api/MoneyPriorities` to submit, list, view and delete Walks and to fetch the tree diagram, with the `IMoneyPrioritiesApi` Refit client for the Web app.
  - A submit with the same answers, Purpose, tree version and limits year as the latest Walk returns that Walk instead of saving a duplicate.

### Changed

- **Glossary**: adds Money Priorities, Priority Tree, Priority Walk, Purpose and Purpose Note.

## [5.13.0] - 2026-10-24

### Changed

- **One theme across the app**: every page now uses the Landing Page's Ledger theme (navy ink, ledger green, brass, IBM Plex Sans), replacing the indigo theme on the signed-in pages.
  - `_Layout` loads `css/ledger.css` on every page, and `site.css` maps the `--app-*` tokens onto its `--lp-*` colours, so buttons, links, focus rings, badges and Bootstrap/Fluent components follow it.
  - Hardcoded indigo, violet, Tailwind-grey and Fluent UI colours in the feature stylesheets and the AI chat widget now use the `--app-*` tokens.
  - The sidebar is navy ink, like the Landing Page's dark band, with a light-green active link and focus ring.
  - Projection charts on the Dashboard and Net Worth pages share one scenario palette: base in ledger green, conservative in slate, aggressive in steel blue, and the FI target in brass. The investment calculator and Budget category charts use the same family.

### Fixed

- **Net Worth validation message is shown as text**: the server's message is no longer inserted as HTML.

## [5.12.0] - 2026-10-24

### Added

- **Remove a data source from Analysis**: the Analysis page offers "Remove Google Sheet data…" and "Remove Budget Tracker data…" for each source that holds data, with a confirmation page showing how many months and transactions will go.
  - Removing the Google Sheet soft-deletes all synced months and transactions; the Google Drive connection stays, so the user can sync again.
  - Removing the Budget Tracker soft-deletes its categories, plans and transactions, needs `REMOVE` typed to confirm, and the default categories return on next use.
  - If the stored Data Source preference pointed at the removed source it is cleared, so Analysis, the Dashboard and search fall back to the source that still has data (ADR 0002, section 4).

### Changed

- **"Remove drive" explains what stays**: its confirmation now says that budget data already synced stays in Analysis, and links to removing it.
- **Default Budget Tracker categories are seeded whenever none are in use**: a user with no live categories, plans or transactions gets the defaults again, instead of only a user who never had a category.

## [5.11.0] - 2026-10-24

### Added

- **Rollback workflow**: a manually run "Roll Back to a Release" workflow redeploys the container images of a chosen release tag with the same deploy and smoke-test steps as a merge to `main`.
  - It fails before deploying when the tag is missing or malformed, an image is gone from GHCR, or database migrations changed since the tag (unless `allow_schema_drift` is set).
  - It shares the `main` deploy's concurrency group, points `latest` at the rolled-back images, and leaves tags and Releases untouched.

### Changed

- **Deploy and smoke-test steps are shared actions**: the deploy job's steps and the smoke-test script moved unchanged into `.github/actions/deploy-azure` and `.github/actions/smoke-test`, used by both the deployment and the rollback.
- **Assemblies carry the release version**: the container builds pass `AppVersion.Current` to `dotnet publish` with `-p:Version`, so the deployed DLLs report the release version, not 1.0.0.

## [5.10.0] - 2026-10-24

### Changed

- **Minified JavaScript and CSS in production**: outside Development, pages load minified copies of the app's own scripts and stylesheets from `/dist`, compressed with Brotli and gzip, while Development still serves the readable originals (#288).
  - Over the wire (Brotli), the app's own JavaScript drops from 155 KB to 89 KB and its CSS from 52 KB to 40 KB.
  - The Release build writes the copies with esbuild (one file each, no bundling, no source maps), so Release builds and the container build need Node 22 (#287); Debug builds need no Node.
  - A page never points at a missing file: `/js` and `/css` references move to `/dist` only when the minified copy exists, and in Development only when the original is missing.
- **Published output holds only what production serves**: a Release publish leaves out the unminified `wwwroot/js` and `wwwroot/css`, the web root's npm and ESLint files and `node_modules`, so production can no longer serve them (#289).
  - Vendored third-party files under `wwwroot/lib` are published as shipped.
  - The container build ignores any local `node_modules` and `wwwroot/dist`.
- **Publish-output CI check**: CI publishes the Web project and fails when the minified files or their `.br`/`.gz` copies are missing, a source map is published outside the vendored folders, or unminified source or tooling remains (#289).

## [5.9.0] - 2026-10-24

### Added

- **Your basket on the Sectors tab**: a "Market | Your basket" switch shows your Fund Basket in the Sectors sunburst and treemap, with the same breadcrumb, keyboard paths and cards; it opens on Your basket when a basket is set (#269).
  - The hierarchy is SEC industry office, SIC industry and the ten largest companies of each industry with "Other (n companies)", then "Not classified by the SEC" by country, Bonds by issuer type, and Cash and other.
  - Every percent is of your basket. Companies with no SEC industry, mostly foreign ones that file no reports with the SEC, are hatched grey and explained in a card with the largest countries; International Corp Fin and Crypto Assets are neutral grey.
  - The basket's data is fetched when the view is first shown, so pages that never open it do not carry it.
- **Lean vs the market**: under the basket chart, each SEC office's share of the SEC-classified part of your basket against its share of every filer's Public float in the scrubber's year, with the difference in points (#270).
  - Each difference has a sign, an arrow and a bar on one axis, in blue (more) or orange (less), so it never relies on colour; a row zooms the chart into its office. It describes your basket and is not a target.
- **Fund industries dataset**: each Tracked Fund's share of net assets by SEC industry office and SIC industry, with the rest by issuer country, bond issuer type and cash, over every holding of its latest N-PORT report, at `api/Research/funds/industries` (#269).
  - Holdings are matched to SEC filers by issuer name only, never by a security identifier; `Research:IssuerCikOverrides` names companies renamed or delisted since a fund reported them.
  - On recorded June 2026 reports, names alone match 98.9% of SPY's and 99.5% of QQQ's stock weight.
  - A refresh reads at most `Research:FundIndustrySubmissionsPerRefresh` (1,000) new company submissions, so the first runs classify more each day.

## [5.8.2] - 2026-10-24

### Removed

- **Google Drive sample link**: the Connect form no longer offers a link to the maintainer's own Drive folder.
  - The "Use sample" button is gone, and the folder field now starts empty instead of pre-filled with that folder.

## [5.8.1] - 2026-10-24

### Fixed

- **Research toggle buttons**: a disabled option, such as "Adjusted for inflation" on the Performance tab before the inflation data has loaded, now looks disabled instead of like a button that ignores clicks.
  - Screen readers announce the note explaining why it is disabled.

## [5.8.0] - 2026-10-24

### Added

- **Landing Page Skyline**: a 3D view of Stanton, California, the product's Headquarters, now sits behind the Landing Page hero.
  - The buildings rise outward from City Hall like a growth chart, then the camera slowly orbits. The Sample plan sits on top as a frosted card.
  - It is drawn with MapLibre GL JS 5.24.0 (BSD-3, vendored) over a self-hosted OpenStreetMap extract, credited on the page and in the Terms. No third-party map service is contacted.
  - It never slows the page: below 992px nothing is downloaded, MapLibre and the data load only after the page has loaded and the browser is idle, and under reduced motion, reduced data, slow connections or without WebGL a still poster stays.
  - `scripts/extract-skyline.mjs` regenerates the data and `scripts/capture-skyline-poster.mjs` the poster. ADR 0013 records the choices; the glossary adds Headquarters and Skyline.

## [5.7.0] - 2026-10-24

### Added

- **Your basket on the World tab**: a "World market | Your basket" switch opens on your Fund Basket's exposure by issuer country when a basket is set, and leaves the market map as it was otherwise (#267).
  - A map of the basket's share by country, weighted by your funds' shares, zooms and pans; places the map cannot draw (Hong Kong, Singapore, Bermuda and others) are dots that keep their size under zoom.
  - The colour scale tops out at the largest country outside the US, which always takes the darkest step; shares under 0.05% read "<0.1%".
  - Callouts give the share outside the US, the share not priced in US dollars and the largest region.
- **Meters and bars**: two home-bias meters (US vs rest of world, US dollar vs other currencies) that screen readers read out, region bars that light their countries on the map, and the currencies the holdings are priced in (#268).
  - The subtitle states that countries are where each issuer is organized as each fund reports it, that ADRs count as US dollars, and that this is not where companies earn their revenue.
- **Fund exposure dataset**: each Tracked Fund's share of net assets by issuer country and currency, over every holding of its latest N-PORT report, at `api/Research/funds/exposure` (#267).
  - Securities-lending collateral is left out and the rest is unallocated; the dataset is rebuilt only when a fund files a newer report.
  - The country-code table gains its two-letter codes (`i18n-iso-countries@7.11.0`), and a table groups countries into five regions.

### Fixed

- **Securities-lending collateral in the attribute form**: collateral flagged as `cashCollateralCondition/@isCashCollateral` (as IWM reports its 17%) is now left out of the fund overlap, as the element form already was.

## [5.6.0] - 2026-10-24

### Added

- **Risk vs return tab on Research**: each Tracked Fund's annualized return against its volatility over a 1-, 3- or 5-year window (3 years by default), for information only (#265).
  - Bubble area follows each fund's net assets, and each fund keeps its colour; your Fund Basket is a dark diamond, computed from its funds' monthly returns rebalanced to its weights.
  - Every point is focusable and opens a card with its annualized return, volatility, Sharpe ratio (with the Treasury bill rate it uses), expense ratio and net assets.
  - "Show as table" lists the funds and the basket sorted by Sharpe ratio, with inline return and volatility bars; a negative Sharpe ratio reads with a minus sign.
- **Role filter, quadrants and labels**: role chips narrow the funds shown while the basket stays and no colour or point moves; dashed lines cross at the basket with descriptive corner labels such as "Higher return, smaller swings than your basket"; labels move aside on short leader lines when funds cluster (#266).
- **Fund facts dataset**: each Tracked Fund's net assets and expense ratio, at `api/Research/funds/facts` (#265).
  - The expense ratio is read from the XBRL of the fund's newest shareholder report (Form N-CSR or N-CSRS), for the share class matching its ticker; a report already read is not fetched again.
  - SPY, a unit investment trust, files no such report, so its expense ratio shows "n/a".
- **ADR 0012**: Research data comes from official public sources (SEC EDGAR, the Bureau of Labor Statistics, the Fed Board, the Treasury, the World Bank), never FRED, precomputed in the daily refresh; the development guide lists every dataset with its endpoint and source.

### Fixed

- **Research XML readers**: the XBRL and Treasury bill readers no longer skip the element after one whose content they read, which made the expense ratio reader miss every other class.

## [5.5.0] - 2026-10-24

### Added

- **Performance tab on Research**: how 100 put into your Fund Basket and into each of its funds would have grown since mid-2019, from each fund's monthly total returns, for information only (#262).
  - "Your basket" is the heavier dark line, rebalanced to its weights every month; each fund keeps its colour, with end labels and a legend.
  - A "Nominal | Adjusted for inflation" switch divides every line by how much CPI-U rose; there is no CPI line and no second axis.
  - Callouts give the basket's total and annualized return, its worst fall from a high and the month it bottomed, and inflation over the period; a table gives the annualized return, total return, worst fall and return after inflation of the basket and each fund.
- **Rate and difference panels**: under the growth chart, the effective federal funds rate and your basket minus a chosen Tracked Fund (VT by default), blue where the basket is ahead and orange where it is behind (#263).
  - One crosshair spans all three panels, moved by pointer or by the arrow keys, Home and End, with a tooltip of every series and the month's rate.
  - A strip under the chart, with 1Y, 3Y, 5Y and Max presets, chooses the period; every line starts at 100 again, and the callouts and table follow.
- **Performance annotations**: muted, labelled bands shade the Fed's hiking cycles and recessions, and a marker shows the basket's worst fall from a high (#264).
  - Hiking cycles are derived from month-on-month rises in the federal funds rate (March 2022 to August 2023 for the last one).
  - Recessions come from configuration (`Research:Recessions`, shipping the 2020 recession), so one can be added without a code change.
- **Macro dataset**: monthly CPI-U from the Bureau of Labor Statistics and the effective federal funds rate and 3-month Treasury bill from the Fed Board's H.15 release, at `api/Research/macro`, refreshed daily with the other Research datasets (#262).
  - When the Fed's release cannot be read, the Treasury bill rate comes from the Treasury's daily bill rates instead; FRED is not used, because its terms forbid caching.
  - Days H.15 marks as having no data are left out, and a month BLS did not publish (October 2025) is filled in between its neighbours on the chart.

## [5.4.2] - 2026-10-24

### Fixed

- **Web wakes the API when it starts**: a Web replica that starts from zero now pings the API at once, so both containers start in parallel.
  - Before, only the Login page woke the API. A visitor who was already signed in, or one who signed in quickly with Google, reached the Dashboard while the API was still waking, waited 30 seconds, and saw an empty Dashboard.
  - The ping is anonymous, throttled to once every two minutes, and never delays Web startup.

## [5.4.1] - 2026-10-24

### Changed

- **Warm during Pacific hours**: production keeps one Web and one API replica running from 08:00 to 22:00 Pacific, so daytime visitors no longer wait for a cold start.
  - Before, both apps scaled to zero after a few idle minutes. Waking each one took about 25 seconds, and the Web's call to a still-waking API often hit the 30-second timeout, leaving the Dashboard empty or in the Unavailable State.
  - The window follows daylight saving through a new `businessHoursTimeZone` deployment parameter. Outside it, both apps still scale to zero.
  - ADR 0011 records why this window was chosen over warm replicas around the clock; ADR 0005 has a matching amendment.

## [5.4.0] - 2026-10-24

### Added

- **Fund Basket bar**: on the Funds tab, pick one to five Tracked Funds from a searchable list grouped by role, each with an optional weight, to view together (#259).
  - Weights that don't add up to 100% are scaled to 100% with a note, and "Use equal weights" resets them. A fund set to 0% is removed.
  - Nothing is saved: the basket lives in the page address (`?basket=VTI:60,BND:40`), so a link or a reload keeps it. A link with untracked funds, repeats or bad weights keeps what it can and says what it left out.
  - Each Tracked Fund keeps one colour wherever it appears, always next to its ticker.
- **Overlap: same holdings**: a matrix of how much of each pair of basket funds' money is in the same securities, by weight, from each fund's latest N-PORT report (#260).
  - A "Most duplicated holdings in your basket" table ranks shared holdings by their share of the basket. Choosing a fund's label filters it, and pointing at or focusing a cell highlights that pair's holdings.
  - Callouts name the most overlapping pair, the share of the basket held by two or more of its funds, and the largest duplicated holding.
  - Securities are matched by CUSIP or ISIN in memory during the refresh only; no identifier is stored, logged or shown (ADR 0010).
- **Correlation: move together**: a matrix of how closely each pair of funds' monthly returns moved together over 1, 3 or 5 years (3 by default), for the basket or for all tracked funds (#261).
  - Values are printed in every cell on a blue-to-red scale, with callouts for the most similar and the least correlated pair.
  - Monthly NAV total returns come from N-PORT Item B.5, read once per filing from its head; QQQ's history runs on across its 2025 conversion.
  - The refresh also computes each fund's total and annualized return, volatility, worst fall and Sharpe ratio per window (against the 3-month Treasury bill from the Fed Board's H.15 release), ready for the later Performance and Risk vs return tabs.
- **Research API**: read endpoints for the fund overlap (`api/Research/funds/overlap`) and fund returns (`api/Research/funds/returns`) datasets.
- **Glossary and ADR**: Tracked Fund and Fund Basket join the glossary; ADR 0010 records that Research never stores or shows security identifiers.

### Changed

- **ETFs tab is now the Funds tab**: it follows 20 Tracked Funds (ETFs and mutual funds), each with a role, up from 6 ETFs; an old `?tab=etfs` link still opens it (#258).
  - The funds refresh saves the filings read after each fund, so a first run over the new funds that stops at the job's time limit carries on from there next time.

### Fixed

- **Research page no longer stalls on data that hasn't loaded**: a dataset no refresh has built yet answered 503, which the Web app retried three times with growing delays, so the page took about 36 seconds and logged a warning with a stack trace per attempt.
  - The Research client no longer retries a 503 or counts it towards the circuit breaker; other transient faults still retry.
  - The page reads its five datasets side by side, and a dataset that has not loaded yet is logged as one information line.

## [5.3.0] - 2026-10-24

### Added

- **Sectors treemap view**: a Sunburst | Treemap toggle above the Sectors chart shows the same zoom, year and figures as tiles (#257).
  - Tiles keep the office colours; industries or companies inside one office step in lightness so neighbours stay apart.
  - Tiles are focusable: Enter zooms in and Escape zooms out. The switch fades in briefly, never with reduced motion.
- **Sectors keyboard paths**: every level of the Sectors chart can be explored without a mouse, in two ways (#256).
  - Arcs: one tab stop; the arrow keys move between neighbouring slices, Enter or Space zooms in, and Escape zooms out with focus back on the group left. A focused arc shows its tooltip.
  - A ranked list under the chart shows the zoomed group's parts, largest first, each a button that zooms in; hovering or focusing a row lights its slice.
- **Sectors takeaway cards**: a "Key takeaways" list in the side panel describes the zoomed group: total, largest part, top 10 companies, Technology share and change on the year before (#255).
  - A card that would say nothing is left out, such as the Technology share inside Technology.

### Changed

- **Sectors breadcrumb**: a clickable breadcrumb above the chart replaces the "Zoom out" button and the focus label; every group above the focus jumps back to it (#255).
- **Sectors tooltip**: a slice's tooltip now also gives its share of all filers and its rank among its neighbours (#255).

## [5.2.0] - 2026-10-24

### Added

- **Press releases on the Company Profile**: the Companies tab lists the announcements a company attached to its SEC 8-K filings as exhibit 99 in the last 12 months, newest first, up to 10, each with its headline, filing date and a link to the release on sec.gov (#271).
  - The headline is read from the start of the release; when none can be read, the entry shows the 8-K's events in plain words instead, so nothing is left out.
  - The section loads after the profile, so reading releases never slows it; a failed read shows "Press releases are unavailable right now" with a Retry, and a company with none says so.
  - These are the company's own announcements only, never third-party coverage.
  - Lists are cached for a day and served from the cache while SEC is unreachable; each request counts toward the 30 lookups a minute an Account may make.
  - `Research:PressReleases:Enabled` on the ApiService (on by default) switches the section off without a code change; the Web app has no switch of its own and leaves the section out when a lookup says the releases are unavailable.

## [5.1.0] - 2026-10-24

### Added

- **Companies tab on Research**: look up any company registered with the SEC by ticker or name and see its Company Profile, from its own SEC filings, for information only (#242).
  - **Identity:** name, ticker, exchange, CIK, SIC industry, SEC industry office, state of incorporation and fiscal year end.
  - **Annual figures:** up to ten fiscal years of revenue, net income, diluted EPS and operating cash flow as bar charts, and a table that adds gross profit, assets, liabilities, equity, shares outstanding, Public Float, margins, growth and liabilities to equity, each derived figure with its formula.
  - **Recent filings:** the 20 latest 10-K, 10-Q and 8-K reports, with 8-K events in plain words and links to EDGAR.
  - Figures are read as reported under US GAAP in US dollars; a figure the company did not report that way shows as "Not reported", never zero. There are no prices and no recommendations.
  - The lookup is kept in the address, so a profile can be bookmarked and Back returns to the previous one. A fund ticker points to the ETFs tab.
  - Lookups are cached for a day, a cached profile is served while SEC is unreachable, and each Account may make 30 lookups a minute.
- **Research tabs in the address**: the Research page opens on the tab named by `?tab=` and keeps the chosen tab there.

## [5.0.1] - 2026-10-23

### Fixed

- **Production deploy**: the deploy no longer fails with "Unable to parse parameter: research" when the `SEC_USER_AGENT` repository variable contains spaces (#217).
  - The workflow now quotes `secUserAgent`, so `azure/arm-deploy` passes it to Bicep as one value.

## [5.0.0] - 2026-10-23

### Added

- **Research page**: a new signed-in page, in the sidebar and linked from Net Worth, shows how the market is made up and how it changed over time, from public SEC and World Bank data, for information only (#217).
  - **Sectors:** a sunburst of the 500 largest SEC filers by Public Float, grouped by SEC industry office and SIC industry, with smaller filers summed into "Other". Click or use the legend to zoom, and turn on "Technology (incl. chip makers)" to see the tech share.
  - **ETFs:** a tree of what SPY, QQQ, VTI, VT, IWM and AIQ hold, by asset category and top 25 holdings, quarter by quarter from late 2019.
  - **World:** a map of stock-market capitalisation by country since 1975, or as a share of GDP.
  - A time scrubber with play/pause moves each tab through its history. Every tab has a table view, keyboard controls, a screen-reader summary, a reduced-motion mode and a phone layout.
  - Each tab names its source and shows when its data is from. If a source has never loaded, only that tab shows the Unavailable State.
- **Daily market-data refresh**: a scheduled job fetches the SEC and World Bank data once a day and keeps the last good snapshot when a source fails (#219).
  - SEC Public Float figures are cleaned before display: values far above a filer's total assets or its other years, and a reviewed list of misreported filings, are left out (#221).
- **ADR 0009**: D3 is admitted for bespoke, interactive visualisations; Chart.js stays the default for standard charts (#218).

### Changed

- **Licence policy**: ISC-licensed dependencies are now allowed, alongside MIT, BSD and Apache 2.0 (#218).

### Database

- **New ResearchSnapshot table**: holds the market-data snapshots behind the Research page. It stores no user data. This schema migration makes this a major release.

## [4.4.0] - 2026-10-23

### Added

- **FI Breakdown on the Net Worth page**: a new "How this is calculated" panel shows how the Savings Rate, FI Target, Progress to FI and FI age come from the values you entered, with each formula worked through in your own numbers (#204).
  - Each card names the inputs it is calculated from; the FI age card also lists the age for the Base, Conservative and Aggressive scenarios, and says so when FI isn't reached by your retirement age.
  - The panel updates as you move the sliders, together with the rest of the page.

### Changed

- **One definition of the FI figures**: Savings Rate, Progress to FI and the age each scenario reaches the FI Target are now calculated once in the Net Worth projection, and the Dashboard reads them from there, so the two pages can't disagree (#204).

## [4.3.0] - 2026-10-23

### Added

- **Money Flow on the Analysis page**: a new "Where your money went" chart shows one month of actual income flowing into each spending category and into savings, with a table of the same figures (#203).
  - In a month where spending exceeded income, the Shortfall and the income flow into one Spending total, and a note says how much more was spent than earned.
  - Each category gets its own colour, with a matching swatch in the table, from a palette checked for colour-blind readers. Income and Spending stay neutral; Shortfall is red and Saved is green.
  - Categories under 3% of the month, and any beyond the five largest, are grouped into "Other" in the chart; the table always lists every category and marks those "(in Other)". Spending a synced Google Sheet doesn't assign to a category shows as "Uncategorized".
  - On phones the table comes first, then the chart.
  - The chart uses the `chartjs-chart-sankey` 0.19.1 plugin (MIT) on the existing Chart.js.
  - The Analysis API's category insights now also carry that month's own income and expenses, because with no month selected the page's summary totals cover the whole year.

## [4.2.20] - 2026-10-23

### Fixed

- **Dead News Feeds Replaced**: three of the six Finance News sources had stopped working in production and are replaced, so every topic shows articles again (#183).
  - CNN Business (Budgeting) failed its HTTPS handshake and has published nothing since 2018: replaced by NerdWallet.
  - Investopedia (Investing) answered 402 Payment Required: replaced by CNBC Investing.
  - Forbes Finance (Retirement) answered 404, which left the Retirement topic always empty: replaced by Retirement Researcher.
  - An article is now always filed under its source's topic. The new feeds tag their items with their own categories ("Mortgages", "Annuities"), which used to take precedence and would never match the topic filter.

## [4.2.19] - 2026-10-23

### Changed

- **Tests Grouped by Feature**: every Feature's tests now live in `tests/PersonalFinance.Tests/<Feature>/` under the Feature's name, as the add-a-feature checklist and ADR 0007 say (#185).
  - `AccountDeletion/` and `DataExport/` become `AccountLifecycle/`; `Budget/` splits into `BudgetPlanner/` and `BudgetTracker/`.
  - The Landing Page, Savings Goals and Search Web controller tests move from `Web/` to `LandingPage/`, `SavingsGoals/` and `Search/`; the Consent tests from `Security/` to `Legal/`; the shared budget Data Source provider tests (ADR 0002) to `Infrastructure/`.
  - `TestTempDataProvider`, used by several Web controller tests, moves from the Budget Planner tests to `TestSupport/`.
  - `Web/`, `Security/`, `Infrastructure/`, `Architecture/` and `TestSupport/` keep only cross-cutting tests and helpers. Only file moves, namespaces and `using`s change; the test count is unchanged.

## [4.2.18] - 2026-10-23

### Fixed

- **Watchlist Save Validated**: saving an article to the Watchlist now checks it first, and the News page says when saving fails (#188).
  - `POST /api/Watchlist` validates the article with `SaveArticleRequestDtoValidator`: title, source and category are required, the address must be a full http or https link, and every field must fit its `SavedArticles` column (title 500, URL 2000, source 200, category 100, notes 2000, image URL 2000). An invalid article answers 400 with the reasons and is not saved; before, an empty field was stored and an overlong one failed in the database as a 500.
  - A test keeps the validator's limits equal to the database column lengths.
  - On the News page, a failed save shows "Couldn't save" on the button, with the reason in its tooltip and accessible label, then lets you try again; it used to fail silently.

## [4.2.17] - 2026-10-23

### Fixed

- **Saving a Removed Article Again**: saving an article you had removed from your Watchlist now brings it back, instead of failing (#186).
  - Removing is a soft delete, and the (Account, URL) index is unique across removed rows too, but saving only looked for articles that were not removed, so it inserted a second row that Azure SQL rejected. It now restores the removed row with the new title, notes and save time.
  - The save time comes from the injected `TimeProvider`.

### Added

- **Watchlist Tests**: the saved-articles Watchlist, which had no tests, is covered (#186).
  - `WatchlistServiceTests`: saving, saving the same URL again, saving after removing, listing only the Account's own articles newest first, removing (a soft delete, only your own), and checking a URL.
  - `WatchlistEndpointTests`: the ApiService controller is signed-in only and scopes every call to the Account in the claims; the Web JSON controller needs the antiforgery token to write and passes a failure on without exception details.
  - Validating the save request is tracked in #188.

## [4.2.16] - 2026-10-23

### Changed

- **News Tests Under Their Feature**: the Finance News tests move from `tests/News/` to `tests/Watchlist/`, the Feature the news feed belongs to, and the add-a-feature checklist in `docs/ARCHITECTURE.md` now states the conventions the code follows (#179).
  - Step 6: one Refit contract per ApiService controller, named after it (`NewsController` → `INewsApi`), as all 17 contracts already are, instead of one `I<Feature>Api` per Feature.
  - Step 8: a Web controller's tests sit in its Feature's test folder; `tests/Web/` holds only tests of cross-cutting Web behaviour. Moving the older folders is tracked in #185, and tests for the saved-articles Watchlist in #186.
  - Only file moves and namespaces change; the test count is unchanged.

## [4.2.15] - 2026-10-23

### Fixed

- **News Topic Filter Offers Only Topics That Match**: the Finance News "All Topics" filter now lists only the topics some configured source covers, Budgeting, Investing, Retirement and General, instead of a fixed list in which Saving and Debt could never match an article (#178).
  - The six feeds send no category of their own, so an article's topic is its source's. General (BBC Business) can now be picked as a topic; Saving and Debt are gone.
  - The topic names are one set of constants, `NewsTopics`, that every source is filed under, and the topics are derived from the source list.

### Changed

- **News Feed Response**: `NewsFeedDto.PersonalFinanceCategories` is renamed `Topics`, and `NewsFeedDto.Categories` (the categories of every fetched article) is removed with the matching `NewsViewModel` properties, since the page never showed it (#178).
  - The 100-character filter limit is unchanged.
  - The dead Forbes, Investopedia and CNN feeds found while deciding this are tracked in #183.

## [4.2.14] - 2026-10-23

### Fixed

- **News Article Dates in UTC**: Finance News articles are now dated by their RSS `pubDate` read as RFC 822 in UTC, whatever the server culture (#177).
  - Before, `DateTime.TryParse` used the server culture and converted to the server's local time zone. It could not read zone names such as `EST` or `PDT`, and on a non-English server culture it could not read the dates at all; those articles were dated at the time of the fetch, which also reorders the newest-first list.
  - Numeric offsets (`-0400`, `+0530`) and the RFC 822 zone names (`GMT`, `UT`, `EST`, `PDT`, ...) are applied; other invariant dates (ISO 8601) are still read, and an unreadable date still falls back to the current time.

### Changed

- **RSS Parser Split**: `RssNewsFeedClient` reads a feed in small steps (fetch, map an item, image, date) instead of one method over the complexity ceiling, and its `CA1502` suppression is removed (#177).
  - The date parsing lives in `Adapters/Rfc822Date.cs`.
  - The two near-identical catch blocks are one: a timeout, an unreachable host or a feed that is not RSS skips that source and logs one warning. Five articles per source, HTML stripping and 250-character descriptions are unchanged.

## [4.2.13] - 2026-10-23

### Fixed

- **News Refresh Failures Shown**: when refreshing the Finance News fails, the page now says so above the articles instead of silently keeping the old ones (#175).
  - `NewsController.Refresh` carries `[JsonApiErrors]` instead of a try/catch, as `docs/ARCHITECTURE.md` asks of JSON actions: a failure reaches the browser as an error status with a safe `{ "error": "..." }` message, never exception details.
  - `news.js` checks the response status and shows that message in a new alert; the current articles stay in place, and the alert hides again on the next successful refresh.
  - The unused `Error` property is removed from `NewsRefreshResponse`. A rejected filter still refreshes to no articles, not an error.

## [4.2.12] - 2026-10-23

### Changed

- **Rejected News Filters Answered by ApiService**: when a News filter is rejected (over 100 characters), `GET /api/News` now answers 400 with the reasons and the filter options, so the Web News page makes one ApiService call in every case (#176).
  - The 400 body is a `NewsFeedDto` with no articles, the validation messages in a new `Errors` list, and the categories, sources and personal finance categories of the whole feed. Successful responses carry an empty `Errors` list.
  - `INewsService.GetFilterOptionsAsync` returns those options from the cached feed.
  - Web's `NewsController` no longer makes a second, unfiltered request to rebuild the filter options; it reads them from the 400 body.
  - The page looks the same: a rejected filter still shows no articles with the filters on offer, and no outage message.

## [4.2.11] - 2026-10-23

### Changed

- **Finance News Read Through ApiService**: the RSS feed fetching, parsing, filtering and caching behind the Finance News page move from Web into ApiService's Watchlist Feature, and Web reads the feed through a Refit client like every other Feature (#171).
  - New `GET /api/News?category=&source=&search=` endpoint, signed-in only, returning the matching articles and the filter options in one `NewsFeedDto`. Each filter is validated server-side to at most 100 characters; the News page shows no articles for a filter ApiService rejects, with its filter options still offered, rather than an outage message.
  - `NewsService` and the RSS client (`Adapters/RssNewsFeedClient`) live in `ApiService/Features/Watchlist/`; the DTOs in `Shared/DTOs/Watchlist/NewsDtos.cs` and the contract in `Shared/Contracts/INewsApi.cs`.
  - Web's `NewsService` and `NewsQueryOptions` are removed. The News page now makes one ApiService call instead of two feed reads.
  - The page, its filters, the refresh endpoint and its JSON are unchanged.

### Fixed

- **News Feed Cache**: the five-minute news cache now holds across requests. It lived on a transient typed `HttpClient` service, so every request read all six feeds again; it is now kept in `IMemoryCache`.

## [4.2.10] - 2026-10-23

### Changed

- **Web Uses the Classic MVC Layout**: `PersonalFinance.Web` moves from Feature folders back to `Controllers/`, `Models/<Controller>/` and `Views/<Controller>/`, so a page's controller, view model and views are found from one another by name (ADR 0008, #170).
  - Every controller is in `Controllers/`, including the JSON-only ones and `NotificationController` (from `Infrastructure/`). View models sit in `Models/<Controller>/`, named after the controller that renders them: the Budget Planner's in `Models/Budget/`, the Landing Page's in `Models/Home/`, the Feedback inbox's in `Models/Admin/`.
  - `UserConsentService` and `NewsService` are in `Services/`, `TimeAgoTagHelper` in `TagHelpers/`. `Infrastructure/` keeps only host plumbing. `NewsService` moves to ApiService in #171.
  - The two Web `Add<Feature>Feature()` extensions are gone; their registrations are inline in `Program.cs`.
  - `Views/_ViewImports.cshtml` imports every `Models.*` namespace, so views name their model without qualification.
  - A new `WebMvcLayoutTests` fails the build when a Web type sits outside these folders, a controller is outside `Controllers/`, or a `Views/` folder has no controller of that name. `FeatureLayoutTests` now checks the Feature rules for ApiService only.
  - AGENTS.md, CONTRIBUTING.md, ARCHITECTURE.md (with a Feature to Web controller map) and the coding standards describe the new layout.
  - No behaviour, route, view or contract change.

## [4.2.9] - 2026-10-23

### Changed

- **Complexity Ceiling and Sealing Enforced**: `CA1502` (cyclomatic complexity), `CA1505` (maintainability index) and `CA1852` (seal internal types) now run at warning severity, so a new method over the ceiling or an unsealed internal class fails the CI build (ADR 0007, #162).
  - Thresholds live in `PersonalFinance/CodeMetricsConfig.txt`: cyclomatic complexity 20 per method, maintainability index 10. No new package; the rules ship with the SDK.
  - The 14 methods over the ceiling today carry an inline `[SuppressMessage]` naming their follow-up: the spreadsheet parser split (5 methods), the Google Drive client seam (3), the financial snapshot, the Analysis dashboard, a net worth projection rule table, splitting role seeding from the admin grant, an API error message table and a news feed parser split. No method was changed.
  - `CA1852` also applies to types that `InternalsVisibleTo` exposes to the tests; the three nested context classes in `AiAssistantService` are now sealed.
  - The coding standards' rule table marks the three rules as enforced and describes the suppression convention.
  - No behaviour, route or contract change.

## [4.2.8] - 2026-10-23

### Changed

- **Feature Layout Contract**: the architecture tests now allow no exceptions, and the project documents describe only the Feature layout (ADR 0007, #161).
  - The legacy allow-list and its `LegacyAllowList_NamesOnlyNamespacesStillInUse` test are deleted from `FeatureLayoutTests`; every ApiService and Web type must live in a `Features.<Feature>` or `Infrastructure` namespace.
  - `docs/CODING_STANDARDS.md`, `docs/ARCHITECTURE.md`, `GLOSSARY.md`, `CONTRIBUTING.md` and `README.md` describe `Features/<Feature>/` with its fixed anatomy and `Infrastructure/`, with the Feedback Feature as the exemplar; the type-based folders are mentioned only in ADR 0001's superseded section.
  - A pull request template (`.github/pull_request_template.md`) carries the Done checklist from AGENTS.md. `CODEOWNERS` stays deferred until a second owner exists.
  - No behaviour, route or contract change.

## [4.2.7] - 2026-10-23

### Changed

- **Web Features in Feature Folders**: every Web controller, view model and UI service now lives in `Web/Features/<Feature>/`, under the same Feature names as ApiService (ADR 0007, #160).
  - Razor views stay in `Views/<Controller>/`; views that name a model now `@using` its Feature namespace.
  - News moves into the Watchlist Feature. Three Web-only Features are added for pages that fit no existing Feature: LandingPage (`HomeController`), Legal (the Legal Pages and the registration Consent service) and InvestmentCalculator. GLOSSARY.md gains Legal Pages, Consent and Investment Calculator, and ADR 0007 lists the new Features.
  - `Web/Infrastructure/` holds what several Features use and what serves the host rather than a Feature: the user-id forwarding handler and API warmer, the Refit client registration, the Unavailable State, the Brevo email sender and templates, the Admin not-found handler and Notifications. ADR 0007 now states the host rule and why controllers keep their route names (`HomeController`, `NewsController`).
  - Web Features with services register them like ApiService does: `AddWatchlistFeature()` (the news feed) and `AddLegalFeature()` (Consent).
  - From the 4.2.6 review: the OpenAI registration class is renamed `OpenAiChatClientRegistration`, and `AdminOperationStatus` and `AdminOperationResult` get their own files.
  - The legacy allow-list in the architecture tests is now empty; #161 deletes it.
  - No behaviour, route or view change.

## [4.2.6] - 2026-10-23

### Changed

- **Remaining ApiService Features in Feature Folders**: SavingsGoals, Community, Watchlist, AiAssistant, Admin and AccountLifecycle now live in `ApiService/Features/<Feature>/`, so every ApiService type sits in a Feature or in `Infrastructure/` (ADR 0007, #159).
  - AccountLifecycle keeps the Deletion Request, the Data Export, the Purge and the daily purge job (`AccountPurgeJob`, still started with `--purge-only` per ADR 0005) together.
  - The OpenAI chat client registration moves to `Features/AiAssistant/Adapters/OpenAiChatClient.cs`; `IAdminService` and its result types move out of `AdminService.cs` into `IAdminService.cs`.
  - The shared `FeatureServiceExtensions` class is gone: `Program.cs` calls one `Add<Feature>Feature()` per Feature after `AddApiServiceInfrastructure()`, which now also registers the shared rate limiter and Notifications (used by the Budget Planner, Analysis and Savings Goals).
  - Service implementations, the cancel-deletion validator and the purge job are `internal`; the legacy allow-list in the architecture tests no longer names any ApiService namespace.
  - No behaviour, route or contract change.

## [4.2.5] - 2026-10-22

### Changed

- **Budget-Family Features in Feature Folders**: NetWorth, BudgetPlanner, BudgetTracker, Analysis, GoogleDrive, Dashboard and Search now live in `ApiService/Features/<Feature>/` with the Feedback anatomy (ADR 0007, #158).
  - Each Feature registers itself through its own `Add<Feature>Feature()`; Google Drive's adapter types sit in `Adapters/`; validators in `Validators/`; service implementations and validators are `internal sealed`.
  - What two or more Features share moves to `ApiService/Infrastructure/`: the Data Source provider cluster (ADR 0002), the net worth projection and mapping helpers, the monthly budget calculator and the current-record queries, registered by `AddApiServiceInfrastructure()` together with the clock.
  - `BudgetController` now takes `IValidator<SaveBudgetRequestDto>` like every other controller instead of the concrete validator.
  - No behaviour, route or contract change.

## [4.2.4] - 2026-10-22

### Changed

- **Feedback Is the First Feature Folder**: the Feedback Feature now lives in `ApiService/Features/Feedback/` and `Web/Features/Feedback/`, the pattern every Feature will follow (ADR 0007, #157).
  - Interface beside implementation, `FeedbackFeature.AddFeedbackFeature()` for its DI, a `Validators/` subfolder, and `internal sealed` for everything but the interface, the controller and the view models.
  - `FeedbackSourcePage` moves from Web Infrastructure into the Feature; the layout and the About page import it from there.
  - Web tests can use a Feature's internal types (`InternalsVisibleTo`), as ApiService tests already could.
  - The add-a-feature checklist in `docs/ARCHITECTURE.md` now says "mirror Feedback" and describes the Feature anatomy; AGENTS.md notes which Features have moved.
  - No behaviour change: sending Feedback, its submission limit and the Admin inbox work as before.

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
