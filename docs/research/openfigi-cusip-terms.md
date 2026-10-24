# Can the refresh send holdings' ISINs to OpenFIGI without a CUSIP licence?

Research note for [#246](https://github.com/tngo0508/PersonalFinance/issues/246), part of map [#230](https://github.com/tngo0508/PersonalFinance/issues/230).
Checked on **2026-10-24** against OpenFIGI's terms, API docs and FAQ, the OMG FIGI specification v1.3, and CUSIP Global Services' (CGS) own pages and its October 2024 comment letter. Each quote links its source. **This is a reading of the terms, not legal advice.**

Not repeated here: the join itself, the match rates, and why LEI and CUSIP -> CIK fail. Those are in [holdings-to-sec-industry.md](https://github.com/tngo0508/PersonalFinance/blob/research/holdings-to-sec-industry/docs/research/holdings-to-sec-industry.md) (#243). SEC's own terms (public information, 10 req/s, declared User-Agent) are in [market-data-sources.md](https://github.com/tngo0508/PersonalFinance/blob/research/market-data-sources/docs/research/market-data-sources.md).

The pipeline being tested: the refresh job (server-side, quarterly) reads ISINs from public Form N-PORT filings. It sends them to OpenFIGI's mapping API to get tickers, then maps ticker -> CIK through SEC's `company_tickers.json`. It stores only per-fund weights keyed by CIK, SIC and country. It never stores or shows a CUSIP or an ISIN.

## TL;DR

- **OpenFIGI allows this kind of use, and it is free.** The API is "free and open to the public". A free key raises the mapping limit from 25 to 250 requests a minute, and from 10 to 100 jobs per request. Nothing forbids automated server-side use: the FAQ suggests bulk mapping through the API. The terms say nothing about caching results.
- **The FIGI licence covers the output, not the input.** Bloomberg dedicates FIGIs to the public domain, and the OMG standard puts FIGI and its "associated metadata" under the MIT licence. But that same standard says ISIN <-> FIGI mappings "cannot be freely redistributed" because of "licensing restrictions on ISIN (though not on FIGI)". CGS also argues the public-domain dedication covers only the FIGI string, not the reference data such as tickers and names.
- **A US ISIN is CGS data.** CGS says "A CUSIP or CINS identifier will always appear in positions 3-11 of a CGS ISIN", and its site terms list "CGS ISINs" in the CUSIP Database.
- **No CGS clause found names this flow as licensable, and none clearly allows it.** CGS says a licence is required when an end user (a) subscribes to a CGS product or (b) "obtains the benefit of CGS Data indirectly through one of CGS's Authorized Data Vendors". The refresh does neither: its ISINs come from SEC filings, and OpenFIGI returns no CUSIPs. Two points stay open:
  - The free-use exception for public sources is limited to **"non-commercial use"**.
  - CGS's general disclaimer says use "including storage in databases ... **may** require" a licence.
- **Verdict: usable, with conditions.**
  - Keep each ISIN only in memory, for the length of one refresh run.
  - Never persist or cache an ISIN -> FIGI/ticker/CIK table.
  - Persist only CIK-, SIC- and country-keyed weights.
  - Send OpenFIGI only the ISINs that name matching could not resolve.
  - Treat OpenFIGI as optional: if it is unavailable, or if CGS objects, the refresh falls back to name matching.
- **What the fallback loses:** name matching alone places about 4 points less of US-fund weight: VTI 95.6% vs 99.7%, SPY 95.7% vs 100%, QQQ 95.9% vs 99.9%, IWM 98.1% vs 99.4%. The gap is AIQ 5.9 points, VT 2.9 and VXUS 0.1. The lost weight shows up as "not classified" companies, not as wrong classifications.
- **Certainty needs CGS in writing.** No public page settles the question. If the app is, or becomes, commercial, ask CGS before relying on OpenFIGI with US ISINs.

## OpenFIGI terms

| Question | Finding | Source |
|---|---|---|
| Free, automated use | "The API is free and open to the public, though unauthenticated traffic will be subject to a lower rate-limit." The FAQ: "Bulk mapping can also be done via the free web-based mapping API at a rate limit of 25,000 jobs per minute." (That is the keyed limit: 250 requests × 100 jobs.) No clause bans automated or server-side calls. | [API docs](https://www.openfigi.com/api/documentation); [FAQ](https://www.openfigi.com/about/faq) |
| API key | "You can sign up for an OpenFIGI account to obtain an API key which provides a higher rate-limit." The key goes in the `X-OPENFIGI-APIKEY` header. The signup page shows no extra terms for keys. Keep the key in configuration or Key Vault, never in source. | API docs; [signup](https://www.openfigi.com/user/signup) |
| Rate limits | "Rate limits are applied per API key, or per IP address when no API key is sent. Each rate-limiting window is 60 seconds long". `/v3/mapping`: 25 requests a minute without a key and 250 with one; 10 jobs per request without a key and 100 with one. `/v3/search` and `/v3/filter`: 5 a minute without a key and 20 with one. A 429 response means you should "Resend the request later with an exponential backoff strategy". | API docs |
| Delisted equities | "Mapping and search requests return only listed equity instruments unless `includeUnlistedEquities` is set to `true`." | API docs |
| Caching or storing results | **The terms say nothing about caching.** The FAQ, on storing FIGIs in a database or sharing them: "There is no cost recovery, licensing or re-use restrictions or hidden fees for access, use, or redistribution of the FIGI symbology." This covers FIGIs, not the ISIN you sent. | [Terms of service](https://www.openfigi.com/docs/terms-of-service) (Last Updated: November 27, 2018); FAQ |
| Third-party identifiers | "Unfortunately, licensing and redistribution restrictions prevents the API from returning 3rd party proprietary identifiers." So OpenFIGI accepts `ID_ISIN` and `ID_CUSIP` as input, but never returns a CUSIP or ISIN. | FAQ; API docs (ID type list) |
| Trademarks | You may make fair use of "FIGI" and "OPENFIGI", but must not imply that Bloomberg endorses you (s. 4). | Terms of service |
| Warranty | FIGIs and related security descriptions are "AS IS". Liability is capped at US$50 (s. 3). | Terms of service |

## The FIGI licence: what it covers

- **Bloomberg's dedication (OpenFIGI terms, s. 1):**
  - "Bloomberg ... hereby dedicates FIGI Identifiers to the public domain and makes FIGI Identifiers available to the public at large for free to facilitate trading, processing and reporting of financial transactions, and research on securities and other financial instruments."
  - "FIGI Identifiers may be freely reproduced, distributed, transmitted, used, modified, built upon, or otherwise exploited by anyone for any purpose, commercial or non-commercial".
  - Section 5: "under no circumstance shall Bloomberg terminate, modify or repudiate the license grant in paragraph 1."
  - ([Terms of service](https://www.openfigi.com/docs/terms-of-service))
- **Scope of the dedication:**
  - The text dedicates "FIGI Identifiers". It defines these as "a unique string of alphanumeric characters".
  - Section 3 separately names "the descriptions of the associated securities" ("Related Security Descriptions"), such as ticker and name, without dedicating them.
  - CGS reads it the same way: "OpenFIGI's public domain dedication is limited to the FIGI identifier itself; the dedication does not extend to reference data." (CGS comment letter, 2024-10-21, footnote 16; [FDIC copy](https://www.fdic.gov/federal-register-publications/cusip-global-services-scott-j-preiss-rin-3064-af96))
- **The standard's licence (OMG FIGI v1.3, formal/26-09-09, September 2026; [spec page](https://www.omg.org/spec/FIGI/), [PDF](https://www.omg.org/spec/FIGI/1.3/PDF)):**
  - Table 8-2 (p. 19) gives `dct:license` as the MIT licence text: "Copyright (c) 2013-2026 Bloomberg LP / Copyright (c) 2015-2026 Object Management Group / Permission is hereby granted, free of charge, ...". It cites "https://opensource.org/licenses/MIT".
  - Annex D.6 (p. 39): FIGIs "are freely redistributable, can be used, transmitted, databased, stored, enriched or otherwise utilized without restrictions aside from ensuring that the MIT Open Source permissions are included and disclosed. As such, there are no restrictions on associating or 'mapping' FIGI or the associated metadata to other standards".
  - Same annex: "other standards or identifiers in the marketplace may carry some level of licensing or restrictions ... users should be aware that the MIT Open Source license would not override or invalidate those licenses or restrictions."
  - **Annex D.2, on ISIN (p. 38):** "Organizations that are licensed to use ISIN are free to map between FIGI and ISIN at the Global share class level. Indeed, this is a common practice. Owing, however, to licensing restrictions on ISIN (though not on FIGI), such mappings cannot be freely redistributed."
  - The OpenFIGI FAQ also says the standard "incorporates the MIT open source license within the standard itself - ensuring the identifier and associated metadata remains open source."
- **Reading:**
  - The FIGI and the ticker that OpenFIGI returns are free to use and store under the MIT licence ("associated metadata"), or under the public-domain dedication for the FIGI itself. Keep the MIT notice in the repo if FIGI metadata is stored or shipped. Storing only a CIK, which comes from SEC's file, avoids even that.
  - The FIGI licence does nothing for the ISIN input. The standard's own authors expect ISIN users to be "licensed to use ISIN", and say an ISIN <-> FIGI mapping is not freely redistributable.

## CGS's position on derived use and ISINs

**A US ISIN carries a CUSIP, and CGS claims it.**

- "A CUSIP or CINS identifier will always appear in positions 3-11 of a CGS ISIN." "CGS is responsible for the assignment of ISINs in the U.S. and in other areas where designated or appointed." ([About CGS Identifiers](https://www.cusip.com/identifiers.html))
- Site terms: "CGS, pursuant to an agreement with the ABA, has the right to license all proprietary rights to the CUSIP database ("CUSIP Database"), which contains CUSIP standard identifiers, CUSIP standard descriptions, CGS ISINs, CGS CINS and other information about financial instruments". These Terms of Use were last updated on 1 March 2022. They are shown on every cusip.com page; read here on [License Fees](https://www.cusip.com/services/license-fees.html).
- CGS letter: "Since 1986, CUSIP has been integral to the interconnected global markets through ISIN because CUSIP is the national identifier used in ISIN for over 30 countries, including the United States and Canada." (footnote 8)
- **Effect on non-US ISINs:** ISINs from other numbering agencies (JP, GB, DE, ...) carry no CUSIP. CGS-assigned ISINs for Canada, Bermuda, the Cayman Islands and other CGS jurisdictions do (identifiers page).

**When CGS says a licence is required.** All quotes below are from the "CGS Licensing Policies FAQs" on [License Fees](https://www.cusip.com/services/license-fees.html):

- "A CGS License is required when: (a) an end user customer subscribes to a particular CGS product or service, and/or (b) an end user customer obtains the benefit of CGS Data indirectly through one of CGS's Authorized Data Vendors."
- "CGS also waives the licensing fees for end user customers that use fewer than 500 unique CGS Identifiers, although such customers must still obtain a CGS License."
- On public sources: "CGS Data may appear in some offering documents and other sources. End users can collect this information themselves for "non-commercial use, provided that the source of such information permitted the reproduction and use of such information." However, CGS notes that "CGS Data generally has not come from publicly available sources ..."".
- Display-only exception: users with "read- or display-only access to CGS Data by virtue of an Authorized Data Vendor's online screen display" need no licence, "provided that they do not export the CGS Data into any other application". This does not apply here.
- Disclaimer shown on the same page: "The use of CGS Data, including storage in databases and distribution to third parties, may require such user to first enter into a license agreement directly with CGS".

**The "internal trading" clause is part of the cusip.com Terms of Use.** It governs people who use CGS's websites and their content: "By accessing and using any of CGS's websites ... you agree to be legally bound by these Terms of Use". The clause:

> "You agree that you shall not publish or distribute in any medium the CUSIP database or any information contained therein or summaries or subsets thereof to any person or entity. Your only use of the CUSIP numbers and descriptions shall be in connection with the internal trading and settlement of security transactions. You further agree that the use of CUSIP numbers and descriptions is not intended to create or maintain ... a master file or database of CUSIP descriptions or numbers".

[holdings-to-sec-industry.md](https://github.com/tngo0508/PersonalFinance/blob/research/holdings-to-sec-industry/docs/research/holdings-to-sec-industry.md) quoted this clause as CGS's general rule. It binds users of the cusip.com site. The refresh never touches that site. The same wording also appears as flow-down terms in vendor agreements (MSRB, CME), cited in that note. It shows how CGS wants CUSIPs used, but it is not a term the refresh has agreed to.

**CGS on public sources, in its own letter (2024-10-21):**

- "CUSIP identifiers and related information also are available without license or fee from certain public sources." (footnote 30, citing the Licensing Policies FAQ)
- "It is CGS's stated policy to not charge subscribers to its database services where the subscriber's sole usage is to fulfill regulatory reporting requirements."
- The rest of the letter argues against the FDTA proposal making FIGI the only identifier. It does not discuss third parties sending ISINs or CUSIPs to OpenFIGI.

**No CGS statement was found on OpenFIGI mapping.** I found no public CGS page, FAQ or letter saying whether sending CUSIPs or US ISINs to OpenFIGI or another third-party mapping service is licensable. The OMG annex D.2 sentence above is the closest statement. It comes from Bloomberg's side, and it is about redistributing mappings, not making them.

**Historical, not applicable:** in EU case COMP/39.592, S&P (then CGS's operator) committed not to charge EEA end users for US ISIN records. The commitments covered "Use within the EEA" only, ran "for a duration of five (5) years", and "automatically expire ... if MGH or S&P ... no longer operates as the US NNA". FactSet has run CGS since 2022 (License Fees page). Source: [draft commitments, April 2011](https://ec.europa.eu/competition/antitrust/cases/dec_docs/39592/39592_1727_3.pdf). The case shows that CGS treats US ISINs as licensable CGS data.

## Verdict for this pipeline

Each question from the ticket, read against the sources above:

| Ticket question | Answer |
|---|---|
| OpenFIGI terms for automated server-side use, the key, rate limits and caching | **Allowed and free.** Get a free key (250 req/min, 100 jobs per request). About 3,800 ISINs is about 38 requests, under a minute a quarter. Back off on 429. The terms are silent on caching. FIGIs and their metadata may be stored under the MIT licence or the public-domain dedication. |
| Is OpenFIGI's ISIN -> FIGI/ticker mapping free of CUSIP licensing? | **The output, yes. The input, no.** FIGI and its metadata are open. The ISIN you send stays CGS data, and the FIGI standard says ISIN <-> FIGI mappings "cannot be freely redistributed". |
| Does reading ISINs from public N-PORT filings and sending them to OpenFIGI, without storing or showing them, trigger a CUSIP licence? | **No clause found says it does, and none clearly says it doesn't.** It meets neither of CGS's stated triggers: no CGS product, and no Authorized Data Vendor as the source. SEC lets its public information be copied. Nothing is published, and no CUSIP or ISIN master file is kept. The risks: CGS's public-source exception says "non-commercial use"; its disclaimer says storage "may" need a licence; and CGS has not addressed third-party mapping. |
| Fallback match rate without OpenFIGI | See the next section: about 4 points of US-fund weight lost, 0.1–5.9 points for international funds. |

**Conditions if OpenFIGI is used** (put them in the spec):

1. **Transient only.** Read the ISIN from the filing, send it in the mapping request, and drop it once the CIK is found, all within one refresh run. No ISIN, CUSIP or ISIN -> anything table in the database, cache, logs or snapshot payload. Log counts, never identifiers.
2. **Persist only CIK-, SIC- and country-keyed weights**, as #243 already says. The ticker -> CIK step uses SEC's public file, so the stored CIK is SEC data.
3. **Name match first, OpenFIGI second.** Send OpenFIGI only the ISINs whose issuer name did not resolve uniquely in `company_tickers.json`. That is a few hundred ISINs a quarter instead of about 3,800. It reduces the CGS data handled. It does not by itself make a licence unnecessary: under 500 identifiers waives the fee, but CGS says such users "must still obtain a CGS License".
4. **Non-US ISINs carry no CUSIP.** For international funds, sending only non-CGS ISINs (not `US`, `CA`, `BM`, `KY`, ...) would avoid CGS data entirely. It gains little: OpenFIGI with `exchCode=US` resolved few foreign issuers (VXUS 1.7%, per #243).
5. **A failed or blocked OpenFIGI call must not fail the refresh.** Fall back to name matching and record the reduced coverage.
6. **Credit:** no attribution is required for FIGI. Do not use the FIGI or OpenFIGI marks in a way that implies Bloomberg endorses the app.

**When to ask CGS first:** if the app charges users, carries ads or is otherwise commercial, the "non-commercial use" wording of CGS's public-source exception does not clearly cover it. The safe options are then a written answer from CGS (contact via cusip.com) or name-only matching.

## The fallback: name matching only

Figures from #243 (latest N-PORT per fund, report dates 2026-05-31 to 2026-07-31). Placed = CIK found and its EDGAR record has a SIC. Name matching = normalized issuer name -> unique CIK in `company_tickers.json`, then EDGAR's `cik-lookup-data.txt` (accepted only with a SIC).

| Fund | Placed with OpenFIGI first | Placed, names only | Lost without OpenFIGI |
|---|---|---|---|
| VTI | 99.7 | 95.6 | 4.1 |
| SPY | 100.0 | 95.7 | 4.3 |
| QQQ | 99.9 | 95.9 | 4.0 |
| IWM | 99.4 | 98.1 | 1.3 |
| VXUS | 36.8 | 36.7 | 0.1 |
| VT | 75.6 | 72.7 | 2.9 |
| AIQ | 86.9 | 81.0 | 5.9 |

What is lost:

- **Mostly placement, not correctness.** In #243 the ticker-file name match never disagreed with FIGI where both found a CIK. The EDGAR full-name list disagreed on 0.06–0.55% of weight, so it is accepted only as a last resort and only with a SIC. Most lost rows fall into "Not classified" rather than a wrong industry.
- **Which names:** issuer names in N-PORT that do not normalize to a unique SEC title (punctuation, abbreviations, share classes, names shared by several CIKs), and companies whose names changed. #243 did not list the missed names one by one for the name-only run.
- **How visible it is:** for VTI, SPY and QQQ, about 1 in 25 dollars ends up unclassified instead of almost none.
- **Improving the fallback without OpenFIGI (not measured):**
  - A hand-maintained alias list (issuer name -> CIK) for the largest misses, stored in configuration. That is SEC data and contains no CUSIP.
  - OpenFIGI's `/v3/search` by issuer name (20 req/min with a key). It sends no ISIN, but it is slower and fuzzier.

## Gaps and unverified items

- **No CGS ruling on this exact flow.** No public CGS document addresses sending US ISINs from public filings to a third-party mapping service. Only CGS can settle it.
- **Whether the app counts as "commercial" use** was not determined. The repo does not say whether the app will charge or carry ads. The answer decides whether CGS's public-source exception applies.
- **Whether Bloomberg/OpenFIGI is a CGS Authorized Data Vendor**, and whether its own CGS licence covers ISIN input from the public, is not public. OpenFIGI returns no CUSIPs, so trigger (b) seems not to apply, but this is unverified.
- **OpenFIGI terms on caching and acceptable use of the API itself:** none found beyond the FIGI dedication and the rate limits. The signup page showed no extra key terms when fetched (the page is script-rendered, so terms shown after signup were not seen).
- **ANNA's position on ISIN licensing** was not found on a primary ANNA page. Only search snippets and secondary reports turned up, so they are not used here.
- **The EU commitments** were read in the April 2011 draft for market test. The final commitments decision (15 Nov 2011) was not opened. It is not needed: the commitments covered only the EEA, and their term has ended.
- **CGS's 2024 letter** was read from the FDIC's copy. Whether the agencies kept FIGI in the final FDTA rule was not checked. It does not change the licence reading.
- **The fallback figures** are from #243's single run. The name-only misses were not itemized, and the alias-list and `/v3/search` improvements were not measured.
