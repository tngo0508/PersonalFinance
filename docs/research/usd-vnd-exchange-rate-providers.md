# Research: USD↔VND exchange-rate providers

**Ticket:** [#337](https://github.com/tngo0508/PersonalFinance/issues/337) (part of #335)  
**Date:** 2026-10-25  
**Question:** Which free or cheap providers publish daily USD↔VND rates whose terms fit the licence policy
([ADR 0009](../adr/0009-adopt-d3-for-bespoke-interactive-visualisations.md): permissive, no paid or dual-licensed
dependencies) and the Research data policy ([ADR 0012](../adr/0012-research-data-from-official-public-sources.md):
official public sources, stored in a daily snapshot, FRED excluded because its terms forbid caching)?

Every claim below was checked against the provider's own documentation, terms or live API on 2026-10-25. Live
responses are quoted where the docs were silent (for example on VND coverage).

## Recommendation

| Role | Provider | Why |
|---|---|---|
| **Primary** | **Frankfurter v2, blended rate** (`https://api.frankfurter.dev/v2/rates?base=USD&quotes=VND`) | No key, no quota, VND verified (daily history back to 1998-07-07), "free for commercial use" and storing rates locally is encouraged, MIT server that can be self-hosted, every rate is a central-bank/official rate. |
| **Fallback** | **fawazahmed0 currency-api on jsDelivr** (`https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/usd.min.json`) | No key, no rate limits, CC0 1.0 data, VND verified, dated URLs give history, second mirror on Cloudflare Pages. Sources are not disclosed, so it is a fallback, not a primary. |
| Official cross-check (optional) | U.S. Treasury Reporting Rates of Exchange (Fiscal Data API) | The only US-government source that carries VND; quarterly, so it cannot be the daily feed, but it is public, unrestricted and is a provider inside Frankfurter too (`UST`). |

Why not the others, in one line each: the ECB and the Fed's H.10 do not publish VND at all; exchangerate.host's free
tier is non-commercial-only, 100 requests/month and keyed; Open Exchange Rates' free tier is keyed, 1,000
requests/month, and its site terms say "domestic and private use" only; ExchangeRate-API's open endpoint works
(VND, daily, no key) but requires a visible attribution link and gives no history on the free tier; the State Bank of
Vietnam has no API or terms; Vietcombank's feed is an unofficial bank rate with "one request every 5 minutes" and no
licence.

Fit with the existing refresher pattern: a `FxRatesRefresher : IResearchDatasetRefresher` reads the primary, and on
`HttpRequestException`/parse failure reads the fallback (the same shape as `MacroRefresher`, which falls back from the
Fed's H.15 to the Treasury). Both sources are plain GET JSON, cacheable (Frankfurter answers with
`cache-control: public, max-age=15805, stale-if-error=86400`), and neither needs a secret, so nothing new goes into
user secrets or Key Vault.

## Comparison

| Provider | VND | Auth | Free limit | Cadence | History (free) | Terms for this app | Cost |
|---|---|---|---|---|---|---|---|
| ECB reference rates | **No** (29 currencies, VND absent) | none | n/a | working days ~16:00 CET | n/a | reuse free with ECB cited | free |
| Frankfurter v2 (blended) | **Yes** (verified; since 1998-07-07) | none | no quotas; abuse rate-limiting only | daily | yes, `date=` and `from=`/`to=` | free incl. commercial; storing encouraged | free; MIT self-host |
| exchangerate.host (apilayer) | 168 currencies claimed; VND not confirmed on the public pages | API key | 100 req/month | daily (free) | yes (free) | **non-commercial only** on free; no redistribution | $14.99/mo for commercial |
| Open Exchange Rates | Yes (174 currencies) | `app_id` | 1,000 req/month, base USD only | hourly | yes per official plans guide (no time-series) | free tier aimed at "personal, small-scale and open source"; site terms "domestic and private use" | $12/mo Developer |
| ExchangeRate-API open endpoint | Yes (165 currencies; live rate verified) | none | IP rate-limited; 429 for 20 min | daily | **no** (historical is Pro+) | **attribution link required**; commercial OK; no redistribution | free; $10/mo Pro |
| ExchangeRate-API Free (keyed) | Yes | API key | 1,500 req/month | daily | no | no attribution; commercial OK | free |
| fawazahmed0 currency-api | **Yes** (verified; 340 codes) | none | "No Rate limits" (jsDelivr CDN) | daily | yes via `@YYYY-MM-DD` (2024-03-06 works, 2024-01-01 does not) | CC0 1.0; sources undisclosed | free |
| Fed H.10 | **No** (23 currencies, VND absent) | none | n/a | weekly release of daily rates | n/a | public domain | free |
| U.S. Treasury Reporting Rates of Exchange | **Yes** (since 2001-03-31) | none | none stated | **quarterly** | yes | "free, without restriction", commercial or not | free |
| State Bank of Vietnam | Yes (central rate) | n/a | n/a | daily central rate, weekly bulletins | no machine-readable archive found | no terms published; no API | free |
| Vietcombank XML/JSON | Yes (buy/transfer/sell) | none | "Only one request every 5 minutes!" | intraday | JSON takes `date=` | "For reference only"; no licence | free |
| World Bank `PA.NUS.FCRF` | Yes | none | none | **annual** | yes | existing source (ADR 0012) | free |

## Provider notes

### ECB euro reference rates and Frankfurter

- The ECB publishes euro reference rates for 29 currencies, "usually updated at around 16:00 CET on every working
  day"; VND is not among them, so the ECB alone cannot answer USD↔VND.
  Source: <https://www.ecb.europa.eu/stats/policy_and_exchange_rates/euro_reference_exchange_rates/html/index.en.html>.
  Frankfurter's own ECB provider record lists 45 codes (current and legacy) and no VND:
  <https://api.frankfurter.dev/v2/providers/ECB>.
- ECB reuse terms: free use of information obtained from the site, with the ECB cited as the source and any
  modification stated. Source: <https://www.ecb.europa.eu/home/disclaimer/html/index.en.html>.
- Frankfurter v2 is no longer ECB-only. It blends "105 central banks and official sources", 223 currencies, history to
  1948, daily updates, no API key, "no quotas or monthly or daily caps" (abuse rate-limiting only), "free for
  commercial use", and "storing rates locally is encouraged". Blended rates are Frankfurter's own calculation; a query
  to a single provider's endpoint (`/v2/providers/{key}/rates`) is subject to that provider's terms. v1 is deprecated
  but stays available. Sources: <https://frankfurter.dev/> and <https://frankfurter.dev/docs>.
- Server licence: MIT, self-hostable with Docker (`lineofflight/frankfurter`). Source:
  <https://github.com/lineofflight/frankfurter>.
- Verified on 2026-10-25:
  - `GET https://api.frankfurter.dev/v2/currencies` lists `{"iso_code":"VND","name":"Vietnamese Đồng","start_date":"1998-07-07","end_date":"2026-10-25"}`.
  - `GET https://api.frankfurter.dev/v2/rates?base=USD&quotes=VND` → `[{"date":"2026-10-25","base":"USD","quote":"VND","rate":25818}]`.
  - `&date=2026-10-20` → 25843; `&from=2026-10-15&to=2026-10-25` returns one row per calendar day; `&date=1998-07-07`
    → 12984. `start_date`/`end_date` and `date=a..b` are rejected (422).
  - `&expand=providers` shows the blend's contributors for VND: BCRA, BDI (Banca d'Italia), BI (Bank Indonesia), BNA,
    BNM (Bank Negara Malaysia), BOM, CBKKW, and others; `GET /v2/providers` lists ~30 providers whose currency list
    includes VND, including `UST` (U.S. Treasury, quarterly) and `MAS`, `NBP`, `NBU`, `CBR` (daily).
  - Response header `cache-control: public, max-age=15805, stale-if-error=86400`.
- Caveat: the blended figure is a derived number (central-bank middle/reference rates, mostly dated the previous
  business day), not a traded market rate, which is the right thing for a reference conversion but should be labelled
  as such on the page ("reference rate, Frankfurter blend of central-bank rates").

### exchangerate.host (apilayer Data Products GmbH)

- Free plan: $0, 100 requests/month, daily updates, historical rates, HTTPS, **non-commercial use only**, no support.
  "Source currency switching" starts at Basic ($14.99/mo, 10,000 requests, hourly). Source:
  <https://exchangerate.host/pricing>.
- Terms: operated by apilayer Data Products GmbH (Vienna); end users may use the data "strictly for their own personal
  use"; no transfer of the data outside your application; storing after termination prohibited; attribution not
  required but a link "would be highly appreciated"; Austrian law. Source: <https://exchangerate.host/terms>.
- The homepage claims "168 forex currencies" and "19 years of historical data"; the documentation now redirects to
  `docs.apilayer.com`, which did not render server-side, so VND coverage was not confirmed from a primary page.
  Source: <https://exchangerate.host/>.
- Verdict: a keyed, 100-call, non-commercial tier with no redistribution clause. Workable for one call a day but the
  weakest terms of the free options; not recommended.

### Open Exchange Rates

- Plans: Free, up to 1,000 requests/month, "hourly updates (with base currency USD)"; Developer $12/mo (10,000,
  all base currencies); Enterprise $47/mo (time-series); Unlimited $97/mo. Source: <https://openexchangerates.org/signup>.
- The official plans guide says the Forever Free plan includes "hourly updates for all rates, historical data and up
  to 1,000 requests per month", excluding multiple base currencies, time-series and conversion. Source:
  <https://support.openexchangerates.org/article/69-plans-pricing-guide>.
- FAQ: free tier is for "personal, small-scale and open source use"; commercial apps are steered to paid plans;
  "You don't need to provide attribution or credit". Source: <https://openexchangerates.org/faq>.
- Site terms: "We only provide our site for domestic and private use" and users agree not to use the site "for any
  commercial or business purposes"; information rights are "non-transferable, non-exclusive, revocable, limited".
  Source: <https://openexchangerates.org/terms>.
- VND is listed (174 currencies plus 22 alternative). Source:
  <https://docs.openexchangerates.org/reference/supported-currencies>. `historical/YYYY-MM-DD.json` goes back to
  1999-01-01; `symbols` is Developer+. Sources: <https://docs.openexchangerates.org/reference/api-introduction>,
  <https://docs.openexchangerates.org/reference/historical-json>.
- Verdict: good data and history, but it needs an `app_id` secret and the free tier is scoped to private/small-scale
  use. Acceptable for this non-commercial app only if the key is treated as a secret; still no better than Frankfurter.

### ExchangeRate-API (exchangerate-api.com)

- Open-access endpoint `https://open.er-api.com/v6/latest/{CODE}`: no key, refreshed once a day, response carries
  `time_next_update_utc`; IPs that exceed the limit get HTTP 429 and "After 20 minutes the rate limit will finish";
  **"We require attribution on the pages you're using these rates with"** a link whose text is "Rates By Exchange Rate
  API". Keyed Free plan: 1,500 requests/month, daily, no attribution. Pro: $10/mo, 60-minute updates. Source:
  <https://www.exchangerate-api.com/docs/free>.
- Terms: Free and paid plans are both "suitable for either commercial or personal use"; "Users are given permission
  to store & re-use any data retrieved from our API" but "caching is for customer end-use only", no re-distribution,
  no use in products offering programmatic access to rate data, Free plans "can be discontinued ... without notice".
  Source: <https://www.exchangerate-api.com/terms>.
- VND is supported (165 currencies). Source: <https://www.exchangerate-api.com/docs/supported-currencies>.
- Historical endpoint "is only available to users on our Pro, Business or Volume plans"; full-list history starts
  2021-01-01. Source: <https://www.exchangerate-api.com/docs/historical-data-requests>.
- Verified on 2026-10-25: `GET https://open.er-api.com/v6/latest/USD` → `result: success`,
  `time_last_update_utc: Sun, 25 Oct 2026 00:02:31 +0000`, `rates.VND = 25817.343746`; header `cache-control: public,
  max-age=3600`.
- Verdict: a viable second fallback for the latest rate only. The attribution link would have to appear on every page
  that shows a converted figure, and there is no free history for a backfill.

### fawazahmed0 currency-api (GitHub / jsDelivr / Cloudflare Pages)

- README: "200+ Currencies", "Daily Updated", "No Rate limits"; endpoints
  `https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{date}/{apiVersion}/{endpoint}` with `{date}` = `latest` or
  `YYYY-MM-DD`, and fallback `https://{date}.currency-api.pages.dev/{apiVersion}/{endpoint}`; the author asks consumers
  to implement the fallback. No data sources, update time or accuracy disclaimer are stated. Source:
  <https://github.com/fawazahmed0/exchange-api>.
- Licence: CC0 1.0 Universal. Source: <https://github.com/fawazahmed0/exchange-api/blob/main/LICENSE>. CC0 is a
  public-domain dedication, more permissive than the four licences ADR 0009 names; since this is data rather than a
  package, no NuGet/npm entry is involved, but the ADR list could be noted as "data under CC0/public domain is fine" when
  the decision is recorded.
- Verified on 2026-10-25: `/v1/currencies.min.json` has 340 codes incl. `vnd: "Vietnamese Dong"`;
  `@latest/v1/currencies/usd.min.json` → `date: 2026-10-25`, `usd.vnd = 25831.81`; the Cloudflare mirror returns the
  same; `@2024-03-06` → 24803.93 (works), `@2024-01-01`, `@2023-01-01`, `@2022-01-01` → HTTP 404, so the dated archive
  reaches back to roughly early 2024 (exact start undocumented).
- Verdict: the best no-strings fallback. Undisclosed sourcing and a single maintainer make it unsuitable as the
  primary under ADR 0012.

### Federal Reserve H.10

- Weekly release of daily "noon buying rates in New York for cable transfers" certified by the New York Fed; 23
  currencies, VND not among them; the Data Download Program is being retired and users are pointed to FRED, whose
  terms ADR 0012 already rules out. Sources: <https://www.federalreserve.gov/releases/h10/current/>,
  <https://www.federalreserve.gov/releases/h10/about.htm>.

### U.S. Treasury Reporting Rates of Exchange (Fiscal Data)

- Quarterly; "exchange rates at which the U.S. government can acquire foreign currencies for official expenditures",
  reported on the last business day of the prior month and explicitly "not current market rates"; data from
  2001-03-31. Source:
  <https://fiscaldata.treasury.gov/datasets/treasury-reporting-rates-exchange/treasury-reporting-rates-of-exchange>.
- API: open, no token; data "offered free, without restriction" for non-commercial or commercial use; base URL
  `https://api.fiscaldata.treasury.gov/services/api/fiscal_service/`, endpoint `v1/accounting/od/rates_of_exchange`.
  Source: <https://fiscaldata.treasury.gov/api-documentation/>.
- Verified on 2026-10-25: `?filter=country_currency_desc:eq:Vietnam-Dong&sort=-record_date` → 2026-09-30: 25959.0,
  2026-06-30: 26255.0, 2026-03-31: 26323.0 (103 rows in total).
- Verdict: the one US-agency source with VND and the same "public domain, cite the agency" footing as BLS and H.15 in
  `IUsAgencyClient`, but quarterly. Useful as an official sanity bound (flag the daily feed if it drifts more than a few
  percent from the latest quarterly rate), not as the daily rate.

### State Bank of Vietnam (SBV)

- The SBV site publishes the daily central rate ("Tỷ giá trung tâm"), reference rates from the Foreign Exchange
  Management Department and weekly bulletins (e.g. "từ 05-09.10.2026"); the homepage ticker shows 25,627.00 VND without
  a label or date. No API, no downloadable series and no terms of use or licence were found; statistics link to
  `dttktt.sbv.gov.vn`. Sources: <https://www.sbv.gov.vn/webcenter/portal/en/home/rm/er>, <https://www.sbv.gov.vn/>.
- Verdict: authoritative but not machine-readable; scraping an HTML portal with no stated terms is not a stable base.
  Frankfurter does not list SBV as a provider either.

### Vietcombank

- XML feed `https://portal.vietcombank.com.vn/Usercontrols/TVPortal.TyGia/pXML.aspx`: 20 currencies with Buy, Transfer
  and Sell in VND; USD on 2026-10-26 (local time) 25,610 / 25,640 / 26,020; the only notice is a comment
  "For reference only. Only one request every 5 minutes!"; no copyright or licence text.
- JSON `https://www.vietcombank.com.vn/api/exchangerates?date=YYYY-MM-DD` returns the same table for a date
  (`UpdatedDate: 2026-10-24T23:00:00+07:00`). The public page did not render server-side.
- Verdict: a commercial bank's posted rates (a spread, not a mid-rate), undocumented and unlicensed. Not recommended.

### World Bank (already a Research source)

- `PA.NUS.FCRF` "Official exchange rate (LCU per US$, period average)" for VN: 2024 = 24164.89, 2023 = 23787.32.
  Annual only, so no help for a daily rate, but it could label a long-run chart. Source:
  <https://api.worldbank.org/v2/country/VN/indicator/PA.NUS.FCRF?format=json&mrv=2>.

## Open points for the implementing ticket

1. Decide whether a blended aggregator of official rates satisfies ADR 0012's "official public source" wording, or
   whether to pin one Frankfurter provider (e.g. `/v2/providers/BDI/rates`, Banca d'Italia, daily, EUR pivot) and accept
   that provider's terms instead. The blend is more robust to one bank's outage.
2. Credit line on the page: "Reference rate: Frankfurter (blend of central-bank rates); fallback: currency-api (CC0)".
   If ExchangeRate-API is ever used, its exact attribution link is mandatory.
3. Backfill: Frankfurter `from=`/`to=` can seed history in one call per year; fawazahmed0 only covers ~2024 onward.
4. Record the decision as an ADR and add the two sources to the ADR 0012 table.
