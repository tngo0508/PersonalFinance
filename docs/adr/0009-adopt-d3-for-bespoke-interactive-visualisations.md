# 0009 – Adopt D3 for Bespoke Interactive Visualisations and Admit the ISC Licence

**Date:** 2026-10-23  
**Status:** Accepted (amends ADR 0001 §2; amended by [ADR 0014](0014-adopt-mermaid-for-authored-diagrams.md) (Mermaid for authored diagrams))

## Context

The app draws its charts with Chart.js 4.5.1 (vendored through libman, plus `chartjs-chart-sankey`), ApexCharts 3.45.2
(CDN, Dashboard only) and one hand-drawn canvas (Investment Calculator). None of them supports a picture the user
manipulates directly, such as dragging a handle that is two-way bound to the page's inputs, with animated transitions
between states. A flagship visualisation for a new Account's first session needs exactly that. D3 v7 does it, but it is
licensed under ISC, and the licence policy (ADR 0001 §2, `AGENTS.md`, `CONTRIBUTING.md`, `docs/CODING_STANDARDS.md`)
names only MIT, BSD (2/3-clause) and Apache 2.0. The ISC grant is functionally the same as MIT and BSD-2-Clause, and
ISC packages already sit in `node_modules` as transitive dependencies of dev tooling.

## Decision

1. **ISC joins the allowed licences** for any package: MIT, BSD (2/3-clause), Apache 2.0 and ISC. Paid, dual-licensed
   and copyleft (GPL, AGPL, LGPL, MPL and the like) stay banned, as do MediatR and AutoMapper.
2. **The list governs direct dependencies**: every NuGet, npm and libman entry, and anything shipped to the browser or
   deployed. Transitive dependencies of dev-only tooling are not audited one by one, but a copyleft licence anywhere in
   the tree still blocks the dependency that brings it in.
3. **Chart.js remains the default.** Use Chart.js (and its plugins) for any chart type it supports where the
   interaction is hover, tooltip or legend toggle. Use D3 only when the picture needs at least one of: direct
   manipulation (a drag handle bound to the page's inputs), a bespoke layout Chart.js has no type for, or choreographed
   transitions between states. Existing Chart.js and ApexCharts charts are not ported to D3.
4. **ApexCharts is not adopted on any new surface.** It stays on the Dashboard as is; new standard charts use Chart.js.
5. **D3 is vendored through libman, never loaded from a CDN at runtime.** The entry is the single UMD bundle
   `d3@7.9.0` from cdnjs, at `wwwroot/lib/d3/d3.min.js`, pinned to an exact version. Any `d3-*` module the bundle does
   not include needs its own pinned libman entry and a licence check.
6. **D3 loads only on pages that use it**, from that view's `Scripts` section, not from `_Layout`. The bundle is
   about 90 KiB gzipped and most pages never need it.

## Considered Options

- **A D3-only exception, with the list unchanged:** rejected. Most `d3-*` modules and many small npm libraries are
  ISC, so each would need its own exception for a licence no less permissive than the three already allowed.
- **Replace the list with a principle** ("permissive, OSI-approved, no copyleft or commercial terms"): rejected as
  broader than needed. It would invite an argument over each new licence.
- **Build the flagship in Chart.js with a drag plugin, or on a hand-drawn canvas:** rejected. Chart.js has no layout
  model for bespoke pictures, and hand-drawn canvas means rebuilding scales, transitions, hit-testing and an
  accessible equivalent that D3 and SVG provide.
- **The per-module `d3-*` files instead of the bundle:** rejected in the vendoring research
  (`research/d3-vendoring`). Eight ordered script tags and eight separately pinned entries, for about 70 KiB gzip
  saved on one page.

## Consequences

- The licence list in `AGENTS.md`, `CONTRIBUTING.md`, `README.md` and `docs/CODING_STANDARDS.md` (§License Policy and
  the repeat in the Feature Implementation Protocol) names ISC, and ADR 0001's Status reads "amended by ADR 0009 (ISC
  admitted)". ADR 0001's body stays as written.
- `libman.json` carries the `d3@7.9.0` entry. No page references the bundle until a feature needs it.
- D3 accessibility rules (reduced motion, keyboard-reachable drags, a text equivalent) belong to the flagship's spec,
  not this ADR.
