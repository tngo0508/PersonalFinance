# 0020 – Cytoscape.js Draws the Financial Universe Graph

**Date:** 2026-11-02  
**Status:** Accepted (amends ADR 0009)

## Context

The Financial Universe (spec #617) shows how one month's finances connect: income sources pool into Income, which flows
into expense categories, Other and Savings or Shortfall; Savings Goals hang off Savings; Net Worth and the Investment
Scenarios follow as relationships. The ApiService projects it as nodes and edges, and the page needs a drawing of a
directed graph that the user can pan, zoom and, in a later slice, drag and inspect.

ADR 0009 gives the app D3 for bespoke pictures, Chart.js for data charts and, through ADR 0014, Mermaid for authored
diagrams. None draws an interactive graph: D3 has no layered layout for a directed graph and no pan, zoom or hit
testing of its own, and Mermaid produces a static picture. The Money Map Plan View tried a graph library before and was
rejected twice (#194, #198): its edges merged into one bus, and the labels fell below reading size. The owner chose a
graph library again for this page, with the prototype on branch `prototype/financial-universe`
(`docs/research/financial-universe-prototype.md`) as the test of whether that failure recurs.

## Decision

1. **Cytoscape.js with the dagre layout draws the Financial Universe graph, and only that graph.** Chart.js stays the
   default for data charts, D3 for bespoke 2D pictures and Mermaid for authored diagrams. Cytoscape is admitted for an
   interactive directed graph derived from the user's data, where the user pans, zooms and later drags and inspects
   nodes. A second such graph needs its own ADR, not a wider reading of this one.
2. **Pinned exactly and vendored through libman from jsDelivr:** `cytoscape@3.34.4` (`dist/cytoscape.min.js` and
   `LICENSE`) and `cytoscape-dagre@4.0.1` (`dist/cytoscape-dagre.min.js` and `LICENSE`). Both are MIT. The extension
   bundles dagre and registers itself when the `cytoscape` global exists, so there are two libman entries, not three.
   cdnjs does not carry the extension and ships neither licence file; ADR 0014 §3 already makes jsDelivr the source of
   record for packages cdnjs lacks.
3. **Loaded only by the Financial Universe view's `Scripts` section, never by `_Layout`.** A test keeps it there.
4. **The list view is the accessible equivalent and is always on the page.** Cytoscape draws on a canvas, which
   assistive technology cannot read. The canvas container carries `role="img"` and a label that says the list below
   holds the same information; the renderer composes no text of its own. Every amount, name and link in the graph is
   also a row in the list.
5. **Why the Money Map failure does not recur.** Bezier edges fan out to their own targets under dagre's
   network-simplex ranker, where the Plan View's taxi edges shared a bus. Income sources pool into one Income node, so
   the graph is five ranks deep and never all-pairs. The prototype's heavy month (24 nodes) reads with no edge overlap
   in a 1280 x 800 window.
6. **Legibility decides the default view, never a pixel breakpoint.** After the fit, the renderer measures the node
   label size against the body font size. Below a named share it opens on the list with the graph a toggle away; the
   share is calibrated on this graph (the prototype read at 72% in a laptop window and failed at 31% on a phone), not
   copied from Money Priorities' 80%, and is re-evaluated on every resize.
7. **The wheel scrolls the page; ctrl+wheel zooms the graph** at the pointer, pinch zooms on touch, and plain
   buttons zoom in, zoom out and fit. A canvas that captured the wheel would trap the page's scroll.
8. **Colours come from the theme tokens through the chart palette** (`core/chart-palette.js`): the Money Flow slot and
   kind tokens for the budget nodes, so Money Flow and the Financial Universe colour a category the same, and the chart
   series for goals, Net Worth and scenarios. The canvas redraws on `themechange`.

## Considered Options

- **D3 with a hand-written layered layout:** rejected. Rank assignment, crossing reduction and edge routing are what
  dagre provides; rewriting them is more code than the vendored library, and pan, zoom and hit testing would follow.
- **Mermaid (ADR 0014):** rejected. It renders a static SVG with no pan, zoom, hover or drag, and the later slices
  need all four. Its layout is dagre too, so it would read the same but do less.
- **A Sankey through `chartjs-chart-sankey`, as Money Flow:** rejected. Goals, Net Worth and scenarios are
  relationships, not flows, and a Sankey cannot draw an unweighted edge.
- **Loading from a CDN at runtime:** rejected, as for every library here; the vendored copy is pinned and committed.

## Consequences

- ADR 0009's Status line reads "amended by ADR 0014 (Mermaid for authored diagrams) and ADR 0020 (Cytoscape for the
  Financial Universe graph)". Its body stays as written.
- `libman.json` gains the two jsDelivr entries, and the vendored files are committed like the others.
- A major upgrade of either package is its own pull request that reruns the prototype's readings.
