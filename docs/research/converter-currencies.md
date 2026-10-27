# Research: currencies the converter's rate sources can quote, and what storing them costs

**Ticket:** [#434](https://github.com/tngo0508/PersonalFinance/issues/434) (part of map #433, Currency converter)  
**Date:** 2026-10-26  
**Builds on:** `research/exchange-rates` (`docs/research/usd-vnd-exchange-rate-providers.md`, ticket #337), which chose
Frankfurter v2 (blended) as primary and the fawazahmed0 currency-api as fallback for USD↔VND. That note is not repeated
here; this one widens the question to the converter's list (VND, JPY, EUR, GBP, CNY, KRW, THB, SGD, AUD, CAD, ...).

Every claim was checked against the provider's own docs, source code or live API on 2026-10-26 (US Pacific; the APIs
were already on 2026-10-27 UTC). The precision figures come from a C# script using `decimal`, the same type
`FxRatesRefresher` uses. This note reports facts and trade-offs. Choosing the list is left to the ticket's follow-up
grilling.

## Short answers

| Question | Answer |
|---|---|
| Frankfurter coverage | 170 current currencies (`/v2/currencies`), all ten in the list. **Every calendar day from 2015-01-01 to 2026-10-26 is present** for nine of them; VND is missing 7 days. Weekends and holidays get a value, carried forward from each central bank's latest rate. |
| currency-api coverage | 339 codes in the latest file, all ten in the list. **History starts 2024-03-02**. Nothing before that, and two archive days are missing. |
| Licence and terms | Unchanged since #337. Frankfurter's blend is "free to use" commercially, needs no key and may be stored. Its server code is MIT. currency-api data is CC0-1.0 with no rate limits. Neither needs a secret. |
| `decimal(18,8)` precision | **Storage loses nothing.** Frankfurter's own output rounding (5 to 6 significant digits) limits accuracy, at up to about 5×10⁻⁵ relative error on a cross rate. Disagreement between sources (about 0.3% on VND, JPY and KRW) is bigger still. A small-unit currency loses digits only if its *inverse* were stored (VND→USD = 0.00003875, 4 significant digits). The current design computes the inverse instead. |
| Rows and backfill | Each currency adds 4,317 rows (2015-01-01 to 2026-10-26) and 365 more per year. Ten currencies come to 43,170 rows (about 3.5 MB), and all 170 to about 734,000 (about 60 MB). A first backfill takes about 9 s per currency from Frankfurter at today's per-currency loop, so about 80 s for the 9 new ones. currency-api cannot backfill before 2024-03-02. |

## 1. Coverage per currency

### Frankfurter v2 (blended)

Sources: `GET https://api.frankfurter.dev/v2/currencies`, `/v2/currency/{code}`, `/v2/providers`, and a live range call
`/v2/rates?base=USD&quotes=VND,JPY,EUR,GBP,CNY,KRW,THB,SGD,AUD,CAD&from=2015-01-01&to=2026-10-26` (43,163 rows, 2.7 MB,
14 s). The homepage now says "228 currencies from 106 central banks and official sources", which counts legacy codes
(<https://frankfurter.dev/>). The current-currency list returns 170, and `/v2/providers` returns 106 providers: 101
daily, 3 monthly, 1 weekly, 1 quarterly.

| Code | Series starts | Days present 2015-01-01 to 2026-10-26 (of 4,317) | Providers (daily) | Home-country official source among them | Output rounding (observed) |
|---|---|---|---|---|---|
| VND | 1998-07-07 | 4,310 (missing 2015-06-13/14, 2016-01-31, 2016-08-06/07, 2017-07-15, 2019-01-12) | 29 (24) | **none** (no State Bank of Vietnam) | integer |
| JPY | 1969-12-01 | 4,317 | 96 (91) | BOJ (daily) | 2 dp |
| EUR | 1999-01-01 | 4,317 | 99 (94) | ECB (daily) | 5 significant digits |
| GBP | 1949-12-21 | 4,317 | 100 (95) | BOE (daily) | 5 significant digits |
| CNY | 1981-01-02 | 4,317 | 84 (79) | CFETS central parity (daily) | 4 dp |
| KRW | 1981-01-02 | 4,317 | 54 (49) | **none** (no Bank of Korea) | 2 dp |
| THB | 1981-01-02 | 4,317 | 49 (44) | BOT (daily) | 3 dp |
| SGD | 1981-01-02 | 4,317 | 60 (55) | MAS (daily) | 4 dp |
| AUD | 1971-01-04 | 4,317 | 85 (80) | RBA (daily) | 4 dp |
| CAD | 1953-05-11 | 4,317 | 90 (85) | BOC (daily) | 4 dp |

Provider counts include five non-daily providers (BIS, HMRC, INFOREURO monthly; JPC weekly; UST quarterly). Frankfurter's
FAQ says such sources "are excluded from the blended feed" (<https://frankfurter.dev/>).

**Gaps.**

- **Weekends and holidays are filled, not empty.** `CarryForward` builds each date's snapshot from "each key's most
  recent rate within a lookback window" of `LOOKBACK_DAYS = 14`
  (<https://github.com/lineofflight/frankfurter/blob/main/lib/carry_forward.rb>). On Saturday 2026-10-24,
  `expand=providers` shows VND blended from Friday rates of most banks plus Saturday rates from BOM and NBKR. One outlier
  is flagged `"excluded": true` (CBM, 258.3). In the 2015–2026 data a weekend value equals the previous day's for only
  1–34% of weekends, depending on the currency (KRW 1%, EUR 2%, VND 34%). Most weekend values are new numbers because some contributing bank
  published that day.
- **VND has 7 missing days in 12 years**, all weekends or isolated days, most likely where no contributor fell inside
  the 14-day window. A past-date lookup on those days needs a fallback rule, for example the nearest earlier day. That
  is the converter-lookup ticket's call.
- **VND and KRW exist only as blends of foreign central banks' cross rates.** Neither has its home central bank among
  the providers. For VND on 2026-10-23 the contributors ranged from 25,632 (CBR) to 26,112 (NBP), a 1.9% spread. The
  blend was 25,824. Every other currency in the list has its own central bank or official fixing among the
  contributors, and `providers=` could pin it (for example `providers=BOJ`), subject to that provider's terms.
- **Thin currencies elsewhere in the 170:** GGP, IMP and JEP have no provider (they are pegged, and "Pegged-currency
  rows omit the field"). BTC, ETH, LTC, XRP, BCH, FKP, SHP and CMD have one daily provider. BTN, KYD, SSP, XPF and ZWG
  have two. A wider list should avoid these or accept a single-source rate.

### fawazahmed0 currency-api

Sources: README <https://github.com/fawazahmed0/exchange-api>, npm registry
`https://registry.npmjs.org/@fawazahmed0/currency-api`, and live `…@{date}/v1/currencies/usd.json` on jsDelivr and
`https://{date}.currency-api.pages.dev`.

- **All ten codes are present** in the latest file (339 keys, dated 2026-10-26). Values carry up to 8 decimal places
  and 10 to 13 significant digits (VND 25837.24246605, JPY 156.8596076, KRW 1321.5512312).
- **History:** the package has one dated version per day from **2024-03-02** to 2026-10-27 (969 versions). `@2024-03-01`
  returns 404 on both mirrors. The older `fawazahmed0/currency-api` GitHub path (`cdn.jsdelivr.net/gh/...@1/2021-06-01/...`)
  also returns 404, so nothing before March 2024 can be reached.
- **Archive holes:** 2025-12-10 is missing on both jsDelivr and Cloudflare. 2026-08-19 is missing on jsDelivr only;
  Cloudflare has it.
- Every calendar day, weekends included, has a file. The README discloses no sources and no update time.

## 2. Licence and terms (ADR 0009, ADR 0012)

Re-checked on 2026-10-26. Nothing has changed since #337.

- **Frankfurter.** FAQ (<https://frankfurter.dev/>): "Is the API free for commercial use? Yes. Blended rates are
  Frankfurter's own calculation and free to use". Direct provider endpoints "remain subject to each provider's terms".
  Storing "is encouraged". "There are no quotas", only abuse rate-limiting. No API key. The server repo is MIT
  (`gh api repos/lineofflight/frankfurter` → `MIT`, pushed 2026-10-25). Throttling is in code: heavy live range computes
  are capped per process (`MAX_HEAVY_COMPUTES`, default 2), and past the cap a request gets "a 503 with Retry-After"
  of 30 s (`lib/heavy_slots.rb`, CHANGELOG #650).
- **currency-api.** Repo licence `CC0-1.0` (`gh api repos/fawazahmed0/exchange-api`, not archived, pushed 2026-10-01).
  The README says "No Rate limits". No key.
- **ADR 0009** names licences for *packages*. Neither source is a package: both are HTTP data, so no NuGet or npm entry
  is added. CC0 and the "free to use" grant are at least as permissive as the listed licences. This matches #337's
  reading.
- **ADR 0012 tension (already open from #337, still open):** the blend is "Frankfurter's own calculation" over official
  rates, not an official publication. For VND and KRW, no home-country official rate exists in the feed at all. For the
  other eight, an official home source could be pinned with `providers=`, but then that provider's terms apply instead
  of Frankfurter's. Terms URLs listed by `/v2/providers` include ECB
  (<https://www.ecb.europa.eu/services/using-our-site/disclaimer/html/index.en.html>), RBA
  (<https://www.rba.gov.au/copyright/>) and BOC (<https://www.bankofcanada.ca/terms/>). MAS and BOT list none.

## 3. Precision of a cross rate with `decimal(18,8)`

Script: C# file-based app (`dotnet run prec.cs`, .NET 10.0.402) on Frankfurter's live 2026-10-26 rates and currency-api's
2026-10-26 file. It applies `decimal.Round(rate, 8, MidpointRounding.AwayFromZero)` exactly as `FxRatesRefresher` does,
then computes cross = rate(USD→To) / rate(USD→From) in `decimal` (28 to 29 significant digits).

**Where the error comes from, largest first:**

1. **Different sources disagree by much more than any rounding.** Here is the same-day cross rate from the two sources:

   | Pair | Frankfurter | currency-api | Difference |
   |---|---|---|---|
   | JPY→VND | 164.2125 | 164.7157 | −0.305% |
   | VND→KRW | 0.0513284 | 0.0511491 | +0.351% |
   | KRW→VND | 19.4824 | 19.5507 | −0.349% |
   | THB→SGD | 0.0378627 | 0.0378156 | +0.125% |
   | EUR→GBP | 0.838349 | 0.838603 | −0.030% |

   A day that falls back to currency-api can therefore jump about 0.3% for the Asian pairs. That matters for labelling the
   source of a converted figure.
2. **Frankfurter rounds its blended output** (`lib/roundable.rb`): rates over 5000 to an integer, over 80 to 2 dp, over
   20 to 3 dp, over 1 to 4 dp, and below 1 to 5 significant figures. The comment there says blends "round on the way
   out", while single-provider responses echo stored digits (`lib/rate_precision.rb`). The worst-case relative error of
   one stored rate is half a unit in the last place: VND 1.9×10⁻⁵, JPY 3.2×10⁻⁵, SGD 3.9×10⁻⁵, AUD 3.5×10⁻⁵, CAD 3.6×10⁻⁵,
   THB 1.5×10⁻⁵, CNY 7.5×10⁻⁶, GBP 6.7×10⁻⁶, EUR 5.6×10⁻⁶, KRW 3.8×10⁻⁶. A cross rate adds both legs' errors:
   - 1,000,000 JPY → 164,212,535.79 VND, ± 8,406 VND (5.1×10⁻⁵), about USD 0.33
   - 10,000,000 VND → 513,283.73 KRW, ± 11.9 KRW (2.3×10⁻⁵)
   - 10,000,000 VND → 60,896.69 JPY, ± 3.1 JPY (5.1×10⁻⁵)
   - 1,000,000 KRW → 19,482,401.97 VND, ± 451 VND
   - 1,000 EUR → 838.35 GBP, ± 0.01 GBP
   - 10,000 THB → 378.63 SGD, ± 0.02 SGD

   So a converted result has about 4 to 5 trustworthy significant digits. Digits beyond that are arithmetic, not
   information.
3. **`decimal(18,8)` storage adds nothing.** Rounding to 8 dp lost exactly 0 on every Frankfurter rate (5 dp at most) and
   on every currency-api rate (8 dp at most). The column holds up to 9,999,999,999.99999999. Frankfurter's largest USD rate
   today is IRR at 1,841,216 and its smallest is BTC at 0.000012272, which would keep 5 significant digits at 8 dp; the
   fiat currencies all sit above 0.3. currency-api's VEB (88,190,655,072) would overflow, but nobody would pick it.
4. **`decimal` division is exact to 28 digits.** 25806 / 157.15 × 157.15 − 25806 = 0.

**Does a small-unit currency lose digits?** Not as stored. USD→VND = 25,806 keeps all its digits. 1 VND → KRW computes as
0.0513283732465318143067503681 at full `decimal` precision, because the inverse and the cross are computed and never
stored. Digits would be lost only if a rate *below* 1 were stored at 8 dp, for example a VND-base or inverse row:

| Inverse stored at 8 dp | Value | Significant digits kept | Relative error |
|---|---|---|---|
| VND→USD | 0.00003875 | 4 | 1.75×10⁻⁵ |
| KRW→USD | 0.00075496 | 5 | 4.9×10⁻⁶ |
| JPY→USD | 0.00636335 | 6 | 4.5×10⁻⁷ |
| EUR→USD | 1.12125221 | 9 | 4.0×10⁻⁹ |

That is the same size as Frankfurter's own rounding for VND, so it would double the error rather than ruin it. The
current "USD base only, inverse computed" rule (ADR 0016 §4) avoids it entirely.

## 4. Rows and first backfill

**Row count.** One row per (USD, quote, day) (unique index `BaseCurrency, QuoteCurrency, Date`). From 2015-01-01 to
2026-10-26 there are 4,317 calendar days, and Frankfurter answers every one (VND 4,310).

| Quote currencies N | Rows to 2026-10-26 | Of which new (VND already stored) | Growth per year | Approx. size* |
|---|---|---|---|---|
| 10 (the list above) | 43,170 | 38,853 | 3,650 | about 3.5 MB |
| 20 | 86,340 | 82,023 | 7,300 | about 7 MB |
| 30 | 129,510 | 125,193 | 10,950 | about 10 MB |
| 50 | 215,850 | 211,533 | 18,250 | about 17 MB |
| 170 (all of Frankfurter) | 733,890 | 729,573 | 62,050 | about 59 MB |

\*Estimate of about 80 bytes per row: `int` Id 4, two `char(3)` 6, `date` 3, `decimal(18,8)` 9, `nvarchar(32)` Source
"frankfurter" 22 + 2, plus row overhead, plus the unique index entry (about 20 bytes). It is an order-of-magnitude
figure, not measured on Azure SQL.

**How the current code would backfill.** `FxRatesRefresher.RefreshAsync` loops over `SupportedCurrencies.All` minus USD.
For each currency it reads the latest Frankfurter-sourced date, and with none it starts at `FirstDate` = 2015-01-01. It
makes one Frankfurter call per currency for the whole range, then one `SaveChangesAsync`. A newly added currency
therefore backfills from 2015 by itself on the next daily run. The loop reads `SupportedCurrencies`, so a converter-only
list would need the refresher to iterate a different list. That is a design choice for the spec.

**Measured times (2026-10-26, from a home connection):**

| Call | Time | Size |
|---|---|---|
| Frankfurter, 1 currency, 2015-01-01 to 2026-10-26 (uncached) | 8.9–9.0 s | 267 KB, 4,317 rows |
| Same call repeated (Cloudflare cache hit) | 0.07 s | |
| Frankfurter, 10 currencies in one call, same range | 14 s | 2.7 MB, 43,163 rows |
| Frankfurter, 1 currency, one year (2016) | 1.1 s | |
| currency-api, one dated file (all currencies) | 0.08 s | 6 KB |

- **Frankfurter:** at today's one-call-per-currency loop, the first backfill of the 9 new currencies takes about 9 × 9 s
  = 80 s. Each call sits under the 30 s `ExchangeRateTimeout` in `ResearchFeature.cs`, but with only about 3× headroom.
  A single multi-quote call (`quotes=A,B,...`) took 14 s for 10 currencies. Answers are cached for a day
  (`cache-control: public, max-age=86400`).
- **Caveat from Frankfurter's code** (`lib/versions/v2/rate_query.rb`): when a daily range cannot be served from the
  materialized blend (with `providers=` or `expand=providers`, or while the blend table is being rebuilt), the server
  recomputes live. That path has `MAX_DAILY_RANGE_YEARS = 5` and `MAX_DAILY_RANGE_QUOTES = 5`, and the comment says
  longer ranges outlive "the request timeout and Cloudflare's origin ceiling". It also falls under the HeavySlots 503
  limit. Plain 12-year ranges were served today. A backfill split into ranges of at most 5 years and at most 5 quotes
  would stay inside every documented limit: 3 calls per currency, about 1 s per year each.
- **currency-api:** it cannot seed history before 2024-03-02. From 2024-03-02 to today it would take 970 files, one per
  day, each holding all currencies: about 80 s sequential at 0.08 s per file. Two days are missing (see §1).

## Trade-offs for the grilling (not decided here)

- **List width vs. source quality:** the ten currencies are all well covered daily back to 1969–1999. Widening to the
  full 170 brings in single-provider and pegged codes. VND and KRW are blend-only, with no home central bank.
- **Blend vs. pinned provider:** pinning (for example `providers=BOJ`) gives an official rate with more digits for eight
  of the ten. It moves terms to each provider, triggers the 5-year and 5-quote live-compute caps, and cannot cover VND
  or KRW.
- **Display precision:** about 5 significant digits are real. Showing more invites false precision. Rounding to the To
  currency's minor units is well within the error for every pair tested.
- **Fallback days:** a currency-api day can differ by about 0.3% from the Frankfurter series for Asian pairs, and it has
  no data before 2024-03.
- **Storage:** even all 170 currencies stay under about 60 MB and about 62,000 rows a year. The cost is the first
  backfill's run time and request count, not space.
