# D3 v7 vendoring path through libman

Research note for [#209](https://github.com/tngo0508/PersonalFinance/issues/209) (part of map [#207](https://github.com/tngo0508/PersonalFinance/issues/207)).
Every fact below was checked on 2026-10-23 against the source linked next to it. Byte sizes were measured by downloading the files with `curl` and running `stat`/`gzip -9` on them, not read from a listing.

## Recommendation

Vendor the single UMD bundle `d3@7.9.0` from **cdnjs** (the manifest's `defaultProvider`), exactly the way Chart.js is vendored today, and load it with one `<script>` tag before the feature script. Do not vendor `d3-selection`, `d3-drag` and `d3-transition` piecemeal.

```json
{
  "library": "d3@7.9.0",
  "destination": "wwwroot/lib/d3",
  "files": [
    "d3.min.js"
  ]
}
```

In the view, matching `Views/Analysis/Index.cshtml` lines 711-712 (vendored libraries carry no `asp-append-version`; only our own `wwwroot/js` files do):

```html
<script src="~/lib/d3/d3.min.js"></script>
<script src="~/js/<feature>/<file>.js" asp-append-version="true"></script>
```

Commit `wwwroot/lib/d3/d3.min.js` alongside the manifest change, as the other `wwwroot/lib/` folders are committed. `Microsoft.Web.LibraryManager.Build` (referenced in `PersonalFinance.Web.csproj` line 29) restores it on build either way.

## Questions from the ticket, answered

### 1. Which libman provider and library id serve the d3@7 UMD bundle?

| Fact | Value | Source |
|---|---|---|
| Current d3 release | **7.9.0**, published 2024-03-12 | `gh api repos/d3/d3/releases/latest` -> `v7.9.0 2024-03-12T22:18:19Z`; https://github.com/d3/d3/releases |
| npm `latest` tag | 7.9.0 | https://data.jsdelivr.com/v1/packages/npm/d3 |
| cdnjs library id | `d3`, version 7.9.0, files `["d3.js", "d3.min.js"]` (flat, no `dist/` prefix) | https://api.cdnjs.com/libraries/d3/7.9.0 |
| jsdelivr / unpkg path | `dist/d3.min.js` (package.json `unpkg` and `jsdelivr` fields both point at it; npm `main` is `src/index.js`, ESM) | https://raw.githubusercontent.com/d3/d3/v7.9.0/package.json ; https://data.jsdelivr.com/v1/packages/npm/d3@7.9.0?structure=flat |
| UMD global | `d3` (rollup `format: "umd", name: "d3"`) | https://raw.githubusercontent.com/d3/d3/v7.9.0/rollup.config.js |
| Official guidance | "we also provide a UMD bundle that exports the `d3` global when loaded as a plain script" | https://d3js.org/getting-started |

The three CDNs serve a byte-identical file: `sha256 f2094bbf6141b359722c4fe454eb6c4b0f0e42cc10cc7af921fc158fceb86539` for
https://cdnjs.cloudflare.com/ajax/libs/d3/7.9.0/d3.min.js, https://cdn.jsdelivr.net/npm/d3@7.9.0/dist/d3.min.js and https://unpkg.com/d3@7.9.0/dist/d3.min.js. So the provider choice is purely about matching the manifest: cdnjs is the `defaultProvider` in `libman.json` and is what Chart.js uses, so the entry needs no `provider` key. The jsdelivr equivalent would be `"library": "d3@7.9.0", "provider": "jsdelivr", "files": ["dist/d3.min.js"]`.

Libman manifest syntax (`name@version`, `provider` overriding `defaultProvider`, `files`, `destination`): https://learn.microsoft.com/en-us/aspnet/core/client-side/libman/libman-vs ("Manually configure LibMan manifest file entries"). That page also notes libman allows only one version of a given library per provider.

### 2. Piecemeal option: d3-selection, d3-drag, d3-transition

| Module | Latest (npm `latest` tag) | On cdnjs? | Runtime deps (package.json) |
|---|---|---|---|
| d3-selection | 3.0.0 | yes, 3.0.0 present (`d3-selection.min.js`) | none |
| d3-drag | 3.0.0 | yes, 3.0.0 present (`d3-drag.min.js`) | d3-dispatch `1 - 3`, d3-selection `3` |
| d3-transition | 3.0.1 | yes, 3.0.1 present (`d3-transition.min.js`) | d3-color, d3-dispatch, d3-ease, d3-interpolate, d3-timer (all `1 - 3`); peer d3-selection `2 - 3` |

Sources: https://data.jsdelivr.com/v1/packages/npm/d3-selection, https://data.jsdelivr.com/v1/packages/npm/d3-drag, https://data.jsdelivr.com/v1/packages/npm/d3-transition (latest tags); https://api.cdnjs.com/libraries/d3-selection?fields=version,versions, https://api.cdnjs.com/libraries/d3-drag?fields=version,versions, https://api.cdnjs.com/libraries/d3-transition?fields=version,versions (version arrays); https://raw.githubusercontent.com/d3/d3-selection/main/package.json, https://raw.githubusercontent.com/d3/d3-drag/main/package.json, https://raw.githubusercontent.com/d3/d3-transition/main/package.json (dependencies). d3@7.9.0 itself depends on `d3-selection: 3`, `d3-drag: 3`, `d3-transition: 3` (https://raw.githubusercontent.com/d3/d3/v7.9.0/package.json), so those are the versions the bundle contains.

Caveat: a cdnjs keyword search (`/libraries?search=d3-drag`) ranks other packages first and shows stale versions; look each library up by exact id (`/libraries/<id>`) instead.

Each micro-module UMD file augments the same `d3` namespace (`(globalThis).d3 = t.d3 || {}` in every header) and its AMD define lists its sibling modules, so the loading order on a plain-script page must respect the dependency graph: dispatch, timer, ease, color, interpolate, selection, then drag and transition. That is 8 files and 8 `<script>` tags for the three modules named in the ticket.

### 3. Minified sizes (measured 2026-10-23)

| File | Minified bytes | gzip -9 bytes |
|---|---|---|
| d3@7.9.0 `d3.min.js` (full bundle) | 279,706 (273 KiB) | 92,376 (90 KiB) |
| d3@7.9.0 `d3.js` (unminified, not needed) | 587,043 | - |
| d3-selection@3.0.0 | 13,522 | 4,299 |
| d3-drag@3.0.0 | 4,186 | 1,701 |
| d3-transition@3.0.1 | 11,718 | 3,808 |
| d3-dispatch@3.0.1 | 1,901 | 932 |
| d3-timer@3.0.1 | 1,947 | 973 |
| d3-ease@3.0.1 | 3,173 | 1,056 |
| d3-interpolate@3.0.1 | 7,863 | 3,210 |
| d3-color@3.1.0 | 10,577 | 4,250 |
| **Piecemeal total (8 files)** | **54,887 (54 KiB)** | **20,229 (20 KiB)** |

Bundle sizes cross-checked against the jsdelivr package listing (`/dist/d3.min.js`: 279,706 bytes): https://data.jsdelivr.com/v1/packages/npm/d3@7.9.0?structure=flat. For comparison, the already-vendored `chart.umd.min.js` (Chart.js 4.5.1) is about 205 KB, so the full D3 bundle is of the same order as what the Analysis page already loads.

### 4. Single bundle or per-module files on a no-bundler page?

**Single bundle.** Reasons, in order of weight:

1. **Manifest and view pattern.** One libman entry and one `<script>` tag matches Chart.js and chartjs-chart-sankey exactly. The piecemeal route is 8 libman entries (each a separate `wwwroot/lib/<module>/` folder to commit), 8 ordered `<script>` tags, and a dependency graph the page has to get right by hand with no tool checking it.
2. **Version drift.** libman pins each library independently, so d3-selection, d3-drag, d3-transition and their five transitive modules each need their own bump; the bundle is one version string, and the d3 release notes (https://github.com/d3/d3/releases) cover the whole set.
3. **Cache behaviour.** Vendored files in `wwwroot/lib/` are referenced without `asp-append-version` (see `Views/Analysis/Index.cshtml` and `_Layout.cshtml`), so a path change on upgrade is what busts the cache; that works the same for one file as for eight. `asp-append-version` only applies to our own `wwwroot/js` files and is unaffected by the choice.
4. **Cost.** About 70 KB gzip extra over piecemeal, on one page, served once per release from the same origin. If a later feature needs only `d3-selection`, nothing stops swapping to piecemeal then; the global stays `d3` either way, so feature code does not change.

D3's own getting-started page recommends the ESM build via `import` for new code (https://d3js.org/getting-started). That does not fit this project: the Web project has no bundler and loads classic scripts (ADR 0008, `_Layout.cshtml`), and the feature scripts are IIFE modules that read globals. The UMD bundle is the officially supported fallback for that case.

### 5. Global-name conflicts with Chart.js, ApexCharts and jQuery

The d3@7.9.0 UMD header is the only place the bundle writes to the global object:

```js
n((t="undefined"!=typeof globalThis?globalThis:t||self).d3=t.d3||{})
```

(first line of `dist/d3.min.js`; the factory body runs under `"use strict"` and only assigns to `this.*` inside constructors). A grep of the minified bundle for `jQuery`, `ApexCharts` and `\bChart\b` returns 0 matches, and it does not touch `$`. So the full set of globals after loading all four libraries is:

| Library | Global(s) | Source |
|---|---|---|
| D3 7.9.0 | `d3` | UMD header above; rollup `name: "d3"` |
| Chart.js 4.5.1 | `Chart` | header of `wwwroot/lib/chartjs/chart.umd.min.js`: `(globalThis).Chart=e()` |
| ApexCharts 3.45.2 | `ApexCharts` | loaded from jsdelivr in `Views/Dashboard/Index.cshtml` line 53 (not vendored through libman) |
| jQuery 3.7.1 | `$`, `jQuery` | `wwwroot/lib/jquery/dist/jquery.min.js` via `_Layout.cshtml` line 272 |

No overlap. D3 exposes `d3.select`, `d3.selectAll` and so on under its own namespace only, so there is no `$` shadowing of the kind older plugins caused with jQuery.

### 6. Licence

- `LICENSE` at tag v7.9.0 is the ISC licence, "Copyright 2010-2023 Mike Bostock": https://raw.githubusercontent.com/d3/d3/v7.9.0/LICENSE
- `package.json` at v7.9.0: `"license": "ISC"`: https://raw.githubusercontent.com/d3/d3/v7.9.0/package.json
- GitHub's licence detection for the repo: `gh api repos/d3/d3/license` -> `ISC ISC License`
- d3-selection, d3-drag and d3-transition `package.json` files also say `ISC` (links in section 2).

cdnjs's metadata (https://api.cdnjs.com/libraries/d3?fields=license) still reports `BSD-3-Clause`, which was D3's licence before the ISC change; the file we would vendor is built from the ISC-licensed repository. ISC is a permissive BSD-family licence, functionally equivalent to BSD-2-Clause. AGENTS.md's licence policy lists MIT, BSD (2/3-clause) and Apache 2.0 by name and does not mention ISC; the recommendation is to treat ISC as accepted under the BSD line, and to add "ISC" to the list in `AGENTS.md`/`docs/CODING_STANDARDS.md` in the implementing PR so the policy text matches the dependency.

## Open points for the implementing ticket

- Add the libman entry and commit `wwwroot/lib/d3/d3.min.js`.
- Add `ISC` to the written licence policy (see section 6).
- Load `~/lib/d3/d3.min.js` only on the page that needs it (its `Scripts` section), not in `_Layout.cshtml`.
