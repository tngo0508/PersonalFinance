# Do N-PORT holdings carry usable issuer country and currency?

Research note for [#232](https://github.com/tngo0508/PersonalFinance/issues/232), part of map [#230](https://github.com/tngo0508/PersonalFinance/issues/230).
Checked on **2026-10-24** against the SEC's Form N-PORT, its EDGAR XML technical specification and schema, and the latest public N-PORT filing of each tracked ETF. Each claim links its source. SEC terms (public information, free to display, 10 req/s, declared User-Agent) are covered in [market-data-sources.md](https://github.com/tngo0508/PersonalFinance/blob/research/market-data-sources/docs/research/market-data-sources.md) and not repeated here.

## TL;DR

- **Yes, every holding has a country and a currency, and none were blank or `XX`.** Across 12 filings (the latest two per fund, 32,700 holdings), every row had `invCountry` and a currency. The only non-ISO value was `N/A`, on 40 derivative rows in the latest filings (futures, total return swaps, FX forwards), worth under 0.01% of net assets.
- **The public country is "where the issuer is organized" (Item C.5.a), not where the business is.** The economic-exposure country (Item C.5.b, `invOthCountry`) is one of the items the SEC never publishes, so it is absent from every public filing. Filers may also use their own conventions, and they do, so **the same company gets different countries in different funds**. SPY puts Linde in IE and Chubb in CH. VTI and VT put both in US. AIQ puts TSMC, SAP, Alibaba and Baidu ADRs (7.5% of the fund) in US. QQQ puts the ARM ADR in GB. VT puts Tencent and Alibaba in HK, not CN or KY.
- **Currency is the denomination currency (Item C.2.b), so it shows listing currency, not economic currency.** ADRs and US-listed foreign companies are USD. For VT (38 currencies) this is close to country. For SPY, QQQ, VTI and IWM, currency is 100% USD.
- **The 25-per-category trim hides most of the exposure.** VT's top 25 common stocks show 4 of its 48 countries. 68.7% of VT's net assets sits in "Other", including 33.9% that is non-US. The snapshot has to carry **country and currency totals per fund, computed from every holding before the trim**. A basket's exposure is then the weighted sum of its funds' totals, which is exact.
- **Codes map cleanly. The vendored map is too coarse.** N-PORT uses ISO 3166-1 alpha-2 (plus `XX` and some historical codes). Each maps to a numeric id through the same `i18n-iso-countries` table already behind `IsoCountryCodes.cs` (its alpha-2 column). But `countries-110m` has no shape for **Hong Kong, Singapore, Bermuda, Cayman, Jersey, Guernsey, Isle of Man, BVI, Marshall Islands or Curaçao**. That leaves 2.8% of VT, 2.1% of AIQ and 3.2% of IWM with no shape. `countries-50m` (same package and licence, 236 shapes) covers all of these except Gibraltar (0.001% of IWM).
- **Two traps for the sum:** IWM's `pctVal` adds up to 116.8%, because 17.0% is securities-lending cash collateral reinvested in a money fund. Exclude collateral rows (`securityLending/cashCollateralCondition/@isCashCollateral="Y"`). Also, `exchangeRt` has no stated direction and filers disagree on it, so use `valUSD` or `pctVal` and never the rate.

## The fields

| Field (XML) | Form item | Meaning | Source |
|---|---|---|---|
| `invCountry` | C.5.a | "Report the ISO country code that corresponds to the country where the issuer is organized." Required on every `invstOrSec`. Type `ISO_COUNTRY_CODE` or `N/A`. | [Form N-PORT](https://www.sec.gov/files/formn-port.pdf), PDF p. 14 (printed p. 13). Schema `eis_NPORT_Filer.xsd`, `invstOrSec` (`minOccurs="1"`), in the [tech spec zip v1.7](https://www.sec.gov/info/edgar/specifications/form-n-port-xml-tech-specs-1.7.zip). |
| `invOthCountry` | C.5.b | "If different from the country where the issuer is organized, also report the ISO country code that corresponds to the country of investment or issuer based on the concentrations of the risk and economic exposure of the investments." Optional. | Same form page. Schema as above (`minOccurs="0"`). |
| (C.5.b is non-public) | General Instruction F | The SEC "does not intend to make public … country of risk and economic exposure (Item C.5.b)", which is why `invOthCountry` appeared 0 times in the 12 filings checked. | [Form N-PORT](https://www.sec.gov/files/formn-port.pdf), PDF p. 5 |
| `curCd` / `currencyConditional/@curCd` | C.2.b | "Indicate the currency in which the investment is denominated." USD goes in `curCd`. A non-USD currency goes in `currencyConditional` with an `exchangeRt` attribute (a schema choice: exactly one of the two). Currencies are ISO 4217 alpha-3. | Form PDF p. 13 and General Instruction G (p. 6). Schema `CONDITIONAL_ISO_CURRENCY_CODE_GROUP`. |
| `currencyConditional/@exchangeRt` | C.2.c | "If currency of investment is not denominated in U.S. dollars, provide the exchange rate used to calculate value." **No direction is specified.** | Form PDF p. 13. Tech spec PDF §3.4 (p. 3-34) lists it as `Decimal 24` with no definition. |
| `valUSD`, `pctVal` | C.2.c, C.2.d | Value in US$, and percentage of net assets. | Form PDF p. 13 |
| `cshNotRptdInCorD` | B.2.f | "Cash and cash equivalents not reported in Parts C and D". Fund level, with no country or currency. | Form PDF p. 8; schema element `cshNotRptdInCorD` |
| Instructions on methodology | General Instruction G | "Funds may respond to this Form using their own internal methodologies and the conventions of their service providers … consistently applied". If a sub-item does not apply, "respond N/A". | Form PDF p. 5 |
| Country code list | — | `ISO_COUNTRY_CODE` enumerates 273 values: ISO 3166-1 alpha-2, `XX` ("UNKNOWN"), and about 20 historical or non-ISO codes (`P8` Netherlands Antilles, `U2` Soviet Union, `Y2` Yugoslavia, `2F` East Timor, …). | `eis_ISO_StateCodes.xsd` in the tech spec zip |

The form PDF read was the version "effective December 11, 2023". The XML tech spec is v1.7 (June 2019), the current one on [the spec page](https://www.sec.gov/info/edgar/specifications/form-n-port-xml-tech-specs.htm).

## Measured coverage

Latest NPORT-P per fund, fetched 2026-10-24 (the previous filing gave the same picture, see the end of this section).

| Fund | Report date | Holdings | Σ pctVal | Country blank / `XX` | `N/A` country | Countries | Currencies | Cash not in C/D |
|---|---|---|---|---|---|---|---|---|
| SPY ([filing](https://www.sec.gov/Archives/edgar/data/884394/000141036826089410/primary_doc.xml)) | 2026-06-30 | 504 | 99.98% | 0 | 0 | 9 (US 97.0, IE 2.0, CH 0.3, BM, NL, LR, CW, SG, JE) | 1 (USD) | 0.24% |
| QQQ ([filing](https://www.sec.gov/Archives/edgar/data/1067839/000106783926000030/primary_doc.xml)) | 2026-06-30 | 105 | 100.28% | 0 | 1 (futures) | 6 (US 94.9, IE 2.0, NL 1.6, GB 0.8, CA 0.7, KY 0.2) | 1 (USD) | 0.19% |
| VTI ([filing](https://www.sec.gov/Archives/edgar/data/36405/000003640526000480/primary_doc.xml)) | 2026-06-30 | 3,546 | 100.47% | 0 | 13 (futures, TRS) | 2 (US 100.5, DK 0.00) | 1 (USD) | 0.00% |
| VT ([filing](https://www.sec.gov/Archives/edgar/data/857489/000085748926000275/primary_doc.xml)) | 2026-07-31 | 10,100 | 100.24% | 0 | 26 (futures, TRS, FX fwds) | 49 (US 62.7, JP 5.8, GB 3.3, TW 3.1, CA 3.0, HK 2.4, KR 2.2, …) | 38 (USD 62.9, EUR 7.3, JPY 5.8, GBP 3.2, TWD 3.1, …) | 0.23% |
| IWM ([filing](https://www.sec.gov/Archives/edgar/data/1100663/000207169126019749/primary_doc.xml)) | 2026-06-30 | 2,017 | **116.82%** | 0 | 0 | 22 (US 109.5 incl. 17.0 collateral, CA 1.6, BM 1.3, KY 0.9, …) | 2 (USD, CAD 0.001) | 0.13% |
| AIQ ([filing](https://www.sec.gov/Archives/edgar/data/1432353/000204825126006484/primary_doc.xml)) | 2026-05-31 | 89 | 100.44% | 0 | 0 | 11 (US 79.4, KR 11.9, DE 2.7, HK 2.1, TW 1.8, JP 1.5, …) | 8 (USD 79.4, KRW 11.9, EUR 3.3, HKD 2.1, …) | 0.15% |

- **Blank or `XX`:** 0 rows in all 12 filings. `XX` is allowed by the schema but was never used.
- **`N/A`:** only on derivatives with `assetCat` DE or DFE (E-mini futures, `TRS:` total return swaps, `CHF/USD FWD` forwards). Their net weight is −0.004% to +0.002% of net assets.
- **Previous filings** ([SPY](https://www.sec.gov/Archives/edgar/data/884394/000141036826055357/primary_doc.xml) 2026-03-31, [QQQ](https://www.sec.gov/Archives/edgar/data/1067839/000106783926000024/primary_doc.xml) 2026-03-31, [VTI](https://www.sec.gov/Archives/edgar/data/36405/000003640526000323/primary_doc.xml) 2026-03-31, [VT](https://www.sec.gov/Archives/edgar/data/857489/000085748926000188/primary_doc.xml) 2026-04-30, [IWM](https://www.sec.gov/Archives/edgar/data/1100663/000207169126015814/primary_doc.xml) 2025-09-30 amendment, [AIQ](https://www.sec.gov/Archives/edgar/data/1432353/000204825126003661/primary_doc.xml) 2026-02-28) gave the same result: 0 blank or `XX`, the same conventions, and IWM again over 100% (115.5%).

## Edge cases

- **Each filer applies its own country convention (allowed by General Instruction G).** The ISIN prefix (the issuer's country at listing) differs from `invCountry` on 0.1% of SPY, 1.7% of QQQ, 3.1% of VTI, 5.1% of VT and 7.2% of AIQ.
  - SPY follows C.5.a literally, so domicile: Linde IE, Chubb CH, Medtronic IE, SLB CW.
  - VTI codes every holding US, Irish-domiciled Linde, Eaton and Medtronic included. VT does the same for US-listed companies, which looks like an index-provider "nationality" rather than domicile.
  - VT puts Chinese companies listed in Hong Kong under HK (Tencent and Alibaba have KY ISINs). VT's CN weight is only 0.66% against HK 2.43%. **A China-vs-Hong Kong reading of the map would mislead.**
  - Airbus (NL ISIN) is FR in VT.
- **ADRs are coded inconsistently.** AIQ codes its ADRs (TSMC, SAP, Alibaba, Baidu, Ericsson, Nokia, Pony AI) US, 7.5% of the fund. VT codes its 48 ADRs (KB Financial, POSCO, iQIYI, …) US, 0.1% of VT. QQQ codes ARM's ADR GB and PDD KY. An ADR always has a US ISIN, so the ISIN cannot recover the underlying country. All ADRs are USD.
- **GDRs:** VT codes London-listed GDRs (Reliance Industries, State Bank of India) GB, in USD.
- **Cash:**
  - Uninvested cash is the fund-level `cshNotRptdInCorD` (0–0.29% of net assets), with no country or currency.
  - Money-market sweep funds appear as STIV holdings coded US/USD (VT 0.98%, VTI 0.70%, QQQ 0.35%).
  - IWM lists a CAD cash balance as an STIV row coded CA/CAD (0.001%). AIQ lists spot FX contracts as STIV rows coded HK/HKD.
  - Whatever is left of 100% after summing `pctVal` (liabilities, receivables) has no country.
- **Securities-lending collateral:** the reinvested cash collateral is a holding flagged `securityLending/cashCollateralCondition isCashCollateral="Y"`, coded US/USD.
  - IWM: 17.02% (BlackRock Cash Funds). QQQ: 0.35%. VTI: 0.44%. VT: 0.51%. AIQ: 0.56% (a BNY repo). SPY: none.
  - Left in, it inflates US by 17 points for IWM and pushes the total over 100%. The loaned securities stay listed as holdings (`isLoanByFund="Y"` on 1,024 IWM rows), so dropping the collateral does not drop exposure.
- **Derivatives:**
  - Futures and swaps carry `N/A` country, or the filer's choice (IWM's Russell 2000 E-mini is coded US).
  - Their `pctVal` is the unrealized gain, not the notional, so index futures (VT's MSCI EAFE and S&P/TSX 60 contracts) add ~0% to exposure even though they stand in for equities.
  - FX forwards (assetCat DFE) carry the currency but `exchangeRt="N/A"` and ~0% value.
  - None of this is material for the six tracked funds.
- **Exchange rate direction varies by filer.**
  - VT: HKD 0.1275, EUR 1.153 (US$ per unit).
  - AIQ: HKD 7.8374 (units per US$).
  - IWM: CAD 1.41825 (units per US$).
  - Ignore `exchangeRt`, since `valUSD` and `pctVal` already come in US$.
- **Report dates differ across funds.** N-PORT is public only for the third month of each fund's fiscal quarter ([Form N-PORT](https://www.sec.gov/files/formn-port.pdf) Instruction F). VT's latest is 2026-07-31, AIQ's 2026-05-31, the others 2026-06-30. A basket mixes dates. The current ETFs dataset already handles this by calendar quarter.

## Impact of the 25-per-category trim

`EtfsSnapshotBuilder.Summarize` keeps the 25 largest holdings of each asset category and sums the rest into `OtherPercent`. The summed rows have no country or currency, and the parser (`NportDocumentParser.ReadHolding`) does not read `invCountry` or the currency at all today.

| Fund | Common-equity weight in "Other" | …of which non-US | …of which non-USD | EC countries visible in top 25 |
|---|---|---|---|---|
| SPY | 48.8% | 2.9% | 0% | 1 of 9 |
| QQQ | 28.3% | 4.3% | 0% | 2 of 6 |
| VTI | 54.8% | 0% | 0% | 1 of 2 |
| VT | **68.7%** | **33.9%** | **33.8%** | **4 of 48** |
| IWM | 92.3% | 6.7% | 0% | 3 of 22 |
| AIQ | 25.4% | 5.6% | 5.6% | 4 of 11 |

So the trimmed dataset cannot produce a country or currency view. For VT, the top 25 show US, TW, KR and NL only. Japan (5.8%), the UK and Canada sit entirely in "Other".

**What the dataset needs instead:** for each fund and quarter, totals by country and by currency, computed in the refresher from every holding before the trim.

- Store a small map per fund, e.g. `{ "US": 62.74, "JP": 5.79, … }` and `{ "USD": 62.85, "EUR": 7.32, … }`: about 50 + 40 entries for VT and a handful for the others. That is a few KB per quarter for six funds, against storing 10,000 VT holdings.
- A basket's exposure is the weighted sum of its funds' maps (basket weight × fund % of net assets). Aggregation is linear, so the result is exact, and the page can compute it client-side when the user picks funds.
- In the refresher:
  - Drop cash-collateral rows.
  - Put `N/A`/`XX` country and the residual (100% − Σ `pctVal` + `cshNotRptdInCorD`) into an explicit "Unallocated / cash" bucket rather than spreading them.
  - Keep the raw filer codes. Do not re-map ADRs. Instead, say on the page that the country is the filer's reported country of organization.

## Country-code mapping to the world-atlas map

- N-PORT country codes are ISO 3166-1 **alpha-2** (`eis_ISO_StateCodes.xsd`). world-atlas `countries-110m` ids are ISO **numeric** strings (`"840"`). `IsoCountryCodes.cs` maps alpha-3 to numeric from the `codes.json` of `i18n-iso-countries@7.11.0` (MIT). The same file's alpha-2 column gives alpha-2 to numeric directly, so no new dependency is needed. Every code seen in the six latest filings (62 codes) is in that table.
- Schema codes that cannot map: `XX`, and historical or non-ISO codes (`P8`, `U2`, `Y2`, `I8`, `G5`, `2F`, …). None appeared in the filings. Bucket them as "Unallocated".
- **No shape in `countries-110m`:** 75 schema codes have an ISO numeric id but no 110m shape (174 of its 177 shapes have an id; N. Cyprus, Somaliland and Kosovo have none). The ones that hold fund money:

| Fund | Weight on codes with no 110m shape | Codes |
|---|---|---|
| SPY | 0.42% | BM 0.16, CW 0.11, SG 0.09, JE 0.05 |
| QQQ | 0.22% | KY 0.22 |
| VTI | 0% | — |
| VT | **2.82%** | **HK 2.43**, SG 0.39 |
| IWM | **3.24%** | BM 1.34, KY 0.85, MH 0.42, VG 0.29, JE 0.18, GG 0.14, SG 0.03, IM, GI |
| AIQ | 2.06% | HK 2.06 |

- `world-atlas@2.0.2/countries-50m.json` (same package, ISC, already allowed by libman/unpkg; 756 KB raw against 110m's ~105 KB) has 236 shapes and covers every code above except Gibraltar (0.001% of IWM). Options:
  - switch the World tab to 50m (payload cost on mobile);
  - keep 110m and draw small-territory markers (dots at a centroid list) for HK, SG and the offshore centres;
  - list them in a side table.

  In every case, show what is off the map rather than dropping it silently.

## Gaps and unverified items

- **Only six funds, two filings each, were measured.** The "0 blank / `XX`" result holds for these sponsors (SSGA, Invesco, Vanguard, BlackRock, Global X). Other sponsors or bond funds may use `XX` more. Not checked.
- **Why Vanguard codes Linde US** is inferred (an index-provider nationality convention, allowed by Instruction G). Vanguard's Part E notes were not read.
- **The economic country of ADRs cannot be recovered from public N-PORT.** One possible fix, not verified: look up each holding's LEI (present on most rows) in GLEIF's free LEI data for the legal-address country. That would still give domicile, not economic exposure. A free ADR-to-underlying mapping was not found.
- **2024 amendments to N-PORT** (more frequent public disclosure) were not checked for whether they change C.5.b's non-public status or the field set. The form PDF read is the 2023-12-11 version.
- **50m payload and rendering cost** on mobile were not measured beyond file size.
- The exact percentages are as of the report dates above and will move each quarter.

Method: filings fetched with a declared SEC User-Agent at under 5 req/s. Parsed with Python `xml.etree`, summing `pctVal` by `invCountry` and by currency (`curCd` or `currencyConditional/@curCd`), and by `assetCat` for the trim check. Collateral was found via `securityLending/cashCollateralCondition`. The shape check compared alpha-2 codes, via `i18n-iso-countries@7.11.0/codes.json`, against the ids in the vendored `countries-110m.json` and in `countries-50m.json`.
