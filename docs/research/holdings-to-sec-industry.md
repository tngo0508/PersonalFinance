# Can fund holdings be mapped to SEC industries for a basket view on Sectors?

Research note for [#243](https://github.com/tngo0508/PersonalFinance/issues/243), part of map [#230](https://github.com/tngo0508/PersonalFinance/issues/230).
Checked on **2026-10-24** against the latest public Form N-PORT of each tracked equity fund, SEC's own ticker and name files, the EDGAR submissions API, the GLEIF API, the OpenFIGI API, and each provider's terms page. Each claim links its source. This is a reading of the terms, not legal advice.

Not repeated here: SEC terms (public information, free to display, 10 req/s, declared User-Agent) are in [market-data-sources.md](https://github.com/tngo0508/PersonalFinance/blob/research/market-data-sources/docs/research/market-data-sources.md). N-PORT country and currency fields, the cash-collateral trap and the 25-per-category trim are in [nport-country-currency.md](https://github.com/tngo0508/PersonalFinance/blob/research/nport-country-currency/docs/research/nport-country-currency.md).

## TL;DR

- **Yes for US funds: 99.4% to 100% of net assets lands in an SEC industry office and SIC industry.** For international funds, only the share issued by SEC-registered companies lands there: **VXUS 36.8%, VT 75.6%, AIQ 86.9%**. The rest is foreign companies with no SEC classification.
- **N-PORT gives no ticker for equities.** The form asks for a ticker only "if ISIN is not available" (Item C.1.e). So ticker -> CIK needs a step from ISIN to ticker first.
- **The join that works: ISIN -> ticker through OpenFIGI, then ticker -> CIK through SEC's `company_tickers.json`, then SIC and office from EDGAR submissions.** FIGIs are public domain. Name matching against SEC's ticker file is the fallback. Where both found a CIK, FIGI and the name match never disagreed.
- **LEI does not work.** The LEIs are on 80–99% of holdings, but GLEIF records the state registry (Delaware etc.) for operating companies, not the SEC CIK. EDGAR's `lei` field was filled for 18 of 5,581 matched CIKs. Joining by LEI placed 0.07% of weight at most.
- **Don't build a CUSIP -> CIK table.** SEC publishes none. CUSIP Global Services says use beyond clearing and settlement needs a CGS licence, and forbids publishing CUSIP data or subsets of it. The free CIK-CUSIP project on GitHub has no licence and is archived. OpenFIGI's terms carry no CUSIP restriction, but use ISINs as input and store only the CIK.
- **The basket view needs per-fund industry totals computed before the 25-per-category trim**, the same rule #232 set for country. VTI spans 370 SIC codes and VT 328, so a per-fund `{SIC: % of net assets}` map is a few KB a quarter. A basket's industry mix is then the weighted sum, which is exact.
- **Fallback hierarchy:** SEC office -> SIC industry -> company. Then for equities with no SEC classification: "Not classified by the SEC" -> issuer country (`invCountry`) -> issuer. Then non-equity rows by asset category, with bonds split by issuer type. Then cash and other.

## The identifiers N-PORT gives

| Field | Form item | Rule | Source |
|---|---|---|---|
| `name`, `lei`, `title` | C.1.a–c | Issuer name, issuer LEI "(if any)", title of issue | [Form N-PORT](https://www.sec.gov/files/formn-port.pdf), PDF p. 12 |
| `cusip` | C.1.d | "CUSIP (if any)" | same, PDF p. 13 |
| `identifiers/isin`, `/ticker`, `/other` | C.1.e | "At least one of: i. ISIN. ii. Ticker (if ISIN is not available). iii. Other unique identifier (if ticker and ISIN are not available)" | same, PDF p. 13 |
| `assetCat`, `issuerCat` | C.4.a–b | Asset type (equity-common, equity-preferred, debt, derivatives, STIV, ...) and issuer type (corporate, U.S. Treasury, U.S. government agency, GSE, municipal, non-U.S. sovereign, private fund, registered fund, other) | same, PDF p. 13 |

**The repo reads none of these identifiers today.** `NportDocumentParser.ReadHolding` reads `name` (or `title`), `assetCat` (or `assetConditional/@assetCat`) and `pctVal`. `NportHolding` is `(Name, AssetCategory, Percent)`. The parser has to be extended to read `isin`, `cusip`, `lei`, `issuerCat` and `invCountry`, and to drop cash-collateral rows.

Coverage in the latest filings, as % of net assets, with cash-collateral rows excluded:

| Fund | Report date | Holdings | Equity (EC+EP) | Has LEI | Has CUSIP | Has ISIN | Has ticker |
|---|---|---|---|---|---|---|---|
| VTI ([filing](https://www.sec.gov/Archives/edgar/data/36405/000003640526000480/primary_doc.xml)) | 2026-06-30 | 3,545 | 99.8 | 97.1 | 99.8 | 99.8 | 0 |
| SPY ([filing](https://www.sec.gov/Archives/edgar/data/884394/000141036826089410/primary_doc.xml)) | 2026-06-30 | 504 | 100.0 | 99.0 | 97.1 | 100.0 | 0 |
| QQQ ([filing](https://www.sec.gov/Archives/edgar/data/1067839/000106783926000030/primary_doc.xml)) | 2026-06-30 | 103 | 99.9 | 99.7 | 99.7 | 99.9 | 0 |
| IWM ([filing](https://www.sec.gov/Archives/edgar/data/1100663/000207169126019749/primary_doc.xml)) | 2026-06-30 | 2,016 | 99.8 | 79.9 | 94.1 | 99.8 | 0 |
| VXUS ([filing](https://www.sec.gov/Archives/edgar/data/736054/000073605426000275/primary_doc.xml)) | 2026-07-31 | 8,867 | 98.9 | 93.2 | 23.9 | 98.9 | 0 |
| VT ([filing](https://www.sec.gov/Archives/edgar/data/857489/000085748926000275/primary_doc.xml)) | 2026-07-31 | 10,099 | 99.3 | 95.8 | 70.6 | 99.3 | 0 |
| AIQ ([filing](https://www.sec.gov/Archives/edgar/data/1432353/000204825126006484/primary_doc.xml)) | 2026-05-31 | 88 | 99.9 | 98.3 | 68.1 | 99.9 | 0 |

- Tickers show up only on futures and FX forwards (`ESU6`, `RTYU6`, `INR`), never on stocks.
- **VXUS** is series `S000002932` of Vanguard STAR Funds, CIK **736054**, class `C000094038`. It is resolved from SEC's [`company_tickers_mf.json`](https://www.sec.gov/files/company_tickers_mf.json) (fields `cik, seriesId, classId, symbol`). VTI (36405 / S000002848), VT (857489 / S000022482), IWM (1100663 / S000004344) and AIQ (1432353 / S000061326) are in the same file. Today `appsettings.json` has a CIK only for SPY and QQQ. SPY is a unit investment trust and is not in the mutual-fund file.

## Join methods

### 1. Ticker -> CIK through SEC's ticker file: usable, but N-PORT has no ticker

- [`company_tickers.json`](https://www.sec.gov/files/company_tickers.json) maps `ticker -> cik_str, title`. It lists about 8,000 normalized company names. Like all sec.gov content it is public information ([SEC privacy and dissemination notice](https://www.sec.gov/privacy)).
- SEC: "We periodically update these files ... but do not guarantee accuracy or scope." ([Accessing EDGAR data](https://www.sec.gov/search-filings/edgar-search-assistance/accessing-edgar-data), last reviewed 2024-06-26).
- **It lists current tickers only.** Companies delisted since the report date are gone: Electronic Arts, Warner Bros. Discovery, AvalonBay, Taylor Morrison, Nuvalent and Webster Financial were missing on 2026-10-24, although they were in the June 2026 filings. Together they were 0.2–0.5% of the US funds.
- Since N-PORT carries no stock tickers, this file is the second step of method 2, or a name index for method 4.

### 2. ISIN -> ticker through OpenFIGI, then ticker -> CIK: the best join

- **API:** `POST https://api.openfigi.com/v3/mapping` with `{"idType":"ID_ISIN","idValue":...,"exchCode":"US"}`. Without a key: 25 requests a minute, 10 jobs per request. With a free key: 250 a minute and 100 jobs. Delisted equities are excluded unless `includeUnlistedEquities` is set ([OpenFIGI API docs](https://www.openfigi.com/api/documentation)).
- **Terms:** Bloomberg "hereby dedicates FIGI Identifiers to the public domain". They may be used "for any purpose, commercial or non-commercial", and you may "use, display, reproduce, distribute and create derivative works". There is no clause on CUSIP or ISIN inputs ([OpenFIGI terms of service](https://www.openfigi.com/docs/terms-of-service)).
- **Measured:** 3,771 distinct equity ISINs (every US ISIN, plus all ISINs of the US-listed funds). 3,623 (96%) returned a US listing. Unkeyed, the run took about 45 minutes, because responses came back slower than the rate limit. A free key makes this a few minutes a quarter.
- Map FIGI's share-class tickers to SEC's style (`BRK/B` -> `BRK-B`).
- **Licensing caveat:** a US ISIN is `US` + CUSIP + a check digit, so sending it is sending a CUSIP. OpenFIGI's terms do not restrict this. Still: do not display ISINs or CUSIPs, and do not keep an ISIN -> CIK table. Store only the CIK-keyed weights (see method 3).

### 3. CUSIP -> CIK: no free official source, licence risk

- **SEC publishes no CUSIP -> CIK map.** The [Official List of Section 13(f) Securities](https://www.sec.gov/rules-regulations/staff-guidance/official-list-section-13f-securities) lists CUSIP and issuer name only, with no CIK. Older editions say the CUSIP numbers and descriptions are used with permission of the CUSIP Service Bureau, with no redistribution without permission. That wording was read in a search extract of [an older edition](https://www.sec.gov/pdf/table13f.pdf), not in the current list.
- **CUSIP Global Services (CGS):**
  - A licence is needed when an end user gets CGS data, directly or through an Authorized Data Vendor.
  - Firms using fewer than 500 identifiers get the fee waived, but "must still obtain a license".
  - Public-source CUSIP data may be collected for "non-commercial use, provided that the source of such information permitted the reproduction and use".
  - Under the terms of use, "Your only use of the CUSIP numbers and descriptions shall be in connection with the internal trading", and publishing or distributing the CUSIP database or subsets of it is prohibited.
  - Source: [CGS licence fees and terms](https://www.cusip.com/services/license-fees.html).
- CGS flow-down terms in vendor agreements say the same: any use "outside of the clearing and settlement of transactions requires a license", and there must be no "substitute master file". Examples: [MSRB subscription agreement](https://www.msrb.org/sites/default/files/2022-09/Subscription-Service-Agreement.pdf), [CME CGS terms](https://cmegroup.com/cusip-global-services-terms.html).
- **Community map:** [leoliu0/cik-cusip-mapping](https://github.com/leoliu0/cik-cusip-mapping) builds CIK-CUSIP pairs from Schedule 13D/13G filings. It has no LICENSE file and was archived on 2025-04-02.
- **Verdict:** joining by CUSIP adds nothing that method 2 does not already give, and a stored CUSIP table looks like a "subset" of the CUSIP database. Do not use it.

### 4. Name -> CIK: a fallback that works

- **How it was measured:** both sides normalized: upper case, `&` -> AND, punctuation and spaces removed, state suffixes (`/DE/`) removed, trailing legal-form words (Inc, Corp, PLC, AG, Holdings, ...) removed, Cos -> Companies. Only a name that maps to exactly one CIK counts as a match.
- **Against `company_tickers.json`:** 93–97% of US-fund weight matches. This includes SEC-registered foreign issuers (TSMC, ASML, SK hynix, HSBC, Toyota, Shell, Royal Bank of Canada, ...). Where name and FIGI both matched, they agreed in every case.
- **Against EDGAR's full name list:** [`cik-lookup-data.txt`](https://www.sec.gov/Archives/edgar/cik-lookup-data.txt) has about 1.06 million names, historical ones included, and is 40 MB. It recovers the delisted US names above. But it also returns entities that are not the issuer, or that have no SIC:
  - Samsung Electronics -> CIK 879316, "SAMSUNG ELECTRONICS CO LTD /FI", no SIC.
  - Siemens Energy AG -> "Siemens Energy AG/ADR", a depositary-receipt registrant.
  - Its CIK differed from FIGI's on 0.06–0.55% of weight.
  - Use it only as the last resort, and only accept a CIK whose submissions carry a SIC.

### 5. LEI -> CIK: does not work for operating companies

- **Terms:** GLEIF data is free and under CC0. Users must not imply that GLEIF endorses their product, and the GLEIF trademark needs permission ([LEI data terms of use](https://www.gleif.org/en/meta/lei-data-terms-of-use)).
- **What the records hold:** each LEI record has a registration authority (`registeredAt`) and an ID at that authority (`registeredAs`). GLEIF's RA list has **RA000665 = "EDGAR", Securities and Exchange Commission**, and when a record uses it, `registeredAs` would be the CIK ([GLEIF RA list v1.9, 2026-09-30](https://www.gleif.org/en/about-lei/gleif-registration-authorities-list)). But operating companies register with their state.
  - Surgery Partners: `RA000602` (Delaware Division of Corporations), `registeredAs` 5722138 ([GLEIF API](https://api.gleif.org/api/v1/lei-records?filter[lei]=549300LC82HG33S19I03)).
  - Measured over every LEI in the seven funds (via `api.gleif.org/api/v1/lei-records?filter[lei]=...`): RA000665 covered **0.00–0.07%** of equity weight. The largest authority was Delaware: 65% of VTI, 66% of SPY, 49% of IWM.
- **Other mappings:** LEI records link to BIC, MIC, OpenCorporates, S&P Global and others, but not to CIK. They carry no industry code (record fields: `bic, mic, ocid, qcc, gem, spglobal`). GLEIF and ANNA publish [daily ISIN-to-LEI files](https://www.gleif.org/en/lei-data/lei-mapping/download-isin-to-lei-relationship-files), which still end at an LEI.
- **EDGAR side:** submissions JSON has a `lei` field (`"lei": null` for Apple, [CIK0000320193.json](https://data.sec.gov/submissions/CIK0000320193.json)). It was filled for **18 of 5,581** matched CIKs.
- **Verdict:** LEI is no join key for CIK today. It stays useful as a stable issuer key for grouping non-SEC foreign issuers across funds.

## Measured match rate by weight

All figures are % of the fund's net assets (`pctVal`), excluding cash-collateral rows. "Placed" means a CIK was found **and** its EDGAR submissions carry a SIC code, so `SectorsClassifier` can give it an office and industry.

| Fund | Equity | CIK via ISIN->FIGI->ticker | + name in ticker file | + EDGAR name list | **Placed (CIK with SIC)** | …with SEC office (`ownerOrg`) | Distinct SIC | Equity not placed |
|---|---|---|---|---|---|---|---|---|
| VTI | 99.8 | 98.5 | 99.4 | 99.8 | **99.7** | 99.7 | 370 | 0.0 |
| SPY | 100.0 | 98.8 | 99.8 | 100.0 | **100.0** | 100.0 | 182 | 0.0 |
| QQQ | 99.9 | 99.4 | 99.4 | 99.9 | **99.9** | 99.9 | 54 | 0.0 |
| IWM | 99.8 | 97.4 | 98.3 | 99.5 | **99.4** | 99.4 | 309 | 0.4 |
| VXUS | 98.9 | 1.7 | 38.8 | 58.0 | **36.8** | 34.4 | 150 | 62.2 |
| VT | 99.3 | 61.8 | 76.2 | 83.6 | **75.6** | 74.6 | 328 | 23.7 |
| AIQ | 99.9 | 81.1 | 88.2 | 93.9 | **86.9** | 86.9 | 22 | 13.0 |

The columns are cumulative: each method only tries the rows the earlier ones missed.

- **Without OpenFIGI** (names only, still CIKs with SIC): VTI 95.1/95.6, SPY 95.5/95.7, QQQ 95.4/95.9, IWM 96.7/98.1, VXUS 32.3/36.7, VT 70.7/72.7, AIQ 81.0/81.0 (ticker file / plus EDGAR name list). So OpenFIGI adds about 4 points for US funds.
- **What is left unplaced in US funds:**
  - A handful of banks whose EDGAR record has no SIC (Bank OZK, Preferred Bank, Hingham: they file with bank regulators, not the SEC).
  - Names that left EDGAR's ticker file after the report date.
- **What is left in international funds:** every unplaced row is a foreign issuer.
  - "No CIK at all": VXUS 40.8%, VT 15.6%, AIQ 6.0%. Examples: Tencent, Roche, Nestlé, Siemens, Commonwealth Bank, Schneider, Allianz, MediaTek.
  - "CIK but no SIC": VXUS 21.3%, VT 8.1%, AIQ 7.0%. These are F-6 ADR registrants (`.../ADR`), Rule 12g3-2(b)-exempt issuers, or old filers (Samsung Electronics, SoftBank, Fast Retailing, BAE Systems, Kioxia, FANUC, Fujitsu).
- **Foreign issuers that file 20-F or 40-F do carry SIC and office.** TSMC is 3674 / 04 Manufacturing, ASML 3559 / 06 Technology, SK hynix 3674 / 04 Manufacturing ([TSMC submissions](https://data.sec.gov/submissions/CIK0001046179.json)).
  - They place cleanly in the basket view.
  - They have no `EntityPublicFloat`, so they are not in the market sunburst, which ranks 10-K filers by Public Float. Basket nodes can therefore appear that the market view does not have.

Method: the latest NPORT-P per fund, fetched with the SEC User-Agent "PersonalFinance research tngo0508@gmail.com" at about 6 req/s, and parsed with Python `xml.etree`. EDGAR submissions were fetched for all 5,581 matched CIKs. GLEIF was queried in batches of 100 LEIs, OpenFIGI in batches of 10 ISINs.

## Edge cases

### Foreign issuers that file no SEC reports (most of VXUS and VEA)

- No free, displayable source gives them a SIC, an SEC office, or any industry code:
  - GICS is proprietary ([market-data-sources.md](https://github.com/tngo0508/PersonalFinance/blob/research/market-data-sources/docs/research/market-data-sources.md)).
  - LEI records carry no industry.
  - OpenFIGI's `marketSector` is only "Equity".
- **Do not guess an SEC industry for them.** Put them in one top-level node, "Not classified by the SEC". Inside it, group by issuer country (`invCountry`, already complete per #232), then issuer.
- Group by LEI where there is one (93% of VXUS weight), so that one company held by two basket funds merges.
- For VXUS this node is about 62% of the fund, so the page should say so in the side panel. Something like: "SEC classifies only companies that file with it. 62% of this basket is foreign companies that do not."
- VEA was not measured. As a developed-markets-only fund, expect a share in this node similar to VXUS's (unverified).

### Bonds

- Bonds are not in the SEC office/SIC hierarchy, and should not be forced into it.
  - Treasuries, agencies and munis have no CIK.
  - Corporate bonds are issued by financing subsidiaries as often as by the parent.
- Use N-PORT `issuerCat` (corporate, U.S. Treasury, U.S. government agency, GSE, municipal, non-U.S. sovereign, ...) under a "Bonds" node.
- Corporate bonds could later be name-matched to the parent's CIK. Not measured: no tracked bond fund was checked here.

### "Other" rows of the 25-per-category trim

- The trimmed ETFs dataset cannot feed this view. In VT, 68.7% of common equity is in "Other" (#232).
- **Compute, per fund and quarter, the % of net assets by office and SIC (and by "Not classified" country) from every holding, before the trim.** The size per fund is one entry per SIC code: 370 for VTI, 328 for VT, 54 for QQQ, plus about 50 countries for the unclassified node.
- **Company leaves:** keep each fund's top N holdings with their CIK, or LEI for unclassified ones, so that a company held by two basket funds merges. Basket weight × fund % gives the company's basket weight.
  - The rest of each industry is that industry's total minus its listed companies. This is the basket's "Other" slice within the industry, and it stays exact, because the industry totals were computed before the trim.
  - The current ETFs builder keeps 25 per asset category. For this view, N per industry (or a weight threshold) fits better. That is a spec decision.

### Cash, derivatives, residual

- STIV (money funds, 0.27% of VTI, 0.74% of VXUS), futures and forwards (net ~0%), the fund-level `cshNotRptdInCorD`, and 100% − Σ`pctVal` go into a "Cash and other" node. The same rule as #232.

## Recommended join and fallback hierarchy

**Join, in the refresher, per holding with `assetCat` EC or EP:**

1. ISIN -> OpenFIGI (`exchCode=US`) -> ticker -> CIK via `company_tickers.json`.
2. Else, normalized issuer name -> unique CIK in `company_tickers.json`.
3. Else (optional), normalized name -> unique CIK in `cik-lookup-data.txt`, accepted only if its submissions have a SIC.
4. CIK -> SIC and office through the existing `SectorsClassifier`. It already gives an office to a filer with no `ownerOrg` from its SIC code.
5. Store only CIK-keyed and industry-keyed weights. Do not store or display CUSIP or ISIN.

**Display hierarchy for the basket view:**

1. SEC industry office -> SIC industry -> company (US filers, and foreign 20-F/40-F filers).
2. "Not classified by the SEC" -> issuer country -> issuer (foreign equities with no SIC).
3. "Bonds" -> issuer type -> (issuer).
4. "Cash and other" (STIV, derivatives, uninvested cash, residual).

If OpenFIGI turns out unsuitable (see gaps), steps 2–3 alone still place 95–98% of US funds. The loss is mostly in the largest names that left the ticker file, so treat this as degraded, not broken.

## Gaps and unverified items

- **OpenFIGI with US ISINs and CGS:** OpenFIGI's terms have no CUSIP clause. Whether CGS considers sending US ISINs (which embed the CUSIP) to a third-party mapping service a licensable "use" was not settled. CGS's own wording ("internal trading" only) is broad. If this matters, ask CGS, or drop step 1 and accept the name-only rates.
- **Which free key tier** applies to a multi-user app was not confirmed with OpenFIGI. The terms allow commercial use of FIGIs.
- **Accuracy of name matching** was checked only by agreement with FIGI (0 conflicts for the ticker-file match). There was no manual audit of the foreign name-only matches.
- **VEA and the tracked bond funds** were not measured.
- **The current 13(f) list's redistribution notice** was seen only in a search extract of older editions.
- **GLEIF's ISIN-to-LEI files** have no licence on their download page. Presumably they fall under the LEI data terms (CC0), but that is not verified. They are not needed for the recommended join.
- **SIC codes are SEC's assignment, not economic sector** (for example, ASML is 3559 "Special Industry Machinery"). Same caveat as the market sunburst. `SectorsClassifier.IsTechnology` already compensates for chip makers.
- Figures are as of the report dates above (2026-05-31 to 2026-07-31) and move each quarter. The delisted-name effect will vary.
