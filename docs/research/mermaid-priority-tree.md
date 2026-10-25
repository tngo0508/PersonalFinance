# Mermaid for the Priority Tree: version, re-render, path styling, libman

Research for [#298](https://github.com/tngo0508/PersonalFinance/issues/298) (map: [#297 Money Priorities](https://github.com/tngo0508/PersonalFinance/issues/297)). Checked on 2026-10-24 against the Mermaid source at tag `mermaid@11.15.0`, the Mermaid GitHub releases, the cdnjs and npm registries, the jsDelivr package API and the LibraryManager (libman) source.

## Answer in one paragraph

Yes. Mermaid can draw the authored tree from generated flowchart text, re-render it after each submit with `mermaid.render(...)` and highlight the computed path with `classDef`/`class` on nodes and `linkStyle` (or edge IDs) on edges, with the SVG scaling to its container and `accTitle`/`accDescr` producing real `<title>`/`<desc>` plus ARIA links. The catch is the version: **cdnjs stopped at `mermaid@11.15.0` (11 May 2026)** while npm is at 12.2.0 (13 Oct 2026), so libman's default cdnjs provider can only vendor 11.15.0. That is a fine, MIT-licensed, 906 KB-gzipped single file; 12.x is reachable through libman's jsdelivr/unpkg providers but is a breaking release (ES2024, Safari 17.4+, ELK layout and `neo` look by default, +0.6 MB gzipped).

## 1. Version, file, size, licence

| Fact | Value | Source |
|---|---|---|
| Latest version on cdnjs | **11.15.0** (released 2026-05-11) | `GET https://api.cdnjs.com/libraries/mermaid` -> `"version":"11.15.0"`; [release mermaid@11.15.0](https://github.com/mermaid-js/mermaid/releases/tag/mermaid%4011.15.0) |
| cdnjs file | `https://cdnjs.cloudflare.com/ajax/libs/mermaid/11.15.0/mermaid.min.js` | `GET https://api.cdnjs.com/libraries/mermaid/11.15.0?fields=files,sri` lists `mermaid.min.js`, `mermaid.js`, `mermaid.esm.min.mjs`, `mermaid.esm.mjs`, `mermaid.core.mjs` and `chunks/...` |
| Size | 3,312,967 bytes raw; **906,583 bytes as served gzip** (`Content-Length` with `Accept-Encoding: gzip`); 904,062 bytes with `gzip -9` locally | HEAD/GET against the cdnjs URL on 2026-10-24 |
| SRI | `sha512-HH52omhHpZF6RfVnGiQwYgYm4H/ya2xsZYLl5xJ4+tLfX+rN4+8zF7V/H/KLeicPrKZYi1g6iBmVkk2AhXTGlg==` | same cdnjs API call |
| Global | the IIFE ends with `globalThis["mermaid"] = ...default`, so `window.mermaid` exists after the script tag; no module loader needed | tail of `mermaid.min.js` |
| Licence | **MIT** ("The MIT License (MIT) Copyright (c) 2014 - 2022 Knut Sveidqvist") | [LICENSE at mermaid@11.15.0](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/LICENSE); npm `license: "MIT"`; cdnjs `license: "MIT"`. Allowed by the repo policy (ADR 0009). |
| Browser floor (11.x) | not declared (`browserslist`/`engines` absent from the 11.15.0 `package.json`) | `GET https://registry.npmjs.org/mermaid/11.15.0` |

Do not vendor `mermaid.esm.min.mjs` on its own: it is a 28 KB entry file that imports the `chunks/mermaid.esm.min/*.mjs` files next to it. The single-file build is `mermaid.min.js`.

### cdnjs is behind npm

npm versions after 11.15.0 (from `https://registry.npmjs.org/mermaid` `time`): 11.16.1 (2026-08-04), 11.17.0 (08-19), 11.17.1 (08-24), 11.17.2 (08-25), 12.0.0 (09-10), 12.1.0 (10-02), 12.2.0 (10-13). None of them is on cdnjs. The cdnjs package definition ([packages/m/mermaid.json](https://github.com/cdnjs/packages/blob/master/packages/m/mermaid.json)) auto-updates from npm with `basePath: dist, files: **/!(*.html)`, and cdnjs's per-file limit is 25 MiB ([cdnjs/tools util/const.go](https://github.com/cdnjs/tools/blob/master/util/const.go), `MaxFileSize = 26214400`); the largest 12.2.0 file is `mermaid.min.js.map` at 19,497,703 bytes, so the limit is not the cause. There is an open request [cdnjs/packages#2024 "Update: mermaid"](https://github.com/cdnjs/packages/issues/2024) and a general [cdnjs/cdnjs#14263 "Libraries not being updated to latest versions"](https://github.com/cdnjs/cdnjs/issues/14263). Treat cdnjs as stale for Mermaid; re-check `api.cdnjs.com/libraries/mermaid` before pinning.

Sizes of the newer single-file builds from jsDelivr (`https://cdn.jsdelivr.net/npm/mermaid@<v>/dist/mermaid.min.js`, measured 2026-10-24): 11.17.2 = 3,572,661 raw / 975,706 gzip; 12.2.0 = 5,494,801 raw / 1,568,807 gzip.

### What 12.0.0 changes (if a later upgrade is considered)

From the [mermaid@12.0.0 release notes](https://github.com/mermaid-js/mermaid/releases/tag/mermaid%4012.0.0):

- "This is a breaking release (ES2024, Safari 17.4+, Node 22.12+)."
- "ELK is now bundled with mermaid and is the default layout algorithm." Flowcharts without a `layout` re-lay out with ELK; "The single-file IIFE build (`mermaid.min.js`) inlines it and grows by roughly 500 kB gzipped."
- A new `neo` default appearance; "add `layout: dagre`, `theme: default` and `look: classic` to your config to keep the old look."
- `flowchart.defaultRenderer` is removed; use top-level `layout`.

In 11.15.0 the schema defaults are `look: classic` and `layout: dagre` ([config.schema.yaml at 11.15.0](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/schemas/config.schema.yaml)). The live docs at mermaid.js.org describe 12.x; read them with that in mind.

## 2. libman entry

libman's cdnjs provider resolves `https://api.cdnjs.com/libraries/{name}/{version}?fields=files` and downloads `https://cdnjs.cloudflare.com/ajax/libs/{name}/{version}/{file}` ([CdnjsCatalog.cs](https://github.com/aspnet/LibraryManager/blob/main/src/LibraryManager/Providers/Cdnjs/CdnjsCatalog.cs), [CdnjsProvider.cs](https://github.com/aspnet/LibraryManager/blob/main/src/LibraryManager/Providers/Cdnjs/CdnjsProvider.cs)). Both URLs answer for `mermaid/11.15.0/mermaid.min.js` (verified above), so this entry in `PersonalFinance/src/PersonalFinance.Web/libman.json` works with the file's existing `"defaultProvider": "cdnjs"`:

```json
{
  "library": "mermaid@11.15.0",
  "destination": "wwwroot/lib/mermaid",
  "files": [ "mermaid.min.js" ]
}
```

libman also ships `jsdelivr` and `unpkg` providers ([LibraryManager README](https://github.com/aspnet/LibraryManager#readme)); a per-library `"provider": "jsdelivr"` with `"library": "mermaid@11.17.2"` or `"mermaid@12.2.0"` and `"files": ["dist/mermaid.min.js"]` is the route to a newer version. Load the script only on the Money Priorities view (a `@section Scripts` block), not in `_Layout`.

## 3. Re-rendering with `mermaid.render` (and why not `mermaid.run`)

API at 11.15.0 ([mermaid.ts](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/mermaid.ts), [mermaidAPI.ts](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/mermaidAPI.ts), [usage docs](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/docs/config/usage.md)):

- `mermaid.render(id, text, container?)` returns `Promise<{ diagramType, svg, bindFunctions }>`. The JSDoc: "Multiple calls to this function will be enqueued to run serially." The caller inserts the result: `element.innerHTML = svg; bindFunctions?.(element);` ("This function binds events to the SVG **after** it is inserted into the DOM.").
- `id` becomes the `id` attribute of the `<svg>`, and the SVG carries its own `<style>` block scoped to `#id` (built by `createUserStyles`/`getStyles` in [styles.ts](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/styles.ts)). Replacing `innerHTML` therefore replaces the styles too; nothing accumulates in `<head>`.
- Internals that matter for flicker and leftovers (mermaidAPI.ts `render`):
  - **With a `container`:** `svgContainingElement.innerHTML = ''` runs *first*, the diagram is drawn inside a temporary `<div id="d{id}">` in that container, the SVG markup is read back, and the temporary div is removed (`removeTempElements`). Between the call and the caller's `innerHTML = svg` the container is empty, so rendering into the visible container blanks the diagram for a frame or more.
  - **Without a `container`:** `removeExistingElements(document, id, 'd'+id, 'i'+id)` removes *any element in the document* with that id, then a temporary div is appended to `document.body` and removed after rendering. So re-using the id of the SVG currently on screen also removes it before the new one is ready.
  - **Recommendation:** render **without a container and with a fresh id per render** (for example `priority-tree-<n>`), then swap the result into the visible element with one `innerHTML` assignment. The old SVG stays visible until the new markup exists; the temporary body div is removed by Mermaid; no stale ids remain because the only SVG left is the new one. Keep `startOnLoad: false`.
  - **Determinism:** `deterministicIds` defaults to `false` ("IDs are generated from the current date"); set `deterministicIds: true` (optionally `deterministicIDSeed`) if E2E tests will assert on internal ids. For the `handDrawn` look, `handDrawnSeed` defaults to `0` meaning "a random seed", so every re-render would redraw the wobbly strokes differently; set a fixed seed so re-renders look identical except for the highlight.
  - **Serialisation:** calls queue, so two quick submits render in order; still disable the submit button until the promise resolves.
- `mermaid.run({ nodes | querySelector, suppressErrors, postRenderCallback })` reads the diagram text from the element's `innerHTML`, renders into it and then **sets `data-processed="true"` and skips elements that already carry it** ("The function tags the processed attributes with the attribute data-processed and ignores found elements with the attribute already set."). Re-running on the same element needs the attribute removed and the text restored, which is why `render` is the better fit for "re-render after each submit".
- `mermaid.parse(text, { suppressErrors })` validates generated text without rendering; `suppressErrorRendering: true` (default `false`) stops the red "Syntax error" diagram from being inserted.
- `maxTextSize` default 50000 characters; an authored tree is far below that.

## 4. Highlighting the path, dimming the rest

All from the flowchart docs at 11.15.0 ([flowchart.md](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/docs/syntax/flowchart.md)):

- **Nodes:** `classDef onPath fill:#...,stroke:#...,stroke-width:3px;` then `class n1,n4,n7 onPath;` (or `n1:::onPath` inline). Several classes share one definition: `classDef a,b font-size:12pt;`. `classDef default ...` applies "to all classes without specific class definitions", which is the natural place for the dimmed look (for example `fill:#f4f4f4,stroke:#bbb,color:#888`).
- **Edges by index:** `linkStyle 3 stroke:#ff3,stroke-width:4px,color:red;` ("the order number of when the link was defined in the graph is used", zero-based) and `linkStyle 1,2,7 color:blue;`; `linkStyle default ...` styles all links. Because the text is generated from the JSON tree in a fixed order, the generator knows every edge's index.
- **Edges by id (cleaner):** "prepend the edge syntax with the ID followed by an `@` character", for example `A e1@--> B`; then `class e1 onPathEdge` with a `classDef onPathEdge stroke:...,stroke-width:...;`. Edge ids also take `e1@{ animate: true }` / `e1@{ animation: fast }` and, from v11.10.0, `e1@{ curve: linear }`. Generate edge ids from the tree (`edge-<from>-<to>`) and the path becomes a list of `class` statements.
- **Do not style from site.css:** "Mermaid's internal styles are injected with `!important` and scoped to the SVG element ID ... External CSS will be silently overridden. The recommended approach is to use the `classDef` syntax." So the highlight belongs in the generated text, which also keeps "picture and path cannot disagree" true for the styling.
- Labels: quote them (`id["Pay off debt: $1,200/mo"]`) and escape with entity codes (`#quot;`, `#35;` for `#`, `#9829;`); markdown strings go in `` "`...`" ``. Dollar amounts need no escaping.

## 5. `securityLevel` and `htmlLabels`

- `securityLevel` default is **`strict`**: "HTML tags in the text are encoded and click functionality is disabled." Other values: `antiscript` (HTML allowed, scripts removed, clicks on), `loose` (HTML allowed, clicks on), `sandbox` (renders in a sandboxed iframe; "may hinder interactive functionality"). Source: config.schema.yaml at 11.15.0.
- Under anything but `loose`, the SVG string is also run through DOMPurify (`DOMPurify.sanitize(svgCode, { ADD_TAGS: ['foreignobject'], ADD_ATTR: ['dominant-baseline'], HTML_INTEGRATION_POINTS: { foreignobject: true } })`, mermaidAPI.ts). Under `sandbox` the output is an `<iframe sandbox>` with a base64 `data:` body, which would break page CSS hooks and Playwright selectors; stay on `strict`.
- `htmlLabels`: the root option has no schema default; the effective value is `evaluate(config.htmlLabels ?? config.flowchart?.htmlLabels ?? true)` ([config.ts `getEffectiveHtmlLabels`](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/config.ts)), so labels render as HTML inside `<foreignObject>` by default; `flowchart.htmlLabels` is deprecated in favour of the root key. With `strict`, user text inside those labels is encoded, so the generated labels (which contain user-entered amounts) are safe either way. `htmlLabels: false` gives plain SVG `<text>` labels, which Playwright can read with `textContent` just as well and which do not depend on `foreignObject` support.

## 6. Responsive SVG

`useMaxWidth` is `true` by default for every diagram type via `BaseDiagramConfig` ("When this flag is set to `true`, the height and width is set to 100% and is then scaled with the available space. If set to `false`, the absolute space required is used."). Implementation ([setupGraphViewbox.js](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/setupGraphViewbox.js)): with `useMaxWidth` the SVG gets `width="100%"`, `style="max-width: {w}px;"` and a `viewBox`, so it shrinks with its container, keeps its aspect ratio and never grows past its natural width. For a tall decision tree on a phone, consider `flowchart LR` versus `TB` and a horizontally scrollable wrapper; the SVG itself needs no extra CSS. `flowchart.wrappingWidth` (default 200) controls label wrapping for markdown strings.

## 7. Accessibility hooks

[accessibility docs](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/docs/config/accessibility.md) and [accessibility.ts](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/accessibility.ts):

- `accTitle: Your suggested money path` (single line) and `accDescr: ...` (single line) or `accDescr { multi-line }` go in the diagram text. They become `<title>` and `<desc>` children of the SVG, referenced by `aria-labelledby` / `aria-describedby`; the SVG also gets `role="graphics-document document"` and `aria-roledescription` set to the diagram type key (a flowchart reports `flowchart-v2`).
- Because the text is generated per Walk, `accDescr` can carry the ordered path ("Step 1: pay down the 24% APR card; Step 2: ...") so the diagram's text equivalent is in the SVG itself; the map's "Not yet specified" item (an ordered list beside the diagram) still applies for keyboard reach and reduced motion. Do not use edge `animate` on the path unless `prefers-reduced-motion` is respected; Mermaid has no built-in reduced-motion switch (the animation is a CSS `stroke-dashoffset` keyframe in its injected styles).

## 8. Dark mode and the Ledger theme

From [theming.md at 11.15.0](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/docs/config/theming.md) and the schema:

- Themes in 11.15.0: `default`, `base`, `dark`, `forest`, `neutral`, `neo`, `neo-dark` (12.x adds `redux*`). "the only theme that can be customized is the `base` theme"; customisation goes through `themeVariables` (`primaryColor`, `primaryTextColor`, `primaryBorderColor`, `lineColor`, `secondaryColor`, `tertiaryColor`, `background`, `mainBkg`, `nodeBorder`, `clusterBkg`, `edgeLabelBackground`, `fontFamily`, `fontSize`, `darkMode`). `darkMode` "Affects how derived colors are calculated. Set value to `true` for dark mode."
- "The theming engine will only recognize hex colors and not color names." CSS custom properties (`var(--ledger-ink)`) are therefore **not** usable in `themeVariables`; resolve the Ledger tokens to hex in JavaScript (`getComputedStyle(document.documentElement).getPropertyValue(...)`) before calling `mermaid.initialize({ theme: 'base', themeVariables: {...} })`.
- Theme colours are baked into the SVG's `<style>` at render time (`initialize` computes `themeVariables` via `theme[...].getThemeVariables`, mermaidAPI.ts). Toggling the site theme does not restyle an existing SVG; the page must call `mermaid.initialize` with the new variables and re-render. Since the page already re-renders on submit, hook the same function to the theme toggle (or `matchMedia('(prefers-color-scheme: dark)')`).
- `classDef` colours for the highlighted path are literal hex in the text, so the generator must emit a light and a dark variant (or the JavaScript must pass the current palette to the text builder).
- Per-diagram overrides are possible in frontmatter (`---\nconfig:\n  theme: base\n  themeVariables: ...\n---`), but `initialize` is simpler for one diagram per page.

## 9. Generating Mermaid from the JSON tree

Nothing in Mermaid distinguishes typed from generated text; `render` takes a string. The generator emits, in a fixed order: the frontmatter (`look`, `theme`, `accTitle`/`accDescr`), `flowchart TB`, one node line per tree node (`q1{"High-interest debt above 10% APR?"}` for decisions, `l1["Leaf: ..."]` for leaves), one edge line per child (`q1 e-q1-l1@-->|Yes| l1`), then `classDef` statements and the `class` lists computed from the path returned by the ApiService. Validate with `mermaid.parse` in tests. The "hand-drawn" owner draft is preserved by `look: handDrawn` (enum `classic | handDrawn | neo`, schema at 11.15.0; example in the [syntax reference](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/docs/intro/syntax-reference.md)) with a fixed `handDrawnSeed`; with `look: classic` the output is the standard Mermaid flowchart. Either way the shapes and layout come from the same text, so the owner's Mermaid draft and the model-generated text can be diffed directly during transcription.

## 10. Recommendation for the spec

1. Pin `mermaid@11.15.0` from cdnjs through libman (`mermaid.min.js` only, ~906 KB gzipped, MIT), loaded only on the Money Priorities page. Record in ADR 0014 that cdnjs is behind npm and that a jsdelivr/unpkg libman provider is the upgrade path; 12.x needs an explicit `layout: dagre`, `look`, Safari 17.4+ acceptance and +0.6 MB.
2. `mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme: 'base', themeVariables: <Ledger hex>, handDrawnSeed: <fixed>, deterministicIds: true })`.
3. On each submit response: build text from the tree model + path, `await mermaid.render('priority-tree-' + (++n), text)`, then `host.innerHTML = svg`. Do not pass the host as the container and do not reuse the id.
4. Highlight with `classDef onPath` + `class ...` on nodes and edge ids + `class` on edges; dim with `classDef default` and `linkStyle default`.
5. Put the ordered path into `accDescr`, and keep the HTML ordered list beside the SVG as the primary text equivalent.
6. Re-render when the site theme toggles.

## Open items

- Whether to accept 11.15.0 (May 2026) or move to the jsdelivr provider for 11.17.2/12.2.0: depends on the browser floor the app promises and the acceptable bundle size.
- Which `look` the owner wants (`classic` versus `handDrawn`) decides whether `handDrawnSeed` matters.
- Playwright assertions on the highlighted path ([#297 "Not yet specified"](https://github.com/tngo0508/PersonalFinance/issues/297)): flowchart node elements get `id="{svgId}-flowchart-{nodeId}-{counter}"` ([flowDb.ts at 11.15.0](https://github.com/mermaid-js/mermaid/blob/mermaid%4011.15.0/packages/mermaid/src/diagrams/flowchart/flowDb.ts), `MERMAID_DOM_ID_PREFIX = 'flowchart-'`) and carry the `classDef` class names, so `svg g.node.onPath` or `[id*="-flowchart-l3-"]` is a stable selector; verify in the re-render prototype.
