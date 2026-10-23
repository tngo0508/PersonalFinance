// PROTOTYPE, throwaway: Research page variants for wayfinder ticket #211. Not production code.
(function () {
    'use strict';

    const SEC = window.RP_SEC, WORLD = window.RP_WORLD, ATLAS = window.RP_ATLAS, ETF = window.RP_ETF;
    const forcedReduce = { on: false };
    const motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
    const dur = () => (forcedReduce.on || motionQuery.matches ? 0 : 750);
    const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    const el = (tag, attrs, html) => {
        const e = document.createElement(tag);
        Object.entries(attrs || {}).forEach(([k, v]) => e.setAttribute(k, v));
        if (html !== undefined) e.innerHTML = html;
        return e;
    };

    // ---------- formatting ----------
    const money = m => (m >= 1e6 ? '$' + (m / 1e6).toFixed(2) + 'T' : m >= 1e3 ? '$' + (m / 1e3).toFixed(1) + 'B' : '$' + Math.round(m) + 'M');
    const pct = v => v.toFixed(1) + '%';

    // ---------- periods ----------
    const secYears = Object.keys(SEC.values).map(Number).sort((a, b) => a - b);
    const worldYears = Object.keys(WORLD.years).map(Number).filter(y => Object.keys(WORLD.years[y]).length > 20).sort((a, b) => a - b);
    const quarters = [];
    for (let y = ETF.quarters.from[0], q = ETF.quarters.from[1]; y < ETF.quarters.to[0] || (y === ETF.quarters.to[0] && q <= ETF.quarters.to[1]);) {
        quarters.push({ y, q, label: 'Q' + q + ' ' + y });
        q++; if (q > 4) { q = 1; y++; }
    }
    const PERIODS = {
        sunburst: secYears.map(String),
        tree: quarters.map(q => q.label),
        map: worldYears.map(String)
    };
    const TABS = [
        { id: 'sunburst', label: 'Sectors', long: 'Sectors and companies', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="4"/><path d="M12 3v5M21 12h-5"/></svg>' },
        { id: 'tree', label: 'ETFs', long: 'Inside six ETFs', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="5" cy="12" r="2"/><circle cx="19" cy="5" r="2"/><circle cx="19" cy="12" r="2"/><circle cx="19" cy="19" r="2"/><path d="M7 12h10M7 12c5 0 5-7 10-7M7 12c5 0 5 7 10 7"/></svg>' },
        { id: 'map', label: 'World', long: 'Stock markets by country', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18"/></svg>' }
    ];

    const state = {
        variant: 'a',
        tab: 'sunburst',
        pos: { sunburst: PERIODS.sunburst.length - 1, tree: PERIODS.tree.length - 1, map: PERIODS.map.length - 1 },
        tech: false,
        metric: 'mcap',
        table: false,
        playing: null
    };

    // ---------- tooltip + live region ----------
    const tip = document.getElementById('tip');
    function showTip(event, html) {
        tip.innerHTML = html; tip.hidden = false;
        const x = Math.min(event.clientX + 14, window.innerWidth - tip.offsetWidth - 8);
        const y = Math.min(event.clientY + 14, window.innerHeight - tip.offsetHeight - 8);
        tip.style.left = x + 'px'; tip.style.top = y + 'px';
    }
    const hideTip = () => { tip.hidden = true; };
    const live = document.getElementById('live');
    const announce = text => { live.textContent = text; };

    // ================= SUNBURST =================
    const OFFICE_COLOR = {
        'Energy & Transportation': '--s1', 'Finance': '--s2', 'Life Sciences': '--s3', 'Manufacturing': '--s4',
        'Real Estate & Construction': '--s5', 'Technology': '--s6', 'Trade & Services': '--s7', 'Industrial Applications and Services': '--s8'
    };
    const officeVar = name => OFFICE_COLOR[name] || '--s-other';

    function sunburstData(year) {
        const offices = new Map();
        SEC.values[year].forEach(([ci, val]) => {
            const [name, ticker, ii] = SEC.companies[ci];
            const [ind, oi] = SEC.industries[ii];
            const office = SEC.offices[oi];
            if (!offices.has(oi)) offices.set(oi, { key: 'o' + oi, name: office, office, children: new Map() });
            const o = offices.get(oi);
            if (!o.children.has(ii)) o.children.set(ii, { key: 'i' + ii, name: titleCase(ind), office, children: [] });
            o.children.get(ii).children.push({ key: 'c' + ci, name: titleCase(name), ticker, office, value: val });
        });
        return {
            key: 'root', name: 'Largest 500 SEC filers', children: [...offices.values()].map(o => ({ ...o, children: [...o.children.values()] }))
        };
    }
    function titleCase(s) {
        if (s !== s.toUpperCase()) return s;
        return s.toLowerCase().replace(/\b([a-z])/g, m => m.toUpperCase()).replace(/\bOf\b|\bAnd\b|\bThe\b/g, m => m.toLowerCase()).replace(/^the/, 'The');
    }

    function Sunburst(host) {
        const width = Math.min(host.clientWidth || 600, 680);
        const radius = width / 6;
        const svg = d3.select(host).append('svg').attr('viewBox', [-width / 2, -width / 2, width, width]).attr('role', 'img')
            .style('max-width', width + 'px').style('margin', '0 auto').style('font', '10px var(--font)');
        const g = svg.append('g');
        const arc = d3.arc().startAngle(d => d.x0).endAngle(d => d.x1).padAngle(d => Math.min((d.x1 - d.x0) / 2, 0.004))
            .padRadius(radius * 1.5).innerRadius(d => d.y0 * radius).outerRadius(d => Math.max(d.y0 * radius, d.y1 * radius - 2));
        const pathG = g.append('g'), labelG = g.append('g').attr('pointer-events', 'none').attr('text-anchor', 'middle').style('user-select', 'none');
        const center = g.append('circle').attr('r', radius).attr('fill', 'transparent').style('cursor', 'pointer');
        const cTitle = g.append('text').attr('text-anchor', 'middle').attr('dy', '-0.6em').style('font', '600 12px var(--font)');
        const cValue = g.append('text').attr('text-anchor', 'middle').attr('dy', '1em').style('font', '600 16px var(--mono)');
        const cHint = g.append('text').attr('text-anchor', 'middle').attr('dy', '3em').style('font', '10px var(--font)').attr('fill', 'var(--muted)');
        const current = new Map();
        let root, focusKey = 'root', year;

        const visible = c => c.y1 <= 3 && c.y0 >= 1 && c.x1 > c.x0;
        const labelVisible = c => c.y1 <= 3 && c.y0 >= 1 && (c.y1 - c.y0) * (c.x1 - c.x0) > 0.05;
        function labelTransform(c) {
            const x = (c.x0 + c.x1) / 2 * 180 / Math.PI, y = (c.y0 + c.y1) / 2 * radius;
            return `rotate(${x - 90}) translate(${y},0) rotate(${x < 180 ? 0 : 180})`;
        }
        function target(d, p) {
            return {
                x0: Math.max(0, Math.min(1, (d.x0 - p.x0) / (p.x1 - p.x0))) * 2 * Math.PI,
                x1: Math.max(0, Math.min(1, (d.x1 - p.x0) / (p.x1 - p.x0))) * 2 * Math.PI,
                y0: Math.max(0, d.y0 - p.depth), y1: Math.max(0, d.y1 - p.depth)
            };
        }
        const fill = d => css(officeVar(d.data.office));
        const opacity = (d, t) => {
            if (!visible(t)) return 0;
            const base = d.children ? 0.9 : 0.7;
            return state.tech && d.data.office !== 'Technology' ? 0.12 : base;
        };
        const shortLabel = (d, t) => {
            const n = d.depth === 3 && d.data.ticker ? d.data.ticker : d.data.name;
            const max = Math.floor((t.y1 - t.y0) * radius / 6);
            return n.length > max ? n.slice(0, Math.max(0, max - 1)) + '2026' : n;
        };

        function render() {
            const nodes = root.descendants().slice(1);
            const p = root.descendants().find(n => n.data.key === focusKey) || root;
            focusKey = p.data.key;
            nodes.forEach(d => { d.target = target(d, p); });
            const t = svg.transition().duration(dur());

            const paths = pathG.selectAll('path').data(nodes, d => d.data.key);
            paths.exit().each(function (d) { const c = current.get(d.data.key); if (c) current.set(d.data.key, { ...c, x1: c.x0 }); })
                .transition(t).attrTween('d', function (d) {
                    const from = this.__cur || d.target, to = { ...from, x1: from.x0 };
                    const i = d3.interpolate(from, to); return s => arc(i(s));
                }).attr('fill-opacity', 0).remove();
            const enter = paths.enter().append('path')
                .each(function (d) { this.__cur = current.get(d.data.key) || { ...d.target, x1: d.target.x0 }; })
                .attr('d', function () { return arc(this.__cur); }).attr('fill-opacity', 0)
                .on('click', (e, d) => { if (d.children) { focusKey = d.data.key; render(); } })
                .on('pointermove', (e, d) => showTip(e, sunTip(d)))
                .on('pointerleave', hideTip);
            enter.merge(paths)
                .attr('fill', fill)
                .style('cursor', d => (d.children ? 'pointer' : 'default'))
                .attr('pointer-events', d => (visible(d.target) ? 'auto' : 'none'))
                .transition(t)
                .attr('fill-opacity', d => opacity(d, d.target))
                .attrTween('d', function (d) {
                    const i = d3.interpolate(this.__cur, d.target);
                    return s => { this.__cur = i(s); current.set(d.data.key, this.__cur); return arc(this.__cur); };
                });

            const labels = labelG.selectAll('text').data(nodes.filter(d => labelVisible(d.target)), d => d.data.key);
            labels.exit().remove();
            labels.enter().append('text').attr('dy', '0.35em').attr('fill-opacity', 0)
                .attr('transform', d => labelTransform(current.get(d.data.key) || d.target))
                .merge(labels)
                .text(d => shortLabel(d, d.target))
                .attr('fill', d => (d.data.office === 'Manufacturing' ? '#0b0b0b' : '#fff'))
                .transition(t)
                .attr('fill-opacity', d => (state.tech && d.data.office !== 'Technology' ? 0 : 1))
                .attrTween('transform', function (d) {
                    const from = current.get(d.data.key) || d.target;
                    const i = d3.interpolate(from, d.target); return s => labelTransform(i(s));
                });

            center.datum(p).on('click', () => { if (p.parent) { focusKey = p.parent.data.key; render(); } });
            cTitle.text(p.depth === 0 ? year + ' 00b7 public float' : p.data.name.length > 24 ? p.data.name.slice(0, 23) + '2026' : p.data.name);
            cValue.text(money(p.value));
            cHint.text(p.parent ? 'Click centre to zoom out' : 'Click a ring to zoom in');
            svg.attr('aria-label', `Sunburst of the largest 500 SEC filers by public float in ${year}. Focus: ${p.data.name}, ${money(p.value)}.`);
        }
        function sunTip(d) {
            const share = d.parent ? (d.value / d.parent.value * 100).toFixed(1) + '% of ' + d.parent.data.name : '';
            const kind = ['', 'SEC industry office', 'SIC industry', d.data.ticker ? 'Ticker ' + d.data.ticker : 'Company'][d.depth];
            return `<b>${d.data.name}</b><span class="muted">${kind}</span><br><span class="mono">${money(d.value)}</span> public float<br><span class="muted">${share}</span>`;
        }
        return {
            update(idx) {
                year = PERIODS.sunburst[idx];
                root = d3.hierarchy(sunburstData(year)).sum(d => d.value || 0).sort((a, b) => b.value - a.value);
                d3.partition().size([2 * Math.PI, root.height + 1])(root);
                render();
                const top = root.leaves().sort((a, b) => b.value - a.value)[0];
                return `${year}: largest 500 filers total ${money(root.value)} public float. Largest: ${top.data.name}, ${money(top.value)}.`;
            },
            refresh: () => render(),
            aggregate: () => secYears.map(y => SEC.values[y].reduce((s, r) => s + r[1], 0)),
            aggregateLabel: 'Total public float, largest 500',
            table(idx) {
                const y = PERIODS.sunburst[idx];
                const rows = SEC.values[y].slice().sort((a, b) => b[1] - a[1]).slice(0, 25).map(([ci, v]) => {
                    const [n, t, ii] = SEC.companies[ci]; const [ind, oi] = SEC.industries[ii];
                    return [titleCase(n), t || '', SEC.offices[oi], titleCase(ind), money(v)];
                });
                return { caption: `Largest 25 of 500 SEC filers by public float, ${y}`, head: ['Company', 'Ticker', 'SEC office', 'SIC industry', 'Public float'], rows, num: [4] };
            }
        };
    }

    // ================= COLLAPSIBLE TREE =================
    const expanded = new Set(['root', 'f:SPY', 'f:SPY/Equity, common']);
    function treeData(qi) {
        const t = quarters.length > 1 ? qi / (quarters.length - 1) : 1;
        const lerp = ([a, b]) => a + (b - a) * t;
        return {
            key: 'root', name: 'Six ETFs', kind: 'root', value: 100,
            children: ETF.funds.map(f => {
                const count = Math.round(lerp(f.holdings));
                const cats = Object.entries(f.cats).map(([name, w]) => ({ key: `f:${f.ticker}/${name}`, name, kind: 'cat', value: lerp(w) }));
                const eq = cats.find(c => c.name === 'Equity, common');
                const hold = f.top.map(([n, a, b]) => ({ name: n, value: lerp([a, b]) })).filter(h => h.value >= 0.05)
                    .sort((x, y) => y.value - x.value);
                const top = hold.slice(0, 25);
                const rest = eq.value - top.reduce((s, h) => s + h.value, 0);
                eq.children = top.map(h => ({ key: `f:${f.ticker}/h/${h.name}`, name: h.name, kind: 'holding', value: h.value }))
                    .concat([{ key: `f:${f.ticker}/other`, name: `Other (${Math.max(0, count - top.length).toLocaleString()} holdings)`, kind: 'other', value: Math.max(0, rest) }]);
                return { key: 'f:' + f.ticker, name: f.ticker, full: f.name, count, kind: 'fund', value: 100, children: cats };
            })
        };
    }

    function Tree(host) {
        const narrow = (host.clientWidth || 800) < 560;
        const dy = narrow ? 120 : 190, dx = 22;
        const scroll = d3.select(host).append('div').attr('class', 'tree-scroll');
        const svg = scroll.append('svg').attr('role', 'tree').attr('aria-label', 'Collapsible tree of six ETFs').style('font', '12px var(--font)');
        const gLink = svg.append('g').attr('fill', 'none').attr('stroke', 'var(--link)').attr('stroke-width', 1.5);
        const gNode = svg.append('g');
        const prev = new Map();
        let qi;
        const r = v => 3 + Math.sqrt(Math.max(0, v)) * 1.05;
        const label = d => {
            if (d.data.kind === 'root') return 'Six ETFs';
            if (d.data.kind === 'fund') return `${d.data.name} 00b7 ${d.data.count.toLocaleString()} holdings`;
            return `${d.data.name}  ${pct(d.data.value)}`;
        };

        function render(sourceKey) {
            const root = d3.hierarchy(treeData(qi));
            root.each(d => { if (d.children && !expanded.has(d.data.key)) { d._children = d.children; d.children = null; } });
            d3.tree().nodeSize([dx, dy])(root);
            let x0 = Infinity, x1 = -Infinity, maxY = 0;
            root.each(d => { x0 = Math.min(x0, d.x); x1 = Math.max(x1, d.x); maxY = Math.max(maxY, d.y); });
            const width = maxY + 280, height = x1 - x0 + dx * 2;
            svg.attr('width', width + 86).attr('height', height).attr('viewBox', [-110, x0 - dx, width + 86, height]);
            const t = svg.transition().duration(dur());
            const src = (sourceKey && root.descendants().find(d => d.data.key === sourceKey)) || root;
            const origin = k => prev.get(k) || prev.get(sourceKey) || { x: src.x, y: src.y };

            const nodes = root.descendants();
            const node = gNode.selectAll('g.tree-node').data(nodes, d => d.data.key);
            const enter = node.enter().append('g').attr('class', 'tree-node').attr('tabindex', 0).attr('role', 'treeitem')
                .attr('transform', d => { const o = origin(d.parent ? d.parent.data.key : 'root'); return `translate(${o.y},${o.x})`; })
                .attr('fill-opacity', 0)
                .on('click', (e, d) => toggle(d))
                .on('keydown', (e, d) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(d); } })
                .on('pointermove', (e, d) => showTip(e, treeTip(d)))
                .on('pointerleave', hideTip);
            enter.append('circle').attr('stroke-width', 2);
            enter.append('text').attr('dy', '0.32em');
            const merged = enter.merge(node);
            merged.attr('aria-expanded', d => (d._children || d.children ? String(!!d.children) : null))
                .attr('aria-label', d => label(d) + (d.data.full ? ', ' + d.data.full : ''));
            merged.select('circle')
                .attr('fill', d => (d._children ? 'var(--brand)' : d.children ? 'var(--surface)' : d.data.kind === 'other' ? 'var(--faint)' : 'var(--brand-soft)'))
                .attr('stroke', d => (d.data.kind === 'other' ? 'var(--faint)' : 'var(--brand)'));
            merged.select('text')
                .attr('x', d => (d.children ? -(r(d.data.value) + 6) : r(d.data.value) + 6))
                .attr('text-anchor', d => (d.children ? 'end' : 'start'))
                .attr('font-weight', d => (d.data.kind === 'fund' || d.data.kind === 'root' ? 600 : 400))
                .text(label)
                .attr('paint-order', 'stroke').attr('stroke', 'var(--surface)').attr('stroke-width', 3);
            merged.transition(t).attr('transform', d => `translate(${d.y},${d.x})`).attr('fill-opacity', 1)
                .select('circle').attr('r', d => r(d.data.value));
            node.exit().transition(t).remove()
                .attr('transform', d => { const p = findAncestorIn(d, nodes) || src; return `translate(${p.y},${p.x})`; })
                .attr('fill-opacity', 0);

            const links = root.links();
            const link = gLink.selectAll('path').data(links, d => d.target.data.key);
            const diag = d3.linkHorizontal().x(d => d.y).y(d => d.x);
            link.enter().append('path')
                .attr('d', d => { const o = origin(d.source.data.key); return diag({ source: o, target: o }); })
                .merge(link).transition(t).attr('d', diag).attr('stroke-width', d => Math.max(1, Math.sqrt(d.target.data.value) * 0.9));
            link.exit().transition(t).remove().attr('d', () => diag({ source: src, target: src }));

            prev.clear(); nodes.forEach(d => prev.set(d.data.key, { x: d.x, y: d.y }));
        }
        function findAncestorIn(d, nodes) {
            const keys = new Map(nodes.map(n => [n.data.key, n]));
            for (let p = d.parent; p; p = p.parent) if (keys.has(p.data.key)) return keys.get(p.data.key);
            return null;
        }
        function toggle(d) {
            if (!d.children && !d._children) return;
            if (expanded.has(d.data.key)) expanded.delete(d.data.key); else expanded.add(d.data.key);
            render(d.data.key);
            announce(`${d.data.name} ${expanded.has(d.data.key) ? 'expanded' : 'collapsed'}`);
        }
        function treeTip(d) {
            if (d.data.kind === 'root') return '<b>Six ETFs</b>Click an ETF to open it';
            if (d.data.kind === 'fund') return `<b>${d.data.name}</b>${d.data.full}<br><span class="mono">${d.data.count.toLocaleString()}</span> holdings`;
            return `<b>${d.data.name}</b><span class="mono">${pct(d.data.value)}</span> of net assets`;
        }
        return {
            update(idx) {
                qi = idx; render();
                const spy = treeData(idx).children[0];
                return `${quarters[idx].label}: SPY top holding ${spy.children[0].children[0].name}, ${pct(spy.children[0].children[0].value)} of net assets.`;
            },
            refresh: () => render(),
            aggregate: () => quarters.map((q, i) => treeData(i).children[0].children[0].children.slice(0, 10).reduce((s, h) => s + h.value, 0) * 1000),
            aggregateLabel: 'SPY: weight of its top 10 holdings',
            aggregateFormat: v => pct(v / 1000),
            table(idx) {
                const rows = [];
                treeData(idx).children.forEach(f => {
                    if (!expanded.has(f.key)) return;
                    f.children.forEach(c => {
                        rows.push([f.name, c.name, '', pct(c.value)]);
                        (c.children || []).forEach(h => rows.push([f.name, c.name, h.name, pct(h.value)]));
                    });
                });
                return { caption: `Holdings of the open ETFs, ${quarters[idx].label} (illustrative sample)`, head: ['ETF', 'Asset category', 'Holding', '% of net assets'], rows, num: [3] };
            }
        };
    }

    // ================= CHOROPLETH =================
    const features = topojson.feature(ATLAS, ATLAS.objects.countries).features.filter(f => f.id !== '010');
    const METRICS = {
        mcap: { label: 'Market cap', thresholds: [1e3, 1e4, 5e4, 1e5, 5e5, 1e6, 5e6], fmt: money, idx: 0, steps: ['$1B', '$10B', '$50B', '$100B', '$500B', '$1T', '$5T'] },
        gdp: { label: 'Market cap as % of GDP', thresholds: [10, 25, 50, 75, 100, 150, 200], fmt: pct, idx: 1, steps: ['10%', '25%', '50%', '75%', '100%', '150%', '200%'] }
    };
    const RAMP = ['--q1', '--q2', '--q3', '--q4', '--q5', '--q6', '--q7', '--q8'];

    function WorldMap(host) {
        const width = Math.max(320, host.clientWidth || 800), height = Math.round(width * 0.5);
        const projection = d3.geoEqualEarth().fitSize([width, height], { type: 'Sphere' });
        const path = d3.geoPath(projection);
        const wrap = d3.select(host);
        const svg = wrap.append('svg').attr('viewBox', [0, 0, width, height]).attr('role', 'img');
        svg.append('path').attr('d', path({ type: 'Sphere' })).attr('fill', 'var(--surface-2)');
        const countries = svg.append('g').selectAll('path').data(features).join('path').attr('d', path)
            .attr('stroke', 'var(--surface)').attr('stroke-width', 0.5).attr('fill', 'var(--nodata)')
            .on('pointermove', (e, f) => showTip(e, mapTip(f))).on('pointerleave', hideTip);
        const legend = wrap.append('div').style('margin-top', '8px');
        let year;
        const val = f => { const r = WORLD.years[year][f.id]; return r ? r[METRICS[state.metric].idx] : null; };
        const color = v => {
            if (v === null || v === undefined) return 'var(--nodata)';
            const m = METRICS[state.metric]; let i = m.thresholds.findIndex(t => v < t); if (i < 0) i = m.thresholds.length;
            return css(RAMP[i]);
        };
        function mapTip(f) {
            const r = WORLD.years[year][f.id];
            const name = WORLD.names[f.id] || f.properties.name;
            if (!r) return `<b>${name}</b><span class="muted">No data for ${year}</span>`;
            return `<b>${name}</b><span class="mono">${money(r[0])}</span> market cap<br><span class="mono">${r[1] === null ? 'n/a' : pct(r[1])}</span> of GDP<br><span class="muted">${year}</span>`;
        }
        function render() {
            countries.transition().duration(dur()).attr('fill', f => color(val(f)));
            const m = METRICS[state.metric];
            legend.html(`<div class="steps" aria-hidden="true">${RAMP.map((v, i) => `<div><i style="background:var(${v})"></i>${i === 0 ? '&lt;' + m.steps[0] : i === RAMP.length - 1 ? '2265' + m.steps[i - 1] : m.steps[i - 1]}</div>`).join('')}<div><i style="background:var(--nodata)"></i>No data</div></div>`);
            const n = Object.keys(WORLD.years[year]).length;
            svg.attr('aria-label', `World map of ${m.label.toLowerCase()} by country, ${year}. ${n} countries have data.`);
        }
        return {
            update(idx) {
                year = PERIODS.map[idx]; render();
                const rows = Object.entries(WORLD.years[year]).sort((a, b) => b[1][0] - a[1][0]);
                return `${year}: ${rows.length} countries report. Largest: ${WORLD.names[rows[0][0]]}, ${money(rows[0][1][0])}.`;
            },
            refresh: () => render(),
            aggregate: () => worldYears.map(y => Object.values(WORLD.years[y]).reduce((s, r) => s + r[0], 0)),
            aggregateLabel: 'Sum of reporting countries',
            table(idx) {
                const y = PERIODS.map[idx];
                const rows = Object.entries(WORLD.years[y]).sort((a, b) => b[1][0] - a[1][0]).map(([k, r]) => [WORLD.names[k] || k, money(r[0]), r[1] === null ? 'n/a' : pct(r[1])]);
                return { caption: `Market capitalisation of listed domestic companies by country, ${y}`, head: ['Country', 'Market cap', '% of GDP'], rows, num: [1, 2] };
            }
        };
    }

    // ================= shared page plumbing =================
    let chart = null, chartHost = null, scrubSync = () => {};
    const FACTORY = { sunburst: Sunburst, tree: Tree, map: WorldMap };
    const SUBTITLE = {
        sunburst: 'Largest 500 SEC filers by public float, grouped by SEC industry office and SIC industry.',
        tree: 'What six well-known ETFs hold, by asset category and top holdings. Illustrative sample in this prototype.',
        map: 'Market capitalisation of listed domestic companies, by country.'
    };
    const CREDIT = {
        sunburst: 'Source: SEC EDGAR (public float, dei:EntityPublicFloat, as of Q2 each year)',
        tree: 'Illustrative sample. The real page reads SEC N-PORT data.',
        map: 'Source: World Bank WDI / WFE (CC BY 4.0). Borders: Natural Earth'
    };
    const ABOUT = {
        sunburst: 'Public float is the value of shares held by outside investors, reported once a year. It is not market cap. US SEC filers only; small and some foreign filers are missing. Smaller filers are not summed into "Other" in this prototype.',
        tree: 'Holdings are reported quarterly. Sizes are % of the fund\'s net assets. Weights here are approximate and interpolated, not filings.',
        map: 'Countries without a reported value for the year are grey. Coverage grows over time, so the world total partly reflects more countries reporting.'
    };

    function setPos(idx, opts) {
        const n = PERIODS[state.tab].length;
        idx = Math.max(0, Math.min(n - 1, idx));
        state.pos[state.tab] = idx;
        const msg = chart.update(idx);
        scrubSync(idx);
        renderTable();
        if (!opts || !opts.quiet) announce(msg);
    }
    function togglePlay() {
        if (state.playing) { clearInterval(state.playing); state.playing = null; syncPlay(); return; }
        const n = PERIODS[state.tab].length;
        if (state.pos[state.tab] >= n - 1) setPos(0, { quiet: true });
        state.playing = setInterval(() => {
            const i = state.pos[state.tab] + 1;
            if (i >= n) { togglePlay(); return; }
            setPos(i, { quiet: true });
        }, Math.max(450, dur() + 150));
        syncPlay();
    }
    function stopPlay() { if (state.playing) togglePlay(); }
    const PLAY = '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M7 4v16l13-8z"/></svg>';
    const PAUSE = '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M6 4h4v16H6zM14 4h4v16h-4z"/></svg>';
    function syncPlay() {
        document.querySelectorAll('[data-play]').forEach(b => {
            b.innerHTML = state.playing ? PAUSE : PLAY;
            b.setAttribute('aria-label', state.playing ? 'Pause' : 'Play through time');
        });
    }

    function tabList(cls, withIcons) {
        const list = el('div', { role: 'tablist', 'aria-label': 'Research views', class: cls });
        TABS.forEach(t => {
            const b = el('button', { role: 'tab', id: 'tab-' + t.id, 'aria-selected': String(t.id === state.tab), tabindex: t.id === state.tab ? '0' : '-1', type: 'button' },
                withIcons ? t.icon + '<span>' + t.label + '</span>' : cls === 'vb-seg' ? t.label : t.long);
            b.addEventListener('click', () => switchTab(t.id));
            b.addEventListener('keydown', e => {
                const i = TABS.findIndex(x => x.id === t.id);
                if (e.key === 'ArrowRight' || e.key === 'ArrowDown') { e.preventDefault(); switchTab(TABS[(i + 1) % TABS.length].id, true); }
                if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') { e.preventDefault(); switchTab(TABS[(i + TABS.length - 1) % TABS.length].id, true); }
            });
            list.appendChild(b);
        });
        return list;
    }
    function switchTab(id, focus) {
        stopPlay();
        state.tab = id; state.table = false;
        render();
        if (focus) document.getElementById('tab-' + id).focus();
    }

    function optionControls(container) {
        container.innerHTML = '';
        if (state.tab === 'sunburst') {
            const b = el('button', { type: 'button', class: 'chip', 'aria-pressed': String(state.tech) }, 'Highlight Technology');
            b.addEventListener('click', () => { state.tech = !state.tech; b.setAttribute('aria-pressed', String(state.tech)); chart.refresh(); });
            container.appendChild(b);
        }
        if (state.tab === 'map') {
            Object.entries(METRICS).forEach(([k, m]) => {
                const b = el('button', { type: 'button', class: 'chip', 'aria-pressed': String(state.metric === k) }, m.label);
                b.addEventListener('click', () => { state.metric = k; optionControls(container); chart.refresh(); renderTable(); });
                container.appendChild(b);
            });
        }
        const t = el('button', { type: 'button', class: 'chip', 'aria-pressed': String(state.table) }, 'Show as table');
        t.addEventListener('click', () => { state.table = !state.table; t.setAttribute('aria-pressed', String(state.table)); renderTable(); });
        container.appendChild(t);
    }
    let tableHost = null;
    function renderTable() {
        if (!tableHost) return;
        tableHost.hidden = !state.table;
        if (!state.table) { tableHost.innerHTML = ''; return; }
        const t = chart.table(state.pos[state.tab]);
        tableHost.innerHTML = `<div class="table-wrap"><table class="data-table"><caption class="eyebrow" style="text-align:left;padding-bottom:6px">${t.caption}</caption><thead><tr>${t.head.map((h, i) => `<th scope="col" class="${t.num.includes(i) ? 'num' : ''}">${h}</th>`).join('')}</tr></thead><tbody>${t.rows.map(r => `<tr>${r.map((c, i) => `<td class="${t.num.includes(i) ? 'num' : ''}">${c}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`;
    }
    function mountChart(host) {
        chartHost = host; host.innerHTML = '';
        chart = FACTORY[state.tab](host);
    }

    // ---------- Variant A: Control deck ----------
    function variantA(app) {
        const tab = TABS.find(t => t.id === state.tab), periods = PERIODS[state.tab];
        app.className = 'va';
        app.innerHTML = `
            <div class="va-head">
                <div><div class="eyebrow">Research</div><h1>The market, over time</h1></div>
                <span class="pill">For information only. Not investment advice.</span>
            </div>`;
        app.appendChild(tabList('va-tabs'));
        const scrub = el('div', { class: 'va-scrub' });
        scrub.innerHTML = `<button type="button" class="btn-icon" data-play></button>
            <div class="va-range"><label class="sr-only" for="vaRange">${state.tab === 'tree' ? 'Quarter' : 'Year'}</label>
            <input type="range" id="vaRange" min="0" max="${periods.length - 1}" step="1" value="${state.pos[state.tab]}">
            <div class="ends"><span>${periods[0]}</span><span>${periods[periods.length - 1]}</span></div></div>
            <div class="period" id="vaPeriod"></div>`;
        app.appendChild(scrub);
        const body = el('div', { class: 'va-body' });
        const card = el('div', { class: 'va-card', role: 'tabpanel', 'aria-labelledby': 'tab-' + state.tab });
        card.innerHTML = `<div style="display:grid;gap:4px;margin-bottom:12px"><h2 style="font-size:1.05rem">${tab.long}</h2><div class="muted" style="font-size:13px">${SUBTITLE[state.tab]}</div></div>`;
        const host = el('div', { class: 'chart' }); card.appendChild(host);
        tableHost = el('div', { style: 'margin-top:12px' }); card.appendChild(tableHost);
        card.appendChild(el('div', { class: 'credit', style: 'margin-top:12px' }, CREDIT[state.tab]));
        const side = el('aside', { class: 'va-side va-card' });
        side.innerHTML = `<section><h2>View</h2><div class="vb-chips" id="vaOpts"></div></section>
            ${state.tab === 'sunburst' ? `<section><h2>SEC industry offices</h2><div class="legend">${Object.entries(OFFICE_COLOR).map(([n, v]) => `<span><i style="background:var(${v})"></i>${n}</span>`).join('')}<span><i style="background:var(--s-other)"></i>Other offices</span></div></section>` : ''}
            <section><h2>About this data</h2><p class="muted" style="margin:0;font-size:13px">${ABOUT[state.tab]}</p></section>
            <section><h2>Data as of</h2><p class="mono muted" style="margin:0;font-size:13px">2026-10-23 (prototype snapshot)</p></section>`;
        body.append(card, side); app.appendChild(body);
        mountChart(host);
        optionControls(side.querySelector('#vaOpts'));
        const range = scrub.querySelector('#vaRange'), periodEl = scrub.querySelector('#vaPeriod');
        range.addEventListener('input', () => { stopPlay(); setPos(+range.value); });
        scrub.querySelector('[data-play]').addEventListener('click', togglePlay);
        scrubSync = idx => { range.value = idx; periodEl.textContent = periods[idx]; range.setAttribute('aria-valuetext', periods[idx]); };
    }

    // ---------- Variant B: Timeline ----------
    function variantB(app) {
        const periods = PERIODS[state.tab];
        app.className = 'vb';
        app.innerHTML = `<div class="vb-top"><div><h1>Research</h1><div class="muted" style="font-size:13px">${SUBTITLE[state.tab]}</div></div></div>`;
        app.querySelector('.vb-top').appendChild(tabList('vb-seg'));
        const chips = el('div', { class: 'vb-chips' });
        app.appendChild(chips);
        const stage = el('div', { class: 'vb-stage', role: 'tabpanel', 'aria-labelledby': 'tab-' + state.tab });
        const host = el('div', { class: 'chart' }); stage.appendChild(host);
        tableHost = el('div', { style: 'margin-top:12px' }); stage.appendChild(tableHost);
        app.appendChild(stage);
        const tl = el('div', { class: 'vb-timeline' });
        tl.innerHTML = `<div class="row"><span class="eyebrow" id="vbAggLabel"></span><span class="mono" style="font-weight:600;font-size:1.1rem" id="vbPeriod"></span></div>`;
        app.appendChild(tl);
        app.appendChild(el('div', { class: 'vb-foot' }, `<span class="credit">${CREDIT[state.tab]}</span><span class="credit">For information only. Not investment advice.</span>`));
        mountChart(host);
        optionControls(chips);
        const play = el('button', { type: 'button', class: 'btn-icon', 'data-play': '' });
        play.addEventListener('click', togglePlay);
        chips.prepend(play);

        const agg = chart.aggregate(), fmt = chart.aggregateFormat || money;
        tl.querySelector('#vbAggLabel').textContent = chart.aggregateLabel;
        const W = Math.max(280, tl.clientWidth - 24), H = 84, pad = 14;
        const svg = d3.select(tl).append('svg').attr('viewBox', [0, 0, W, H]);
        const x = d3.scaleLinear().domain([0, periods.length - 1]).range([pad, W - pad]);
        const y = d3.scaleLinear().domain([0, d3.max(agg)]).range([H - 18, 6]);
        svg.append('path').datum(agg).attr('fill', 'var(--brand-soft)').attr('d', d3.area().x((d, i) => x(i)).y0(H - 18).y1(d => y(d)).curve(d3.curveMonotoneX));
        svg.append('path').datum(agg).attr('fill', 'none').attr('stroke', 'var(--brand)').attr('stroke-width', 2).attr('vector-effect', 'non-scaling-stroke')
            .attr('d', d3.line().x((d, i) => x(i)).y(d => y(d)).curve(d3.curveMonotoneX));
        const tickEvery = Math.ceil(periods.length / 8);
        svg.append('g').selectAll('text').data(periods.map((p, i) => [p, i]).filter(([, i]) => i % tickEvery === 0 || i === periods.length - 1))
            .join('text').attr('x', d => x(d[1])).attr('y', H - 4).attr('text-anchor', 'middle').attr('fill', 'var(--muted)')
            .style('font', '10px var(--mono)').text(d => d[0]);
        const handle = svg.append('g').attr('class', 'vb-handle').attr('tabindex', 0).attr('role', 'slider')
            .attr('aria-label', state.tab === 'tree' ? 'Quarter' : 'Year').attr('aria-valuemin', 0).attr('aria-valuemax', periods.length - 1);
        handle.append('line').attr('y1', 0).attr('y2', H - 16).attr('stroke', 'var(--text)').attr('stroke-width', 2).attr('vector-effect', 'non-scaling-stroke');
        handle.append('rect').attr('x', -9).attr('y', 0).attr('width', 18).attr('height', H - 16).attr('fill', 'transparent');
        const dot = handle.append('circle').attr('r', 6).attr('fill', 'var(--text)').attr('stroke', 'var(--surface)').attr('stroke-width', 2).attr('vector-effect', 'non-scaling-stroke');
        const toIdx = clientX => { const b = svg.node().getBoundingClientRect(); return Math.round(x.invert((clientX - b.left) / b.width * W)); };
        svg.on('pointerdown', e => { stopPlay(); setPos(toIdx(e.clientX)); });
        svg.call(d3.drag().on('drag', e => { const se = e.sourceEvent; setPos(toIdx(se.touches ? se.touches[0].clientX : se.clientX), { quiet: true }); })
            .on('end', () => announce(chart.update(state.pos[state.tab]))));
        handle.on('keydown', e => {
            const i = state.pos[state.tab];
            const step = { ArrowRight: 1, ArrowUp: 1, ArrowLeft: -1, ArrowDown: -1, PageUp: 4, PageDown: -4 }[e.key];
            if (step) { e.preventDefault(); stopPlay(); setPos(i + step); }
            if (e.key === 'Home') { e.preventDefault(); setPos(0); }
            if (e.key === 'End') { e.preventDefault(); setPos(periods.length - 1); }
        });
        scrubSync = idx => {
            handle.attr('transform', `translate(${x(idx)},0)`).attr('aria-valuenow', idx).attr('aria-valuetext', periods[idx]);
            dot.attr('cy', y(agg[idx]));
            tl.querySelector('#vbPeriod').textContent = `${periods[idx]} 00b7 ${fmt(agg[idx])}`;
        };
    }

    // ---------- Variant C: Stage ----------
    function variantC(app) {
        const tab = TABS.find(t => t.id === state.tab), periods = PERIODS[state.tab];
        app.className = 'vc';
        const rail = el('nav', { class: 'vc-rail', 'aria-label': 'Research' });
        rail.appendChild(el('div', { class: 'brand' }, 'Research'));
        rail.appendChild(tabList('', true)).style.display = 'contents';
        const main = el('main', { class: 'vc-main', role: 'tabpanel', 'aria-labelledby': 'tab-' + state.tab });
        main.innerHTML = `<div class="vc-title"><div><h1>${tab.long}</h1><div class="muted" style="font-size:13px">${SUBTITLE[state.tab]}</div></div><span class="pill">For information only</span></div>
            <div class="vc-options" id="vcOpts"></div>
            <div class="vc-stage"><div class="vc-year"><div class="vc-year-label" tabindex="0" role="slider" aria-label="${state.tab === 'tree' ? 'Quarter' : 'Year'}, drag sideways" aria-valuemin="0" aria-valuemax="${periods.length - 1}"></div></div>
            <div style="height:clamp(72px, 15vw, 196px)"></div><div class="vc-ticks" aria-hidden="true"></div><div class="vc-chart chart"></div></div>
            <div class="credit">${CREDIT[state.tab]}</div>`;
        app.append(rail, main);
        const host = main.querySelector('.vc-chart');
        tableHost = el('div'); main.insertBefore(tableHost, main.querySelector('.credit'));
        mountChart(host);
        optionControls(main.querySelector('#vcOpts'));
        const play = el('button', { type: 'button', class: 'vc-play', 'data-play': '' });
        play.addEventListener('click', togglePlay); app.appendChild(play);
        const ticks = main.querySelector('.vc-ticks');
        ticks.innerHTML = periods.map(() => '<i></i>').join('');
        const label = main.querySelector('.vc-year-label');
        let startX = 0, startIdx = 0, pxPerStep = 28;
        label.addEventListener('pointerdown', e => {
            stopPlay(); label.setPointerCapture(e.pointerId); label.classList.add('dragging');
            startX = e.clientX; startIdx = state.pos[state.tab];
            pxPerStep = Math.max(10, Math.min(40, (main.clientWidth * 0.8) / periods.length));
        });
        label.addEventListener('pointermove', e => {
            if (!label.classList.contains('dragging')) return;
            const idx = startIdx + Math.round((e.clientX - startX) / pxPerStep);
            if (idx !== state.pos[state.tab]) setPos(idx, { quiet: true });
        });
        const end = () => { if (label.classList.contains('dragging')) { label.classList.remove('dragging'); announce(chart.update(state.pos[state.tab])); } };
        label.addEventListener('pointerup', end); label.addEventListener('pointercancel', end);
        label.addEventListener('keydown', e => {
            const i = state.pos[state.tab];
            const step = { ArrowRight: 1, ArrowUp: 1, ArrowLeft: -1, ArrowDown: -1, PageUp: 4, PageDown: -4 }[e.key];
            if (step) { e.preventDefault(); stopPlay(); setPos(i + step); }
            if (e.key === 'Home') { e.preventDefault(); setPos(0); }
            if (e.key === 'End') { e.preventDefault(); setPos(periods.length - 1); }
        });
        scrubSync = idx => {
            label.textContent = periods[idx];
            label.setAttribute('aria-valuenow', idx); label.setAttribute('aria-valuetext', periods[idx]);
            [...ticks.children].forEach((t, i) => t.classList.toggle('on', i === idx));
        };
    }

    // ---------- render + prototype switcher ----------
    const VARIANTS = { a: ['A', 'Control deck', variantA], b: ['B', 'Timeline', variantB], c: ['C', 'Stage', variantC] };
    const KEYS = Object.keys(VARIANTS);
    const app = document.getElementById('app');
    function render() {
        stopPlay(); hideTip();
        app.innerHTML = ''; tableHost = null;
        VARIANTS[state.variant][2](app);
        syncPlay();
        setPos(state.pos[state.tab], { quiet: true });
        const [k, n] = VARIANTS[state.variant];
        document.getElementById('protoLabel').textContent = `${k} (${n})`;
    }
    function setVariant(v) {
        state.variant = v;
        try { history.replaceState(null, '', '#variant-' + v); } catch (e) { /* sandboxed */ }
        render();
    }
    function readHash() { const m = /variant-([abc])/.exec(location.hash); return m ? m[1] : 'a'; }
    const cycle = d => setVariant(KEYS[(KEYS.indexOf(state.variant) + d + KEYS.length) % KEYS.length]);
    document.getElementById('protoPrev').addEventListener('click', () => cycle(-1));
    document.getElementById('protoNext').addEventListener('click', () => cycle(1));
    const motionBtn = document.getElementById('protoMotion');
    motionBtn.addEventListener('click', () => { forcedReduce.on = !forcedReduce.on; motionBtn.setAttribute('aria-pressed', String(forcedReduce.on)); });
    document.addEventListener('keydown', e => {
        const a = document.activeElement;
        if (a && a !== document.body && !a.closest('.proto-bar')) return;
        if (e.key === 'ArrowLeft') cycle(-1);
        if (e.key === 'ArrowRight') cycle(1);
    });
    let resizeTimer, lastWidth = window.innerWidth;
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => { if (window.innerWidth !== lastWidth) { lastWidth = window.innerWidth; render(); } }, 250);
    });
    window.addEventListener('hashchange', () => { const v = readHash(); if (v !== state.variant) { state.variant = v; render(); } });
    state.variant = readHash();
    render();
})();
