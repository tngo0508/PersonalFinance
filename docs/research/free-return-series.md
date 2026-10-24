# Free return, inflation and rate series for the Research page

Research note for [#231](https://github.com/tngo0508/PersonalFinance/issues/231) (part of map [#230](https://github.com/tngo0508/PersonalFinance/issues/230)).
It builds on the earlier note [market-data-sources.md](https://github.com/tngo0508/PersonalFinance/blob/research/market-data-sources/docs/research/market-data-sources.md) (#215), which settled that no commercial free price API allows display to other users and that SEC EDGAR, the World Bank, BEA and some FRED series are usable. That work is not repeated here.

Checked on **2026-10-24**. "Verified" means I called the endpoint myself (SEC with the User-Agent `PersonalFinance research tngo0508@gmail.com`) and read the response. Terms pages that would not load with `curl` (FRED, BLS) were read through a web-fetch tool; those rows say so. This is a reading of the terms, not legal advice.

The use case is unchanged: the ApiService fetches server-side, **stores the result in the Research snapshot table**, and shows it to signed-in users, for information only.

## TL;DR

- **Yes, free fund returns exist, and they are complete for all six tracked ETFs.** Form N-PORT Part B Item B.5 carries the fund's **monthly NAV total return** (percent, distributions reinvested, net of expenses) for each of the three months the report covers, per share class. The public quarterly filings tile the calendar with no gaps: **84 months (Jul 2019 to Jun 2026) for SPY, QQQ, VTI and IWM, 84 for VT (Aug 2019 to Jul 2026), 81 for AIQ (Sep 2019 to May 2026)**. Verified by fetching all 169 distinct filings. Values match the funds' own shareholder reports to the second decimal.
- **The lag is about 2 to 4 months.** A quarter's filing is public about 60 days after the quarter ends (SPY's June 2026 quarter was filed 2026-08-28). There are no daily values and no market-price returns.
- **Expense ratios: free and tagged for 5 of 6.** The tailored shareholder reports (Form N-CSR / N-CSRS, inline XBRL) tag `oef:ExpenseRatioPct` per share class: VTI 0.03%, VT 0.06%, IWM 0.19%, QQQ 0.19%, AIQ 0.68% (verified). **SPY is a unit investment trust: it files no N-CSR and no XBRL**, so its expense ratio exists only as text in its prospectus.
- **Inflation and rates: use the publishers, not FRED.** FRED's legal terms forbid storing or caching FRED content without the Bank's written consent, which rules out our snapshot design. The same series are public domain at the source: **BLS CPI flat files (monthly, 1947 onward)** and the **Fed Board's H.15 XML bundle (fed funds 1954 onward, 3-month T-bill 1934 onward, 10-year 1953 onward, daily and monthly)**. Both were verified and need no API key.
- **Index levels: no clean free source.** FRED's S&P 500 is "Copyrighted: Pre-Approval Required". Fund shareholder reports on EDGAR do tag 10 years of index growth lines, but the index provider's rights in those numbers are unclear. **Use a tracked fund (VT or VTI) as the benchmark** for "relative outperformance".
- **Event dates:** rate-hike dates are on the Fed's FOMC page (2003 onward); drawdowns can be computed from the N-PORT monthly series (month-end resolution); recession dates (NBER) need hand-entered rows with a citation.
- **No paid licence decision is needed** for sections 2, 3 and 4, as long as the page accepts monthly data that lags by a quarter.

## Per-source table

| Source | What | Cadence | History | Tracked-ETF coverage | Licence / attribution | Status |
|---|---|---|---|---|---|---|
| SEC Form N-PORT, Item B.5 (`NPORT-P` `primary_doc.xml`, `<returnInfo><monthlyTotReturns>`) | Monthly total return per fund or class, in percent | Monthly values, published quarterly about 60 days after the fiscal quarter ends | Jul–Sep 2019 onward (the first public filings) | All 6, no gaps. See the coverage table below | SEC public information, free to redistribute ([sec.gov/privacy](https://www.sec.gov/privacy)). No attribution required; credit "Source: SEC EDGAR" | Verified |
| SEC Form N-PORT data sets ([page](https://www.sec.gov/data-research/sec-markets-data/form-n-port-data-sets), [readme](https://www.sec.gov/files/nport_readme.pdf)) | Same B.5 data as table `MONTHLY_TOTAL_RETURN` (columns `MONTHLY_TOTAL_RETURN1..3`, plus class id) | Quarterly ZIPs (latest `2026q3_nport.zip`; page last reviewed 2026-09-30) | Oct 2019 onward | All filers | As above | Readme read; ZIPs not downloaded |
| SEC N-CSR / N-CSRS tailored shareholder reports (inline XBRL, `oef:` taxonomy) | `ExpenseRatioPct` per class; in annual reports also `AvgAnnlRtrPct` (1, 5, 10 year or since inception) and `AccmVal` (growth of $10,000 line, fund and indexes) | Semi-annual (expense ratio); annual (performance) | Tailored reports began with mid-2024 fiscal periods (my reading of the filing dates seen; compliance date not checked). `AccmVal` reaches back 10 years | 5 of 6: **not SPY** (a UIT; its last N-CSR was 2004, and it has no XBRL filings) | SEC public information. **Index lines inside the reports:** third-party index values, rights unclear (see Gaps) | Verified for all 6 |
| BLS CPI flat files (`download.bls.gov/pub/time.series/cu/cu.data.1.AllItems`) | CPI-U all items, SA (`CUSR0000SA0`) and NSA (`CUUR0000SA0`) | Monthly | 1947-01 to 2026-09 | Not fund data | "everything that we publish ... is in the public domain"; "we do ask that you cite the Bureau of Labor Statistics as the source" ([bls.gov copyright](https://www.bls.gov/opub/copyright-information.htm), read through the fetch tool) | Verified (flat file with a contact User-Agent). The BLS API (`api.bls.gov`) returned "The BLS website is busy" from this machine |
| Fed Board H.15 XML bundle ([release](https://www.federalreserve.gov/releases/h15/), [`FRB_h15_xml.zip`](https://www.federalreserve.gov/releases/h15/data/FRB_h15_xml.zip)) | Effective fed funds (`RIFSPFF_N.*`), T-bills (3-month `RIFSGFSM03_N.*`), Treasury constant maturities (3-month `RIFLGFCM03`, 2-year `RIFLGFCY02`, 10-year `RIFLGFCY10`) and more, 263 series | Business-day, weekly, monthly, annual | Fed funds 1954-07; 3-month T-bill monthly 1934-01 (daily 1954-01); 10-year monthly 1953-04 (daily 1962-01); 3-month CMT 1981-09. Latest daily value 2026-10-22 | Not fund data | "information on Board's website is in the public domain and may be copied and distributed without permission. Please cite to the Board as the source" ([disclaimer](https://www.federalreserve.gov/disclaimer.htm), read through the fetch tool) | Verified (zip downloaded and parsed, 4.3 MB) |
| Fed FOMC open market operations page ([openmarket.htm](https://www.federalreserve.gov/monetarypolicy/openmarket.htm)) | Every target-rate change: date, basis points, new level | Per FOMC decision | 2003-06-25 to 2026-09-17 (+25 bp to 3.75–4.00%) | Not fund data | Fed Board public domain, as above | Read through the fetch tool. HTML only, no data file |
| US Treasury daily par yield curve ([XML feed](https://home.treasury.gov/resource-center/data-chart-center/interest-rates/pages/xml?data=daily_treasury_yield_curve&field_tdr_date_value_month=202609)) | Par yields 1 month to 30 years | Daily | Verified for 2026-09. Start year not checked | Not fund data | US government work, so no copyright under 17 U.S.C. §105. I found no Treasury-specific reuse statement | Verified (endpoint). Licence inferred |
| FRED (CPIAUCSL, FEDFUNDS, DGS10, TB3MS, DFEDTARU, USREC, SP500) | Re-publishes the series above | — | — | — | **Legal terms item (p): "You may not, without the Bank's prior written consent: Store, cache, or archive any portion of the FRED® Services or FRED® Content ... or incorporate any FRED® Content in any database, compilation, archive, cache, or other medium." API item (l) repeats this for the API** ([fred.stlouisfed.org/legal](https://fred.stlouisfed.org/legal/), read through the fetch tool twice). Tags: CPIAUCSL, FEDFUNDS, DGS10, TB3MS and DFEDTARU are "Public Domain: Citation Requested"; USREC is "Copyrighted: Citation Required"; SP500 is "Copyrighted: Pre-Approval Required" ("Reproduction of S&P 500 in any form is prohibited except with the prior written permission of S&P Dow Jones Indices LLC") | **Not usable for a cached snapshot.** `curl` to fred.stlouisfed.org timed out from this machine |

### N-PORT Item B.5 coverage, verified

The script read every public `NPORT-P` and `NPORT-P/A` filing listed by EDGAR's filing index (`browse-edgar ... type=NPORT-P`) for each fund, and stopped reading each `primary_doc.xml` at `</returnInfo>`.

| ETF | Filer / series | Class reported | Months | Range | Gaps | Notes |
|---|---|---|---|---|---|---|
| SPY | CIK 884394 (UIT) | none (single return) | 84 | 2019-07 .. 2026-06 | none | Returns given to 2 decimals |
| QQQ | CIK 1067839 | none until 2025-09, then `C000271435` (series `S000101292`) | 75 + 9 = 84 | 2019-07 .. 2026-06 | none | **QQQ converted from a UIT to an open-end ETF series in 2025.** The two parts must be joined: the trust return up to 2025-09, then the new class |
| VTI | series `S000002848` | 6 classes; ETF class `C000007808` | 84 | 2019-07 .. 2026-06 | none | Pick the ETF class by `classId` (from `company_tickers_mf.json`) |
| VT | series `S000022482` | 3 classes (a 4th ends in 2019); ETF class `C000065038` | 84 | 2019-08 .. 2026-07 | none | Fiscal quarter ends Jan/Apr/Jul/Oct |
| IWM | series `S000004344` | `C000012074` | 84 | 2019-07 .. 2026-06 | none | One `NPORT-P/A` amendment; values agree |
| AIQ | series `S000061326` | `C000198548` | 81 | 2019-09 .. 2026-05 | none | One `NPORT-P/A`; fiscal quarter ends Feb/May/Aug/Nov |

Other checks:
- **Meaning and units.** Form N-PORT Item B.5.a: "Monthly total returns of the Fund for each of the preceding three months. If the Fund is a Multiple Class Fund, report returns for each Class. Such returns shall be calculated in accordance with the methodologies outlined in Item 26(b)(1) of Form N-1A ..." ([Form N-PORT](https://www.sec.gov/files/formn-port.pdf), version effective 2023-12-11). This is a NAV return, not a market-price return. Values are percents (`rtn1="10.48"` means 10.48%). Cross-check: IWM's B.5 values match the month-to-month changes of `oef:AccmVal` in its 2026 annual report for 2019-07..09 and 2025-10..2026-03 (for example, 2026-01 is 5.35% in both).
- **Public.** General Instruction F makes third-month reports public on filing. Its list of withheld items (B.7, B.8, B.9, B.10, C.5.b, C.7, Part D and others) does not include B.5. Each public filing carries all three months, so the quarterly public filings give a continuous monthly series.
- **Amendments.** Where an amendment and the original filing overlapped, the values agreed. The pipeline should still let the latest filing for a period win.
- **Sector funds work the same way.** The Select Sector SPDR series (XLK, XLF, XLE, XLV, XLI, XLY, XLP, XLU, XLB, XLRE, XLC, all CIK 1064641) appear in `company_tickers_mf.json`. XLK's latest `NPORT-P` (period 2026-06-30) carries `monthlyTotReturn classId="C000017601"`, and the series has 28 public filings.
- **Cadence change ahead.** The 2024 N-PORT amendments (monthly filing, each report public 60 days after month end) had their compliance date pushed to 2027-11-17 for larger fund groups ([press release 2025-64](https://www.sec.gov/newsroom/press-releases/2025-64)). A February 2026 proposal would bring back quarterly publication ([IC-35962 fact sheet](https://www.sec.gov/files/ic-35962-fact-sheet.pdf)). Either way B.5 still yields monthly values. Only the lag changes. I did not check whether the proposal is final.

### Expense ratios, verified

| ETF | `oef:ExpenseRatioPct` | Period | Filing |
|---|---|---|---|
| SPY | **not tagged** (UIT: no N-CSR since 2004, `isXBRL` 0 on every filing in `submissions/CIK0000884394.json`) | — | Prospectus `485BPOS` (HTML, 2026-01-26) only |
| QQQ | 0.19% | 2025-10-01 .. 2026-03-31 | N-CSRS 2026-06-01 (first report since the conversion; no annual performance tags yet) |
| VTI | 0.03% | 2025-12-31 .. 2026-06-30 | N-CSRS 2026-08-27 |
| VT | 0.06% | 2025-11-01 .. 2026-04-30 | N-CSRS 2026-07-01 |
| IWM | 0.19% | 2025-04-01 .. 2026-03-31 | N-CSR 2026-06-04 |
| AIQ | 0.68% | 2025-12-01 .. 2026-05-31 | N-CSRS 2026-08-06 |

`companyfacts` and `frames` return no facts for fund trusts (Vanguard Index Funds returned 404, iShares Trust and Global X Funds returned empty `facts`). The data has to be read from each filing's XBRL instance (`*_htm.xml` in the filing folder). The prospectus `485BPOS` filings also carry inline XBRL (iShares' 2026-07-27 filing has R pages), but I did not check their risk/return tags. The shareholder report is enough.

The annual-report performance tags differ between fund families. `AvgAnnlRtrPct` is tagged in every annual report checked (iShares, Vanguard, Global X). QQQ has no annual report since its conversion yet. The `AccmVal` growth line has 120 monthly points for IWM (iShares), but only 11 annual points for VTI and 9 for AIQ, and 41 for VT. Treat it as a long-history extra, not a series for calculations.

## What each series rules in or out

### Section 2: performance and macro context

| Visualisation | Verdict | Source and caveat |
|---|---|---|
| Base-100 multi-line chart of the tracked ETFs | **In** | N-PORT B.5, chained monthly. Common start 2019-09 (AIQ is the latest starter). Last point lags a quarter, and fund fiscal quarters differ, so the common end date is the earliest latest month (AIQ, 2026-05). Label it "NAV total return, month end" |
| Stacked cumulative growth of a picked basket | **In** | Weighted monthly returns from B.5. Any rebalancing rule must be stated (for example, monthly rebalance to equal weight) |
| Difference chart (relative outperformance) | **In against a tracked fund; out against a named index** | Benchmark = VT or VTI from B.5. No displayable free index series (see Gaps) |
| Brushing and synchronized crosshair | **In** | About 84 points per fund, so tiny payloads. Daily resolution is **out** |
| Inflation overlay / real returns | **In** | BLS CPI-U monthly. Cite BLS |
| Rate context lines (fed funds, 3-month bill, 10-year, 2s10s) | **In** | Fed H.15 XML, daily or monthly. Cite the Board |
| Event annotations: rate hikes | **In** | Fed FOMC page (2003 onward). It is HTML only: hold the list in config with the source link, or derive the moves from the daily effective fed funds rate (H.15), which picks up each move the next day |
| Event annotations: drawdowns | **In, computed** | Peak-to-trough from the B.5 month-end series. It understates intra-month lows (for example March 2020). Say so in the callout |
| Event annotations: recessions | **Hand-entered** | NBER chronology. FRED's USREC is copyrighted and FRED forbids caching. A handful of rows with an NBER citation. NBER's own terms not checked |

### Section 3: risk vs return

| Element | Verdict | Source and caveat |
|---|---|---|
| Annualized return vs volatility scatter | **In** for 1-, 3- and 5-year windows | B.5. Volatility = stdev of monthly returns × √12. A 5-year window has 60 observations; there is not enough history for 10 years (only through `AvgAnnlRtrPct`, as a static number, and not for SPY) |
| Sharpe ratio in hover cards | **In** | Excess return over the 3-month T-bill (H.15 `RIFSGFSM03_N.M`, monthly average, converted to a monthly rate) |
| Expense ratio in hover cards | **In for 5 of 6** | `oef:ExpenseRatioPct` from N-CSR/N-CSRS. **SPY needs a decision:** hand-enter it from the prospectus with a source link, or show "not reported in XBRL" |
| Bubble size | **In** | N-PORT `netAssets` (already parsed by `NportDocumentParser`) |
| Quadrant overlays, category filter | **In** | Computed from the above. Categories come from config, not from a source |

### Section 4: sector correlation

| Element | Verdict | Source and caveat |
|---|---|---|
| Correlation matrix of the tracked funds | **In** | B.5, about 80 overlapping months |
| Sector correlation matrix | **In only if sector funds join the tracked list** | The Select Sector SPDRs carry B.5 (XLK verified). That depends on [#233](https://github.com/tngo0508/PersonalFinance/issues/233) (which funds Research tracks). Correlation computed from holdings is **out**: there are no free security prices |

## Gaps and unverified items

1. **No displayable index series.** S&P, Nasdaq and Dow series on FRED are "Pre-Approval Required". The `AccmVal` / `AvgAnnlRtrPct` index lines in N-CSR XBRL (for example Russell 2000 and Russell 3000 in IWM's report) are on sec.gov, but the index providers' rights in those values are unclear. I found no SEC or provider statement either way. Treat them as out unless someone gets a legal opinion.
2. **FRED is out for anything we store.** This supersedes the earlier note's "FRED usable with care" for the snapshot design. The prohibition was read through a fetch tool, twice with the same result, but not with `curl` (the site timed out). The FRED API terms page itself has no caching sentence. The prohibition sits in the legal terms that the API terms incorporate.
3. **The Fed Data Download Program is being retired.** The H.15 page: "During the week of November 9, the 'Build Your Package' feature in the Data Download Program (DDP) will be removed in preparation for the eventual retirement of the DDP." It points users to FRED. The `FRB_h15_xml.zip` bundle used here still works (file dated 2026-10-23), but whether it survives the retirement is **unverified**. If it goes, the fallback is Treasury's yield-curve feed (public domain) for bills and yields. The effective fed funds rate is also published by the New York Fed (not checked).
4. **SPY expense ratio** is not machine-readable (see above).
5. **QQQ series join.** QQQ's return history is split between the old trust (no class id) and the new class `C000271435` under the same CIK. The spec must say how to join them. The current `Research:Etfs` config gives QQQ `Cik 1067839`, which still lists both parts.
6. **Lag and staleness.** As of 2026-10-24 the newest months are 2026-06 (SPY, QQQ, VTI, IWM), 2026-07 (VT) and 2026-05 (AIQ). The UI should show "as of" per fund.
7. **Not checked:** NBER's terms for its cycle dates; the Treasury yield-curve history depth; whether B.5 values in the quarterly data-set ZIPs match the XML filings (they should, since the readme maps the table to Item B.5); the 485BPOS risk/return XBRL tags.

## How this was verified

- `https://www.sec.gov/files/company_tickers_mf.json` to map tickers to series and class ids.
- `https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&CIK=<series or CIK>&type=NPORT-P&...&output=atom` for the filing lists (28–29 filings per fund, 2019-11 to 2026-09).
- `primary_doc.xml` of each of the 169 distinct filings, parsed for `repPdDate` and `monthlyTotReturn`. Every month was assigned to a calendar month and checked for gaps and for disagreements between overlapping filings.
- N-CSR / N-CSRS XBRL instances for all six funds (`index.json` of each filing folder, then `*_htm.xml`).
- `https://www.sec.gov/files/formn-port.pdf` and `https://www.sec.gov/files/nport_readme.pdf`, converted with `pdftotext`.
- `FRB_h15_xml.zip` and `cu.data.1.AllItems` downloaded and parsed.
