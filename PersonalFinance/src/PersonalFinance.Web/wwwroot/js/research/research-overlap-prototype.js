/* global d3 */
/**
 * PROTOTYPE, throwaway (wayfinder ticket #236, "Fund overlap view: chord diagram or force graph?"). Never merge.
 * Three variants of a Fund Basket overlap view, mounted at the top of the Research page's ETFs tab, switched by
 * ?variant=A|B|C and the floating bar. Only loaded in Development when ?variant= is present.
 * Data: /prototype/fund-overlap/holdings.json, the latest public N-PORT filing of eight funds, common equity only.
 * Overlap of two funds = the sum, over securities both hold, of the smaller of the two weights (by CUSIP).
 *
 *   A  Chord diagram: one arc per fund, ribbons sized by pairwise overlap; hover a fund to isolate it; a
 *      minimum-overlap slider filters ribbons.
 *   B  Force graph: fund nodes linked to the holdings two or more basket funds share; holding size = basket weight;
 *      click a fund to isolate its cluster; the slider hides holdings below a basket weight.
 *   C  Matrix + table: a pairwise overlap heatmap and a ranked table of the most duplicated holdings.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    if (!page || typeof d3 === 'undefined') {
        return;
    }
    const VARIANTS = [
        { key: 'A', name: 'Chord diagram' },
        { key: 'B', name: 'Force graph' },
        { key: 'C', name: 'Matrix + duplicates table' }
    ];
    // Fixed categorical slot per fund (dataviz reference palette, light mode): colour follows the fund, never its rank.
    const SLOT = { VTI: '#2a78d6', SPY: '#eb6834', QQQ: '#1baf7a', VGT: '#eda100', AIQ: '#e87ba4', SCHD: '#008300', VUG: '#6250d6', IWM: '#e34948' };
    const BLUES = ['#cde2fb', '#9ec5f4', '#6da7ec', '#3987e5', '#256abf', '#184f95', '#0d366b'];
    const MAX_FUNDS = 5;
    const params = new URLSearchParams(window.location.search);
    const variant = (VARIANTS.find((v) => v.key === (params.get('variant') || 'A').toUpperCase()) || VARIANTS[0]).key;
    const reduced = () => page.prefersReducedMotion();
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
    const fmt = (v) => `${v.toFixed(1)}%`;

    // ---------- model ----------

    function model (data, basket) {
        const tickers = basket.map((b) => b.t);
        const weightSum = d3.sum(basket, (b) => b.w) || 1;
        const maps = Object.fromEntries(tickers.map((t) => [t, new Map(data.funds[t].h.map(([k, n, p]) => [k, { n, p }]))]));
        const pair = (a, b) => {
            let w = 0;
            let c = 0;
            for (const [k, v] of maps[a]) {
                const o = maps[b].get(k);
                if (o) {
                    w += Math.min(v.p, o.p);
                    c += 1;
                }
            }
            return { w, c };
        };
        const matrix = tickers.map((a) => tickers.map((b) => (a === b ? 0 : pair(a, b).w)));
        // Securities held by two or more basket funds, with their basket weight (fund weight x holding weight).
        const sec = new Map();
        basket.forEach(({ t, w }) => {
            for (const [k, v] of maps[t]) {
                const e = sec.get(k) || { k, name: v.n, funds: [], basketPct: 0 };
                e.funds.push({ t, p: v.p });
                e.basketPct += (w / weightSum) * v.p;
                sec.set(k, e);
            }
        });
        const shared = Array.from(sec.values()).filter((s) => s.funds.length > 1).sort((a, b) => b.basketPct - a.basketPct);
        const sharedTotal = d3.sum(shared, (s) => s.basketPct);
        let best = null;
        for (let i = 0; i < tickers.length; i++) {
            for (let j = i + 1; j < tickers.length; j++) {
                if (!best || matrix[i][j] > best.w) {
                    best = { a: tickers[i], b: tickers[j], w: matrix[i][j] };
                }
            }
        }
        return { tickers, matrix, shared, sharedTotal, best, pair };
    }

    function callouts (m) {
        const items = [];
        if (m.best) {
            items.push({ label: 'Most overlapping pair', value: fmt(m.best.w), note: `${m.best.a} and ${m.best.b} share this much by weight` });
        }
        items.push({ label: 'Held by 2+ of your funds', value: fmt(m.sharedTotal), note: `of your basket, across ${m.shared.length.toLocaleString('en-US')} securities` });
        if (m.shared[0]) {
            items.push({ label: 'Largest duplicated holding', value: fmt(m.shared[0].basketPct), note: `${m.shared[0].name}, in ${m.shared[0].funds.map((f) => f.t).join(', ')}` });
        }
        return items;
    }

    // ---------- variants ----------

    function chord (host, m, state, tooltip, redraw) {
        const size = Math.min(460, host.clientWidth || 460);
        const outer = size / 2 - 40;
        const inner = outer - 14;
        const svg = d3.select(host).append('svg').attr('viewBox', [-size / 2, -size / 2, size, size]).attr('class', 'po-svg');
        // Arc length = the sum of a fund's pairwise overlaps; pairs under the filter are dropped.
        const mat = m.matrix.map((row, i) => row.map((v, j) => (i !== j && v >= state.min ? v : 0)));
        const chords = d3.chord().padAngle(0.04).sortSubgroups(d3.descending)(mat);
        const arc = d3.arc().innerRadius(inner).outerRadius(outer);
        const ribbon = d3.ribbon().radius(inner - 2);
        const g = svg.append('g');
        const ribbons = g.append('g').attr('fill-opacity', 0.7).selectAll('path').data(chords).join('path')
            .attr('d', ribbon).attr('fill', (d) => SLOT[m.tickers[d.source.value >= d.target.value ? d.source.index : d.target.index]])
            .attr('stroke', 'var(--bs-body-bg, #fff)').attr('stroke-width', 1)
            .on('pointermove', (e, d) => tooltip(e, [`${m.tickers[d.source.index]} ↔ ${m.tickers[d.target.index]}`, `${fmt(m.matrix[d.source.index][d.target.index])} overlap by weight`]))
            .on('pointerleave', () => tooltip(null));
        const groups = g.append('g').selectAll('g').data(chords.groups).join('g');
        groups.append('path').attr('d', arc).attr('fill', (d) => SLOT[m.tickers[d.index]]).attr('tabindex', 0)
            .attr('role', 'button').attr('aria-label', (d) => `${m.tickers[d.index]}: overlaps ${m.tickers.filter((t, j) => j !== d.index).map((t) => `${t} ${fmt(m.matrix[d.index][m.tickers.indexOf(t)])}`).join(', ')}`)
            .on('pointerenter focus', (e, d) => isolate(d.index)).on('pointerleave blur', () => isolate(null));
        groups.append('text').each((d) => { d.angle = (d.startAngle + d.endAngle) / 2; })
            .attr('dy', '0.35em').attr('class', 'po-label')
            .attr('transform', (d) => `rotate(${d.angle * 180 / Math.PI - 90}) translate(${outer + 8}) ${d.angle > Math.PI ? 'rotate(180)' : ''}`)
            .attr('text-anchor', (d) => (d.angle > Math.PI ? 'end' : null)).text((d) => m.tickers[d.index]);
        function isolate (i) {
            ribbons.transition().duration(reduced() ? 0 : 150)
                .attr('fill-opacity', (d) => (i === null || d.source.index === i || d.target.index === i ? 0.75 : 0.06));
        }
        if (!chords.length) {
            svg.append('text').attr('text-anchor', 'middle').attr('class', 'po-empty').text('No pair overlaps above the filter');
        }
        void redraw;
    }

    function force (host, m, state, tooltip) {
        const w = host.clientWidth || 720;
        const h = 520;
        const svg = d3.select(host).append('svg').attr('viewBox', [0, 0, w, h]).attr('class', 'po-svg');
        const shown = m.shared.filter((s) => s.basketPct >= state.minHolding).slice(0, 40);
        const fundNodes = m.tickers.map((t) => ({ id: t, kind: 'fund' }));
        const secNodes = shown.map((s) => ({ id: s.k, kind: 'sec', s }));
        const links = shown.flatMap((s) => s.funds.map((f) => ({ source: f.t, target: s.k, p: f.p })));
        const nodes = fundNodes.concat(secNodes);
        const r = d3.scaleSqrt().domain([0, d3.max(shown, (s) => s.basketPct) || 1]).range([3, 22]);
        const sim = d3.forceSimulation(nodes)
            .force('link', d3.forceLink(links).id((d) => d.id).distance((l) => 120 + 60 / (1 + l.p)).strength(0.25))
            .force('charge', d3.forceManyBody().strength((d) => (d.kind === 'fund' ? -900 : -120)))
            .force('center', d3.forceCenter(w / 2, h / 2))
            .force('collide', d3.forceCollide((d) => (d.kind === 'fund' ? 30 : r(d.s.basketPct) + 2)));
        const link = svg.append('g').attr('stroke-opacity', 0.35).selectAll('line').data(links).join('line')
            .attr('stroke', (l) => SLOT[l.source.id || l.source]).attr('stroke-width', (l) => Math.max(1, Math.sqrt(l.p) * 1.5));
        const node = svg.append('g').selectAll('g').data(nodes).join('g').attr('class', (d) => `po-node po-node--${d.kind}`);
        node.filter((d) => d.kind === 'fund').append('circle').attr('r', 24).attr('fill', (d) => SLOT[d.id])
            .attr('stroke', 'var(--bs-body-bg, #fff)').attr('stroke-width', 2);
        node.filter((d) => d.kind === 'fund').append('text').attr('text-anchor', 'middle').attr('dy', '0.35em').attr('class', 'po-node-label').text((d) => d.id);
        node.filter((d) => d.kind === 'sec').append('circle').attr('r', (d) => r(d.s.basketPct)).attr('fill', '#9ca3af')
            .attr('stroke', 'var(--bs-body-bg, #fff)').attr('stroke-width', 1.5);
        node.filter((d) => d.kind === 'sec' && r(d.s.basketPct) > 12).append('text').attr('text-anchor', 'middle').attr('dy', '0.35em')
            .attr('class', 'po-sec-label').text((d) => d.s.name.split(' ')[0].slice(0, 6));
        node.attr('tabindex', 0).attr('role', 'button')
            .attr('aria-label', (d) => (d.kind === 'fund' ? `${d.id}. Press Enter to isolate its cluster.` : `${d.s.name}: ${fmt(d.s.basketPct)} of your basket, in ${d.s.funds.map((f) => f.t).join(', ')}`))
            .on('pointermove', (e, d) => (d.kind === 'sec' ? tooltip(e, [d.s.name, `${fmt(d.s.basketPct)} of your basket`, ...d.s.funds.map((f) => `${f.t}: ${fmt(f.p)} of the fund`)]) : tooltip(e, [d.id, 'Click to isolate'])))
            .on('pointerleave', () => tooltip(null))
            .on('click keydown', (e, d) => {
                if (e.type === 'keydown' && e.key !== 'Enter') {
                    return;
                }
                if (d.kind === 'fund') {
                    state.isolate = state.isolate === d.id ? null : d.id;
                    apply();
                }
            })
            .call(d3.drag().on('start', (e, d) => { if (!e.active) { sim.alphaTarget(0.3).restart(); } d.fx = d.x; d.fy = d.y; })
                .on('drag', (e, d) => { d.fx = e.x; d.fy = e.y; })
                .on('end', (e, d) => { if (!e.active) { sim.alphaTarget(0); } d.fx = null; d.fy = null; }));
        function apply () {
            const keep = (d) => !state.isolate || d.id === state.isolate || (d.kind === 'sec' && d.s.funds.some((f) => f.t === state.isolate));
            node.transition().duration(reduced() ? 0 : 150).style('opacity', (d) => (keep(d) ? 1 : 0.12));
            link.transition().duration(reduced() ? 0 : 150).attr('stroke-opacity', (l) => (!state.isolate || l.source.id === state.isolate ? 0.45 : 0.03));
        }
        sim.on('tick', () => {
            nodes.forEach((d) => { d.x = Math.max(24, Math.min(w - 24, d.x)); d.y = Math.max(24, Math.min(h - 24, d.y)); });
            link.attr('x1', (l) => l.source.x).attr('y1', (l) => l.source.y).attr('x2', (l) => l.target.x).attr('y2', (l) => l.target.y);
            node.attr('transform', (d) => `translate(${d.x},${d.y})`);
        });
        if (reduced()) {
            sim.stop();
            for (let i = 0; i < 300; i++) {
                sim.tick();
            }
            sim.on('tick')();
        }
        apply();
    }

    function matrixAndTable (host, m, state, tooltip) {
        const n = m.tickers.length;
        const cell = 64;
        const pad = 56;
        const svg = d3.select(host).append('svg').attr('viewBox', [0, 0, pad + n * cell, pad + n * cell]).attr('class', 'po-svg po-svg--matrix');
        const colour = d3.scaleQuantize().domain([0, 100]).range(BLUES);
        m.tickers.forEach((t, i) => {
            svg.append('text').attr('x', pad - 8).attr('y', pad + i * cell + cell / 2).attr('dy', '0.35em').attr('text-anchor', 'end').attr('class', 'po-label').text(t);
            svg.append('text').attr('x', pad + i * cell + cell / 2).attr('y', pad - 10).attr('text-anchor', 'middle').attr('class', 'po-label').text(t);
            m.tickers.forEach((u, j) => {
                const v = m.matrix[i][j];
                const g = svg.append('g').attr('transform', `translate(${pad + j * cell},${pad + i * cell})`);
                g.append('rect').attr('width', cell - 2).attr('height', cell - 2).attr('rx', 4)
                    .attr('fill', i === j ? 'var(--bs-tertiary-bg, #f3f4f6)' : colour(v))
                    .attr('tabindex', i === j ? null : 0).attr('aria-label', i === j ? null : `${t} and ${u}: ${fmt(v)} overlap by weight`)
                    .on('pointermove', (e) => i !== j && tooltip(e, [`${t} ↔ ${u}`, `${fmt(v)} overlap by weight`, `${m.pair(t, u).c} securities in common`]))
                    .on('pointerleave', () => tooltip(null));
                if (i !== j) {
                    g.append('text').attr('x', (cell - 2) / 2).attr('y', (cell - 2) / 2).attr('dy', '0.35em').attr('text-anchor', 'middle')
                        .attr('class', 'po-cell').attr('fill', v > 45 ? '#fff' : '#0b0b0b').attr('pointer-events', 'none').text(`${Math.round(v)}%`);
                }
            });
        });
        const table = el('table', 'po-table');
        const head = el('thead');
        const headRow = el('tr');
        headRow.append(el('th', null, 'Holding'), el('th', null, 'Held by'), el('th', 'num', '% of basket'));
        head.append(headRow);
        table.append(el('caption', null, 'Most duplicated holdings in your basket'), head);
        const tbody = el('tbody');
        m.shared.slice(0, 12).forEach((s) => {
            const tr = el('tr');
            const by = el('td');
            s.funds.forEach((f) => {
                const chip = el('span', 'po-chip', f.t);
                chip.style.setProperty('--c', SLOT[f.t]);
                by.append(chip);
            });
            tr.append(el('td', null, s.name), by, el('td', 'num', fmt(s.basketPct)));
            tbody.append(tr);
        });
        table.append(tbody);
        host.append(table);
        void state;
    }

    // ---------- mount ----------

    function mount (data) {
        const panel = document.getElementById('research-panel-etfs');
        const tabButton = document.getElementById('research-tab-etfs');
        if (!panel) {
            return;
        }
        const card = el('div', 'research-card po-card');
        card.append(el('h2', 'research-card__title', 'How much your funds overlap'),
            el('p', 'research-card__subtitle', `Latest reported holdings, common stock only. Overlap is the share of two funds' money in the same companies. ${data.source}.`));
        const picker = el('div', 'po-picker');
        picker.setAttribute('role', 'group');
        picker.setAttribute('aria-label', 'Fund Basket (prototype stub, up to 5)');
        const controls = el('div', 'po-controls');
        const stats = el('div', 'po-stats');
        const stage = el('div', 'po-stage');
        const hint = el('p', 'po-hint');
        card.append(picker, controls, stats, stage, hint);
        panel.prepend(card);
        const tip = el('div', 'research-tooltip');
        tip.hidden = true;
        document.body.append(tip);
        const tooltip = (e, lines) => {
            if (!e) {
                tip.hidden = true;
                return;
            }
            tip.replaceChildren(...lines.map((l, i) => el('span', i ? 'research-tooltip__muted' : 'research-tooltip__name', l)));
            tip.hidden = false;
            tip.style.position = 'fixed';
            tip.style.left = `${Math.min(e.clientX + 14, window.innerWidth - 260)}px`;
            tip.style.top = `${e.clientY + 14}px`;
        };
        const state = { basket: ['VTI', 'QQQ', 'VGT', 'AIQ'].map((t) => ({ t, w: 25 })), min: 0, minHolding: 0.2, isolate: null };

        function renderPicker () {
            picker.replaceChildren(el('span', 'po-picker__label', 'Fund Basket (stub):'), ...Object.keys(data.funds).map((t) => {
                const on = state.basket.some((b) => b.t === t);
                const b = el('button', `po-fund${on ? ' po-fund--on' : ''}`, t);
                b.type = 'button';
                b.style.setProperty('--c', SLOT[t]);
                b.setAttribute('aria-pressed', String(on));
                b.title = data.funds[t].name;
                b.disabled = !on && state.basket.length >= MAX_FUNDS;
                b.addEventListener('click', () => {
                    state.basket = on ? state.basket.filter((x) => x.t !== t) : state.basket.concat({ t, w: 20 });
                    state.isolate = null;
                    draw();
                });
                return b;
            }));
        }

        function renderControls () {
            controls.replaceChildren();
            if (variant === 'C') {
                return;
            }
            const label = el('label', 'po-slider');
            const isA = variant === 'A';
            const value = isA ? state.min : state.minHolding;
            const input = el('input');
            input.type = 'range';
            input.min = '0';
            input.max = isA ? '60' : '2';
            input.step = isA ? '5' : '0.1';
            input.value = String(value);
            const out = el('output', null, isA ? `${value}%` : `${value.toFixed(1)}%`);
            input.addEventListener('input', () => {
                if (isA) {
                    state.min = Number(input.value);
                } else {
                    state.minHolding = Number(input.value);
                }
                draw(true);
            });
            label.append(el('span', null, isA ? 'Hide pairs that overlap less than' : 'Hide holdings below this share of your basket'), input, out);
            controls.append(label);
        }

        function draw (keepControls) {
            renderPicker();
            if (!keepControls) {
                renderControls();
            }
            stage.replaceChildren();
            stats.replaceChildren();
            if (state.basket.length < 2) {
                stage.append(el('p', 'po-empty', 'Add at least two funds to your basket to see how they overlap.'));
                return;
            }
            const m = model(data, state.basket);
            stats.replaceChildren(...callouts(m).map((c) => {
                const d = el('div', 'proto-stat po-stat');
                d.append(el('span', 'po-stat__label', c.label), el('span', 'po-stat__value', c.value), el('span', 'po-stat__note', c.note));
                return d;
            }));
            if (variant === 'A') {
                chord(stage, m, state, tooltip, draw);
                hint.textContent = 'Hover or Tab to a fund arc to isolate its ribbons. Ribbon width is the overlap by weight between two funds.';
            } else if (variant === 'B') {
                force(stage, m, state, tooltip);
                hint.textContent = 'Grey circles are holdings two or more of your funds share, sized by their share of your basket. Click or press Enter on a fund to isolate its cluster; drag to rearrange.';
            } else {
                matrixAndTable(stage, m, state, tooltip);
                hint.textContent = 'Each cell is the overlap by weight between two funds. The table lists holdings in two or more of your funds.';
            }
        }

        if (tabButton) {
            tabButton.click();
        }
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
            const response = await fetch('/prototype/fund-overlap/holdings.json');
            mount(await response.json());
        } catch (err) {
            document.body.append(el('p', 'po-empty', 'Prototype data failed to load.'));
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
