/* global d3 */
/**
 * PROTOTYPE, throwaway (wayfinder ticket #234, "Sectors upgrade: breadcrumbs, percent tooltips and a treemap view").
 * Three variants of the Sectors tab on the existing /Research route, switched by ?variant=A|B|C and the floating bar.
 * Only loaded in Development when ?variant= is present (see Views/Research/Index.cshtml). Reads real SEC data
 * (top 500 filers per year, 2011-2025) from /prototype/sectors-upgrade/sectors-real.json, converted from the
 * prototype/research-page branch. Never merge this file to main.
 *
 *   A  Sunburst + breadcrumb: clickable trail, focusable arcs (arrow keys, Enter, Escape), takeaways in the side panel.
 *   B  Treemap first: zoomable treemap with the breadcrumb as its header bar, a Treemap/Sunburst toggle, a stat strip.
 *   C  Sunburst + drill list: small sunburst beside a ranked bar list of the focus's children; the list is the
 *      keyboard path; callouts are sentences above the list.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    if (!page || typeof d3 === 'undefined') {
        return;
    }
    const { formatMoney, prefersReducedMotion } = page;
    const VARIANTS = [
        { key: 'A', name: 'Sunburst + breadcrumb' },
        { key: 'B', name: 'Treemap first' },
        { key: 'C', name: 'Sunburst + drill list' }
    ];
    const OFFICE_SLOTS = ['Energy & Transportation', 'Finance', 'Life Sciences', 'Manufacturing',
        'Real Estate & Construction', 'Technology', 'Trade & Services', 'Industrial Applications and Services'];
    const MS = 750;
    const RINGS = 3;

    const params = new URLSearchParams(window.location.search);
    const variant = (VARIANTS.find((v) => v.key === (params.get('variant') || 'A').toUpperCase()) || VARIANTS[0]).key;

    const colour = (office) => {
        const slot = OFFICE_SLOTS.indexOf(office);
        const token = slot >= 0 ? `--research-office-${slot + 1}` : '--research-office-neutral';
        return getComputedStyle(document.documentElement.querySelector('.research') || document.body)
            .getPropertyValue(token).trim() || '#9ca3af';
    };
    const labelInk = (office) => (office === 'Manufacturing' ? '#0b0b0b' : '#ffffff');
    const pct = (v, of) => (of > 0 ? `${(v / of * 100).toFixed(1)}%` : '0.0%');
    const duration = () => (prefersReducedMotion() ? 0 : MS);
    const shorten = (t, n) => (t.length > n ? `${t.slice(0, Math.max(1, n - 1))}…` : t);
    const el = (tag, cls, text) => {
        const e = document.createElement(tag);
        if (cls) {
            e.className = cls;
        }
        if (text !== undefined) {
            e.textContent = text;
        }
        return e;
    };

    // ---------- shared data ----------

    function buildRoot (data, index) {
        const period = data.values[index];
        const offices = new Map();
        period.ranked.forEach((row) => {
            const c = data.companies[row.company];
            const ind = data.industries[c.industry];
            const officeName = data.offices[ind.office];
            if (!offices.has(ind.office)) {
                offices.set(ind.office, { key: `o${ind.office}`, name: officeName, office: officeName, kind: 'office', children: new Map() });
            }
            const o = offices.get(ind.office);
            if (!o.children.has(c.industry)) {
                o.children.set(c.industry, { key: `i${c.industry}`, name: ind.name, office: officeName, kind: 'industry', technology: ind.technology, children: [] });
            }
            o.children.get(c.industry).children.push({
                key: `c${row.company}`, name: c.name, ticker: c.ticker, office: officeName, kind: 'company',
                technology: ind.technology, value: row.publicFloat
            });
        });
        const children = Array.from(offices.keys()).sort((a, b) => a - b).map((k) => {
            const o = offices.get(k);
            return { ...o, children: Array.from(o.children.values()) };
        });
        return d3.hierarchy({ key: 'root', name: 'All filers', kind: 'root', children })
            .sum((d) => (d.children ? 0 : d.value || 0))
            .sort((a, b) => (a.depth === 1 && b.depth === 1 ? 0 : b.value - a.value));
    }

    const findKey = (root, key) => root.descendants().find((d) => d.data.key === key) || root;
    const kindLabel = (n) => ({ root: 'All filers', office: 'SEC industry office', industry: 'SIC industry', company: n.data.ticker ? `Ticker ${n.data.ticker}` : 'No ticker' })[n.data.kind];

    /** Tooltip lines shared by every variant: name, kind, value, % of parent, % of all, rank among siblings. */
    function tooltipFor (tooltip) {
        return page.createTooltip(tooltip, (line, n) => {
            const root = n.ancestors().pop();
            const parts = [line(n.data.name, 'research-tooltip__name'), line(kindLabel(n), 'research-tooltip__muted')];
            const value = el('span');
            value.append(line(formatMoney(n.value), 'research-tooltip__value'), ' Public float');
            parts.push(value);
            if (n.parent && n.parent !== root) {
                parts.push(line(`${pct(n.value, n.parent.value)} of ${n.parent.data.name}`, 'research-tooltip__muted'));
            }
            parts.push(line(`${pct(n.value, root.value)} of all filers`, 'research-tooltip__muted'));
            if (n.parent) {
                const rank = n.parent.children.indexOf(n) + 1;
                parts.push(line(`#${rank} of ${n.parent.children.length} in ${n.parent.data.name}`, 'research-tooltip__muted'));
            }
            return parts;
        });
    }

    /** Key takeaways for the focus: largest child, top-10 concentration, Technology share. */
    function takeaways (root, focus, periodLabel) {
        const leaves = focus.leaves().filter((d) => d.data.kind === 'company').sort((a, b) => b.value - a.value);
        const top10 = d3.sum(leaves.slice(0, 10), (d) => d.value);
        const biggestChild = focus.children ? focus.children.slice().sort((a, b) => b.value - a.value)[0] : null;
        const tech = d3.sum(focus.leaves().filter((d) => d.data.technology), (d) => d.value);
        const prev = root.prevRoot ? findKey(root.prevRoot, focus.data.key) : null;
        const change = prev && prev.data.key === focus.data.key && prev.value > 0 ? (focus.value / prev.value - 1) * 100 : null;
        return [
            { label: 'Total', value: formatMoney(focus.value), note: `${focus.data.name}, ${periodLabel}${ focus.depth ? ` · ${pct(focus.value, root.value)} of all` : ''}` },
            biggestChild && { label: 'Largest part', value: pct(biggestChild.value, focus.value), note: biggestChild.data.name },
            leaves.length > 10 && { label: 'Top 10 companies', value: pct(top10, focus.value), note: `of ${leaves.length} companies shown` },
            { label: 'Technology (incl. chip makers)', value: pct(tech, focus.value), note: formatMoney(tech) },
            change !== null && { label: 'Change on prior year', value: `${change >= 0 ? '+' : ''}${change.toFixed(1)}%`, note: 'same group, Public float' }
        ].filter(Boolean);
    }

    /** A breadcrumb <nav>: every ancestor is a button except the last. */
    function renderCrumbs (host, focus, onPick, cls) {
        host.replaceChildren();
        const nav = el('nav', `proto-crumbs ${cls || ''}`);
        nav.setAttribute('aria-label', 'Sectors drill-down');
        const ol = el('ol');
        focus.ancestors().reverse().forEach((n, i, all) => {
            const li = el('li');
            if (i === all.length - 1) {
                const cur = el('span', 'proto-crumbs__current', `${n.data.name} · ${formatMoney(n.value)}`);
                cur.setAttribute('aria-current', 'location');
                li.append(cur);
            } else {
                const b = el('button', 'proto-crumbs__link', n.data.name);
                b.type = 'button';
                b.addEventListener('click', () => onPick(n.data.key));
                li.append(b);
            }
            ol.append(li);
        });
        nav.append(ol);
        host.append(nav);
    }

    // ---------- sunburst (A and C) ----------

    function createSunburst (host, opts) {
        let svg, arcG, labelG, centre, unit, arc, root, focus;
        const maxSize = opts.maxSize || 600;

        function layout () {
            const size = Math.max(220, Math.min(host.clientWidth || maxSize, maxSize));
            unit = size / 2 / (RINGS + 1);
            arc = d3.arc().startAngle((d) => d.x0).endAngle((d) => d.x1)
                .padAngle((d) => Math.min((d.x1 - d.x0) / 2, 0.003)).padRadius(unit * 2)
                .innerRadius((d) => d.y0 * unit).outerRadius((d) => Math.max(d.y0 * unit, d.y1 * unit - 2));
            d3.select(host).selectAll('svg').remove();
            svg = d3.select(host).append('svg').attr('viewBox', [-size / 2, -size / 2, size, size]).attr('class', 'proto-sunburst');
            arcG = svg.append('g');
            labelG = svg.append('g').attr('text-anchor', 'middle').attr('pointer-events', 'none');
            centre = svg.append('g').attr('text-anchor', 'middle').style('cursor', 'pointer').on('click', () => focus.parent && opts.onFocus(focus.parent.data.key));
            centre.append('circle').attr('r', unit - 2).attr('fill', 'transparent');
            centre.append('text').attr('class', 'proto-centre-title').attr('y', -10);
            centre.append('text').attr('class', 'proto-centre-value').attr('dy', '0.6em');
        }

        const target = (n) => {
            const span = focus.x1 - focus.x0;
            const a = (x) => Math.max(0, Math.min(1, (x - focus.x0) / span)) * 2 * Math.PI;
            return { x0: a(n.x0), x1: a(n.x1), y0: Math.max(0, n.y0 - focus.depth), y1: Math.max(0, n.y1 - focus.depth) };
        };
        const visible = (t) => t.y0 >= 1 && t.y1 <= RINGS + 1 && t.x1 > t.x0;
        const labelT = (t) => {
            const a = (t.x0 + t.x1) / 2 * 180 / Math.PI;
            return `rotate(${a - 90}) translate(${(t.y0 + t.y1) / 2 * unit},0) rotate(${a < 180 ? 0 : 180})`;
        };

        function draw (newRoot, focusKey, animate) {
            root = newRoot;
            d3.partition().size([2 * Math.PI, RINGS + 1])(root);
            focus = findKey(root, focusKey);
            const nodes = root.descendants().slice(1).filter((d) => d.x1 > d.x0);
            nodes.forEach((d) => { d.t = target(d); });
            const tr = svg.transition().duration(animate ? duration() : 0).ease(d3.easeCubicInOut);

            const paths = arcG.selectAll('path').data(nodes, (d) => d.data.key);
            paths.exit().transition(tr).attrTween('d', function () {
                const i = d3.interpolate(this.__a, { ...this.__a, x1: this.__a.x0 });
                return (t) => arc(this.__a = i(t));
            }).remove();
            const enter = paths.enter().append('path')
                .each(function (d) { this.__a = { ...d.t, x1: d.t.x0 }; })
                .on('click', (e, d) => d.children && opts.onFocus(d.data.key))
                .on('pointermove', (e, d) => opts.tooltip.show(e, d))
                .on('pointerleave', () => opts.tooltip.hide())
                .on('keydown', (e, d) => opts.onKey && opts.onKey(e, d));
            const all = enter.merge(paths)
                .attr('fill', (d) => colour(d.data.office))
                .attr('class', (d) => `proto-arc${d.children ? ' proto-arc--zoomable' : ''}`)
                .attr('pointer-events', (d) => (visible(d.t) ? null : 'none'))
                .attr('aria-label', (d) => `${d.data.name}, ${formatMoney(d.value)}, ${pct(d.value, d.parent.value)} of ${d.parent.data.name}`);
            if (opts.focusableArcs) {
                all.attr('role', 'button').attr('tabindex', -1);
            }
            all.transition(tr)
                .attr('fill-opacity', (d) => (!visible(d.t) ? 0 : (opts.highlightKey && opts.highlightKey() && d.data.key !== opts.highlightKey() && !d.ancestors().some((a) => a.data.key === opts.highlightKey()) ? 0.25 : [1, 1, 0.85, 0.7][d.depth - focus.depth])))
                .attrTween('d', function (d) {
                    const i = d3.interpolate(this.__a, d.t);
                    return (t) => arc(this.__a = i(t));
                });

            const labelled = nodes.filter((d) => visible(d.t) && (d.t.x1 - d.t.x0) * (d.t.y0 + d.t.y1) / 2 * unit > 13);
            const labels = labelG.selectAll('text').data(labelled, (d) => d.data.key);
            labels.exit().remove();
            labels.enter().append('text').attr('dy', '0.35em').attr('class', 'proto-arc-label')
                .merge(labels)
                .attr('fill', (d) => labelInk(d.data.office))
                .text((d) => shorten(d.data.kind === 'company' && d.data.ticker ? d.data.ticker : d.data.name, Math.floor((unit - 6) / 6)))
                .attr('fill-opacity', 0)
                .transition(tr).attr('fill-opacity', 1).attr('transform', (d) => labelT(d.t));

            centre.select('.proto-centre-title').text(focus.depth ? shorten(focus.data.name, Math.floor(unit / 4)) : opts.periodLabel());
            centre.select('.proto-centre-value').text(formatMoney(focus.value));
            svg.attr('role', 'img').attr('aria-label', `Sunburst: ${focus.ancestors().reverse().map((n) => n.data.name).join(' › ')}, ${formatMoney(focus.value)}`);
            return { root, focus, nodes };
        }

        layout();
        return {
            draw,
            relayout: layout,
            arcNode: (key) => arcG.select(`path[data-k="${key}"]`).node(),
            arcs: () => arcG.selectAll('path'),
            refreshOpacity: () => draw(root, focus.data.key, false)
        };
    }

    // ---------- treemap (B) ----------

    function createTreemap (host, opts) {
        let svg, w, h;
        function layout () {
            w = Math.max(280, host.clientWidth || 720);
            h = Math.round(Math.min(560, Math.max(320, w * 0.62)));
            d3.select(host).selectAll('svg').remove();
            svg = d3.select(host).append('svg').attr('viewBox', [0, 0, w, h]).attr('class', 'proto-treemap');
        }
        function draw (root, focusKey, animate, prevFocusKey) {
            const focus = findKey(root, focusKey);
            // Lay out the focus's subtree only: children as tiles, grandchildren drawn faintly inside them.
            const sub = d3.hierarchy(focus.data).sum((d) => (d.children ? 0 : d.value || 0)).sort((a, b) => b.value - a.value);
            d3.treemap().size([w, h]).paddingInner(2).paddingTop((d) => (d.depth === 1 && d.children ? 18 : 0)).round(true)(sub);
            const tiles = sub.children || [];
            const t = svg.transition().duration(animate ? duration() : 0).ease(d3.easeCubicInOut);

            // Zoom-in feel: new tiles grow from the tile that was clicked (or shrink back into it on zoom-out).
            const from = prevFocusKey ? (tiles.find((d) => d.data.key === prevFocusKey) || null) : null;
            const g = svg.selectAll('g.proto-tile').data(tiles, (d) => d.data.key);
            g.exit().transition(t).style('opacity', 0).remove();
            const enter = g.enter().append('g').attr('class', 'proto-tile')
                .attr('transform', from ? `translate(${from.x0},${from.y0}) scale(${(from.x1 - from.x0) / w},${(from.y1 - from.y0) / h})` : (d) => `translate(${d.x0},${d.y0})`)
                .style('opacity', 0);
            enter.append('rect').attr('class', 'proto-tile__rect');
            enter.append('g').attr('class', 'proto-tile__kids');
            enter.append('text').attr('class', 'proto-tile__name').attr('x', 6).attr('y', 13);
            enter.append('text').attr('class', 'proto-tile__value').attr('x', 6).attr('y', 28);
            const all = enter.merge(g);
            all.on('click', (e, d) => {
                const real = findKey(root, d.data.key);
                if (real.children) {
                    opts.onFocus(real.data.key);
                }
            }).on('pointermove', (e, d) => opts.tooltip.show(e, findKey(root, d.data.key)))
                .on('pointerleave', () => opts.tooltip.hide())
                .attr('tabindex', 0).attr('role', 'button')
                .attr('aria-label', (d) => `${d.data.name}, ${formatMoney(d.value)}, ${pct(d.value, focus.value)} of ${focus.data.name}`)
                .on('keydown', (e, d) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        const real = findKey(root, d.data.key);
                        if (real.children) {
                            opts.onFocus(real.data.key, true);
                        }
                    } else if ((e.key === 'Escape' || e.key === 'Backspace') && focus.parent) {
                        e.preventDefault();
                        opts.onFocus(focus.parent.data.key, true);
                    }
                });
            all.transition(t).style('opacity', 1).attr('transform', (d) => `translate(${d.x0},${d.y0})`);
            all.select('rect').transition(t)
                .attr('width', (d) => Math.max(0, d.x1 - d.x0)).attr('height', (d) => Math.max(0, d.y1 - d.y0))
                .attr('fill', (d) => colour(d.data.office)).attr('fill-opacity', focus.depth >= 1 ? 0.9 : 1);
            all.select('.proto-tile__kids').each(function (d) {
                const kids = (d.children || []);
                const s = d3.select(this).selectAll('rect').data(kids, (k) => k.data.key);
                s.exit().remove();
                s.enter().append('rect').attr('class', 'proto-tile__kid').merge(s)
                    .attr('x', (k) => k.x0 - d.x0).attr('y', (k) => k.y0 - d.y0)
                    .attr('width', (k) => Math.max(0, k.x1 - k.x0)).attr('height', (k) => Math.max(0, k.y1 - k.y0));
            });
            all.select('.proto-tile__name').attr('fill', (d) => labelInk(d.data.office))
                .text((d) => ((d.x1 - d.x0) > 50 && (d.y1 - d.y0) > 16 ? shorten(d.data.kind === 'company' && d.data.ticker ? `${d.data.ticker} · ${d.data.name}` : d.data.name, Math.floor((d.x1 - d.x0 - 10) / 6.5)) : ''));
            all.select('.proto-tile__value').attr('fill', (d) => labelInk(d.data.office))
                .text((d) => ((d.x1 - d.x0) > 70 && (d.y1 - d.y0) > (d.children ? 46 : 34) ? `${formatMoney(d.value)} · ${pct(d.value, focus.value)}` : ''));
            svg.attr('role', 'group').attr('aria-label', `Treemap of ${focus.data.name}`);
            return { focus };
        }
        layout();
        return { draw, relayout: layout, firstTile: () => svg.select('g.proto-tile').node() };
    }

    // ---------- variants ----------

    function mount (data) {
        const panel = document.getElementById('research-panel-sectors');
        const card = panel.querySelector('.research-card');
        const aside = panel.querySelector('.research-panel');
        // Hide the production chart pieces this prototype replaces.
        ['.research-sunburst__toolbar', '#research-sunburst'].forEach((s) => {
            const n = card.querySelector(s);
            if (n) {
                n.hidden = true;
                n.style.display = 'none';
            }
        });
        const tech = aside.querySelector('#research-tech-toggle');
        if (tech) {
            [tech, aside.querySelector('#research-tech-note'), ...aside.querySelectorAll('.research-panel__title')].slice(0, 4).forEach((n) => n && (n.style.display = 'none'));
            const legend = aside.querySelector('#research-sectors-legend');
            if (legend) {
                if (legend.previousElementSibling) {
                    legend.previousElementSibling.style.display = 'none';
                }
                legend.style.display = 'none';
            }
        }
        const host = el('div', `proto proto--${variant}`);
        card.querySelector('#research-sunburst').after(host);
        const tooltip = tooltipFor(document.getElementById('research-tooltip'));
        const live = document.getElementById('research-live');
        const state = { index: data.values.length - 1, focusKey: 'root', root: null, view: 'treemap', highlight: null };
        const periodLabel = () => data.periods[state.index];

        const rootFor = (i) => {
            const r = buildRoot(data, i);
            r.prevRoot = i > 0 ? buildRoot(data, i - 1) : null;
            return r;
        };

        function renderCards (target, items, cls) {
            target.replaceChildren(...items.map((t) => {
                const c = el('div', `proto-stat ${cls || ''}`);
                c.append(el('span', 'proto-stat__label', t.label), el('span', 'proto-stat__value', t.value), el('span', 'proto-stat__note', t.note));
                return c;
            }));
        }

        let render;
        if (variant === 'A') {
            const crumbs = el('div', 'proto-crumbs-host');
            const chartHost = el('div', 'proto-chart');
            const hint = el('p', 'proto-hint', 'Click a slice, or Tab into the chart and use the arrow keys: ←/→ siblings, Enter drills in, Esc goes up.');
            host.append(crumbs, chartHost, hint);
            const side = el('section', 'proto-takeaways');
            side.append(el('h2', 'research-panel__title', 'Key takeaways'));
            const sideBody = el('div', 'proto-takeaways__body');
            side.append(sideBody);
            aside.prepend(side);
            let activeKey = null;
            const sb = createSunburst(chartHost, {
                tooltip, periodLabel, focusableArcs: true,
                onFocus: (k) => { state.focusKey = k; activeKey = null; render(true); },
                onKey: (e, d) => {
                    const sibs = d.parent.children.filter((s) => s.x1 > s.x0);
                    const i = sibs.indexOf(d);
                    let next = null;
                    if (e.key === 'ArrowRight' || e.key === 'ArrowDown') { next = sibs[(i + 1) % sibs.length]; }
                    if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') { next = sibs[(i - 1 + sibs.length) % sibs.length]; }
                    if (next) { e.preventDefault(); activeKey = next.data.key; focusArc(next); }
                    if ((e.key === 'Enter' || e.key === ' ') && d.children) { e.preventDefault(); state.focusKey = d.data.key; activeKey = null; render(true, true); }
                    if (e.key === 'Escape' && state.focusKey !== 'root') {
                        e.preventDefault();
                        activeKey = state.focusKey;
                        state.focusKey = findKey(state.root, state.focusKey).parent.data.key;
                        render(true, true);
                    }
                }
            });
            const focusArc = (d) => {
                sb.arcs().attr('tabindex', (n) => (n === d ? 0 : -1)).classed('proto-arc--active', (n) => n === d)
                    .filter((n) => n === d).each(function () { this.focus(); });
                const box = chartHost.getBoundingClientRect();
                tooltip.show({ clientX: box.left + box.width * 0.75, clientY: box.top + 10 }, d);
            };
            render = (animate, keyboard) => {
                state.root = rootFor(state.index);
                const { focus } = sb.draw(state.root, state.focusKey, animate);
                state.focusKey = focus.data.key;
                renderCrumbs(crumbs, focus, (k) => { state.focusKey = k; render(true); });
                renderCards(sideBody, takeaways(state.root, focus, periodLabel()), 'proto-stat--stacked');
                // Roving tabindex: one arc in the tab order, the first child of the focus (or the one we came from).
                const firstRing = focus.children ? focus.children.filter((c) => c.x1 > c.x0) : [];
                const entry = (activeKey && firstRing.find((c) => c.data.key === activeKey)) || firstRing[0];
                sb.arcs().attr('tabindex', (n) => (n === entry ? 0 : -1)).classed('proto-arc--active', false);
                if (keyboard && entry) {
                    setTimeout(() => focusArc(entry), duration() + 20);
                }
                return focus;
            };
        } else if (variant === 'B') {
            const bar = el('div', 'proto-treebar');
            const crumbs = el('div', 'proto-crumbs-host proto-crumbs-host--bar');
            const toggle = el('div', 'proto-seg');
            toggle.setAttribute('role', 'group');
            toggle.setAttribute('aria-label', 'Chart type');
            ['treemap', 'sunburst'].forEach((v) => {
                const b = el('button', 'proto-seg__btn', v === 'treemap' ? 'Treemap' : 'Sunburst');
                b.type = 'button';
                b.dataset.view = v;
                b.addEventListener('click', () => { if (state.view !== v) { state.view = v; swap(); } });
                toggle.append(b);
            });
            bar.append(crumbs, toggle);
            const strip = el('div', 'proto-strip');
            const stage = el('div', 'proto-stage');
            const tmHost = el('div', 'proto-chart proto-chart--tm');
            const sbHost = el('div', 'proto-chart proto-chart--sb');
            stage.append(tmHost, sbHost);
            host.append(bar, stage, strip, el('p', 'proto-hint', 'Click a tile to drill in; the header bar is the breadcrumb. Tab to a tile, Enter drills in, Esc goes up.'));
            let prevFocus = null;
            const onFocus = (k, keyboard) => { prevFocus = state.focusKey; state.focusKey = k; render(true, keyboard); };
            const tm = createTreemap(tmHost, { tooltip, onFocus });
            const sb = createSunburst(sbHost, { tooltip, periodLabel, onFocus, maxSize: 520 });
            const swap = () => {
                toggle.querySelectorAll('button').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.view === state.view)));
                // Crossfade with a slight scale: the two layouts share colour and focus, so the eye keeps its place.
                const show = state.view === 'treemap' ? tmHost : sbHost;
                const hide = state.view === 'treemap' ? sbHost : tmHost;
                d3.select(hide).interrupt().transition().duration(duration() / 2).style('opacity', 0).style('transform', 'scale(0.96)')
                    .on('end', () => { hide.hidden = true; });
                show.hidden = false;
                d3.select(show).interrupt().style('opacity', 0).style('transform', 'scale(1.04)')
                    .transition().delay(duration() / 3).duration(duration() / 2).style('opacity', 1).style('transform', 'scale(1)');
                render(false);
            };
            render = (animate, keyboard) => {
                state.root = rootFor(state.index);
                const { focus } = tm.draw(state.root, state.focusKey, animate && state.view === 'treemap', prevFocus);
                sb.draw(rootFor(state.index), state.focusKey, animate && state.view === 'sunburst');
                state.focusKey = focus.data.key;
                prevFocus = null;
                renderCrumbs(crumbs, focus, (k) => onFocus(k));
                renderCards(strip, takeaways(state.root, focus, periodLabel()));
                if (keyboard) {
                    setTimeout(() => { const n = tm.firstTile(); if (n) { n.focus(); } }, duration() + 20);
                }
                return focus;
            };
            sbHost.hidden = true;
            toggle.querySelector('[data-view="treemap"]').setAttribute('aria-pressed', 'true');
            toggle.querySelector('[data-view="sunburst"]').setAttribute('aria-pressed', 'false');
        } else {
            const grid = el('div', 'proto-split');
            const chartHost = el('div', 'proto-chart proto-chart--small');
            const right = el('div', 'proto-drill');
            const crumbs = el('div', 'proto-crumbs-host proto-crumbs-host--stepper');
            const sentences = el('ul', 'proto-sentences');
            const list = el('ol', 'proto-list');
            list.setAttribute('aria-label', 'Parts of the current group, largest first');
            right.append(crumbs, sentences, list);
            grid.append(chartHost, right);
            host.append(grid, el('p', 'proto-hint', 'The list is the keyboard path: Tab to a row, Enter drills in. Hover a row to light its slice.'));
            const sb = createSunburst(chartHost, {
                tooltip, periodLabel, maxSize: 420,
                highlightKey: () => state.highlight,
                onFocus: (k) => { state.focusKey = k; render(true); }
            });
            const setHighlight = (k) => {
                state.highlight = k;
                sb.arcs().transition().duration(prefersReducedMotion() ? 0 : 150)
                    .attr('fill-opacity', (d) => {
                        const vis = d.t.y0 >= 1 && d.t.y1 <= RINGS + 1 && d.t.x1 > d.t.x0;
                        if (!vis) {
                            return 0;
                        }
                        if (k && d.data.key !== k && !d.ancestors().some((a) => a.data.key === k)) {
                            return 0.2;
                        }
                        return 1;
                    });
            };
            render = (animate, keyboard) => {
                state.root = rootFor(state.index);
                state.highlight = null;
                const { focus } = sb.draw(state.root, state.focusKey, animate);
                state.focusKey = focus.data.key;
                renderCrumbs(crumbs, focus, (k) => { state.focusKey = k; render(true, true); }, 'proto-crumbs--stepper');
                const tk = takeaways(state.root, focus, periodLabel());
                sentences.replaceChildren(...tk.slice(1).map((t) => el('li', null, `${t.label}: ${t.value} (${t.note})`)));
                const kids = (focus.children || []).slice().sort((a, b) => b.value - a.value).slice(0, 15);
                const max = kids.length ? kids[0].value : 1;
                list.replaceChildren(...kids.map((k, i) => {
                    const li = el('li', 'proto-row');
                    const b = el('button', 'proto-row__btn');
                    b.type = 'button';
                    b.disabled = !k.children;
                    const bar = el('span', 'proto-row__bar');
                    bar.style.setProperty('--w', `${k.value / max * 100}%`);
                    bar.style.setProperty('--c', colour(k.data.office));
                    b.append(el('span', 'proto-row__rank', String(i + 1)),
                        el('span', 'proto-row__name', k.data.kind === 'company' && k.data.ticker ? `${k.data.name} (${k.data.ticker})` : k.data.name),
                        el('span', 'proto-row__pct', pct(k.value, focus.value)),
                        el('span', 'proto-row__val', formatMoney(k.value)), bar);
                    b.setAttribute('aria-label', `${k.data.name}: ${formatMoney(k.value)}, ${pct(k.value, focus.value)} of ${focus.data.name}, ${pct(k.value, state.root.value)} of all filers${k.children ? '. Press Enter to drill in.' : ''}`);
                    b.addEventListener('click', () => { state.focusKey = k.data.key; render(true, true); });
                    li.addEventListener('pointerenter', () => setHighlight(k.data.key));
                    li.addEventListener('pointerleave', () => setHighlight(null));
                    b.addEventListener('focus', () => setHighlight(k.data.key));
                    b.addEventListener('blur', () => setHighlight(null));
                    li.append(b);
                    return li;
                }));
                if ((focus.children || []).length > 15) {
                    list.append(el('li', 'proto-row proto-row--more', `+ ${focus.children.length - 15} more`));
                }
                if (keyboard) {
                    const first = list.querySelector('button:not([disabled])') || crumbs.querySelector('button');
                    if (first) {
                        first.focus();
                    }
                }
                return focus;
            };
        }

        page.registerTab('sectors', {
            periods: data.periods,
            transitionMs: MS,
            update (index, options) {
                state.index = index;
                const sub = document.getElementById('research-sectors-subtitle');
                if (sub) {
                    sub.textContent = `The ${data.values[index].ranked.length} largest SEC filers in ${data.periods[index]} (real data, prototype), by SEC industry office and SIC industry.`;
                }
                const ends = panel.querySelectorAll('.research-scrubber__ends span');
                if (ends.length === 2) {
                    ends[0].textContent = data.periods[0];
                    ends[1].textContent = data.periods[data.periods.length - 1];
                }
                const focus = render(options && options.animate);
                return `${data.periods[index]}: ${focus.data.name}, ${formatMoney(focus.value)}.`;
            },
            table (index) {
                const r = buildRoot(data, index);
                const leaves = r.leaves().sort((a, b) => b.value - a.value).slice(0, 25);
                return {
                    caption: `The 25 largest SEC filers by Public float in ${data.periods[index]}`,
                    columns: [{ label: 'Rank', numeric: true }, { label: 'Company' }, { label: 'SIC industry' }, { label: '% of all', numeric: true }, { label: 'Public float', numeric: true }],
                    rows: leaves.map((l, i) => [String(i + 1), l.data.name, l.parent.data.name, pct(l.value, r.value), formatMoney(l.value)])
                };
            }
        });
        if (live) {
            live.textContent = '';
        }
    }

    // ---------- floating variant switcher (prototype only) ----------

    function switcher () {
        const i = VARIANTS.findIndex((v) => v.key === variant);
        const go = (d) => {
            const next = VARIANTS[(i + d + VARIANTS.length) % VARIANTS.length].key;
            const p = new URLSearchParams(window.location.search);
            p.set('variant', next);
            window.location.search = p.toString();
        };
        const bar = el('div', 'proto-switcher');
        const prev = el('button', null, '←');
        const next = el('button', null, '→');
        prev.type = next.type = 'button';
        prev.setAttribute('aria-label', 'Previous variant');
        next.setAttribute('aria-label', 'Next variant');
        prev.addEventListener('click', () => go(-1));
        next.addEventListener('click', () => go(1));
        bar.append(prev, el('span', null, `PROTOTYPE ${variant} (${VARIANTS[i].name})`), next);
        document.body.append(bar);
        document.addEventListener('keydown', (e) => {
            const t = e.target;
            if (t.closest && (t.closest('input, textarea, [contenteditable], svg, .proto-list, .proto-crumbs, .proto-seg'))) {
                return;
            }
            if (e.key === 'ArrowLeft') { go(-1); }
            if (e.key === 'ArrowRight') { go(1); }
        });
    }

    async function loadData () {
        try {
            const response = await fetch('/prototype/sectors-upgrade/sectors-real.json');
            mount(await response.json());
        } catch (err) {
            document.body.append(el('p', 'proto-hint', 'Prototype data failed to load.'));
        }
    }

    function init () {
        switcher();
        loadData();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
