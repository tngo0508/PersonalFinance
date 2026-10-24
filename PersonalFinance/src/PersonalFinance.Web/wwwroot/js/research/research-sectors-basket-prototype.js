/* global d3 */
/**
 * PROTOTYPE, throwaway (wayfinder ticket #245, "Sectors basket view: hierarchy, unclassified holdings and leaves per
 * industry"). Never merge. Mounted on the Sectors tab behind a "Market | Your basket" switch, switched by
 * ?variant=A|B|C and the floating bar. Data: /prototype/sectors-basket/basket-sectors.json (per-fund weights by SEC
 * industry office, SIC industry and company; unmatched equity under "Not classified by the SEC" by issuer country;
 * bonds and cash in their own groups; no CUSIP or ISIN).
 *
 *   A  Basket sunburst: office -> industry -> company, zoom by click, breadcrumb, a "companies per industry" control
 *      (5 / 10 / 25, rest summed into "Other"), the unclassified group in grey.
 *   B  Basket treemap: the same hierarchy one level at a time, the unclassified block hatched and explained beside the
 *      chart, a callout for how much the SEC does not classify.
 *   C  Lean vs the market: one bar per SEC industry office, your basket against the market's Public Float mix, with the
 *      difference in points; click an office for its industries. No company level.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    if (!page || typeof d3 === 'undefined') {
        return;
    }
    const VARIANTS = [
        { key: 'A', name: 'Basket sunburst + leaves control' },
        { key: 'B', name: 'Basket treemap + unclassified panel' },
        { key: 'C', name: 'Lean vs the market (offices)' }
    ];
    const OFFICES = ['Energy & Transportation', 'Finance', 'Life Sciences', 'Manufacturing', 'Real Estate & Construction', 'Technology', 'Trade & Services', 'Industrial Applications and Services'];
    const FUND = { VTI: '#2a78d6', SPY: '#eb6834', QQQ: '#1baf7a', IWM: '#e34948', VXUS: '#6250d6', VT: '#eda100', AIQ: '#e87ba4' };
    const params = new URLSearchParams(window.location.search);
    const variant = (VARIANTS.find((v) => v.key === (params.get('variant') || 'A').toUpperCase()) || VARIANTS[0]).key;
    const fmt = (v) => (v >= 10 ? `${v.toFixed(0)}%` : (v < 0.05 ? '<0.1%' : `${v.toFixed(1)}%`));
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
    const tokenColour = (name) => {
        const i = OFFICES.indexOf(name);
        const root = document.querySelector('.research') || document.body;
        const v = getComputedStyle(root).getPropertyValue(i >= 0 ? `--research-office-${i + 1}` : '--research-office-neutral').trim();
        return v || '#9ca3af';
    };
    const groupColour = (kind, name) => (kind === 'SEC' ? tokenColour(name) : (kind === 'BD' ? '#b08800' : '#9ca3af'));
    const label = (part) => part.slice(part.indexOf('|') + 1);
    const countryName = new Intl.DisplayNames(['en'], { type: 'region' });

    // ---------- model ----------

    function combine (data, basket) {
        const total = d3.sum(basket, (b) => b.w) || 1;
        const map = new Map();
        basket.forEach(({ t, w }) => data.funds[t].rows.forEach(([k, p]) => map.set(k, (map.get(k) || 0) + (w / total) * p)));
        return map;
    }
    function tree (map, leaves) {
        const groups = new Map();
        for (const [k, p] of map) {
            const [g, i, c] = k.split('~');
            if (!groups.has(g)) {
                groups.set(g, new Map());
            }
            const ind = groups.get(g);
            if (!ind.has(i)) {
                ind.set(i, []);
            }
            ind.get(i).push({ k: c, p });
        }
        const children = Array.from(groups, ([g, ind]) => {
            const kind = g.split('|')[0];
            const name = label(g);
            return {
                key: g, name, kind, colour: groupColour(kind, name),
                children: Array.from(ind, ([i, cs]) => {
                    cs.sort((a, b) => b.p - a.p);
                    const keep = cs.slice(0, leaves);
                    const rest = cs.slice(leaves);
                    const iname = i.startsWith('ctry|') ? countryName.of(label(i)) || label(i) : (i.startsWith('cat|') ? `Issuer type ${label(i)}` : label(i).replace(/^(\d{4})\|/, ''));
                    const kids = keep.map((c) => ({ key: `${g}~${i}~${c.k}`, name: label(c.k), value: c.p, kind, colour: groupColour(kind, name) }));
                    if (rest.length) {
                        kids.push({ key: `${g}~${i}~other`, name: `Other (${rest.length})`, value: d3.sum(rest, (c) => c.p), kind, other: true, colour: groupColour(kind, name) });
                    }
                    return { key: `${g}~${i}`, name: iname, kind, colour: groupColour(kind, name), children: kids };
                })
            };
        });
        const order = (n) => (n.kind === 'SEC' ? 0 : n.kind === 'NC' ? 1 : n.kind === 'BD' ? 2 : 3);
        children.sort((a, b) => order(a) - order(b) || OFFICES.indexOf(a.name) - OFFICES.indexOf(b.name));
        return d3.hierarchy({ key: 'root', name: 'Your basket', children }).sum((d) => (d.children ? 0 : d.value || 0))
            .sort((a, b) => (a.depth === 1 ? 0 : (a.data.other ? 1 : 0) - (b.data.other ? 1 : 0) || b.value - a.value));
    }
    function callouts (root) {
        const nc = (root.children || []).find((c) => c.data.kind === 'NC');
        const sec = d3.sum((root.children || []).filter((c) => c.data.kind === 'SEC'), (c) => c.value);
        const topOffice = (root.children || []).filter((c) => c.data.kind === 'SEC').sort((a, b) => b.value - a.value)[0];
        const companies = root.leaves().filter((l) => l.data.kind === 'SEC' && !l.data.other).sort((a, b) => b.value - a.value);
        return [
            { label: 'Classified by the SEC', value: fmt(sec), note: nc ? `${fmt(nc.value)} is not: mostly foreign companies that file no SEC reports` : 'all equity holdings placed' },
            topOffice && { label: 'Largest SEC industry office', value: fmt(topOffice.value), note: topOffice.data.name },
            companies[0] && { label: 'Largest company', value: fmt(companies[0].value), note: companies[0].data.name }
        ].filter(Boolean);
    }

    // ---------- views ----------

    function sunburst (host, root, state, tooltip, redraw, crumbs) {
        const size = Math.min(560, host.clientWidth || 560);
        const rings = 3;
        const unit = size / 2 / (rings + 1);
        d3.partition().size([2 * Math.PI, rings + 1])(root);
        const focus = root.descendants().find((d) => d.data.key === state.focus) || root;
        const span = focus.x1 - focus.x0;
        const t = (d) => ({ x0: Math.max(0, Math.min(1, (d.x0 - focus.x0) / span)) * 2 * Math.PI, x1: Math.max(0, Math.min(1, (d.x1 - focus.x0) / span)) * 2 * Math.PI, y0: d.y0 - focus.depth, y1: d.y1 - focus.depth });
        const arc = d3.arc().startAngle((d) => d.x0).endAngle((d) => d.x1).padAngle(0.003).innerRadius((d) => d.y0 * unit).outerRadius((d) => d.y1 * unit - 1.5);
        const svg = d3.select(host).append('svg').attr('viewBox', [-size / 2, -size / 2, size, size]).attr('class', 'sb-svg');
        const nodes = root.descendants().slice(1).map((d) => ({ d, s: t(d) })).filter(({ s }) => s.y0 >= 1 && s.y1 <= rings + 1 && s.x1 - s.x0 > 0.001);
        svg.append('g').selectAll('path').data(nodes).join('path').attr('d', ({ s }) => arc(s))
            .attr('fill', ({ d }) => d.data.colour).attr('fill-opacity', ({ d }) => (d.data.other ? 0.4 : [1, 1, 0.85, 0.7][d.depth - focus.depth] || 0.7))
            .attr('class', ({ d }) => (d.data.kind === 'NC' ? 'sb-arc sb-arc--nc' : 'sb-arc'))
            .attr('tabindex', ({ d }) => (d.children ? 0 : null)).attr('role', ({ d }) => (d.children ? 'button' : null))
            .attr('aria-label', ({ d }) => `${d.data.name}: ${fmt(d.value)} of your basket`)
            .on('click', (e, { d }) => { if (d.children) { state.focus = d.data.key; redraw(); } })
            .on('keydown', (e, { d }) => { if (e.key === 'Enter' && d.children) { state.focus = d.data.key; redraw(); } })
            .on('pointermove', (e, { d }) => tooltip(e, [d.data.name, `${fmt(d.value)} of your basket`, d.parent && d.parent.depth ? `${fmt(d.value / d.parent.value * 100)} of ${d.parent.data.name}` : '', d.data.kind === 'NC' && d.depth === 1 ? 'Foreign companies that file no SEC reports have no SEC industry code.' : ''].filter(Boolean)))
            .on('pointerleave', () => tooltip(null));
        svg.append('g').attr('text-anchor', 'middle').attr('pointer-events', 'none').selectAll('text')
            .data(nodes.filter(({ s }) => (s.x1 - s.x0) * (s.y0 + s.y1) / 2 * unit > 14)).join('text')
            .attr('transform', ({ s }) => { const a = (s.x0 + s.x1) / 2 * 180 / Math.PI; return `rotate(${a - 90}) translate(${(s.y0 + s.y1) / 2 * unit},0) rotate(${a < 180 ? 0 : 180})`; })
            .attr('dy', '0.35em').attr('class', 'sb-arc-label').attr('fill', ({ d }) => (d.data.name === 'Manufacturing' || d.data.kind === 'BD' ? '#0b0b0b' : '#fff'))
            .text(({ d }) => { const n = d.data.name; const max = Math.floor((unit - 6) / 6); return n.length > max ? `${n.slice(0, max - 1)}…` : n; });
        const c = svg.append('g').attr('text-anchor', 'middle').style('cursor', focus.parent ? 'pointer' : 'default').on('click', () => { if (focus.parent) { state.focus = focus.parent.data.key; redraw(); } });
        c.append('circle').attr('r', unit - 2).attr('fill', 'transparent');
        c.append('text').attr('class', 'sb-centre-t').attr('y', -8).text(focus.depth ? (focus.data.name.length > 18 ? `${focus.data.name.slice(0, 17)}…` : focus.data.name) : 'Your basket');
        c.append('text').attr('class', 'sb-centre-v').attr('dy', '0.9em').text(fmt(focus.value));
        crumbs(focus);
    }

    function treemap (host, root, state, tooltip, redraw, crumbs) {
        const w = host.clientWidth || 700;
        const h = Math.round(Math.min(480, w * 0.6));
        const focus = root.descendants().find((d) => d.data.key === state.focus) || root;
        const sub = d3.hierarchy(focus.data).sum((d) => (d.children ? 0 : d.value || 0)).sort((a, b) => b.value - a.value);
        d3.treemap().size([w, h]).paddingInner(2).round(true)(sub);
        const svg = d3.select(host).append('svg').attr('viewBox', [0, 0, w, h]).attr('class', 'sb-svg sb-svg--tm');
        const defs = svg.append('defs');
        defs.append('pattern').attr('id', 'sb-hatch').attr('patternUnits', 'userSpaceOnUse').attr('width', 8).attr('height', 8).attr('patternTransform', 'rotate(45)')
            .call((p) => { p.append('rect').attr('width', 8).attr('height', 8).attr('fill', '#d1d5db'); p.append('line').attr('x1', 0).attr('x2', 0).attr('y1', 0).attr('y2', 8).attr('stroke', '#9ca3af').attr('stroke-width', 3); });
        const g = svg.selectAll('g').data(sub.children || []).join('g').attr('transform', (d) => `translate(${d.x0},${d.y0})`)
            .attr('tabindex', 0).attr('role', 'button').attr('class', 'sb-tile')
            .attr('aria-label', (d) => `${d.data.name}: ${fmt(d.value)} of your basket`)
            .on('click', (e, d) => { if (d.data.children) { state.focus = d.data.key; redraw(); } })
            .on('keydown', (e, d) => { if (e.key === 'Enter' && d.data.children) { state.focus = d.data.key; redraw(); } if (e.key === 'Escape' && focus.parent) { state.focus = focus.parent.data.key; redraw(); } })
            .on('pointermove', (e, d) => tooltip(e, [d.data.name, `${fmt(d.value)} of your basket`]))
            .on('pointerleave', () => tooltip(null));
        g.append('rect').attr('width', (d) => d.x1 - d.x0).attr('height', (d) => d.y1 - d.y0)
            .attr('fill', (d) => (d.data.kind === 'NC' ? 'url(#sb-hatch)' : d.data.colour)).attr('fill-opacity', (d) => (d.data.other ? 0.45 : 1));
        g.append('text').attr('x', 6).attr('y', 15).attr('class', 'sb-tile-name').attr('fill', (d) => (d.data.kind === 'NC' || d.data.name === 'Manufacturing' || d.data.kind === 'BD' ? '#111' : '#fff'))
            .text((d) => ((d.x1 - d.x0) > 60 && (d.y1 - d.y0) > 18 ? (d.data.name.length > (d.x1 - d.x0) / 7 ? `${d.data.name.slice(0, Math.floor((d.x1 - d.x0) / 7) - 1)}…` : d.data.name) : ''));
        g.append('text').attr('x', 6).attr('y', 30).attr('class', 'sb-tile-val').attr('fill', (d) => (d.data.kind === 'NC' || d.data.name === 'Manufacturing' || d.data.kind === 'BD' ? '#111' : '#fff'))
            .text((d) => ((d.x1 - d.x0) > 50 && (d.y1 - d.y0) > 34 ? fmt(d.value) : ''));
        crumbs(focus);
    }

    function lean (host, root, market, state, redraw) {
        const offices = (root.children || []).filter((c) => c.data.kind === 'SEC');
        const names = Array.from(new Set(offices.map((o) => o.data.name).concat(Object.keys(market)))).filter((n) => n !== 'Unassigned');
        const basketSec = d3.sum(offices, (o) => o.value) || 1;
        const rows = names.map((n) => {
            const o = offices.find((x) => x.data.name === n);
            const b = o ? o.value / basketSec * 100 : 0;
            return { n, b, m: market[n] || 0, d: b - (market[n] || 0), node: o };
        }).sort((a, b) => b.b - a.b);
        const max = d3.max(rows, (r) => Math.max(r.b, r.m)) || 1;
        const list = el('div', 'sb-lean');
        const head = el('div', 'sb-lean__row sb-lean__head');
        head.append(el('span', null, 'SEC industry office'), el('span', null, 'Your basket vs market'), el('span', 'num', 'Difference'));
        list.append(head);
        rows.forEach((r) => {
            const row = el('button', 'sb-lean__row');
            row.type = 'button';
            row.setAttribute('aria-expanded', String(state.open === r.n));
            const bars = el('span', 'sb-lean__bars');
            const bb = el('span', 'sb-lean__bar sb-lean__bar--b');
            bb.style.width = `${r.b / max * 100}%`;
            bb.style.background = tokenColour(r.n);
            const mb = el('span', 'sb-lean__bar sb-lean__bar--m');
            mb.style.width = `${r.m / max * 100}%`;
            bars.append(bb, el('span', 'sb-lean__v', `${fmt(r.b)} basket`), mb, el('span', 'sb-lean__v sb-lean__v--m', `${fmt(r.m)} market`));
            const diff = el('span', `num sb-lean__d ${r.d >= 0 ? 'sb-up' : 'sb-down'}`, `${r.d >= 0 ? '+' : ''}${r.d.toFixed(1)} pts`);
            row.append(el('span', 'sb-lean__name', r.n), bars, diff);
            row.addEventListener('click', () => { state.open = state.open === r.n ? null : r.n; redraw(); });
            list.append(row);
            if (state.open === r.n && r.node) {
                const sub = el('ol', 'sb-lean__sub');
                r.node.children.slice().sort((a, b) => b.value - a.value).slice(0, 10).forEach((i) => sub.append(el('li', null, `${i.data.name} · ${fmt(i.value)} of your basket`)));
                list.append(sub);
            }
        });
        host.append(list);
        const nc = (root.children || []).find((c) => c.data.kind === 'NC');
        host.append(el('p', 'sb-hint', `Shares are of the SEC-classified part of your basket${nc ? ` (${fmt(100 - nc.value)} of it)` : ''}, against the top 500 SEC filers by Public Float in 2025. Points of difference describe the lean; they are not a target.`));
    }

    // ---------- mount ----------

    function mount (data) {
        const panel = document.getElementById('research-panel-sectors');
        if (!panel) {
            return;
        }
        const existing = Array.from(panel.children);
        const seg = el('div', 'sb-seg');
        const card = el('div', 'research-card sb-card');
        const setView = (v) => {
            seg.querySelectorAll('button').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.v === v)));
            existing.forEach((n) => { n.style.display = v === 'market' ? '' : 'none'; });
            card.style.display = v === 'basket' ? '' : 'none';
        };
        [['market', 'Market'], ['basket', 'Your basket']].forEach(([v, t]) => {
            const b = el('button', 'sb-seg__btn', t);
            b.type = 'button';
            b.dataset.v = v;
            b.addEventListener('click', () => setView(v));
            seg.append(b);
        });
        panel.prepend(seg, card);
        card.append(el('h2', 'research-card__title', 'What your basket owns, by SEC industry'),
            el('p', 'research-card__subtitle', 'Each fund\'s latest reported holdings, grouped by the SEC office that reviews each company\'s filings and its SIC industry. Foreign companies that file no SEC reports have no SEC industry and are grouped by country instead.'));
        const picker = el('div', 'sb-picker');
        const controls = el('div', 'sb-controls');
        const stats = el('div', 'sb-stats');
        const crumbsHost = el('div', 'sb-crumbs');
        const stage = el('div', `sb-stage sb-stage--${variant}`);
        const side = el('aside', 'sb-side');
        const body = el('div', 'sb-body');
        body.append(stage, side);
        card.append(picker, controls, stats, crumbsHost, body, el('p', 'research-card__source', `Source: ${data.source}`));
        const tip = el('div', 'research-tooltip');
        tip.hidden = true;
        document.body.append(tip);
        const tooltip = (e, lines) => {
            if (!e || !lines) {
                tip.hidden = true;
                return;
            }
            tip.replaceChildren(...lines.map((l, i) => el('span', i ? 'research-tooltip__muted' : 'research-tooltip__name', l)));
            tip.hidden = false;
            tip.style.position = 'fixed';
            tip.style.left = `${Math.min(e.clientX + 16, window.innerWidth - 300)}px`;
            tip.style.top = `${e.clientY + 16}px`;
        };
        const state = { basket: [{ t: 'VTI', w: 60 }, { t: 'VXUS', w: 40 }], leaves: 10, focus: 'root', open: null };
        const crumbs = (focus) => {
            const ol = el('ol');
            focus.ancestors().reverse().forEach((n, i, all) => {
                const li = el('li');
                if (i === all.length - 1) {
                    li.append(el('span', 'sb-crumb-cur', `${n.data.name} · ${fmt(n.value)}`));
                } else {
                    const b = el('button', 'sb-crumb', n.data.name);
                    b.type = 'button';
                    b.addEventListener('click', () => { state.focus = n.data.key; draw(); });
                    li.append(b);
                }
                ol.append(li);
            });
            crumbsHost.replaceChildren(ol);
        };

        function draw () {
            picker.replaceChildren(el('span', 'po-picker__label', 'Fund Basket (stub):'), ...Object.keys(data.funds).map((t) => {
                const item = state.basket.find((b) => b.t === t);
                const b = el('button', `po-fund${item ? ' po-fund--on' : ''}`, item ? `${t} ${item.w}%` : t);
                b.type = 'button';
                b.style.setProperty('--c', FUND[t]);
                b.setAttribute('aria-pressed', String(Boolean(item)));
                b.disabled = !item && state.basket.length >= 5;
                b.addEventListener('click', () => {
                    const next = item ? state.basket.filter((x) => x.t !== t) : state.basket.concat({ t, w: 0 });
                    const each = Math.round(100 / Math.max(1, next.length));
                    state.basket = next.map((x) => ({ t: x.t, w: each }));
                    state.focus = 'root';
                    draw();
                });
                return b;
            }));
            controls.replaceChildren();
            if (variant === 'A' || variant === 'B') {
                const wrap = el('div', 'pp-seg');
                wrap.append(el('span', 'pp-seg__label', 'Companies per industry'));
                [5, 10, 25].forEach((n) => {
                    const b = el('button', 'pp-seg__btn', String(n));
                    b.type = 'button';
                    b.setAttribute('aria-pressed', String(state.leaves === n));
                    b.addEventListener('click', () => { state.leaves = n; draw(); });
                    wrap.append(b);
                });
                controls.append(wrap);
            }
            stage.replaceChildren();
            side.replaceChildren();
            crumbsHost.replaceChildren();
            if (!state.basket.length) {
                stats.replaceChildren();
                stage.append(el('p', 'sb-hint', 'Add a fund to your basket.'));
                return;
            }
            const root = tree(combine(data, state.basket), state.leaves);
            stats.replaceChildren(...callouts(root).map((s) => {
                const d = el('div', 'po-stat');
                d.append(el('span', 'po-stat__label', s.label), el('span', 'po-stat__value', s.value), el('span', 'po-stat__note', s.note));
                return d;
            }));
            if (variant === 'A') {
                sunburst(stage, root, state, tooltip, draw, crumbs);
                side.append(el('h3', 'sb-h', 'Groups'));
                const ul = el('ul', 'sb-legend');
                (root.children || []).forEach((c) => {
                    const li = el('li');
                    const b = el('button', 'sb-legend__btn');
                    b.type = 'button';
                    const sw = el('span', `sb-swatch${c.data.kind === 'NC' ? ' sb-swatch--nc' : ''}`);
                    sw.style.background = c.data.colour;
                    b.append(sw, `${c.data.name} · ${fmt(c.value)}`);
                    b.addEventListener('click', () => { state.focus = c.data.key; draw(); });
                    li.append(b);
                    ul.append(li);
                });
                side.append(ul, el('p', 'sb-hint', 'Click a slice or a group to zoom in; click the centre or the breadcrumb to go back. "Other (n)" sums the companies beyond the chosen count in each industry.'));
            } else if (variant === 'B') {
                treemap(stage, root, state, tooltip, draw, crumbs);
                const nc = (root.children || []).find((c) => c.data.kind === 'NC');
                const box = el('div', 'sb-nc');
                box.append(el('h3', 'sb-h', 'Not classified by the SEC'), el('p', 'sb-nc__big', nc ? fmt(nc.value) : '0%'));
                box.append(el('p', 'sb-hint', 'Companies outside the US that do not file reports with the SEC have no SEC industry. They are shown hatched, grouped by the country each fund reports. There is no free industry classification for them.'));
                if (nc) {
                    const ol = el('ol', 'sb-nc__list');
                    nc.children.slice().sort((a, b) => b.value - a.value).slice(0, 6).forEach((c) => ol.append(el('li', null, `${c.data.name} · ${fmt(c.value)}`)));
                    box.append(ol);
                }
                side.append(box, el('p', 'sb-hint', 'Click a tile to drill in, Escape to go up.'));
            } else {
                lean(stage, root, data.marketOfficeShare, state, draw);
                side.append(el('p', 'sb-hint', 'No company level here: this view answers "where does my basket lean compared with the market". Click an office to list its largest industries in your basket.'));
            }
        }

        setView('basket');
        draw();
    }

    function switcher () {
        const i = VARIANTS.findIndex((v) => v.key === variant);
        const go = (d) => {
            const p = new URLSearchParams(window.location.search);
            p.set('variant', VARIANTS[(i + d + VARIANTS.length) % VARIANTS.length].key);
            window.location.search = p.toString();
        };
        const bar = el('div', 'proto-switcher');
        const prev = el('button', null, '←');
        const next = el('button', null, '→');
        prev.type = 'button';
        next.type = 'button';
        prev.setAttribute('aria-label', 'Previous variant');
        next.setAttribute('aria-label', 'Next variant');
        prev.addEventListener('click', () => go(-1));
        next.addEventListener('click', () => go(1));
        bar.append(prev, el('span', null, `PROTOTYPE ${variant} (${VARIANTS[i].name})`), next);
        document.body.append(bar);
        document.addEventListener('keydown', (e) => {
            if (e.target.closest && e.target.closest('input, textarea, [contenteditable], svg, button')) {
                return;
            }
            if (e.key === 'ArrowLeft') {
                go(-1);
            }
            if (e.key === 'ArrowRight') {
                go(1);
            }
        });
    }

    async function init () {
        switcher();
        try {
            const response = await fetch('/prototype/sectors-basket/basket-sectors.json');
            mount(await response.json());
        } catch (err) {
            document.body.append(el('p', 'sb-hint', `Prototype failed: ${err.message}`));
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
