# Financial Universe: Cytoscape.js readability prototype

Throwaway prototype backing the Financial Universe spec. It answers one question before any slice starts: does a
Cytoscape.js graph with the dagre layout stay readable for a heavy user, or do the edges merge into one bus the way
the Money Map Plan View did in #198?

- Page: `PersonalFinance/src/PersonalFinance.Web/wwwroot/prototype/financial-universe-prototype.html` on this branch
  only. It loads `cytoscape@3.34.4` from cdnjs and `cytoscape-dagre@4.0.1` from jsdelivr (cdnjs does not carry the
  extension). Both are MIT. The extension's UMD bundle includes dagre and registers itself when the `cytoscape`
  global exists, so the feature needs two libman entries, not three.
- Date: 2026-11-01.

## Fixture

A heavy user for one month, in the reporting currency:

| Kind | Nodes |
|---|---|
| Income sources | Salary 6,200; Freelance 900; Dividends 150, pooled into one Income node (7,250) |
| Expense categories | 12 categories; the 4 under 3% of income fold into Other (305), as `MoneyFlowBuilder` does |
| Savings | 3,000 (income minus expenses) |
| Savings Goals | 5, one with no contribution this month |
| Net Worth | one node (starting balance, annual savings) |
| Investment Scenarios | 4 |

24 nodes, 23 edges. Edges from Income to categories and Savings, and from Savings to goals, are weighted (width
scales with amount). Savings to Net Worth and Net Worth to scenarios are dashed, unweighted relationships.

## Measurements

| Viewport | Fit zoom | Node label size | Share of 16px body font | Reads? |
|---|---|---|---|---|
| 1280 x 800, light | 0.88 | 11.5px | 72% | Yes, every label legible, no edge overlap |
| 1280 x 800, dark | 0.88 | 11.5px | 72% | Yes, same layout, token colours applied |
| 375 x 812 (phone) | 0.39 | 5.0px | 31% | No, labels unreadable at fit zoom |

Screenshots: `images/financial-universe/desktop-light-1280x800.jpg`, `desktop-dark-1280x800.jpg`,
`phone-375x812.jpg`.

## Findings

1. **Edges do not merge.** With bezier edges and dagre's network-simplex ranker, every edge fans out to its own
   target. The #198 failure came from taxi edges sharing a bus; it does not recur here.
2. **Five ranks is the natural depth**: sources, Income, categories and Savings, goals and Net Worth, scenarios.
   Pooling income sources into one Income node is needed because the data does not say which source paid for
   which category; all-pairs edges would be meaningless.
3. **The 80% legibility rule needs calibrating for this page.** Money Priorities switches to its narrow view when
   labels fall under 80% of the body font. At a 1280 x 800 laptop viewport this graph sits at 72% and still reads
   well, because the limit is the viewport height, not the width. The slice that implements the switch should
   measure the trigger on this graph rather than copy the 80% constant.
4. **Phone width confirms the list-view default.** At 375px the fit zoom makes labels 5px tall; no amount of layout
   tuning fixes that, so the page opens on the grouped list there with a "Show graph" toggle.
5. **ctrl+wheel zoom works** without trapping page scroll; buttons for zoom in, zoom out and fit are plain HTML and
   keyboard reachable. Hover highlighting of a node's neighbourhood (slice 3) was cheap to add and reads well.
6. **Theme**: colours come from CSS custom properties read at init. The real page must redraw on the app's
   `themechange` event through the existing palette subscription, since the canvas does not restyle itself.
