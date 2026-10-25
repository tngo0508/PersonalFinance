# Localization and Multi-Currency (USD/VND) — Implementation Spec

**Status:** Ready for implementation
**Decided in:** Wayfinder map [#335](https://github.com/tngo0508/PersonalFinance/issues/335) (umbrella issue
[#295](https://github.com/tngo0508/PersonalFinance/issues/295)). Each section links the ticket that holds the full
reasoning; this document is the hand-off, not the debate.
**ADRs:** [0015 JSON localization with a custom string localizer](../adr/0015-json-localization-with-custom-string-localizer.md),
[0016 Per-amount currency with a reporting currency](../adr/0016-per-amount-currency-with-reporting-currency.md).

## 1. Goal

The app speaks English and Vietnamese, designed so a third language is a data change, and every amount carries its own
currency (USD and VND first) while totals are shown in the user's Reporting Currency. Translations are machine-drafted by
Azure AI Translator (free tier) and reviewed in pull requests. Nothing here is a general multi-currency calculator; that is
a later effort built on this model.

Out of scope (closed on the map, never graduates): currencies beyond USD and VND, right-to-left languages, translating
Legal Pages or third-party data (Research, News), hiring translators, a user override of exchange rates, per-language
URLs / `hreflang` on the Landing Page, translating or language-tagging Community user content, the multi-currency
calculator feature.

## 2. Decisions, by area

### 2.1 Translation mechanism ([#338](https://github.com/tngo0508/PersonalFinance/issues/338), [#340](https://github.com/tngo0508/PersonalFinance/issues/340))

- **Storage:** one flat JSON file per language, `PersonalFinance.Web/Resources/i18n/en.json` and `vi.json`. No `.resx`.
- **Keys:** flat dotted PascalCase, one per line: `"SavingsGoals.Card.Remaining"`. Shared keys under `Common.`; keys a
  JS module needs under `<Feature>.Js.`. The same key is used in Razor (`@L["…"]`) and JS (`PFI18n.t('…')`).
- **Placeholders:** named, `{amount}`, matched by name so a translation can reorder them (`L.Named`,
  `PFI18n.t(key, { amount })`). Positional `{0}` only where the framework formats the string (DataAnnotations, model
  binding). Markup inside a sentence goes through `L.NamedHtml`, which splits the template on its placeholders.
- **Plurals:** CLDR category suffixes, `Key.one` / `Key.other`. `Intl.PluralRules` in JS; a small per-language table in
  C#. Vietnamese has only `.other`.
- **Localizer:** an in-house `IStringLocalizer` + `IStringLocalizerFactory` in
  `PersonalFinance.Web/Infrastructure/Localization/` (~150 lines; the prototype on branch `prototype/json-localizer` is
  the reference, not code to merge). Loads every language once at startup into immutable dictionaries; singleton;
  registered **before** `AddViewLocalization()` / `AddDataAnnotationsLocalization()`, which otherwise `TryAdd` the resx
  factory. One shared key space: the factory ignores MVC's per-view base names.
- **Fallback chain:** `CurrentUICulture → Parent → … → en`. A key found only in `en` returns English with
  `ResourceNotFound = false` and logs event 3401 `LocalizationFallback` (Information). A key found nowhere returns the key
  with `ResourceNotFound = true` and logs 3402 `LocalizationNotFound` (Warning). Missing translations never break a page.
- **Framework message paths:** Razor via `IViewLocalizer`; DataAnnotations via `AddDataAnnotationsLocalization` (only
  attributes with an `ErrorMessage`; the seven bare `[Required]` in the repo need keys); model binding via the eleven
  `ModelBindingMessageProvider` setters in `IConfigureOptions<MvcOptions>`; jQuery unobtrusive validation needs nothing
  (it reads server-rendered `data-val-*`). `IdentityErrorDescriber` has no hook: subclass it with an injected localizer and
  register with `AddErrorDescriber<T>()`.
- **Identity pages:** about 22 Account and Manage pages are still served precompiled from the `Identity.UI` RCL and cannot
  be localized in place. Scaffold them into `Areas/Identity/Pages` first.
- **Encoding:** Razor HTML-encodes non-ASCII by default (`Còn` → `C&#xF2;n`). Configure `WebEncoderOptions` with
  `UnicodeRanges.All`. The JSON island uses `JavaScriptEncoder.Create(UnicodeRanges.All)`, which still escapes `<`, `>`, `&`.
- **JS strings:** inline per page. A `<js-strings prefix="SavingsGoals.Js." />` tag helper renders a
  `<script type="application/json" id="i18n-strings">` island with that page's keys, already resolved through the fallback
  chain. One core module, `wwwroot/js/core/i18n.js`, reads it and `<html lang>`. No fetched bundle.

### 2.2 Culture and formatting ([#339](https://github.com/tngo0508/PersonalFinance/issues/339))

- The request culture follows the UI language: `vi` → `vi-VN`, `en` → `en-US`, for separators, dates and typed input. No
  separate region or format picker; the settings page says in one line that Vietnamese also uses Vietnamese number and
  date formats.
- **The currency symbol never comes from the culture.** Every `"C"` / `"C0"` format (about 30 sites in Web and ApiService)
  is replaced by a shared money formatter that takes a currency code and renders symbol and minor units for that currency
  with separators from the current culture: `$1,234.56`, `1.234.567 ₫`. A test bans `"C"` formats, like the existing
  `[Required] bool` rule. The same formatter exists in `core/i18n.js`; the ~34 hardcoded `'en-US'` arguments and five
  locale-less `toLocaleString` calls are replaced by it, including D3 and Chart.js axes and tooltips. `navigator.language`
  is never used.
- `AppCulture.ApplyDefault()` keeps pinning `en-US` as the process default for background work (refreshers, jobs, logs).
  `RequestLocalization` sets `SupportedUICultures = [en, vi]`; `SupportedCultures` becomes `[en-US, vi-VN]` when slice L2
  lands, not before. Emails sent in response to a user action render in the user's language and culture.
- **Input parsing** follows the request culture: a Vietnamese user types `1.234,56`. Decimal model binding and the
  as-you-type JS formatting both honour it. This is the riskiest piece; see §5.
- ICU is present (no `InvariantGlobalization`), so `vi-VN` resolves in the chiseled images.

### 2.3 Language preference ([#341](https://github.com/tngo0508/PersonalFinance/issues/341))

- A per-user preference Entity in the `UserBudgetSourcePreference` pattern (`AuditableEntity`, soft delete): `Language`
  now, `ReportingCurrency` in slice C1. Not a column on `AspNetUsers`; no `ApplicationUser` subclass.
- The row is the source of truth. On sign-in and on every change, the standard `.AspNetCore.Culture` cookie is written so
  the built-in `CookieRequestCultureProvider` does the per-request work. Provider order: cookie → `Accept-Language` →
  `en`. The query-string provider is removed (the prototype used it for its demo only). No URL language prefix.
- Anonymous pages (Landing, sign-in, register, legal) follow `Accept-Language` when it names a supported language, else
  English; a visible switcher on those pages sets the cookie. No geo-IP.
- Switcher: the Language section on the scaffolded Account settings pages is the source of truth; a quick switcher also
  lives in the `_LoginPartial` dropdown, not the mobile bottom nav. Switching is a form POST with antiforgery, never a GET.
- Emails render in the recipient's stored language; registration seeds the preference row from the request culture.
- In-app Notifications are stored as key + arguments and rendered at read time. The AI Assistant receives the user's
  language in its system prompt.

### 2.4 Translation automation ([#336](https://github.com/tngo0508/PersonalFinance/issues/336), [#344](https://github.com/tngo0508/PersonalFinance/issues/344), [#345](https://github.com/tngo0508/PersonalFinance/issues/345))

- **Provider:** Azure AI Translator, Free F0 (2 M characters/month, hourly burst cap, 429 on bursts), region `westus2`,
  endpoint `https://api.cognitive.microsofttranslator.com/`. Credentials: `AZURE_TRANSLATOR_KEY`,
  `AZURE_TRANSLATOR_REGION`, `AZURE_TRANSLATOR_ENDPOINT` in the git-ignored `.env` locally and as a GitHub Actions secret
  plus two variables. The running containers never see the key. OpenAI is not used for translation.
- **Script:** `npm run i18n:translate`, a Node script in `PersonalFinance.Web/tools/` with a Node test (pattern:
  `check-publish.js` / `check-publish.test.js`). Run by the author or agent inside the feature PR and committed as its own
  commit, `i18n: translate N keys (vi)`. CI never writes translations.
- **Stale detection:** `Resources/i18n/i18n.lock.json` records, per language and key, a hash of the English text it was
  translated from. The script sends keys that are missing or whose English hash changed, nothing else. Hand corrections
  survive until the English changes; then machine output overwrites and the PR diff is the safety net. No review pins.
- **Protection:** `textType=html`; every `{name}` placeholder wrapped in `<span class="notranslate">`; terms from
  `Resources/i18n/glossary.vi.json` applied as dynamic-dictionary markup
  (`<mstrans:dictionary translation="…">net worth</mstrans:dictionary>`). After translating, the script verifies every
  English placeholder is present in the output; on mismatch it writes nothing for that key and lists it. Batches of at most
  1,000 texts / 50,000 characters; 429 back-off.
- **CI:** an `i18n-report` step in the existing `lint-js` job writes to the job summary. Reported, never failing: missing
  keys per language (plural-aware: skip categories the language lacks), stale keys, keys referenced in Razor/JS but absent
  from `en.json`. Failing: malformed JSON, and a translation whose placeholders do not match the English.
- The PR template reminds authors to run the script. Reviewing the Vietnamese is optional; approving never requires it.

### 2.5 Currency model ([#342](https://github.com/tngo0508/PersonalFinance/issues/342))

Money lives as **parameter documents** (`NetWorthParameters`, `UserBudgetAllocation`, `PriorityWalk`) and **line items**
(`BudgetTransaction`, `MonthlyBudgetLine`, `SavingsGoal`, `SpreadsheetBudgetSnapshot`, `SpreadsheetTransactionRecord`).
There is no ledger of assets.

- One `CurrencyCode` per money-bearing row: document-level on the three parameter documents, row-level on the five
  line-item entities. Never per field.
- `string`, fixed length 3, ISO 4217, non-null, migration default `'USD'`. Validated against a `SupportedCurrencies`
  constant in `PersonalFinance.Shared` (code, display name, minor-unit decimals, symbol). No enum, no lookup table.
- Columns stay `decimal(18,2)`; VND stores `.00` and renders with 0 decimals. Round to minor units on write with
  `MidpointRounding.AwayFromZero`; converted totals round once at the end of a sum.
- `ReportingCurrency` on the user preference Entity, default `USD`; changing it never rewrites stored amounts.
- Inputs default to the Reporting Currency with a per-row `USD | VND` selector; parameter documents show it once.
- `GoogleDriveConnection.CurrencyCode`, chosen at connect time (default Reporting Currency); imported rows inherit it.
- Not money: `BaseRoi`, `ConservativeRoi`, `AggressiveRoi`, `DebtApr`, `EmployerMatchPercent`, `EmergencyFundMonths`;
  `AiUsageRecord.EstimatedCostUsd` stays USD-only; Research figures stay in their source currency.
- `ExchangeRate` Entity, system data like `ResearchSnapshot` (not `AuditableEntity`): `BaseCurrency`, `QuoteCurrency`,
  `Date`, `Rate decimal(18,8)`, `Source`; unique on (base, quote, date). USD is the only base; the inverse is computed.

### 2.6 Exchange rates and conversion ([#337](https://github.com/tngo0508/PersonalFinance/issues/337), [#343](https://github.com/tngo0508/PersonalFinance/issues/343))

- **Provider:** Frankfurter v2 blended rate (`api.frankfurter.dev/v2/rates?base=USD&quotes=VND`; no key, no quota, free
  for commercial use, history to 1998) as primary; fawazahmed0 currency-api on jsDelivr (CC0) as fallback. ECB and Fed
  H.10 do not publish VND. Keyed free tiers were rejected on their terms. Findings: branch `research/exchange-rates`.
- `FxRatesRefresher` in the Research-refresher pattern (primary-then-fallback like `MacroRefresher`), daily, no secrets.
- Conversion happens only server-side in ApiService through one `ICurrencyConverter` in `Infrastructure/`. DTOs carry
  line items as `(Amount, CurrencyCode)` plus `ReportingAmount`, and totals already in the Reporting Currency. Web and JS
  never multiply by a rate; `MoneyFlowBuilder` and `FiBreakdownBuilder` sum `ReportingAmount`.
- Dated items (transactions, monthly lines) convert at the rate on their own date; undated balances (goals, parameter
  documents, dashboard "now" figures) use the latest rate. The net-worth projection converts its inputs once at the latest
  rate, then projects in the Reporting Currency.
- Missing rate: nearest prior; older than 3 days shows "rate as of {date}"; none at all shows "conversion unavailable" and
  items display in their original currency. Never silently zero.
- Same currency as the Reporting Currency: no lookup, no indicator. Mixed lists show every line's original amount and
  currency; the total carries a caption "converted at {rate} on {date}".
- `IMemoryCache` keyed by (quote, date), evicted by the refresher after it writes; cold reads batch one query per date range.

## 3. Slices

Every merged PR is a Release (ADR 0006), so each slice leaves the app working. Version bumps follow `AGENTS.md`:
schema migration ⇒ **major**, feature ⇒ minor; numbers below assume nothing else merges in between and will shift.
Translation slices finish before currency slices start, because C2 depends on the L2 formatter and on every page already
using keys.

| Slice | Content | Bump |
|---|---|---|
| **L1** | i18n plumbing: localizer, `en.json`/`vi.json`, `RequestLocalization` providers (cookie, `Accept-Language`), `WebEncoderOptions`, `core/i18n.js` + `<js-strings>` tag helper, `<html lang>`, `i18n:translate` script + `i18n.lock.json` + `glossary.vi.json` + CI `i18n-report`, Identity RCL scaffolding, `IdentityErrorDescriber` subclass, model-binding messages, **user preference Entity** (`Language`), language switcher (settings section + `_LoginPartial`), anonymous-page switcher, emails in the stored language, Notifications as key + args, Savings Goals translated as the proof page. Updates `DIAGRAMS.md` (entity, localizer) and the `ARCHITECTURE.md` add-a-feature checklist. | **major** (7.0.0) |
| **L2** | Money formatter (C# + JS) taking a currency code; `"C"` ban test; `SupportedCultures = [en-US, vi-VN]`; culture-driven input parsing with its test set; `SupportedCurrencies` constant (USD only used yet). | minor |
| **L3…Ln** | One PR per Feature area, each with its `i18n: translate` commit: Dashboard; Budget Planner; Budget Tracker; Net Worth; Money Priorities; Analysis + Google Drive; Research + News shell; Community shell; Identity pages + emails; Landing Page + Legal notice ("legal text is provided in English"). | minor each |
| **C1** | Currency schema: `CurrencyCode` on the eight entities (default USD), `ReportingCurrency` on the preference Entity, `GoogleDriveConnection.CurrencyCode`, `ExchangeRate` table, `FxRatesRefresher`. Updates `DIAGRAMS.md` (entity, job). Inspect the generated migration. | **major** (8.0.0) |
| **C2** | `ICurrencyConverter`, DTO `ReportingAmount`, totals and indicators, per-row selector, Reporting Currency setting, Playwright smoke for a VND line in a USD total. | minor |

## 4. Adding a language (future, data only)

1. Add `Resources/i18n/<lang>.json` (may start empty) and `glossary.<lang>.json`.
2. Add the language to the supported list used by `RequestLocalization` and the switcher, and its plural categories to
   the C# plural table.
3. Run `npm run i18n:translate`; commit; review the diff.
4. `vi-VN`-style culture mapping and the money formatter need no change. No code in the localizer changes.

## 5. Tests the slices must include

- xUnit: localizer fallback chain and `ResourceNotFound`; both log events; money formatter in `en-US` and `vi-VN` for
  USD and VND; the `"C"` format ban; decimal model binding in both cultures (zero, negative where allowed, thousands
  separators, rounding); converter rate-date selection, nearest-prior, 3-day indicator, "unavailable", same-currency
  short-circuit, round-once; `SupportedCurrencies` validation on every money endpoint.
- Node: the `i18n:translate` script (lock hashing, placeholder protection and verification, 429 back-off) and the report
  (plural-aware diff, unreferenced keys).
- Playwright: one smoke test that switches to Vietnamese, asserts the Savings Goals page and the switcher (L1); one that a
  VND line shows its original amount and the total carries the conversion caption (C2). Not a duplicated suite.
- Security (`financial-security` skill): the switcher POST has antiforgery; preference reads are scoped to the
  authenticated user; the translator key never reaches logs, containers or the client.

## 6. Hand-off

Implementation sessions start from this document and the two ADRs; the prototype branch and the research branches are
read-only references. Questions that this spec does not answer are new work: open an issue rather than deciding in a PR.
