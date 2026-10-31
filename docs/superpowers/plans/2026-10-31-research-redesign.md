# Research Redesign (Search First) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the six-tab Research page with fast, search-first pages (Home, Sectors, World, Funds, Basket, Fund, Company) that keep every current feature except the two approved simplifications.

**Architecture:** Each page is its own action on the Web `ResearchController`, reading only its datasets through a 10-minute in-memory snapshot cache. The existing D3 chart modules are reused as they are: a page puts their panels in the DOM (`research-panel-<id>`) and they register with `ResearchPage.registerTab` exactly as today, minus the tablist. Two small ApiService endpoints (dataset status, search index) feed Home and the search box. New pages ship unlinked; the last task turns `/Research` into Home and redirects the old `?tab=` links.

**Tech Stack:** .NET 10 MVC + Refit, EF Core (Azure SQL / InMemory), D3 7, vanilla JS IIFE modules, xUnit, `node --test`, Playwright (`e2e-tests/research-walkthrough.js`).

**Spec:** `docs/specs/research-redesign.md` (prototype: branch `prototype/research-redesign`).

## Global Constraints

- Layers: Web → ApiService → Data; Web controllers call `IResearchApi` only, no business logic (AGENTS.md).
- ADR 0010: no CUSIP/ISIN anywhere, including URLs; a holding links to a company **by name** (`/Research/Company?q=<name>`).
- ADR 0012: data only from the existing official sources; no new outside calls.
- User-facing text: English keys only in `PersonalFinance.Web/Resources/i18n/en.json`, prefix `Research.`; JS strings under `Research.Js.` (read through `<js-strings prefix="Research.Js." />` and `PFI18n.t`). Never edit `vi.json` or `i18n.lock.json` by hand.
- Every chart keeps its "Show table" twin, its Unavailable State (`_UnavailableState` partial), "About this data" and "Data as of".
- Keep: the Performance month brush, world map zoom and colour scale, scrubbers with play, Technology lens, reduced-motion behaviour.
- Remove: the sunburst and its Sunburst/Treemap toggle; the D3 holdings tree (`research-funds.js`).
- The Fund Basket lives only in the address (`basket=TICKER:weight,...`, up to five funds); nothing is stored server-side.
- JS: IIFE + `'use strict'`, JSDoc on public functions, files under `wwwroot/js/research/`; `[hidden]` never overridden by a CSS `display` rule.
- C#: explicit types unless the type is on the right-hand side (IDE0008); `Async` suffix; one class per file.
- Each PR (see "Release slices" below) bumps `AppVersion` (minor) and adds a `CHANGELOG.md` section `## [x.y.z] - YYYY-MM-DD`.
- Verify per task: `dotnet test PersonalFinance.sln`, `npm run lint`, `npm run test:i18n`; per PR also `bash .github/scripts/e2e-inmemory.sh`.

## Review Focus

1. **The basket gets lost when you move between pages** (nav link, search result, Back, reload): every Research link and search navigation must carry `basket=`. Test: Task 5 `withBasket` unit tests; Task 13 e2e step "basket survives Home → Fund → Basket → Back".
2. **A slow company suggestion lands after a newer one** and replaces the results for what you now typed. Test: Task 5 `latestOnly` unit test.
3. **A bad or unknown ticker in a URL** (`/Research/Fund/xyz`, `/Research/Company/<script>`, lower case `vti`): unknown fund → the not-found view (404), a rejected ticker → the same, lower case → works. Test: Tasks 11 and 12 controller tests.
4. **One dataset missing breaks a whole page**: only that section shows its Unavailable State. Test: one controller test per page with that page's first dataset returning 503.
5. **Old bookmarks** (`/Research?tab=companies&ticker=MSFT&basket=VTI:100`, `?tab=etfs`) land on the right page with the basket kept. Test: Task 13 redirect theory.

## Release slices

| PR | Tasks | Version | User-visible? |
|---|---|---|---|
| 1 | 1–4 | 17.1.0 | Faster page loads only |
| 2 | 5–7 | 17.2.0 | No (new pages unlinked) |
| 3 | 8–10 | 17.3.0 | No |
| 4 | 11–12 | 17.4.0 | No |
| 5 | 13 | 17.5.0 | Yes: the redesign goes live, old tab URLs redirect (minor: a feature, with no schema change and no broken links) |

## File structure

| File | Responsibility |
|---|---|
| `Web/Services/ResearchSnapshotCache.cs` (+ `IResearchSnapshotCache.cs`) | 10-minute in-memory cache of successful snapshot reads |
| `ApiService/Features/Research/ResearchController.cs` | + `GET status`, `GET search-index` |
| `ApiService/Features/Research/ResearchStatusQuery.cs` | Metadata-only query (no `Payload`) |
| `ApiService/Features/Research/ResearchSearchIndexBuilder.cs` | Projects funds/sectors/world snapshots into the search index |
| `Shared/DTOs/Research/ResearchStatusDto.cs`, `ResearchSearchIndexDto.cs` | New DTOs |
| `Web/Controllers/ResearchController.cs` | Actions: `Index` (Home), `Sectors`, `World`, `Funds`, `Basket`, `Fund`, `Company`, `SearchIndex` (JSON) + existing JSON actions |
| `Web/Models/Research/*PageViewModel.cs` | One view model per page; replaces `ResearchViewModel` |
| `Web/Views/Research/{Index,Sectors,World,Funds,Basket,Fund,Company,NotFound}.cshtml` | Pages |
| `Web/Views/Research/_ResearchNav.cshtml`, `_FundHoldingsList.cshtml` | Sub-navigation + compact search; holdings list |
| `Web/TagHelpers/ResearchTermTagHelper.cs` | `<research-term key="...">` glossary button |
| `wwwroot/js/research/research-search.js` | Search combobox (local index + company suggestions) |
| `wwwroot/js/research/research-glossary.js` | Glossary popover |
| `wwwroot/js/research/research-similar.js` | Fund page "Similar funds" ranking |
| `wwwroot/js/research/research-basket-math.js` | Basket return/volatility/Sharpe, shared by risk and the Basket overview |
| `wwwroot/js/research/research-sectors.js` | Renamed from `research-sunburst.js`, treemap only |
| `tools/research-*.test.js` | `node --test` tests for the pure JS above, added to `npm run test:i18n` |

---

### Task 1: Measure the baseline

**Files:** none committed; numbers go in the PR 1 description.

- [ ] **Step 1:** Start the apps on InMemory with the fixture (`bash .github/scripts/e2e-inmemory.sh` starts them; or `dotnet run` both with `Research:UseFixture` as that script sets). Sign in with a fresh account, then time `GET /Research` 10 times (`curl -s -o /dev/null -w '%{time_total}\n' -b cookies.txt <web>/Research`). Record the median and the HTML size (`-w '%{size_download}'`).
- [ ] **Step 2:** Record the number of script requests on `/Research` from the browser's network panel. These are the "before" numbers each later PR compares against.

### Task 2: Cache snapshot reads in Web

**Files:**
- Create: `PersonalFinance.Web/Services/IResearchSnapshotCache.cs`, `PersonalFinance.Web/Services/ResearchSnapshotCache.cs`
- Modify: `PersonalFinance.Web/Program.cs` (register as singleton), `PersonalFinance.Web/Controllers/ResearchController.cs` (`ReadAsync` goes through the cache)
- Test: `tests/PersonalFinance.Tests/Research/ResearchSnapshotCacheTests.cs`

**Interfaces:**
- Produces: `Task<T> IResearchSnapshotCache.GetOrReadAsync<T>(string dataset, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken) where T : class`; `public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10)`. A thrown exception (503, unreachable, cancellation) propagates and nothing is cached. Backed by `IMemoryCache`, key `research:{dataset}`.

- [ ] **Step 1: Write the failing tests**
  - `GetOrReadAsync_SecondCallWithinLifetime_DoesNotReadAgain` (read counter == 1, same instance returned)
  - `GetOrReadAsync_AfterLifetime_ReadsAgain` (use `FakeTimeProvider`-driven `MemoryCacheOptions.Clock`... or `MemoryCache` with `Clock = new FakeSystemClock`; advance 10 min + 1 s → counter == 2)
  - `GetOrReadAsync_WhenTheReadThrows_DoesNotCacheTheFailure` (first call throws `ApiException` 503, second succeeds → counter == 2)
  - `GetOrReadAsync_DifferentDatasets_AreCachedSeparately`
  - In `ResearchWebControllerTests`: `Index_ReadsEachDatasetOncePerLifetime_AcrossTwoRequests` (two `Index` calls on one cache → each `IResearchApi` getter called once).
- [ ] **Step 2:** `dotnet test PersonalFinance.sln --filter ResearchSnapshotCache` → FAIL (type missing).
- [ ] **Step 3:** Implement `ResearchSnapshotCache`; `ResearchController` takes `IResearchSnapshotCache` and its private `ReadAsync` calls `GetOrReadAsync(dataset, read, ct)` inside the existing try/catch (so failures still log and return null). Update `CreateController` in the existing Research web controller tests to pass a real cache over `new MemoryCache(new MemoryCacheOptions())`.
- [ ] **Step 4:** `dotnet test PersonalFinance.sln` → PASS.
- [ ] **Step 5: Commit** `research: cache snapshot reads in Web for 10 minutes`

### Task 3: Dataset status endpoint

**Files:**
- Create: `Shared/DTOs/Research/ResearchStatusDto.cs`, `ApiService/Features/Research/ResearchStatusQuery.cs`
- Modify: `ApiService/Features/Research/{IResearchService,ResearchService,ResearchController}.cs`, `Shared/Contracts/IResearchApi.cs`
- Test: `tests/PersonalFinance.Tests/Research/ResearchStatusTests.cs`

**Interfaces:**
- Produces: `ResearchStatusDto { List<ResearchDatasetStatusDto> Datasets }`, `ResearchDatasetStatusDto { string Dataset; DateOnly SourceAsOf; DateTime BuiltAt }`; `Task<ResearchStatusDto> IResearchService.GetStatusAsync(CancellationToken)`; `GET api/Research/status` (200 always, an empty list when nothing has loaded); Refit `Task<ResearchStatusDto> IResearchApi.GetStatusAsync(CancellationToken cancellationToken = default)`; `internal static IQueryable<ResearchDatasetStatusDto> ResearchStatusQuery.Build(AppDbContext db)` ordered by `Dataset`, projecting no `Payload`.

- [ ] **Step 1: Write the failing tests**
  - `GetStatusAsync_ListsEachStoredDataset_OrderedByName_WithItsSourceAsOfAndBuiltAt` (seed two `ResearchSnapshot` rows via `ResearchSnapshotStore.StoreAsync`)
  - `GetStatusAsync_WithNoSnapshots_ReturnsAnEmptyList`
  - `StatusQuery_DoesNotSelectThePayload` (`ResearchStatusQuery.Build(db).ToQueryString()` does not contain `Payload`; follow the pattern used for `ResearchSnapshotStore.PayloadQuery` in the existing tests, including the Azure SQL translation check from step 8 of the add-a-feature checklist in `docs/ARCHITECTURE.md`)
  - In `ResearchEndpointTests`: `Status_RequiresSignIn_AndReturns200`
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the query, service method, endpoint (same auth/rate-limit attributes as the other `ResearchController` GETs) and Refit method.
- [ ] **Step 4:** Run `dotnet test PersonalFinance.sln` → PASS.
- [ ] **Step 5: Commit** `research: add dataset status endpoint`

### Task 4: Search index endpoint + Web JSON action

**Files:**
- Create: `Shared/DTOs/Research/ResearchSearchIndexDto.cs`, `ApiService/Features/Research/ResearchSearchIndexBuilder.cs`
- Modify: `ApiService/Features/Research/{IResearchService,ResearchService,ResearchController}.cs`, `Shared/Contracts/IResearchApi.cs`, `Web/Controllers/ResearchController.cs`
- Test: `tests/PersonalFinance.Tests/Research/ResearchSearchIndexTests.cs`, `ResearchWebControllerTests.cs`

**Interfaces:**
- Produces: `ResearchSearchIndexDto { List<TrackedFundDto> Funds; List<string> Offices; List<ResearchSearchIndustryDto> Industries; List<string> Countries }`, `ResearchSearchIndustryDto { string Name; string Office }`; `static ResearchSearchIndexDto ResearchSearchIndexBuilder.Build(FundsSnapshotDto? funds, SectorsSnapshotDto? sectors, WorldSnapshotDto? world)`; `GET api/Research/search-index`; Refit `GetSearchIndexAsync`; Web `GET /Research/SearchIndex` → JSON (through `IResearchSnapshotCache`, dataset key `search-index`, `[JsonApiErrors]` like `FundIndustries`).

- [ ] **Step 1: Write the failing tests**
  - `Build_TakesFundsInSnapshotOrder_IndustriesWithTheirOfficeName_AndCountriesByName` (an industry with `Office = 1` gets `Offices[1]`)
  - `Build_WithAMissingSnapshot_LeavesOnlyThatPartEmpty` (sectors null → `Offices` and `Industries` empty, funds and countries filled)
  - `Build_DropsDuplicateIndustryNamesWithinAnOffice`
  - Web: `SearchIndex_ReturnsTheIndexAsJson` and `SearchIndex_WhenApiServiceFails_KeepsTheStatusWithASafeMessage` (mirror the existing `FundIndustries` tests)
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement; the service reads the three snapshots side by side and calls the builder.
- [ ] **Step 4:** `dotnet test PersonalFinance.sln` → PASS.
- [ ] **Step 5:** Bump `AppVersion` to 17.1.0, add the CHANGELOG section (Changed: **Faster Research**: …), run `bash .github/scripts/e2e-inmemory.sh`, commit `research: search index endpoint`, open PR 1 with Task 1's before/after numbers.

### Task 5: Research shell: navigation, search, basket links, glossary

**Files:**
- Create: `Web/Views/Research/_ResearchNav.cshtml`, `wwwroot/js/research/research-search.js`, `wwwroot/js/research/research-glossary.js`, `Web/TagHelpers/ResearchTermTagHelper.cs`, `tools/research-search.test.js`, `tools/research-basket.test.js`
- Modify: `wwwroot/js/research/research-basket.js`, `wwwroot/css/research.css`, `Resources/i18n/en.json`, `package.json` (`test:i18n` list)
- Test: the two tool tests, `tests/PersonalFinance.Tests/Research/ResearchTermTagHelperTests.cs`

**Interfaces:**
- Produces (JS):
  - `ResearchSearch.match(index, query) → Array<{ kind: 'fund'|'office'|'industry'|'country', label: string, href: string }>`: case-insensitive substring on ticker, name, office, industry and country; funds first, then offices, industries, countries; at most 8. Hrefs: `/Research/Fund/{ticker}`, `/Research/Sectors?focus={office}` or `{office}/{industry}`, `/Research/World?country={name}`, all URL-encoded.
  - `ResearchSearch.latestOnly(fn) → wrapped fn`: resolves only the most recent call's promise; earlier ones resolve to `null`.
  - The combobox in `[data-research-search]`: fetches `/Research/SearchIndex` once on first focus, calls `/Research/Suggest?q=` (debounced 200 ms, at least 2 characters) through `latestOnly`, renders groups Funds / Companies / Sectors / Industries / Countries as `role="listbox"` options, Arrow keys/Enter/Escape, `aria-activedescendant`; a fund ticker typed into it goes to the Fund page. Empty result copy `Research.Js.Search.NoMatch`. `/` focuses it unless an input has focus.
  - `ResearchBasket.withBasket(href, basket) → string`: sets or replaces `basket=` on a Research href, keeping its other query values and hash; a basket of zero funds removes the parameter. `ResearchBasket.linkify(root)`: rewrites every `a[data-research-link]` under `root` on load and whenever the basket changes. Buttons `[data-research-basket-toggle="TICKER"]` add the fund (weight `ResearchBasket` default) or remove it, with `aria-pressed` and a disabled state with title `Research.Js.Basket.Full` at five funds. The nav count `[data-research-basket-count]` follows the basket.
  - `ResearchGlossary`: a click on `.research-term` opens a popover (`role="tooltip"`, linked by `aria-describedby`), Escape or an outside click closes it.
- Produces (C#): `<research-term key="volatility">Volatility</research-term>` renders `<button type="button" class="research-term" data-term-text="{localized Research.Glossary.Volatility}">Volatility</button>`; an unknown key renders the content only (no button).
- Produces (Razor): `_ResearchNav` model `string ActivePage` (`home|sectors|world|funds|basket`); renders links with `data-research-link`, the basket count and, unless `ActivePage == "home"`, the compact `[data-research-search]`.
- i18n keys (exact copy from the spec's glossary list): `Research.Glossary.{Basket,Overlap,Correlation,Volatility,Annualized,Sharpe,Growth100,Real,PublicFloat,Office,NetAssets,Fee,Issuer,PricedIn}`; `Research.Nav.{Label,Home,Sectors,World,Funds,Basket}`; `Research.Search.{Label,Placeholder,PlaceholderShort}`; `Research.Js.Search.{NoMatch,Funds,Companies,Sectors,Industries,Countries,Loading,Failed}`; `Research.Js.Basket.{Full,Added,Removed}`. Take the wording from the prototype's `GLOSSARY` object and search box.

- [ ] **Step 1: Write the failing tests**
  - `research-search.test.js`: `match` finds `vti` → first result kind `fund`, href `/Research/Fund/VTI`; `bank` → industry `Banks` with href `/Research/Sectors?focus=Finance%2FBanks`; `japan` → country; empty query → `[]`; never more than 8. `latestOnly`: two calls, the first resolving last → first resolves `null`, second its value.
  - `research-basket.test.js`: `withBasket('/Research/Fund/VTI', [{ticker:'VTI',weight:60},{ticker:'BND',weight:40}])` → `/Research/Fund/VTI?basket=VTI%3A60%2CBND%3A40` (use whatever encoding `ResearchBasket.format` already produces); keeps `?view=risk` and `#growth`; replaces an existing `basket=`; an empty basket removes it.
  - `ResearchTermTagHelperTests`: known key → button with the localized text; unknown key → plain content.
- [ ] **Step 2:** `npm run test:i18n` and `dotnet test` → FAIL.
- [ ] **Step 3:** Implement. Load the JS modules the way `tools/investment-math.test.js` loads its module (a `vm` context with `window`); keep `match`, `latestOnly` and `withBasket` free of DOM access so they load there.
- [ ] **Step 4:** `npm run test:i18n`, `npm run lint`, `dotnet test PersonalFinance.sln` → PASS.
- [ ] **Step 5: Commit** `research: navigation, search, basket links and glossary`

### Task 6: Home page (unlinked, at `/Research/Home` until Task 13)

**Files:**
- Create: `Web/Views/Research/Home.cshtml`, `Web/Models/Research/ResearchHomeViewModel.cs`, `Web/Services/ResearchBasketPresets.cs`, `wwwroot/js/research/research-home.js`
- Modify: `Web/Controllers/ResearchController.cs` (action `Home`), `wwwroot/css/research.css`, `en.json`
- Test: `ResearchWebControllerTests.cs`

**Interfaces:**
- Consumes: `IResearchApi.GetStatusAsync` (Task 3), `_ResearchNav` (Task 5).
- Produces: `ResearchHomeViewModel { IReadOnlyList<ResearchDatasetFreshness> Datasets; IReadOnlyList<ResearchBasketPreset> Presets }`, `ResearchDatasetFreshness { string Dataset; string LabelKey; DateOnly? AsOf }` (one row per user-facing dataset, in this order: `sectors`, `etfs` (the funds dataset's stored key), `fund-returns`, `fund-facts`, `fund-overlap`, `fund-exposure`, `world`, `macro`; the internal ones the status also lists (`sec-filers`, `etf-filings`, `fx-rates`, …) are skipped; `AsOf` null when not loaded. Web cannot reference `ResearchDatasets`, so keep these keys in one `static readonly string[]` on the view model with a comment pointing at `ApiService/Features/Research/ResearchDatasets.cs`). `ResearchBasketPresets.All`: `Three-fund` (VTI 60, VXUS 30, BND 10), `S&P 500 only` (SPY 100), `Growth tilt` (QQQ 50, VTI 50), `Income` (SCHD 50, BND 30, VNQ 20); `research-home.js` hides a preset whose tickers are not all in the search index's funds.
- The page: hero with the big `[data-research-search]`, starter links (a tracked fund, `Microsoft`, `Semiconductors`, `Japan`, "Compare all funds"), the first-visit strip (dismissed state in `localStorage` key `pf.research.homeSteps`, every access in try/catch, shown when storage fails), the basket card (rendered client-side from the address: chips, "Open basket report", presets), four explore links (Sectors, World, All tracked funds, Risk vs return) with static copy, the freshness list, the not-investment-advice line. Home reads **no** snapshot payload.

- [ ] **Step 1: Write the failing tests**
  - `Home_ReadsOnlyTheDatasetStatus` (every other `IResearchApi` getter not called)
  - `Home_ListsEachDatasetWithItsAsOf_AndNotLoadedForAMissingOne`
  - `Home_WhenTheStatusCannotBeRead_StillRendersWithEveryDatasetNotLoaded`
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** `dotnet test`, `npm run lint` → PASS.
- [ ] **Step 5: Commit** `research: Home page`

### Task 7: Sectors page, treemap only

**Files:**
- Create: `Web/Views/Research/Sectors.cshtml`, `Web/Models/Research/ResearchSectorsPageViewModel.cs`
- Rename + modify: `wwwroot/js/research/research-sunburst.js` → `research-sectors.js`
- Modify: `research-sectors-basket.js`, `research.css`, `en.json`, `Web/Controllers/ResearchController.cs`
- Test: `ResearchWebControllerTests.cs`, `e2e-tests/research-walkthrough.js` (steps 2–6e → "Sectors 1…")

**Interfaces:**
- Produces: action `Sectors(string? focus, string? view)` reading `sectors`, plus `funds` (for the basket view, as `showSectorsBasket` does today). View model holds the existing `ResearchTabViewModel<SectorsSnapshotDto, SectorsPeriodDto>`, the funds tab model and `Focus` (validated: at most two `/`-separated parts, each 1–80 chars, otherwise null) and `View` (`market|basket`, else `market`). The panel keeps the id `research-panel-sectors` so `registerTab('sectors', …)` and the scrubber work unchanged.
- `research-sectors.js` exposes `window.ResearchSectors = { focusPath(parts: string[]): boolean }` (zooms to an office or office/industry by name; false when not found, leaving the chart at the top) and reads `data-research-focus` on load.
- Removed: the sunburst drawing, `data-research-view` toggle, `Research.Sectors.{Sunburst,Treemap,ChartType}` keys (Task 13 removes unused keys in one pass; remove these here).

- [ ] **Step 1: Write the failing tests**
  - `Sectors_ReadsSectorsAndFundsOnly`
  - `Sectors_WhenTheSectorsDataHasNeverLoaded_ShowsItsUnavailableState`
  - `Sectors_KeepsAValidFocus_AndDropsAMalformedOne` (theory: `Technology` kept; `A/B/C`, 81 chars → null)
  - e2e: move steps 2–6e to open `/Research/Sectors`; delete the sunburst-only assertions (step 6b's slice fading becomes the treemap tile fading); add "Sectors 7: `?focus=Finance/Banks` opens zoomed with the breadcrumb All sectors › Finance › Banks".
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** `dotnet test`, `npm run lint`, `bash .github/scripts/e2e-inmemory.sh ui` → PASS.
- [ ] **Step 5:** Bump to 17.2.0 + CHANGELOG, commit `research: Sectors page, treemap only`, open PR 2.

### Task 8: World page

**Files:** Create `Views/Research/World.cshtml`, `Models/Research/ResearchWorldPageViewModel.cs`; modify `research-world.js` (`window.ResearchWorld.selectCountry(name): boolean`, read `data-research-country` on load), `ResearchController.cs`, e2e steps 8–8c.

**Interfaces:** action `World(string? country, string? view)` reading `world`, `funds`, `exposure`; `View` = `market|basket`; panels keep ids `research-panel-world` and the world-basket ids from `_WorldTab.cshtml`.

- [ ] **Step 1: Failing tests:** `World_ReadsWorldFundsAndExposureOnly`; `World_WhenTheWorldDataHasNeverLoaded_StillShowsTheBasketView`; e2e "World 4: `?country=Japan` selects Japan and its detail".
- [ ] **Step 2–4:** Run → FAIL; implement by moving `_WorldTab.cshtml` markup into the page; run → PASS.
- [ ] **Step 5: Commit** `research: World page`

### Task 9: Funds page with the holdings list

**Files:** Create `Views/Research/Funds.cshtml`, `Views/Research/_FundHoldingsList.cshtml`, `Models/Research/ResearchFundsPageViewModel.cs`, `Models/Research/FundCompareRow.cs`; delete `wwwroot/js/research/research-funds.js` and its CSS in `research-funds.css`; modify `ResearchController.cs`, e2e (holdings and risk steps).

**Interfaces:**
- Action `Funds(string? view, int? window, string? sort, string? quarter)` reading `funds`, `fund-returns`, `fund-facts`. `view` ∈ `table|risk|correlation|holdings` (else `table`); `window` ∈ {1,3,5} years (else 3); `sort` ∈ `ticker|role|return|volatility|sharpe|fee|assets` (else `return`, descending for numbers, ascending for text, ties by ticker); `quarter` must be one of the funds snapshot's periods (else the latest).
- `FundCompareRow { string Ticker; string Name; string? Role; decimal? AnnualReturn; decimal? Volatility; decimal? Sharpe; decimal? ExpenseRatio; decimal NetAssets }` built from the snapshots' precomputed per-window figures (no maths in the controller beyond picking the window); a fund with no facts shows "not filed" for the fee (SPY), as the Risk tab says today.
- `_FundHoldingsList` model: the funds period for `quarter`; renders `<details>` per fund → per asset category (name, %) → its `Top` holdings and "Other (n)", each holding name linking to `/Research/Company?q={name}` with `data-research-link`.
- The risk and correlation views reuse `research-risk.js` and `research-correlation.js` panels (ids from `_RiskTab.cshtml` and the correlation card), correlation opening on "All tracked funds".

- [ ] **Step 1: Failing tests:** `Funds_SortsByTheChosenColumn_TiesByTicker` (theory over the sort keys); `Funds_IgnoresAnUnknownViewWindowSortOrQuarter`; `Funds_WhenTheReturnsHaveNeverLoaded_ShowsTheTableWithoutReturns_AndTheRiskViewUnavailable`; `FundHoldingsList_LinksHoldingsToTheCompanySearchByName_NeverByIdentifier` (render the partial and assert no 9-character CUSIP-shaped or ISIN-shaped token, reusing the ADR 0010 regex from the existing identifier test); e2e "Funds 1–4" (table sort, risk roles filter, correlation all funds, holdings open a fund and a category).
- [ ] **Step 2–4:** Run → FAIL; implement; run → PASS (`npm run lint` must show no reference to the deleted module).
- [ ] **Step 5: Commit** `research: Funds page with a holdings list`

### Task 10: Basket page

**Files:** Create `Views/Research/Basket.cshtml`, `Models/Research/ResearchBasketPageViewModel.cs`, `wwwroot/js/research/research-basket-math.js`, `wwwroot/js/research/research-basket-overview.js`, `tools/research-basket-math.test.js`; modify `research-risk.js` (use `ResearchBasketMath`), `research-basket.js` (the bar renders inside the page's rail; add "Copy link" and presets), `ResearchController.cs`, `package.json`, e2e steps 7–7j.

**Interfaces:**
- Action `Basket()` reading `funds`, `fund-overlap`, `fund-returns`, `macro`, `fund-facts`, `fund-exposure`, `sectors`. Sections in order with ids: `basket-overview`, `basket-growth` (Performance panel), `basket-risk`, `basket-overlap`, `basket-correlation`, `basket-sectors` (the lean section from `research-sectors-basket.js`), `basket-countries` (world-basket panel); a sticky section list links to them.
- `ResearchBasketMath.summary(returnsSnapshot, factsSnapshot, exposureSnapshot, basket, years) → { annualReturn, volatility, sharpe, expenseRatio, usShare, missingFees: string[] }`; the basket's monthly return is the weight-scaled sum of its funds' monthly returns, rebalanced monthly, exactly as `research-risk.js` computes its basket point today (move that code, do not re-derive it); `expenseRatio` averages the funds that have one, re-weighted, and lists the others in `missingFees`.
- Empty basket: one prompt (`Research.Basket.EmptyPrompt`) with presets; the sections are not drawn.

- [ ] **Step 1: Failing tests:** `research-basket-math.test.js`: one fund at 100% → its own precomputed figures; 50/50 of two funds over a fixture of 3 months → hand-computed values to 1e-9; a fund without a fee → excluded and listed; weights not summing to 100 → scaled. Controller: `Basket_ReadsItsSevenDatasets`; `Basket_WhenTheOverlapHasNeverLoaded_ShowsOnlyThatSectionUnavailable`. e2e: steps 7–7j open `/Research/Basket?basket=…`; add "Basket overview figures match the risk chart's basket point".
- [ ] **Step 2–4:** Run → FAIL; implement; run → PASS, including `bash .github/scripts/e2e-inmemory.sh`.
- [ ] **Step 5:** Bump 17.3.0 + CHANGELOG, commit `research: Basket page`, open PR 3.

### Task 11: Fund page

**Files:** Create `Views/Research/Fund.cshtml`, `Views/Research/NotFound.cshtml`, `Models/Research/ResearchFundPageViewModel.cs`, `wwwroot/js/research/research-similar.js`, `tools/research-similar.test.js`; modify `research-performance.js` (option: a fixed fund set plus a "Compare with" select of basket or any tracked fund), `research-risk.js` (option `highlight: ticker`), `ResearchController.cs`, `package.json`.

**Interfaces:**
- Action `Fund(string ticker)` (route `Research/Fund/{ticker}`): upper-cases; a ticker that fails `CompanyLookupRules.TryNormalizeTicker` or is not in the funds snapshot → `NotFound` view with status 404 and a search box. Reads `funds`, `fund-returns`, `macro`, `fund-facts`, `fund-overlap`, `fund-exposure`.
- Header: name, role, net assets, fee (or "not filed"), 1/3/5-year annualized return, the basket toggle (Task 5), "Compare all funds".
- Sections: growth (Performance panel, this fund only + "Compare with"), risk (all funds, this one highlighted, basket diamond when a basket is set), similar funds, what it holds (top 10 holdings across categories by %, linked by name as in Task 9), where the money goes (by issuer country from exposure; by SEC office from `/Research/FundIndustries`, fetched when that toggle is first chosen).
- `ResearchSimilar.byOverlap(overlapSnapshot, ticker) → Array<{ ticker, percent }>` and `ResearchSimilar.byCorrelation(returnsSnapshot, ticker, years) → Array<{ ticker, value }>`, both descending, excluding the fund itself and funds with no value; use the snapshots' existing pair figures (no recomputation).
- Company search: typing a tracked fund's ticker into the search (or `/Research/Company/VTI`) redirects to `/Research/Fund/VTI` (replaces today's "Company 6" link to the Funds tab).

- [ ] **Step 1: Failing tests:** `research-similar.test.js` (order, exclusion, missing pair); controller theory `Fund_ForAnUnknownOrRejectedTicker_Returns404NotFoundView` (`xyz`, `<script>`, empty) and `Fund_IsCaseInsensitive` (`vti`); `Fund_WhenTheReturnsHaveNeverLoaded_KeepsTheHoldingsSections`; `Company_ForATrackedFundTicker_RedirectsToTheFundPage`; e2e "Fund 1–4" (open from search, compare with basket, add to basket updates the nav count, a holding name opens the company search).
- [ ] **Step 2–4:** Run → FAIL; implement; run → PASS.
- [ ] **Step 5: Commit** `research: Fund page`

### Task 12: Company page

**Files:** Create `Views/Research/Company.cshtml`; modify `research-companies.js` (read `data-research-ticker` / `data-research-query` instead of the tab's address handling; drop `selectTabById` and `research:tabchange` use), `ResearchController.cs`, e2e Company 1–8.

**Interfaces:** action `Company(string? ticker, string? q)` (routes `Research/Company/{ticker?}`): no snapshot reads; a rejected ticker → `NotFound` (404); `q` (normalized with `CompanyLookupRules.TryNormalizeQuery`, else ignored) pre-fills the search and opens its suggestions. The JSON actions `Company`, `Releases`, `Suggest` stay as they are, renamed only if routing forces it (then keep the old names as aliases for one release).

- [ ] **Step 1: Failing tests:** `Company_ReadsNoSnapshot`; `Company_WithARejectedTicker_Returns404`; `Company_WithAQuery_PassesTheNormalizedQueryToTheView`; e2e Company 1–8 against `/Research/Company`, "Company 1" now opening on the page's empty search.
- [ ] **Step 2–4:** Run → FAIL; implement; run → PASS.
- [ ] **Step 5:** Bump 17.4.0 + CHANGELOG, commit `research: Company page`, open PR 4.

### Task 13: Go live: Home at `/Research`, redirects, clean-up, docs

**Files:** Modify `ResearchController.cs` (`Index` becomes Home; `Home` action removed), `Views/Research/Index.cshtml` (replaced by Home's markup); delete `_SectorsTab`-era partials (`_FundsTab`, `_PerformanceTab`, `_RiskTab`, `_WorldTab`, `_CompaniesTab`), `Models/Research/ResearchViewModel.cs`, the tablist code in `research-page.js` (`initTablist`, `selectTab`, `selectTabById`, the `research:tabchange` event and its listeners in `research-basket.js`, `research-companies.js`, `research-risk.js`, `research-performance.js`, `research-world-basket.js`), unused `Research.*` keys in `en.json`; update `docs/DIAGRAMS.md` (Research feature row, section 10 read path with the cache and the two new endpoints), `docs/ARCHITECTURE.md` if it names the tabs, `GLOSSARY.md` (Fund Basket: "carried by every Research link"); e2e walkthrough final order.

**Interfaces:** `Index(string? tab, string? ticker)`: with `tab` set, redirect (302, keeping `basket` from the query) — `sectors`→`Sectors`, `funds` and `etfs`→`Basket`, `performance`→`Basket` + `#basket-growth`, `risk`→`Funds?view=risk`, `world`→`World`, `companies`→`Company/{ticker}` (or `Company` without one); unknown `tab` → Home. Without `tab`, Home.

- [ ] **Step 1: Failing tests:** theory `Index_RedirectsEachOldTab_KeepingTheBasket` (all rows above, with `basket=VTI:100`); `Index_WithAnUnknownTab_ShowsHome`; e2e "Shell 1: Home → search 'VTI' → Fund → Add to basket → nav Basket keeps it → Back keeps it → reload keeps it"; "Shell 2: at 390 px no page scrolls sideways on any Research page"; "Shell 3: no Research page loads `d3.min.js` except those with a D3 chart" (Home and Company before a lookup load none).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the redirects, delete the old code, remove unused keys (`npm run test:i18n` reports orphans), update the docs.
- [ ] **Step 4:** `dotnet test PersonalFinance.sln`, `npm run lint`, `npm run test:i18n`, `bash .github/scripts/e2e-inmemory.sh` → PASS. Repeat Task 1's measurements on Home and on the Basket page (cold and warm cache) and put before/after in the PR.
- [ ] **Step 5:** Bump `AppVersion` to 17.5.0 + CHANGELOG (Changed: **Research redesign**: …; Removed: the sunburst chart and the holdings tree), commit `research: search-first pages go live`, open PR 5.
