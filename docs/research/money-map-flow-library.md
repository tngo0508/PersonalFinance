# Which library draws the Money Map Flow View best?

Research for [#194](https://github.com/tngo0508/PersonalFinance/issues/194), part of the Money Map map
[#193](https://github.com/tngo0508/PersonalFinance/issues/193). Gathered 2026-10-23 from primary sources (npm registry,
the packages' published files on jsDelivr, GitHub repos and issues, official docs). Sizes were measured by downloading
the minified file and gzipping it at level 9; they are not taken from a third-party size site.

## Recommendation

- **Flow View: `chartjs-chart-sankey` 0.19.1 (MIT) on the Chart.js 4.5.1 the app already vendors.** It is the only
  candidate that draws a real Sankey (band width proportional to the amount), it adds about **7 KB gzip**, and it reuses
  the Chart.js loading, colour and tooltip patterns the Analysis, Budget, Net Worth and Google Drive pages already use.
- **Plan View: Cytoscape.js 3.34.4 (MIT) with `cytoscape-dagre` 4.0.1 (MIT).** It fits: a small DAG laid out left to
  right by dagre, clickable nodes via the `tap` event (mouse and touch), styling from a JSON stylesheet. Cost is about
  **148 KB gzip**, about twice Chart.js, so load it only on the Money Map page.
- **Do not use Cytoscape for the Flow View.** A node-link graph with edge widths mapped to amounts is not a Sankey:
  edges do not stack into the nodes, so the eye cannot read "this share of income went here". Using one library for both
  views would save nothing: the Flow View then needs Cytoscape (148 KB) instead of a 7 KB plugin on a library already
  vendored.
- **No candidate gives keyboard or screen-reader access to the drawing.** Both libraries render to `<canvas>`. The
  accessible table that #193 already requires is the real accessible view; the canvas gets `role="img"` and an
  `aria-label`. Plan View nodes need an HTML equivalent (links or buttons in the table/list) for keyboard users.

## Comparison

| | chartjs-chart-sankey | Cytoscape.js + cytoscape-dagre | Apache ECharts | vis-network | d3-sankey |
|---|---|---|---|---|---|
| Version (npm latest) | 0.19.1 (2026-09-12) | 3.34.4 (2026-10-15) + 4.0.1 (2026-08-28) | 6.1.0 (2026-05-19) | 10.1.2 (2026-08-19) | 0.12.3 (2019-09-02) |
| License | MIT | MIT + MIT (bundles dagre v3, MIT) | Apache-2.0 | Apache-2.0 OR MIT | BSD-3-Clause |
| Minified / gzip | 18.6 KB / **7.0 KB** (+ Chart.js already loaded: 203.6 / 68.9 KB) | 425.4 / 133.3 KB + 44.6 / 15.2 KB = **~148 KB** | 1095.6 / **359.6 KB** (full build) | 636.7 / **150.9 KB** (standalone UMD) | small, but needs d3 modules and you write all SVG drawing |
| Real Sankey (proportional bands) | Yes | No (node-link only) | Yes (`sankey` series) | No | Layout only, you draw it |
| Dependency graph (Plan View) | No | Yes (dagre DAG layout) | Yes (`graph` series, no DAG layout) | Yes (hierarchical layout) | No |
| On cdnjs (LibMan default provider) | No, use `jsdelivr` provider | Cytoscape yes; dagre ext. no, use `jsdelivr` | Yes | n/a | n/a |
| Maintenance | Active; author is Chart.js's #2 contributor | Very active; 11k stars | Active, Apache project | Active | Unchanged since 2019 |
| Built-in accessibility | None (Chart.js: canvas + ARIA is the app's job) | None (maintainer: app's job) | `aria` option generates a text description; decal patterns | None for node focus | SVG, so DOM-reachable, but all hand-built |

Rejected outright:

- **`cytoscape-elk` / `elkjs`:** `cytoscape-elk` is MIT but depends on `elkjs`, whose npm license is
  `EPL-2.0 OR GPL-3.0-or-later` (its `LICENSE.md` is the Eclipse Public License 2.0). Outside the allow-list.
- **`cytoscape-klay` / `klayjs`:** `cytoscape-klay` is MIT but depends on `klayjs`, licensed `EPL-1.0` (last release
  2016). Outside the allow-list.
- **Highcharts:** npm `license` is a link to the commercial Highcharts licence. Paid; excluded.
- **Plotly (`plotly.js-dist-min`):** MIT and has a Sankey trace, but 4.7 MB minified / 1.44 MB gzip for one chart.
- **JointJS:** MPL-2.0, already excluded by #193.

## Findings by criterion

### License

Checked against the npm registry `latest` manifest of each package and, where the dist file has one, its banner:

- `cytoscape` 3.34.4: MIT, no runtime dependencies.
  ([npm](https://www.npmjs.com/package/cytoscape), [repo](https://github.com/cytoscape/cytoscape.js))
- `cytoscape-dagre` 4.0.1: MIT, no runtime dependencies; `@dagrejs/dagre` ^3 is a devDependency bundled into the dist.
  The README states "Dagre v3 is bundled into the distributed extension files, so you do not need to include a separate
  dagre script". `@dagrejs/dagre` 3.1.1 and `@dagrejs/graphlib` 4.0.5 are both MIT.
  ([README](https://github.com/cytoscape/cytoscape.js-dagre))
- `chartjs-chart-sankey` 0.19.1: MIT (dist banner "Released under the MIT license"), only a peer dependency on
  `chart.js >=3.3.0`. ([repo](https://github.com/kurkle/chartjs-chart-sankey))
- `elkjs` 0.12.0: `EPL-2.0 OR GPL-3.0-or-later`. `klayjs` 0.4.1: `EPL-1.0`. Both excluded.
- `echarts` 6.1.0: Apache-2.0 (deps `zrender` and `tslib`). `vis-network` 10.1.2: `(Apache-2.0 OR MIT)`.
  `d3-sankey` 0.12.3: BSD-3-Clause.

The old unscoped `dagre` 0.8.5 (2019, MIT, pulls in `lodash` and `graphlib`) is superseded by `@dagrejs/dagre`;
`cytoscape-dagre` 4.x no longer needs it.

### Size

Measured from the jsDelivr copy of each package's minified browser build:

| File | Minified | gzip |
|---|---|---|
| `chart.js@4.5.1/dist/chart.umd.min.js` (already vendored) | 203.6 KB | 68.9 KB |
| `chartjs-chart-sankey@0.19.1/dist/chartjs-chart-sankey.min.js` | 18.6 KB | 7.0 KB |
| `cytoscape@3.34.4/dist/cytoscape.min.js` | 425.4 KB | 133.3 KB |
| `cytoscape-dagre@4.0.1/dist/cytoscape-dagre.min.js` | 44.6 KB | 15.2 KB |
| `echarts@6.1.0/dist/echarts.min.js` | 1095.6 KB | 359.6 KB |
| `vis-network@10.1.2/standalone/umd/vis-network.min.js` | 636.7 KB | 150.9 KB |
| `plotly.js-dist-min@4.1.2/plotly.min.js` | 4721.1 KB | 1441.6 KB |

### Flow layout readability (~5–20 nodes)

- **Sankey plugin.** Purpose-built for this: "drawing flows between named nodes as directional bands whose width is
  proportional to the flow value — useful for visualizing ... budgets" (README). Dataset options from the
  [usage docs](https://chartjs-chart-sankey.pages.dev/usage/) cover what the Flow View needs: `column` (pin Income left,
  categories middle, Savings right), `priority` (sort categories by amount), `labels` (display names keyed by node),
  `nodeLabels` and `flowLabels` (show amounts, scriptable, so small flows can hide their label), `nodePadding`,
  `nodeMinSize` (keeps tiny categories visible), `colorFrom`/`colorTo`/`colorMode`/`flowColor`, and
  `orientation: 'vertical'` for narrow screens. With 5–20 nodes in two or three columns this reads well.
- **Cytoscape + dagre.** `rankDir: 'LR'` gives a clean left-to-right layered layout, and `width: mapData(amount, ...)`
  can scale edge thickness ([style docs](https://js.cytoscape.org/#style/mappers)). But edges are independent strokes;
  they do not stack into proportional node heights, so the "where did the money go" share is not visible. A worse fit.
- **ECharts** has a full Sankey series too and would read as well as the plugin, at 50x the added weight.

Model edge cases for #196 to settle (not library problems, but the Sankey makes them visible): a Sankey has to balance,
so a month where expenses exceed income needs an explicit inflow node (e.g. "From savings" or "Shortfall"), and a
negative Savings figure cannot be drawn as a band.

### Dark-mode theming

The Web app has **no dark mode today**: no `data-bs-theme` or `prefers-color-scheme` in `_Layout.cshtml` or
`wwwroot/css/site.css` (checked on main at 4.2.19). Both libraries can follow a theme when one exists:

- Chart.js colours are plain JS options, so a page can read CSS custom properties with `getComputedStyle` and rebuild
  or `update()` the chart; the Sankey plugin's colour options are scriptable.
- Cytoscape restyles at runtime with `cy.style(newStylesheet)` or `cy.style().fromJson(...).update()`
  ([core style docs](https://js.cytoscape.org/#cy.style)).
- ECharts is the only one with a built-in dark theme (`echarts.init(el, 'dark')`).

### Touch and mobile

- Chart.js listens to touch events by default (`touchstart`, `touchmove` are in the default `events` list,
  [interactions docs](https://www.chartjs.org/docs/latest/configuration/interactions.html)), so Sankey tooltips work on
  tap. Charts are responsive to their container. `orientation: 'vertical'` helps on phones.
- Cytoscape advertises "abstracted and unified touch events" and "pinch to zoom: touch & desktop"; the `tap` event covers
  mouse and touch ([js.cytoscape.org](https://js.cytoscape.org/)). For a small static Plan View, set
  `userZoomingEnabled: false`, `userPanningEnabled: false` and `autoungrabify: true` so a page scroll on a phone does not
  get captured by the graph and nodes cannot be dragged out of place.

### Keyboard and screen-reader support

- **Chart.js:** "it is up to the user to create the `canvas` element in a way that is accessible": give it `role="img"`
  and an `aria-label`, or meaningful fallback content inside the canvas
  ([accessibility docs](https://www.chartjs.org/docs/latest/general/accessibility.html)). The Sankey docs say nothing
  about accessibility.
- **Cytoscape:** nodes are not focusable. The maintainer's answer to keyboard requests is that "accessibility is
  something that needs to be built at the app level" ([#2962](https://github.com/cytoscape/cytoscape.js/issues/2962),
  closed; also [#3091](https://github.com/cytoscape/cytoscape.js/issues/3091), closed as stale).
- So for both views the accessible table/list from #193 carries the content; for the Plan View it must also carry the
  "click a node" actions (links to the editing page) as real `<a>`/`<button>` elements.
- ECharts' `aria.show` auto-generates a text description and `aria.decal` adds colour-blind patterns
  ([handbook](https://echarts.apache.org/handbook/en/best-practices/aria/)); still not keyboard-navigable, and not worth
  its size given the table is required anyway.

### Vendoring

The app vendors client libraries with LibMan (`PersonalFinance/src/PersonalFinance.Web/libman.json`, default provider
`cdnjs`) into `wwwroot/lib/<name>/`, and **commits the restored files to git** (e.g.
`wwwroot/lib/chartjs/chart.umd.min.js`). Views include them with a plain `<script src="~/lib/...">` in the page's
scripts section (e.g. `Views/Analysis/Index.cshtml`). Entries for the recommendation:

```json
{
  "library": "chartjs-chart-sankey@0.19.1",
  "provider": "jsdelivr",
  "destination": "wwwroot/lib/chartjs-chart-sankey",
  "files": [ "dist/chartjs-chart-sankey.min.js" ]
},
{
  "library": "cytoscape@3.34.4",
  "destination": "wwwroot/lib/cytoscape",
  "files": [ "cytoscape.min.js" ]
},
{
  "library": "cytoscape-dagre@4.0.1",
  "provider": "jsdelivr",
  "destination": "wwwroot/lib/cytoscape-dagre",
  "files": [ "dist/cytoscape-dagre.min.js" ]
}
```

- `chartjs-chart-sankey` and `cytoscape-dagre` are not on cdnjs (cdnjs API returns 404), so they use the `jsdelivr`
  provider, as the `@fluentui/*` entries already do. `cytoscape` 3.34.4 is on cdnjs (MIT) with `cytoscape.min.js` at the
  root of its file list, so the default provider works.
- Both UMD builds self-register when loaded after their host by plain `<script>` tags: the Sankey build calls
  `Chart.register(...)` on the global `Chart`, and the `cytoscape-dagre` README says "Plain HTML/JS has the extension
  registered for you automatically". Load order: `chart.umd.min.js` then the Sankey plugin; `cytoscape.min.js` then
  `cytoscape-dagre.min.js`.
- Small oddity: the `cytoscape-dagre@4.0.1` minified file's banner still says `4.0.0`. Harmless, but pin by version in
  LibMan, not by the banner.

### Maintenance (GitHub, 2026-10-23)

| Repo | Stars | Last push | Open issues | Notes |
|---|---|---|---|---|
| [cytoscape/cytoscape.js](https://github.com/cytoscape/cytoscape.js) | 11,247 | 2026-10-21 | 27 | Releases 3.34.2–3.34.4 in Aug–Oct 2026; maintainer `maxkfranz` |
| [cytoscape/cytoscape.js-dagre](https://github.com/cytoscape/cytoscape.js-dagre) | 287 | 2026-08-28 | 6 | Official Cytoscape org extension |
| [dagrejs/dagre](https://github.com/dagrejs/dagre) | 5,835 | 2026-08-08 | 178 | Revived under the `@dagrejs` scope (v3) |
| [kurkle/chartjs-chart-sankey](https://github.com/kurkle/chartjs-chart-sankey) | 185 | 2026-10-17 | 7 | Pre-1.0; four releases on 2026-09-12; author `kurkle` is Chart.js's second-largest contributor |
| [kieler/elkjs](https://github.com/kieler/elkjs) | 2,844 | 2026-10-19 | – | Licence excludes it |
| [d3/d3-sankey](https://github.com/d3/d3-sankey) | 947 | 2023-11-13 | – | No release since 2019 |

Risk to note: the Sankey plugin is pre-1.0 and effectively one maintainer, so pin the exact version. Its blast radius
is small (one 19 KB file, one view, table fallback always present).

## Does Cytoscape.js fit the Plan View?

Yes. The Plan View is a handful of nodes (Net Worth parameters, budget, Savings Rate, FI Target, Progress to FI,
Savings Goals) with directed "feeds into" edges: a small DAG, which is what dagre is for. Concretely:

- Layout: `cy.layout({ name: 'dagre', rankDir: 'LR', nodeDimensionsIncludeLabels: true }).run()`; `curve-style: taxi`
  gives right-angled, hierarchically bundled edges suited to DAGs ([style docs](https://js.cytoscape.org/#style/taxi-edges)).
- Click: `cy.on('tap', 'node', handler)` covers mouse and touch.
- Styling: node labels from `data(label)`, colours by a node class (entered vs computed), all in a JSON stylesheet.
- Keep it static: disable zoom/pan/grab as above.
- Cost is the weight (~148 KB gzip) for what may be under ten nodes. If #199 judges that too heavy, the fallback is a
  hand-laid HTML/CSS grid of buttons with an SVG overlay for arrows, which would also give keyboard focus for free. That
  is a design call for #197/#199, not a reason to reject Cytoscape now.

## Notes for other tickets

- **#196 (Flow View model):** a Sankey must balance. Decide how a deficit month is drawn (an inflow node such as
  "From savings") and how negative savings is shown; tiny categories can use `nodeMinSize` and hidden flow labels, or
  be grouped into "Other".
- **#197 (Plan View model):** node click actions must also exist as links in the accessible list, since canvas nodes
  are not keyboard-reachable.
- **#198 (prototype):** use Chart.js + Sankey for the Flow View and Cytoscape + dagre for the Plan View. The app has no
  dark mode, so "judge in dark mode" can only be a mock (e.g. a toggle on the throwaway page), not the real app theme.
- **ELK is not an option** for any later layout need (EPL/GPL); dagre is the DAG layout to use.
