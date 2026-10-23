# Free market-data sources whose terms allow showing data to users

Research note for [#215](https://github.com/tngo0508/PersonalFinance/issues/215) (part of map [#207](https://github.com/tngo0508/PersonalFinance/issues/207)).
Checked on **2026-10-23** against each provider's own terms page or API docs, linked next to every claim. Where a page would not load and only a search-engine extract of the provider's own page was available, the row says so. This is a reading of the terms, not legal advice.

The use case being tested: a multi-user web app whose ApiService fetches server-side, caches, and displays the data to signed-in users. Free tier only, end-of-day or delayed, information only, no scraping.

## TL;DR

- **Every commercial free tier fails the display test.** Alpha Vantage, Finnhub, Twelve Data, Financial Modeling Prep and Tiingo all limit the free key to personal / internal use and forbid showing the data to other people without a paid or written licence. Nasdaq Data Link forbids redistribution outside the order form. None of them can feed the Research page.
- **Public-sector sources pass.** SEC EDGAR (public information, no key), World Bank (CC BY 4.0), BEA (display explicitly allowed, attribution notice), OECD (reuse incl. commercial, citation), Natural Earth / US Census boundaries (public domain). FRED passes only for series FRED does not flag as third-party copyrighted, and needs a notice plus a link to its terms.
- **Biggest gap: no displayable free source of daily prices, so no true market cap.** SEC's `dei:EntityPublicFloat` (USD, reported annually in the 10-K, as of the end of Q2) is the best free stand-in for company size, and it comes with HQ state (`loc`) for free.
- **Boundaries:** `world-atlas@2.0.2` and `us-atlas@3.0.1` are ISC; neither is on cdnjs. They are on jsdelivr/unpkg (both libman providers). `topojson@3.0.2` (BSD-3-Clause) is on cdnjs.

## Candidate by candidate

### SEC EDGAR APIs (data.sec.gov) — usable

| Question | Finding | Source |
|---|---|---|
| Display / redistribution | "Information presented on sec.gov is considered public information and may be copied or further distributed by users of the web site without the SEC's permission." | https://www.sec.gov/privacy (Website Dissemination; last reviewed 2023-11-29) |
| API key | "These APIs do not require any authentication or API keys to access." | https://www.sec.gov/search-filings/edgar-application-programming-interfaces (last reviewed 2025-04-08) |
| Rate limit | "Current max request rate: 10 requests/second." Declare a User-Agent, e.g. "Sample Company Name AdminContact@<sample company domain>.com". "The SEC does not allow botnets or automated tools to crawl the site." | https://www.sec.gov/search-filings/edgar-search-assistance/accessing-edgar-data (last reviewed 2024-06-26); https://www.sec.gov/privacy |
| Bulk | `companyfacts.zip` and `submission.zip`, "republished nightly at approximately 3:00 a.m. ET". Use these for the cache instead of per-company calls. | EDGAR APIs page above |
| CORS | data.sec.gov does not support CORS, so it must be fetched server-side (which is the plan anyway). | EDGAR APIs page above |
| Attribution | None required. Credit "Source: SEC EDGAR" as good practice. | — |

Coverage (verified by calling the endpoints on 2026-10-23 with a declared User-Agent):

- **Classification:** `submissions/CIK##########.json` carries `sic`, `sicDescription`, `ownerOrg`, `tickers`, `exchanges` (Apple: `3571`, "Electronic Computers", "06 Technology", `AAPL`, Nasdaq). The SIC list with titles is published at https://www.sec.gov/search-filings/standard-industrial-classification-sic-code-list. SIC gives division -> major group -> industry -> company, which is a sunburst hierarchy. (GICS is the industry-standard sector scheme, but it is proprietary and not in any of these free sources.)
- **Company size:** `companyfacts` exposes `dei:EntityCommonStockSharesOutstanding` (Apple: quarterly since 2009) and `dei:EntityPublicFloat`. The frames API returns one value per filer per period: `frames/dei/EntityPublicFloat/USD/CY2024Q2I.json` returned 4,357 companies, `CY2010Q2I` returned 1,873. Each row has a `loc` field (e.g. `US-IL`), the filer's location.
- **Ticker map:** https://www.sec.gov/files/company_tickers.json (CIK, ticker, title).
- **ETF holdings:** ETFs file Form N-PORT (SPY's submissions list 28 `NPORT-P` filings among recent forms). The SEC publishes quarterly N-PORT data-set ZIPs of fund holdings, "October 2019 - September 2026" (https://www.sec.gov/data-research/sec-markets-data/form-n-port-data-sets, last reviewed 2026-09-30). Form N-PORT General Instruction F: "Information reported on Form N-PORT for the third month of each Fund's fiscal quarter will be made publicly available upon filing." (https://www.sec.gov/files/formn-port.pdf). So holdings are quarterly snapshots, not daily.
- **Prices:** none. EDGAR has no market prices.

Stale / ambiguous: the SEC adopted N-PORT amendments in Aug 2024 (monthly filings, public after 60 days) with compliance pushed to 2027; the only sources found for those dates were fund filings on EDGAR, not an SEC release, so verify before relying on the cadence. `EntityPublicFloat` is the market value of shares held by non-affiliates, not total market cap, and is reported once a year as of the last business day of Q2: label it honestly in the UI.

### FRED (St. Louis Fed) — usable with care

| Question | Finding | Source |
|---|---|---|
| Display | No commercial-use ban. Apps used by others must "display to the users of your application the link to these Terms of Use", and the app's own terms must say users are bound by the FRED API terms. | https://fred.stlouisfed.org/docs/api/terms_of_use.html (no date on page; may change "from time to time without notice") |
| Third-party series | "Data series available through the FRED® API, may be owned by third parties and subject to copyright restrictions." For anything beyond personal use "you must contact the data owner to obtain permission"; the Fed says it cannot grant it. | same |
| Prohibited | "Use the FRED® API for any application that replicates or attempts to replace the essential user experience" of FRED/ALFRED. | same |
| Attribution | "Place the following notice prominently on your application:" "This product uses the FRED® API but is not endorsed or certified by the Federal Reserve Bank of St. Louis." | same |
| API key | Required ("you must have register for an API Key"). | same |
| Rate limit | Terms give no number, only "Use an unreasonable amount of bandwidth" as prohibited. 120 requests/minute is widely quoted but I found it only on third-party pages, so treat it as unverified. | same |

Coverage: US macro and per-state series (many of which originate at BEA/BLS, which are public-sector). Stock-index series (e.g. S&P 500) are third-party copyrighted and fall under the permission clause: do not use them. For per-state data prefer BEA directly, which removes the third-party question.

### BEA (Bureau of Economic Analysis) — usable, per-US-state series

| Question | Finding | Source |
|---|---|---|
| Display | "You may use the BEA API to develop a service or service to search, display, analyze, retrieve, view and otherwise "get" information from BEA data." | https://apps.bea.gov/API/_pdf/bea_api_tos.pdf |
| Attribution | Display "prominently within the application: "This product uses the Bureau of Economic Analysis (BEA) Data API but is not endorsed or certified by BEA."" Do not modify content and still claim BEA as source. | same |
| API key | Registration required (https://apps.bea.gov/api/signup/). | signup page |
| Rate limit | 100 requests/minute, 100 MB/minute, 30 errors/minute; HTTP 429 with `RETRY-AFTER`. Read from a search extract of the user guide dated 2026-04-20, not opened directly. | https://apps.bea.gov/api/_pdf/bea_web_service_api_user_guide.pdf |

Coverage: Regional dataset (GDP and personal income by state, annual). History depth not verified in this pass (needs a key); confirm when building.

### World Bank (WDI API) — usable, per-country series

| Question | Finding | Source |
|---|---|---|
| Licence | CC BY 4.0 "is the default license for all Datasets produced by the World Bank itself"; it "allows users to copy, modify and distribute data in any format for any purpose, including commercial use." | https://datacatalog.worldbank.org/public-licenses (no page date) |
| Attribution | "Users are only obligated to give appropriate credit (attribution) and indicate if they have made any changes". Some third-party datasets carry other licences, so check per series. | same |
| Per-series licence | Checked via the API metadata: `CM.MKT.LCAP.CD` (Market capitalization of listed domestic companies, current US$; source World Federation of Exchanges) and `NY.GDP.MKTP.CD` (GDP) are both `License_Type: CC BY-4.0`. | `https://api.worldbank.org/v2/sources/2/series/CM.MKT.LCAP.CD/metadata?format=json` |
| Key / limits | No key needed; no published rate limit found. | — |
| History | US+JP market cap: values 1975-2025, WDI `lastupdated` 2026-10-08. | `https://api.worldbank.org/v2/country/US;JP/indicator/CM.MKT.LCAP.CD?format=json&date=1975:2025` |

This is the best fit for the per-country choropleth with a year scrubber.

### OECD — usable, secondary

OECD terms (as returned by search; direct fetch of the page was refused with HTTP 403): data may be extracted, downloaded, adapted, shared and embedded "for any purpose, including commercial use", with credit using the dataset's citation or "OECD (year), dataset name, data source, DOI or URL"; some data carry extra restrictions or third-party interests. https://www.oecd.org/en/about/terms-conditions.html. Covers OECD members only, so it adds little over the World Bank for a world map.

### Alpha Vantage free tier — not usable

- Licence is "for personal, non-commercial use", and use is "commercial use" if "You plan to use or provide information accessed through the Alpha Vantage Platform as part of any type of commercial activity that allows individuals or entities other than User to access information directly or indirectly", or if used "on behalf of a corporation ... and not as an individual". Terms of Service, section 2.a (served as a PDF from https://www.alphavantage.co/terms_of_service/, no date in the document).
- Free key: "25 API requests per day" (https://www.alphavantage.co/support/). Realtime and 15-minute delayed US data are premium-only (same page).

### Finnhub free tier — not usable

- "All plan listed on Finnhub website is strictly for personal use unless explicitly stated otherwise." "Personal plan can't be used by any business even internally without a written approval." Users "agree to not redistribute or share access to data or derived results". "All data must be deleted should your subscription to that data ends." https://finnhub.io/terms-of-service (no date on page).
- Free: 60 API calls/minute (pricing page, read via search extract; the page is JavaScript-rendered). https://finnhub.io/pricing

### Twelve Data Basic (free) — not usable

- Terms (last updated 2026-01-01): prohibits "Use Free Tier data for commercial purposes" (2.3(l)); you may "Redistribute or provide external display of Data only if and as expressly authorized" (2.2(e)); prohibits "Store or cache Data beyond permitted timeframes specified in the Documentation" (2.3(g)). https://twelvedata.com/terms
- Basic plan: 8 requests/minute, 800/day, "Internal non-display usage", "For testing and side projects". https://twelvedata.com/pricing

### Financial Modeling Prep Basic (free) — not usable

- Terms (last updated 2023-08-01, the oldest of the set): "2.2.2 Data Display: Without a specific agreement with FMP, customers are prohibited from showcasing FMP Services or Data on platforms including but not limited to websites, blogs, software products, or applications designed for utilization by multiple individuals, irrespective of whether such usage is complimentary or paid". Personal Use licence is "strictly for their own personal, non-business and non-commercial purposes". https://site.financialmodelingprep.com/terms-of-service
- Pricing: Basic $0, "250 Calls / Day", end-of-day data, 5 years history; "Displaying or redistributing data sourced from FMP requires a specific Data Display and Licensing Agreement with FMP." https://site.financialmodelingprep.com/pricing-plans

### Tiingo — not usable

- Terms 7.3: "All data via the API is for internal consumption only." "Redistribution is only available upon special request and permission, and comes with additional fees." Redistribution must carry "Data sourced by Tiingo" with a link. Last updated 2026-10-06. https://app.tiingo.com/tos/
- Tiingo's developer program exempts apps where each user enters their own Tiingo token (https://www.tiingo.com/documentation/appendix/developers, via search extract). That would make each of our users sign a Tiingo contract: not a fit.

### Nasdaq Data Link — not usable

The terms page did not render for the fetcher; search extracts of https://data.nasdaq.com/terms say the client shall not "redistribute, disseminate ... or otherwise permit or provide access to the Data in any manner not detailed in Section 1.1 or 1.2", and that updated terms "apply as of November 1, 2026". Stale/ambiguous: the terms change in nine days; re-read them if this source is ever reconsidered.

### Stooq — excluded

No documented API or terms of use found on https://stooq.com/. Its CSV download links are undocumented, so using them would be scraping, which is ruled out.

## Map boundaries (choropleth)

| Package | Version | Licence | Underlying data | cdnjs | jsdelivr/unpkg |
|---|---|---|---|---|---|
| `world-atlas` | 2.0.2 | ISC (repo LICENSE, "Copyright 2013-2019 Michael Bostock") | Natural Earth vector data v4.1.0 | **No** (`api.cdnjs.com/libraries/world-atlas` -> 404) | Yes: `countries-110m.json` 107,761 B, `countries-50m.json` 756,420 B |
| `us-atlas` | 3.0.1 | ISC | Census Bureau cartographic boundaries, **2017 edition** | **No** (404) | Yes (`states-10m.json`, `states-albers-10m.json`, ...) |
| `topojson` (bundle incl. client) | 3.0.2 | BSD-3-Clause | — | Yes: `topojson.min.js` | Yes |
| `topojson-client` | 3.1.0 | ISC | — | No | Yes: `dist/topojson-client.min.js` |

Sources: npm registry `latest` for each package; https://api.cdnjs.com/libraries/<name>; https://data.jsdelivr.com/v1/packages/npm/world-atlas@2.0.2?structure=flat; READMEs at https://github.com/topojson/world-atlas and https://github.com/topojson/us-atlas.

- Natural Earth: "All versions of Natural Earth raster + vector map data found on this website are in the public domain." "Crediting the authors is unnecessary." https://www.naturalearthdata.com/about/terms-of-use/
- Census boundaries are US-government works (public domain).
- libman: the repo's `libman.json` uses `defaultProvider: cdnjs`; the atlases need a per-library `"provider": "jsdelivr"` (or `unpkg`). Alternatively commit the JSON files under `wwwroot/` as static assets.
- Stale: world-atlas was last pushed 2021-08-11 (Natural Earth 4.1.0; Natural Earth has since released 5.x); us-atlas uses 2017 boundaries (last pushed 2024-01-02). Fine for a state/country choropleth, but borders are not current.

## Recommendation per visualisation

| Visualisation | Source | Notes |
|---|---|---|
| Sunburst: sector -> industry -> company, sized by value | SEC EDGAR `submissions` (SIC) + frames `dei/EntityPublicFloat/USD/CY{year}Q2I` | Size is public float, not market cap; label it so. Year scrubber = one frames call per year (back to ~2009 for good coverage). Hierarchy from SIC division / major group / code. |
| Collapsible tree: ETF -> holdings | SEC N-PORT quarterly data sets (or the fund's latest `NPORT-P` filing) | Quarterly snapshots since Oct 2019; holdings carry name, CUSIP/ISIN, value, % of net assets. |
| Choropleth, per country | World Bank WDI `CM.MKT.LCAP.CD` (and GDP etc.) + `world-atlas` | CC BY 4.0: credit "World Bank, World Development Indicators" (and WFE for market cap). 1975-2025. |
| Choropleth, per US state | BEA Regional (state GDP / personal income) + `us-atlas`; optionally SEC public float summed by `loc` state | Show BEA's required notice. FRED only if a series is not flagged third-party, plus FRED's notice and terms link. |

Attribution footer for the Research page: SEC EDGAR; World Bank WDI (CC BY 4.0) / WFE; BEA notice (exact text above); FRED notice and terms link if FRED is used.

## Gaps

1. **No free, displayable EOD price feed.** Every price API's free tier forbids display to other users. True market cap, price charts and returns need either a paid display licence (FMP "Data Display and Licensing Agreement", Twelve Data redistribution add-on, Tiingo redistribution) or dropping prices from the page.
2. **No GICS sectors.** SIC is older and coarser in places (e.g. many tech firms sit under business services `7370`-`7374`).
3. **ETF holdings are quarterly** and lag the quarter end; no daily holdings for free.
4. **Public float is annual** (Q2 snapshot) and excludes affiliate holdings; small/foreign filers are missing from frames.
5. **Unverified numbers:** FRED's rate limit, BEA history depth, and the N-PORT 2027 cadence change.
