# Which Vietnamese authorities may the Atelier draw on?

Research for [#501](https://github.com/tngo0508/PersonalFinance/issues/501), under the map [#491 Learning page (Atelier)](https://github.com/tngo0508/PersonalFinance/issues/491). It follows [#492](https://github.com/tngo0508/PersonalFinance/issues/492) (branch `research/atelier-sources`, `docs/research/atelier-sources.md`). Checked 2026-10-27/28 against each site's own footer, policy and feed pages, and the law text on vbpl.vn. Not legal advice: these are the published terms, read as written.

**Context.** OpenAI drafts Term Entries only from an owner-kept allowlist of Approved Sources. Every entry cites its sources, and the owner reviews it before it is published. No open web search at runtime. An entry may carry an *In Vietnam* note (≤ 60 words, plain English) that must cite a Vietnamese source.

**Access note.** All checks ran from a US connection. The GDT and CIC sites could not be reached from it (GDT: connection refused; CIC: "Sorry, you have been blocked" in a real browser too). Those two rows are unconfirmed, not negative.

## Answer in one screen

| Source | Reuse terms found | Education / glossary | API / feed | Renders without JS? | Verdict |
|---|---|---|---|---|---|
| Tax Department (Cục Thuế, MoF), `gdt.gov.vn` | **Not checked: site unreachable** | Not checked | Not checked | Not checked | **Link-only until vetted.** Use the law (by number, via vbpl.vn) for tax notes. |
| Ministry of Finance policy Q&A, `portal.mof.gov.vn/hoidapcstc` | None (footer: owner, editor, contacts only) | Public tax/finance Q&A, VI | None found | **Yes** (server-rendered) | **Concept + cite + link** (optional extra) |
| State Bank of Vietnam, `sbv.gov.vn` | **None.** Footer says only "© Cổng thông tin điện tử Ngân hàng Nhà nước Việt Nam" | "Phổ biến kiến thức" (bank cards, e-banking, Vietnamese money), VI; EN news only | None found (footer RSS icon has no link) | Yes, but a WAF rejects non-browser clients | **Concept + cite + link** |
| HOSE, `hsx.vn` | **None.** No copyright line, no terms page | No glossary; "Góc nhà đầu tư" holds one item | RSS (`api.hsx.vn/n/api/v1/News/NewsFeed`); paid data feed | **No** (React app) | **Link-only** (cite for listing facts) |
| HNX, `hnx.vn` | **"All rights reserved"** + "contact HNX when publishing information from this website" | **Glossary** (`/vi-vn/thuat-ngu.html`, EN `/en-gb/thuat-ngu.html`), FAQ | RSS (4 channels); paid info service | **Mostly yes** (live widgets need JS) | **Concept + cite + link.** Never reproduce glossary text. |
| National Credit Information Center (CIC), `cic.org.vn` | **Not checked: blocked (403)** | Not checked | Not checked | Not checked | **Link-only until vetted.** Cite the SBV/law for credit-information facts. |
| Laws and administrative texts (vbpl.vn) | **Not protected by copyright** (IP Law Art. 15(2)) | n/a | n/a (robots.txt disallows `/api/`) | No (SPA) | **Draft + cite** by law number and article |

## Tax Department (formerly General Department of Taxation), gdt.gov.vn

- **Name and site.** The Ministry of Finance portal still links the tax authority at `https://gdt.gov.vn/` (logo link on https://www.mof.gov.vn/), and its own news calls the body "Cục Thuế" (e.g. a 26/10/2026 procurement notice on mof.gov.vn). The rename from Tổng cục Thuế to Cục Thuế under the MoF from 01/03/2025 by Decision 381/QĐ-BTC is reported only by secondary sources (e.g. https://lsvn.vn/mot-so-thay-doi-quan-trong-ve-co-cau-to-chuc-cua-co-quan-thue-tu-01-3-2025-a154489.html). **The decision itself was not read.**
- **Reachability.** `www.gdt.gov.vn`, `gdt.gov.vn`, `thuedientu.gdt.gov.vn` and `hotronnt.gdt.gov.vn` all refused connections on ports 80 and 443 (DNS resolves to 103.9.200.37), from curl, the in-app browser and WebFetch. The Internet Archive was offline at the time. **So nothing about its terms, education pages, feeds or rendering could be confirmed.**
- **Alternative found.** The MoF "Hỏi đáp chính sách tài chính" portal publishes citizens' questions with the Ministry's answers. One open question (28/10/2026) asks how a resident individual's income from trading on foreign platforms is taxed (https://portal.mof.gov.vn/hoidapcstc/home/cthoidap/166885). Pages render without JavaScript. Its footer names the owner ("Cơ quan chủ quản: Bộ Tài chính"), the responsible person and contacts, and has **no copyright or reuse statement**. The main MoF portal (https://www.mof.gov.vn/) is a JavaScript single-page app.
- **Rule for now.** For *In Vietnam* tax notes (e.g. personal income tax on securities transfers), cite the tax law or decree by number and article on vbpl.vn (draft + cite, see Article 15 below). Link to gdt.gov.vn only after someone with a Vietnamese connection checks it.

## State Bank of Vietnam (SBV), sbv.gov.vn

- **Reuse terms.** **None found.** The footer reads "© Cổng thông tin điện tử Ngân hàng Nhà nước Việt Nam", then the address (49 Lý Thái Tổ, Hà Nội), the editorial-board phone and email (`thuongtrucweb@sbv.gov.vn`), and an incident contact (https://sbv.gov.vn/vi/trang-chu, page HTML). No terms of use, no "ghi rõ nguồn" (cite the source) line, no licence.
- **Education content.** "Phổ biến kiến thức" (https://sbv.gov.vn/vi/pho-bien-kien-thuc) has a legal-knowledge section and community topics: "Tiền Việt Nam – những điều bạn nên biết" (Vietnamese money), "Một số thông tin về thẻ ngân hàng" (bank cards: debit, credit, prepaid; domestic vs international; https://sbv.gov.vn/vi/mot-so-thong-tin-ve-the-ngan-hang) and "Những điều cần biết khi giao dịch qua ngân hàng điện tử" (e-banking). There is also a public Q&A (`/vi/hỏi-đáp`) and statistics (interest rates, exchange rate, credit). English (`https://sbv.gov.vn/en/trang-chu`) carries news and a few financial-education reports, **no glossary**.
- **API / feed.** None found. The footer shows an RSS icon with no link. Some statistics live on a separate Oracle WebCenter portal (`dttktt.sbv.gov.vn`).
- **Rendering and access.** Pages are server-rendered (Liferay): a single request with a browser user agent returned the full bank-card article in the HTML. But a firewall answers other clients with "The requested URL was rejected" (curl with its default agent, 2026-10-27). `robots.txt` disallows nothing. Do not spoof a browser agent to get past the firewall; have the owner open the page and paste the text into the draft, or link only.
- **Fit.** Bank cards, deposits, interest-rate and exchange-rate facts, credit-information rules (the CIC is an SBV unit).
- **Rule.** Concept + cite + link, same as the SSC. SBV circulars (Thông tư … /TT-NHNN) are legal normative documents and can be cited and paraphrased freely (Article 15).

## Ho Chi Minh City Stock Exchange (HOSE), hsx.vn

- **Reuse terms.** **None found.** The footer gives only the name, address and phone, plus an RSS link (https://www.hsx.vn/vi/ and /en/). No copyright line and no terms or disclaimer page in the menu.
- **Education content.** No glossary. "Góc nhà đầu tư" (investor corner, https://www.hsx.vn/vi/tin-tuc/goc-nha-dau-tu) holds a single notice, about exchange-visit tours (15/05/2026). The "Văn bản pháp quy" section lists laws, decrees, circulars and HOSE rules.
- **API / feed.** RSS index at `https://api.hsx.vn/n/api/v1/News/NewsFeed` (served as `application/rss+xml`, one channel per news category). Trading data is a **paid** information service: HOSE sells data packages "để sử dụng nội bộ hoặc phân phối lại" (for internal use or redistribution) under a published price list (https://www.hsx.vn/vi/dich-vu/cung-cap-thong-tin). No public API terms or rate limits found.
- **Rendering.** **JavaScript-only.** The HTML is a React shell ("You need to enable JavaScript to run this app"). `robots.txt` allows everything.
- **Rule.** Link-only for education. It may be cited for a listing fact (e.g. "VN30 is an HOSE index") with a link. Never pull market data into the Atelier: that is a paid, licensed product.

## Hanoi Stock Exchange (HNX), hnx.vn

- **Reuse terms.** The footer says "© 2016 Bản quyển thuộc "Sở Giao dịch Chứng khoán Hà Nội"" (English footer: "© 2016 Copyright by "Hanoi Stock Exchange". All rights reserved.") and, in Vietnamese only, "Liên hệ với Sở Giao dịch Chứng khoán Hà Nội khi phát hành thông tin từ Website này" (contact HNX when publishing information from this website) (https://hnx.vn/vi-vn/rss.html, https://hnx.vn/en-gb/home.html). The disclaimer (https://hnx.vn/vi-vn/khuyen-cao.html, EN `/en-gb/khuyen-cao.html`) says HNX takes no responsibility for users' use of the information, which may change without notice. Nothing about AI use either way.
- **Education content.** The best fit of the four. A **glossary** in Vietnamese (https://hnx.vn/vi-vn/thuat-ngu.html: underwriting, securities, convertible securities, primary/secondary market, ...) and English (https://hnx.vn/en-gb/thuat-ngu.html), plus an FAQ (https://hnx.vn/vi-vn/hoi-dap.html: trading hours, professional investors, opening an account, who regulates HNX).
- **API / feed.** RSS list at https://hnx.vn/vi-vn/rss.html with four channels, e.g. `https://hnx.vn/1/vi_vn/thong-tin-cong-bo-tu-so.rss` (announcements from the exchange, latest item 22/10/2026) and `/4/vi_vn/su-kien-dao-tao.rss` (training events). Item links point to `http://hnx.vn:7978/...`. Market data is a separate information service (`/dich-vu-cctt.html`). No rate limits published.
- **Rendering.** Server-rendered: the glossary, FAQ and disclaimer text is in the HTML. Live price widgets are Vue templates that need JavaScript.
- **Rule.** Concept + cite + link. "All rights reserved" plus "contact us before publishing" rules out reproducing or closely paraphrasing the glossary. Use it to check that our wording matches Vietnamese usage, write the explanation ourselves, cite "Source: Hanoi Stock Exchange (HNX)" with the URL and access date. Ask HNX (`/lien-he.html`) before any closer reuse.

## National Credit Information Center (CIC), cic.org.vn

- **Not checked.** `https://cic.org.vn/` and `https://cic.gov.vn/` return 403 "Sorry, you have been blocked" to curl, WebFetch and the in-app browser alike (2026-10-27). I did not try to get around the block. Which of the two domains is canonical was not confirmed from a primary source.
- **What is known only from secondary sources.** Bank and news articles say CIC paused registration for personal products on its website from 01/04/2026 and points users to the "CIC Credit Connect" app, and that people impersonating CIC offer to "raise credit scores" for a fee (e.g. https://nhadautu.vn/canh-bao-tinh-trang-mao-danh-cic-de-lua-dao-nang-diem-tin-dung-d77528.html). **None of this is confirmed.**
- **Rule.** Link-only until someone with a Vietnamese connection checks the site's footer and terms. A credit-score Term Entry that needs an *In Vietnam* note should cite the SBV circular on credit-information activity by number (draft + cite under Article 15), not CIC's site text.

## Article 15 of the Law on Intellectual Property (official text)

**Source.** The National Legal Database run by the Ministry of Justice: the original law, https://vbpl.vn/van-ban/chi-tiet/luat-so-huu-tri-tue-so-50-2005-qh11--16748 (status "Hết hiệu lực một phần", partly superseded), and the 2025 amendment, Law No. 131/2025/QH15 of 10/12/2025, **in force from 01/04/2026**, https://vbpl.vn/van-ban/chi-tiet/luat-sua-doi-bo-sung-mot-so-dieu-cua-luat-so-huu-tri-tue-so-131-2025-qh15--187492.

**Current text** (Art. 15, "Các đối tượng không thuộc phạm vi bảo hộ quyền tác giả", objects not protected by copyright):

1. "Tin tức thời sự thuần tuý đưa tin." (Pure news of the day.)
2. "Văn bản quy phạm pháp luật, văn bản hành chính, văn bản khác thuộc lĩnh vực tư pháp và bản dịch chính thức của văn bản đó." (Legal normative documents, administrative documents, other judicial documents, and their official translations.)
3. "Quy trình, hệ thống, phương pháp hoạt động, khái niệm, nguyên lý, số liệu." (Procedures, systems, methods of operation, concepts, principles, data.)
4. Added by Law 131/2025, Art. 1(7): "Ý tưởng; khẩu hiệu; tên tác phẩm một cách độc lập." (Ideas; slogans; titles of works on their own.)

Clauses 1–3 are unchanged from 2005. The 2022 amendment (Law 07/2022/QH15, on vbpl.vn) does not touch Article 15; the 2009, 2019 and 93/2025 amendments were not opened, but the 2025 law amends Article 15 only by adding clause 4.

**What it means for the Atelier.**

- **Laws, decrees, circulars and official decisions** (e.g. the Securities Law, an SBV circular, a tax decree) and their official translations are outside copyright. The Atelier may quote, paraphrase and AI-summarise them freely. Cite the number and article, and link the vbpl.vn page.
- **Concepts, principles and data** are also outside copyright, so explaining what a margin account or a credit score is cannot infringe on its own. What is protected is an agency's particular **wording**: its explanatory articles, glossaries and FAQ answers are not normative or administrative documents, so treat them as copyrighted (HNX says so explicitly).
- **New AI clause (not asked, but relevant).** Law 131/2025 adds Art. 7(5): organisations and individuals may use lawfully published, publicly accessible "văn bản và dữ liệu" (texts and data) "để phục vụ mục đích nghiên cứu khoa học, thử nghiệm, huấn luyện hệ thống trí tuệ nhân tạo" (for scientific research, testing, training AI systems), provided this does not unreasonably harm the author's rights. For copyrighted texts and data, that use must also follow Government rules. This covers **training**, not publishing AI-written paraphrases. Those implementing Government rules were not checked.

## Recommended additions to the Approved Source allowlist

Rows 1–4 of the first allowlist (Investor.gov, IRS, CFPB, SSC) stay as they are. Add:

| # | Approved Source | Scope (URL prefix) | Used for | Usage rule |
|---|---|---|---|---|
| 7 | Vietnamese laws and official documents (National Legal Database) | `https://vbpl.vn/van-ban/chi-tiet/` | *In Vietnam* notes on tax, banking, securities and credit rules | **Draft + cite.** Not copyrighted (IP Law Art. 15(2)). Cite law/decree/circular number + article, link the vbpl.vn page, note the in-force date. Owner pastes the article text into the draft (the site is a JS app; `/api/` is disallowed in robots.txt). |
| 8 | State Bank of Vietnam | `https://sbv.gov.vn/vi/`, `https://sbv.gov.vn/en/` | Banking, cards, deposits, interest and exchange-rate notes | **Concept + cite + link.** Own words, cite "Source: State Bank of Vietnam", URL, access date. Quote SBV circulars freely (row 7). Manual fetch: the firewall rejects bots, and we do not spoof a browser. |
| 9 | Hanoi Stock Exchange | `https://hnx.vn/vi-vn/`, `https://hnx.vn/en-gb/` | Securities terms (glossary, FAQ) | **Concept + cite + link.** All rights reserved: never reproduce or closely paraphrase glossary or FAQ text; use it to check Vietnamese terminology. Ask HNX before closer reuse. |
| 10 | Ministry of Finance policy Q&A | `https://portal.mof.gov.vn/hoidapcstc/` | Official answers on tax treatment (optional) | **Concept + cite + link.** Cite the answer's date. Prefer the underlying law (row 7) where the answer names one. |
| 11 | HOSE | `https://www.hsx.vn/` | Further reading; listing facts | **Link-only.** No education content; market data is a paid licensed product. |
| 12 | Tax Department (Cục Thuế) | `https://gdt.gov.vn/` | Further reading on tax procedures | **Link-only until vetted.** Site unreachable from outside Vietnam during this research. |
| 13 | CIC | `https://cic.org.vn/` | Further reading on credit reports | **Link-only until vetted.** Site blocked; canonical domain unconfirmed. |

**Rules for every Vietnamese source:** the *In Vietnam* note is written in plain English in our own words. It cites the Vietnamese source by name, URL and access date, and a law by number and article. Never imply endorsement by a state body. Since none of these sites publishes reuse terms, any closer reuse than concept + cite + link needs the agency's written permission.

## Could not confirm

- Anything about gdt.gov.vn (terms, education pages, feeds, rendering): unreachable from a US connection.
- Anything about the CIC site, and whether `cic.org.vn` or `cic.gov.vn` is canonical: blocked.
- The text of Decision 381/QĐ-BTC renaming the tax authority (only secondary sources and the MoF portal's own usage seen).
- Rate limits for any of the sites (none published) and HNX/HOSE data-service terms (price lists not read).
- Whether the 2009, 2019 or 93/2025 amendments touched Article 15 (the 2022 and 2025 ones were read; the 2005 text on vbpl.vn matches clauses 1–3).
- The Government rules that Art. 7(5) (AI training on copyrighted text) refers to.
- Whether SBV's English site has any glossary beyond news and reports.
