# Which free sources may the Atelier draw on?

Research for [#492](https://github.com/tngo0508/PersonalFinance/issues/492), under the map [#491 Learning page (Atelier)](https://github.com/tngo0508/PersonalFinance/issues/491). Checked 2026-10-27 against each site's own terms, policy and API pages. Not legal advice: these are the published terms, read as written.

**Context.** OpenAI drafts Term Entries (ETF, margin interest, short selling, capital gains tax, ...) only from an owner-kept allowlist of Approved Sources. Every entry cites its sources, and the owner reviews it before it is published. No open web search at runtime.

## Answer in one screen

| Source | Licence of its own content | AI paraphrase with citation? | Official API / feed | Languages | Verdict |
|---|---|---|---|---|---|
| Investor.gov (SEC) | SEC: may be copied without permission, citation asked | **Yes** | None for education content | EN, ES | **Approve: draft + cite** |
| IRS | Federal-employee content is not copyrighted, credit asked | **Yes** | No API for content; email bulletins | EN, ES, **VI**, others | **Approve: draft + cite** (US tax notes) |
| CFPB | Public domain, citation asked | **Yes** | Complaint-data API only (no Ask CFPB API) | EN only now (language hub redirects to the homepage) | **Approve: draft + cite** (credit and banking terms) |
| FINRA | All rights reserved; non-commercial only; **ML/AI use banned** | **No** | Developer API is for registration/market data, not education | EN | **Link-only** |
| SSC / UBCKNN (Vietnam) | No terms or copyright notice found | Unconfirmed: paraphrase lightly, cite, link | None found (RSS link returns 404) | VI, EN (partial) | **Approve with limits: concept + cite + link** (Vietnam notes) |
| YouTube (Data API v3 + embed) | Each video belongs to its creator; YouTube terms apply | **No** (no transcripts, no downloading) | Data API v3, API key, free quota | Per video | **Embed/link-only**, metadata cached ≤ 30 days |

## Investor.gov (SEC)

- **Reuse.** Investor.gov is an SEC site, and its footer "Privacy" link goes to `https://www.sec.gov/privacy.htm` (checked in the page HTML of https://www.investor.gov/). The SEC's policy says information on sec.gov "may be copied or further distributed by users of the web site without the SEC's permission", and asks for "appropriate citation to the SEC as the source" (https://www.sec.gov/privacy). That page speaks of sec.gov; the SEC's general federal-work status is the basis for applying it to Investor.gov, but **Investor.gov has no reuse statement of its own that I could find** (its `/privacy-policy` page carries none, and `/terms-of-use` and `/website-policies` return 404).
- **Limits.** "Please do not use the SEC seal or any of the other logos or artwork from this site" (https://www.sec.gov/privacy). Investor.gov's own leave-site notice says the SEC does not endorse external sites (https://www.investor.gov/privacy-policy, page script), so never imply SEC endorsement of the Atelier.
- **API / feed.** None for education content. The SEC's RSS list has press releases, speeches, litigation and trading suspensions, but no Investor.gov or investor-alert feed (https://www.sec.gov/about/rss-feeds).
- **Automated access.** The SEC limits clients to "no more than 10 requests per second" and blocks "unclassified" bots (https://www.sec.gov/privacy). Its EDGAR guidance asks automated clients to "declare your user agent in request headers" (https://www.sec.gov/search-filings/edgar-search-assistance/accessing-edgar-data). Not confirmed whether the same limits apply to the investor.gov host.
- **Fit.** Best fit for investing terms: a full A–Z glossary at https://www.investor.gov/introduction-investing/investing-basics/glossary (e.g. `.../glossary/exchange-traded-fund-etf` resolves). Spanish at https://www.investor.gov/informacion-en-espanol. **No Vietnamese found.**

## IRS

- **Reuse.** Content "created or maintained by federal employees in the course of their duties is not subject to copyright and may be freely copied". "Credit is requested." Third-party copyrighted material on or linked from the site needs the holder's permission (https://www.irs.gov/about-irs/use-of-content-from-irsgov, reviewed 01-Apr-2026).
- **Limits.** Federal law bars using Treasury/IRS seals, emblems or names in a way that could convey "the false impression" of approval or association (same page; cites 18 U.S.C. § 701, 31 U.S.C. § 333, 31 C.F.R. Part 37).
- **API / feed.** No content API found. IRS news goes out as email/text subscriptions (IRS Tax Tips, Newswire) (https://www.irs.gov/newsroom/irs-news-bulletins). An RSS feed could not be confirmed (`/downloads/rss` returned 403).
- **Languages.** Vietnamese homepage at https://www.irs.gov/vi. It notes the English version is the authoritative one. Spanish news also exists.
- **Fit.** US tax notes (capital gains tax, wash sales, IRA limits). Rules change yearly, so cite the tax year.

## CFPB

- **Reuse.** "Information created by the CFPB is in the public domain and you may reproduce, publish, or otherwise use it without the Bureau's permission." It asks: "Please consider appropriate citation to the Bureau as the source." Third-party copyrighted material on the site keeps its own rights. The CFPB logo and name are registered trademarks (https://www.consumerfinance.gov/privacy/website-privacy-policy/, "Legal notices > Copyright and trademark").
- **API / feed.** The Consumer Complaint Database has an open data API (https://www.consumerfinance.gov/data-research/consumer-complaints/, docs https://cfpb.github.io/api/ccdb/api.html). Its docs do not state keys or rate limits. **Ask CFPB** ("clear, impartial answers to hundreds of financial questions", 12 categories) has no API or feed (https://www.consumerfinance.gov/ask-cfpb/).
- **Languages.** As of 2026-10-27, `https://www.consumerfinance.gov/language/` and `/language/vi/` and `/es/` all redirect to the homepage. The Vietnamese glossaries a 2024 CFPB presentation described (https://files.consumerfinance.gov/f/documents/cfpb_multilingual-resources-presentation_2024-09.pdf) **could not be found on the live site**. Treat CFPB as English-only.
- **Fit and caveat.** Consumer credit terms (APR, credit score, overdraft, debt collection), not investing. The site has been restructured (its homepage now leads with a deregulatory agenda), so pages may move or disappear. Keep the citation's access date, and recheck links during review.

## FINRA — link-only

- **Terms** (https://www.finra.org/terms-of-use): content "shall be used ONLY for your own non-commercial personal or professional use". No reproduction without prior written consent, except fair use. No scraping, robots or database-building. It also bans use "in conjunction with any machine learning, neural network, deep learning" or similar tools.
- **Reprints** need written permission and must be "exactly as provided by FINRA". Use is educational only, and reprints must not promote the sender's services (https://www.finra.org/investors/investor-alert-reprint-policy; requests: https://www.finra.org/contact-finra/permission-use-finra-copyrighted-material).
- **Linking** is allowed and encouraged (reprint policy). The terms require no framing, no implied sponsorship, and removal within five days if FINRA revokes permission (terms of use).
- **API.** The FINRA API Platform (https://developer.finra.org/) offers registration and market datasets behind a gateway account. It has no education content, and it has its own terms and fees pages, which I did not review.
- **Rule.** Never fetch FINRA pages into the drafting prompt, and never paraphrase them with AI. A Term Entry may carry a plain "Further reading" link (for example to FINRA's margin or short-selling Investor Insights). The owner may still write their own summary by hand.

## SSC / UBCKNN (Vietnam)

- **Site.** https://ssc.gov.vn/webcenter/portal/ubck (Vietnamese), with an English portal at https://ssc.gov.vn/webcenter/portal/ssc. It is an Oracle WebCenter portal that needs JavaScript (plain HTTP fetches get a "requires JavaScript" stub). I read it in a browser.
- **Reuse terms.** **None found.** The footer gives the press licence (300/GP-BC), the responsible person and contacts. It has no copyright or "ghi rõ nguồn" (cite the source) notice, and the "Hướng dẫn sử dụng" (user guide) page is empty.
- **Vietnamese law (secondary sources only).** Article 15 of the Law on Intellectual Property excludes legal and administrative documents of state bodies, as well as concepts, principles and data, from copyright protection. This is summarised from law-firm and university write-ups (e.g. https://thuvienphapluat.vn/hoi-dap-phap-luat/cac-doi-tuong-khong-thuoc-pham-vi-bao-ho-quyen-tac-gia-235097.html), **not from the official text on vbpl.vn**, and the version after the 2025 amendment is unconfirmed. The SSC's explanatory Q&A and "Kiến thức chứng khoán" (securities knowledge) articles are probably not "văn bản" (official documents) in that sense. Assume they are protected.
- **Content.** The "Kiến thức chứng khoán" section has Q&A for investors (e.g. foreign individual investors, Aug 2026), plus legal texts (Securities Law, decrees, circulars) and market statistics.
- **API / feed.** None found. The footer's RSS link (`/pages_r/f/rss`) leads to a 404 login page.
- **Rule.** Use it as the citation for *In Vietnam* notes. Draft the concept in the model's own words, quote laws by article number, add little or no paraphrase of the SSC's prose, and always link. Ask the SSC (contact page `/pages_r/m/giithiu/linh`) before any closer reuse.

## YouTube (Data API v3, embeds) — embed/link-only

- **Auth / cost.** "Every request must either specify an API key (with the `key` parameter) or provide an OAuth 2.0 token" (https://developers.google.com/youtube/v3/docs). OAuth is only required for writes and private data. There is no fee: the default quota is 10,000 units a day across endpoints, plus a separate bucket of 100 `search.list` calls a day at 1 unit each. `videos.list`, `channels.list` and `playlistItems.list` cost 1 unit each (https://developers.google.com/youtube/v3/getting-started, https://developers.google.com/youtube/v3/determine_quota_cost). Quota extensions require a compliance audit (Developer Policies III.D.3).
- **Allowlisted channels.** Use `channels.list` and then `playlistItems.list` on each channel's uploads playlist (1 unit each) rather than `search.list`.
- **Data rules** (https://developers.google.com/youtube/terms/developer-policies): store API data "for no longer than 30 calendar days", then refresh or delete it (III.E.4). No derived metrics (III.E.4.h). Do not download, cache or store audiovisual content (III.E.1). No scraping (III.E.6). Make clear that YouTube is the source (III.F.2). Do not modify the player (III.I.6).
- **Transcripts.** `captions.download` "requires the user to have permission to edit the video" and costs 200 units (https://developers.google.com/youtube/v3/docs/captions/download). Captions of third-party videos are not available, and the YouTube ToS bans automated access and downloading (https://www.youtube.com/t/terms). **So OpenAI must not summarise video content.** A Term Entry may only embed or link a video the owner has watched.
- **Embed rules.** The viewport must be at least 200×200 px, and no overlays (https://developers.google.com/youtube/terms/required-minimum-functionality). The page must send an HTTP Referer, so do not use `noreferrer` (same page; error 153 otherwise: https://support.google.com/youtube/answer/171780). Leave autoplay off (https://developers.google.com/youtube/player_parameters). Privacy-enhanced `youtube-nocookie.com` is available (support page). Owners can disable embedding.
- **Obligations.** Publish a privacy policy covering the API client (API Services ToS §7, https://developers.google.com/youtube/terms/api-services-terms-of-service). The Developer Policies require it to link Google's privacy policy (III.A.2). Accept the API terms and Developer Policies (§1–3). The Atelier's Privacy page would need a YouTube section.
- **Simplest option.** A plain iframe embed of an owner-chosen video, with no Data API at all, avoids quota, keys and the 30-day cache rule. The embed and ToS rules still apply.

## Other high-trust sources seen

- **MyMoney.gov** (Financial Literacy and Education Commission): a federal financial-literacy hub covering saving, investing and scams, in EN and ES (https://www.mymoney.gov/). Its "Sites, Policies and Notices" page was not checked. Mostly links out to the agencies above, so it adds little as a drafting source.
- **For Vietnam**, the better fits for *In Vietnam* notes are probably the tax authority (gdt.gov.vn, for personal income tax on securities), the State Bank of Vietnam (sbv.gov.vn) and the exchanges' (HOSE/HNX) investor-education pages. **None of their terms were checked.** Research them before adding any of them.

## Recommended first Approved Source allowlist

| # | Approved Source | Scope (URL prefix) | Used for | Usage rule |
|---|---|---|---|---|
| 1 | Investor.gov (SEC) | `https://www.investor.gov/` | Investing terms (ETF, margin, short selling, ...) | **Draft + cite.** AI may paraphrase. Cite "Source: U.S. SEC, Investor.gov" with the URL and access date. No SEC seal or logos, no endorsement claim. Skip third-party material. |
| 2 | IRS | `https://www.irs.gov/` (incl. `/vi`) | *In the US* tax notes | **Draft + cite.** Cite "Source: IRS" with the URL, tax year and access date. No seals, no association claim. Prefer the English page as authoritative, and the VI page only as a terminology aid. |
| 3 | CFPB | `https://www.consumerfinance.gov/` | Credit and banking terms | **Draft + cite.** Cite "Source: CFPB". No logo. Recheck links at review time (the site is volatile). |
| 4 | SSC / UBCKNN | `https://ssc.gov.vn/` | *In Vietnam* securities notes | **Concept + cite + link.** Write the explanation in our own words. Cite laws and circulars by number. Do not paraphrase the SSC's articles closely, and always link. Manual fetch (JS-only site). |
| 5 | FINRA | `https://www.finra.org/investors/` | Further reading | **Link-only.** Never in a drafting prompt, never AI-paraphrased, never framed. |
| 6 | YouTube channels (owner-picked) | Specific channel IDs | Optional video | **Embed/link-only.** No AI summary of videos. Data API metadata kept ≤ 30 days. Referer on, autoplay off, nocookie domain. Privacy-policy update needed first. |

**Rules for every source:** drafting reads only allowlisted URLs at draft time and never browses at runtime. Fetch pages politely (≤ 1 request/s, an identifying User-Agent). Store the source URL and access date with each Term Entry. The owner checks the citation during review. Never imply government endorsement, and keep the Disclaimer link.

## Could not confirm

- A reuse statement on Investor.gov itself. Its policy links point to sec.gov's, and its terms pages 404.
- Whether the SEC's 10 requests/second limit covers investor.gov.
- Any SSC reuse terms, an SSC feed, and the official text of IP Law Article 15 after the 2025 amendment.
- Whether the CFPB's Vietnamese/Spanish materials still exist anywhere (the hub redirects to the homepage).
- The CFPB complaint API's key and rate-limit rules, and the FINRA API's fees and terms (not relevant to drafting).
- An IRS RSS feed.
- The terms of the Vietnamese alternatives (GDT, SBV, HOSE, HNX).
