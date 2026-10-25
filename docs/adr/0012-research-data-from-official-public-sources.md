# 0012 – Research Data Comes from Official Public Sources, Precomputed in a Daily Refresh

**Date:** 2026-10-24  
**Status:** Accepted

## Context

The Research page shows market and economic data for information only: what funds hold, how they returned, how they
moved together, inflation and interest rates. The app is non-commercial and stores what it shows. No free commercial
price API allows showing its data to other users. The obvious aggregator for economic series, FRED, forbids storing or
caching its content without written consent ("You may not ... store, cache, or archive any portion of the FRED®
Services or FRED® Content"), which rules it out for a stored snapshot. The same series are public domain at their
publishers.

## Decision

1. **Every Research figure comes from an official public source,** credited on the page:

   | Data | Source |
   |---|---|
   | Company Public Float, sectors | SEC EDGAR (XBRL frames, company submissions) |
   | Fund holdings by SEC industry | Form N-PORT holdings matched by issuer name to SEC's company list, then company submissions |
   | Fund holdings, net assets, monthly total returns, issuer country and currency | SEC EDGAR, Form N-PORT (Item B.5 for returns, C.5.a and C.2.b for country and currency) |
   | Fund expense ratios | SEC EDGAR, shareholder report (N-CSR/N-CSRS) XBRL, `oef:ExpenseRatioPct` |
   | Consumer prices (CPI-U) | U.S. Bureau of Labor Statistics flat file |
   | Federal funds rate, 3-month Treasury bill | Federal Reserve Board, H.15 data bundle |
   | Treasury bill fallback | U.S. Treasury daily bill rates (only when H.15 cannot be read) |
   | Market cap, GDP | World Bank |

   FRED and paid or dual-licensed data are not used. Security identifiers are handled under ADR 0010.
2. **A daily refresh job downloads, computes and stores everything,** one snapshot per dataset in the existing
   Research snapshot table (no schema change per dataset). The page never calls a source while someone browses. A
   source that fails leaves only its own datasets stale.
3. **The refresh precomputes every per-fund figure:** returns per window, volatility, Sharpe ratio, worst fall, pairwise
   correlation and overlap, and the Fed's hiking cycles. **The browser only combines the Fund Basket:** it weights the
   funds' monthly returns (rebalanced monthly), compounds them for the chosen period, and divides by CPI growth.
4. **Hand-kept facts are configuration, not code:** recessions are `Research:Recessions` in `appsettings.json`, with a
   neutral label until NBER's terms are checked; Tracked Funds and their roles are `Research:Funds`; issuers SEC's company
   list no longer names (renamed, re-formed or delisted since a fund reported them) are `Research:IssuerCikOverrides`.
6. **Fund holdings are matched to SEC filers by issuer name only,** never by a security identifier: the same name, then
   the same name before its legal form (for a foreign issuer only when the two legal forms agree), then for a US issuer
   the same words in any order. A name that leads to two filers is no match. A stock with no match, or whose filer has no
   SIC code, is shown as "Not classified by the SEC" by issuer country, never guessed into an industry.
7. **A missing value stays missing:** a fund with no shareholder report XBRL (SPY, a unit investment trust) shows "n/a"
   for its expense ratio, a month BLS did not publish stays empty in the snapshot, and H.15's no-data days are skipped.
   The chart says when it fills a month in.

## Considered Options

- **FRED for CPI and rates:** rejected. Its terms forbid caching, and the snapshot design caches.
- **A commercial market data API for prices and returns:** rejected. None found allows display to other users for free,
  and fund NAV returns from N-PORT cover what the page needs, monthly and about a quarter behind.
- **Computing every figure in the browser from the monthly returns:** rejected for per-fund figures. The refresh can be
  tested on recorded filings, and the page stays small. The basket is the exception because it depends on the user's
  weights.
- **ISIN to ticker through OpenFIGI for the holdings match:** rejected. FIGIs are public domain, but a US ISIN embeds
  the CUSIP, and whether sending it to a third-party service is licensable CUSIP use was left open. Names alone match
  98.9% of SPY's and 99.5% of QQQ's stock weight on their June 2026 reports, before overrides.
- **Hand-entering expense ratios:** rejected. They change twice a year, and the shareholder report XBRL carries them for
  every fund but SPY.

## Consequences

- Figures are monthly and lag by about a quarter (N-PORT), and expense ratios by up to six months; each tab shows its
  "Data as of".
- The daily job makes more downloads as datasets are added (BLS about 3 MB, H.15 about 4 MB, one shareholder report per
  fund when a new one is filed), within the job's 30-minute limit.
- The Fed is retiring its Data Download Program. If the H.15 bundle goes, the Treasury feed covers the bill rate but not
  the federal funds rate, which then keeps its stored months until another source is chosen.
- Most of an international fund is "Not classified by the SEC" (about 60% of VXUS): foreign companies that file no
  reports with the SEC have no SEC industry, and no free, displayable industry code exists for them.
- Adding a dataset means a refresher, a snapshot DTO, a read endpoint and a fixture for the E2E walkthrough; no
  migration.
