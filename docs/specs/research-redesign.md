# Research redesign: search first

**Status:** Approved for planning, 2026-10-31
**Prototype:** branch `prototype/research-redesign`, file
`PersonalFinance/src/PersonalFinance.Web/wwwroot/prototype/research-redesign/search-first.html` (run the
`research-prototype` entry of `.claude/launch.json`). Its "Coverage" panel maps every current feature to its new home.

## Problem

The Research page is slow, clunky and confusing for a new user.

- **Slow.** `ResearchController.Index` waits for eight datasets before it renders anything. Each one is read from
  `ResearchSnapshot.Payload`, deserialized in ApiService, serialized over HTTP, deserialized in Web and serialized again
  into JSON data islands. Nothing is cached. The page then loads about ten scripts, plus D3 and TopoJSON, whichever tab
  is open.
- **Confusing.** Six tabs open straight into dense charts. The Fund Basket bar shows on some tabs and not others, each
  tab has its own "Market / Your basket" switch, and nothing gives a starting point or a plain answer.

## Decision

Variant C of the prototype: **search first**, with a page per question instead of a tab per dataset.

| Page | Route | Datasets it reads | Comes from |
|---|---|---|---|
| Home | `/Research` | status only | new |
| Sectors | `/Research/Sectors?focus=Office/Industry&view=basket` | sectors (+ funds, industries lazily, in the basket view) | Sectors tab |
| World | `/Research/World?country=Name&view=basket` | world, funds, exposure | World tab |
| Funds | `/Research/Funds?view=table\|risk\|correlation\|holdings` | funds, returns, facts | Funds tab (holdings, correlation), Risk tab |
| Your basket | `/Research/Basket` | funds, overlap, returns, macro, facts, exposure, sectors | Funds tab (overlap, correlation), Performance, Risk, Sectors and World basket views |
| Fund | `/Research/Fund/{ticker}` | funds, returns, macro, facts, overlap, exposure | new |
| Company | `/Research/Company/{ticker}` or `?q=name` | none server-side (profile fetched as JSON, as today) | Companies tab |

- **One search box** on every Research page (and `/` to focus it): tracked funds, SEC offices, industries, countries
  (from a small search index) and any SEC filer (from the existing company suggestions).
- **The Fund Basket still lives only in the address** (`?basket=VTI:60,BND:40`). Every Research link carries it, and
  "Add to basket" appears on fund pages, the Funds table and search results. The Basket page has the editor (the
  current basket bar), presets and "Copy link".
- **Each section fails on its own**, as each tab does today, and keeps "Show table", "About this data" and "Data as of".
- **Plain-language help:** a dismissible first-visit strip on Home and glossary terms (dotted underline, explained on
  click) for jargon: basket, overlap, correlation, volatility, annualized return, Sharpe ratio, growth of 100, adjusted
  for inflation, Public float, SEC office, net assets, expense ratio, issuer country, priced in.
- **Old links keep working:** `/Research?tab=<id>[&ticker=][&basket=]` redirects to the matching page.

### Simplifications (approved 2026-10-31)

1. **Sectors draws the treemap and ranked list only.** The sunburst and the Sunburst/Treemap toggle are removed.
2. **"What the tracked funds hold" becomes an expandable list** (fund › asset category › largest holdings), not the
   D3 circle tree.

### Kept as they are

The Performance month brush and its arrow-key readout; the world map's zoom and colour-scale choice; the year and
quarter scrubbers with play; the Technology (incl. chip makers) lens; the IFRS and missing-revenue notes; press-release
loading, empty and unavailable states; recession bands; reduced-motion behaviour.

## Speed targets

- Home renders without reading any snapshot payload.
- Each page reads only its own datasets (table above).
- Web keeps each snapshot in memory for 10 minutes (snapshots change once a day), so a second page view reads nothing
  from ApiService.

## Non-goals

- No change to the refresh job, the snapshots or ADR 0010 (no security identifiers anywhere, including links: a
  holding links to the Company page by name, which the company search resolves).
- No "funds that hold this company" list on the Company page (it would need name matching in Web).
- No saved baskets.
