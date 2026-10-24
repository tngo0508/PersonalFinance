/* global d3 */
/**
 * PROTOTYPE, throwaway (wayfinder ticket #240, "Risk vs return tab: windows, bubbles, quadrants and hover cards").
 * Never merge. Draws into the prototype-only "Risk vs return" tab, switched by ?variant=A|B|C and the floating bar.
 * Data: /prototype/risk-return/risk-return.json (N-PORT monthly NAV returns and net assets, five verified expense
 * ratios, Fed H.15 3-month T-bill). Annualized return = compounded monthly returns; volatility = standard deviation
 * of monthly returns x sqrt(12); Sharpe = (annualized return - average T-bill) / volatility.
 *
 *   A  Bubble scatter + quadrants: volatility (x) vs annualized return (y), bubble area = net assets, the basket as a
 *      diamond; quadrant lines through the basket, labelled descriptively; role filter chips; hover/focus card.
 *   B  Ranked rows: one row per fund sorted by Sharpe ratio, with return and volatility as inline bars. No scatter.
 *   C  Window trails: the same scatter, each fund drawn as a path 5Y -> 3Y -> 1Y; Sharpe guide lines from the T-bill
 *      rate instead of quadrants.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    if (!page || typeof d3 === 'undefined') {
        return;
    }
    const VARIANTS = [
        { key: 'A', name: 'Bubble scatter + quadrants' },
        { key: 'B', name: 'Ranked rows by Sharpe' },
        { key: 'C', name: 'Window trails + Sharpe lines' }
    ];
    const SLOT = { VTI: '#2a78d6', VT: '#eb6834', VXUS: '#1baf7a', BND: '#eda100', QQQ: '#e87ba4', SCHD: '#008300', VGT: '#6250d6', IWM: '#e34948' };
    const BASKET = '#111827';
    const WINDOWS = [['1Y', 12], ['3Y', 36], ['5Y', 60]];
    const params = new URLSearchParams(window.location.search);
    const variant = (VARIANTS.find((v) => v.key === (params.get('variant') || 'A').toUpperCase()) || VARIANTS[0]).key;
    const pct = (v) => `${v >= 0 ? '+' : ''}${v.toFixed(1)}%`;
    const money = (v) => (v >= 1e12 ? `$${(v / 1e12).toFixed(2)}T` : `$${(v / 1e9).toFixed(0)}B`);
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

    // ---------- model ----------

    function months (data) {
        const all = Object.values(data.funds).map((f) => new Set(Object.keys(f.returns)));
        return Object.keys(data.funds.VTI.returns).filter((m) => all.every((s) => s.has(m))).sort();
    }
    function measure (returns, window, tbill) {
        const r = window.map((m) => returns[m] / 100);
        const growth = r.reduce((g, x) => g * (1 + x), 1);
        const ann = (Math.pow(growth, 12 / r.length) - 1) * 100;
        const vol = d3.deviation(r) * Math.sqrt(12) * 100;
        const rf = d3.mean(window, (m) => tbill[m] || 0);
        return { ann, vol, sharpe: vol > 0 ? (ann - rf) / vol : 0, rf };
    }
    function basketReturns (data, basket, window) {
        const total = d3.sum(basket, (b) => b.w);
        return Object.fromEntries(window.map((m) => [m, d3.sum(basket, (b) => (b.w / total) * data.funds[b.t].returns[m])]));
    }
    function rows (data, basket, n, all) {
        const window = all.slice(-n);
        const out = Object.entries(data.funds).map(([t, f]) => ({ t, f, ...measure(f.returns, window, data.tbill) }));
        if (basket.length) {
            out.push({ t: 'Your basket', basket: true, f: { name: basket.map((b) => `${b.t} ${b.w}%`).join(', '), role: 'Basket' }, ...measure(basketReturns(data, basket, window), window, data.tbill) });
        }
        return { list: out, window };
    }
    function card (r) {
        const lines = [r.t === 'Your basket' ? 'Your basket' : `${r.t} · ${r.f.name}`, `Annualized return ${pct(r.ann)}`, `Volatility ${r.vol.toFixed(1)}%`, `Sharpe ratio ${r.sharpe.toFixed(2)} (vs 3-month T-bills, ${r.rf.toFixed(1)}%)`];
        if (!r.basket) {
            lines.push(`Expense ratio ${r.f.expenseRatio === null ? 'not available' : `${r.f.expenseRatio.toFixed(2)}%`}`, `Net assets ${money(r.f.netAssets)} (all share classes)`);
        } else {
            lines.push(r.f.name);
        }
        return lines;
    }

    // ---------- mount ----------

    function mount (data) {
        const panel = document.getElementById('research-panel-risk');
        const tab = document.getElementById('research-tab-risk');
        if (!panel) {
            return;
        }
        const all = months(data);
        const c = el('div', 'research-card pr-card');
        c.append(el('h2', 'research-card__title', 'Return and volatility, side by side'),
            el('p', 'research-card__subtitle', 'How much each fund returned per year, against how much its monthly returns swung, over the window you choose. Past figures only; they describe, they do not rank funds as good or bad.'));
        const picker = el('div', 'pr-picker');
        const controls = el('div', 'pr-controls');
        const statsEl = el('div', 'pr-stats');
        const stage = el('div', 'pr-stage');
        const hint = el('p', 'pr-hint');
        c.append(picker, controls, statsEl, stage, hint, el('p', 'research-card__source', `Sources: ${data.source}`));
        panel.append(c);
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
        const roles = Array.from(new Set(Object.values(data.funds).map((f) => f.role)));
        const state = { basket: [{ t: 'VTI', w: 60 }, { t: 'VXUS', w: 30 }, { t: 'BND', w: 10 }], win: 36, roles: new Set(roles) };

        function seg (label, options, value, onPick) {
            const wrap = el('div', 'pp-seg');
            wrap.append(el('span', 'pp-seg__label', label));
            options.forEach(([v, text]) => {
                const b = el('button', 'pp-seg__btn', text);
                b.type = 'button';
                b.setAttribute('aria-pressed', String(v === value));
                b.addEventListener('click', () => onPick(v));
                wrap.append(b);
            });
            return wrap;
        }

        function draw () {
            picker.replaceChildren(el('span', 'po-picker__label', 'Fund Basket (stub):'), ...Object.keys(data.funds).map((t) => {
                const item = state.basket.find((b) => b.t === t);
                const b = el('button', `po-fund${item ? ' po-fund--on' : ''}`, item ? `${t} ${item.w}%` : t);
                b.type = 'button';
                b.style.setProperty('--c', SLOT[t]);
                b.setAttribute('aria-pressed', String(Boolean(item)));
                b.disabled = !item && state.basket.length >= 5;
                b.addEventListener('click', () => {
                    const next = item ? state.basket.filter((x) => x.t !== t) : state.basket.concat({ t, w: 0 });
                    const each = Math.round(100 / Math.max(1, next.length));
                    state.basket = next.map((x) => ({ t: x.t, w: each }));
                    draw();
                });
                return b;
            }));
            controls.replaceChildren();
            if (variant !== 'C') {
                controls.append(seg('Window', WINDOWS.map(([l, n]) => [n, l]), state.win, (v) => { state.win = v; draw(); }));
            }
            const filter = el('div', 'pp-seg');
            filter.append(el('span', 'pp-seg__label', 'Show'));
            roles.forEach((r) => {
                const b = el('button', 'pp-seg__btn', r);
                b.type = 'button';
                b.setAttribute('aria-pressed', String(state.roles.has(r)));
                b.addEventListener('click', () => {
                    if (state.roles.has(r)) {
                        state.roles.delete(r);
                    } else {
                        state.roles.add(r);
                    }
                    draw();
                });
                filter.append(b);
            });
            controls.append(filter);
            stage.replaceChildren();
            const { list, window } = rows(data, state.basket, state.win, all);
            const visible = list.filter((r) => r.basket || state.roles.has(r.f.role));
            const funds = visible.filter((r) => !r.basket);
            const basket = visible.find((r) => r.basket);
            const hiR = d3.greatest(funds, (r) => r.ann);
            const loV = d3.least(funds, (r) => r.vol);
            const hiS = d3.greatest(funds, (r) => r.sharpe);
            const period = `${d3.utcFormat('%b %Y')(d3.utcParse('%Y-%m')(window[0]))} – ${d3.utcFormat('%b %Y')(d3.utcParse('%Y-%m')(window[window.length - 1]))}`;
            statsEl.replaceChildren(...[
                hiR && { label: 'Highest return in this set', value: hiR.t, note: `${pct(hiR.ann)} a year, ${period}` },
                loV && { label: 'Smallest swings in this set', value: loV.t, note: `${loV.vol.toFixed(1)}% volatility` },
                hiS && { label: 'Most return per unit of volatility', value: hiS.t, note: `Sharpe ratio ${hiS.sharpe.toFixed(2)}` }
            ].filter(Boolean).map((s) => {
                const d = el('div', 'po-stat');
                d.append(el('span', 'po-stat__label', s.label), el('span', 'po-stat__value', s.value), el('span', 'po-stat__note', s.note));
                return d;
            }));

            if (variant === 'B') {
                const sorted = visible.slice().sort((a, b) => b.sharpe - a.sharpe);
                const maxR = d3.max(sorted, (r) => Math.abs(r.ann)) || 1;
                const maxV = d3.max(sorted, (r) => r.vol) || 1;
                const table = el('table', 'pr-rows');
                const head = el('thead');
                const hr = el('tr');
                ['Fund', 'Sharpe ratio', 'Annualized return', 'Volatility', 'Expense ratio'].forEach((h, i) => hr.append(el('th', i ? 'num' : null, h)));
                head.append(hr);
                const tb = el('tbody');
                sorted.forEach((r) => {
                    const tr = el('tr', r.basket ? 'pr-row--basket' : null);
                    tr.tabIndex = 0;
                    const name = el('td');
                    const sw = el('span', 'pr-dot');
                    sw.style.background = r.basket ? BASKET : SLOT[r.t];
                    name.append(sw, r.basket ? `Your basket (${r.f.name})` : `${r.t} · ${r.f.name}`);
                    const bar = (v, max, cls) => {
                        const td = el('td', 'num pr-barcell');
                        const b = el('span', `pr-bar ${cls}`);
                        b.style.width = `${Math.abs(v) / max * 100}%`;
                        td.append(b, el('span', 'pr-barval', cls === 'pr-bar--vol' ? `${v.toFixed(1)}%` : pct(v)));
                        return td;
                    };
                    tr.append(name, el('td', 'num pr-strong', r.sharpe.toFixed(2)), bar(r.ann, maxR, 'pr-bar--ret'), bar(r.vol, maxV, 'pr-bar--vol'),
                        el('td', 'num', r.basket ? '—' : (r.f.expenseRatio === null ? 'n/a' : `${r.f.expenseRatio.toFixed(2)}%`)));
                    tr.addEventListener('pointermove', (e) => tooltip(e, card(r)));
                    tr.addEventListener('pointerleave', () => tooltip(null));
                    tb.append(tr);
                });
                table.append(el('caption', null, `Sorted by Sharpe ratio, ${period}`), head, tb);
                stage.append(table);
                hint.textContent = 'One row per fund, sorted by return per unit of volatility. The bars make the two ingredients of the Sharpe ratio visible. "n/a" marks an expense ratio not in the fund\'s XBRL.';
                return;
            }

            const w = stage.clientWidth || 900;
            const h = Math.round(Math.min(520, w * 0.6));
            const m = { t: 20, r: 24, b: 44, l: 56 };
            const svg = d3.select(stage).append('svg').attr('viewBox', [0, 0, w, h]).attr('class', 'pr-svg').attr('role', 'group')
                .attr('aria-label', 'Scatter of volatility against annualized return');
            let points = visible;
            let trails = null;
            if (variant === 'C') {
                trails = visible.map((r) => ({ r, path: WINDOWS.slice().reverse().map(([l, n]) => {
                    const win = all.slice(-n);
                    const ret = r.basket ? basketReturns(data, state.basket, win) : r.f.returns;
                    return { l, ...measure(ret, win, data.tbill) };
                }) }));
                points = trails.flatMap((t) => t.path);
            }
            const x = d3.scaleLinear().domain([0, d3.max(points, (p) => p.vol) * 1.1]).nice().range([m.l, w - m.r]);
            const y = d3.scaleLinear().domain(d3.extent(points, (p) => p.ann)).nice().range([h - m.b, m.t]);
            if (y.domain()[0] > 0) {
                y.domain([0, y.domain()[1]]);
            }
            svg.append('g').attr('class', 'pp-grid').attr('transform', `translate(${m.l},0)`).call(d3.axisLeft(y).ticks(6).tickSize(-(w - m.l - m.r)).tickFormat((v) => `${v}%`));
            svg.append('g').attr('class', 'pp-grid').attr('transform', `translate(0,${h - m.b})`).call(d3.axisBottom(x).ticks(6).tickSize(-(h - m.t - m.b)).tickFormat((v) => `${v}%`));
            svg.append('text').attr('class', 'pr-axis-title').attr('x', w - m.r).attr('y', h - 8).attr('text-anchor', 'end').text('Volatility (annualized standard deviation of monthly returns) →');
            svg.append('text').attr('class', 'pr-axis-title').attr('x', m.l).attr('y', 12).text('↑ Annualized return');
            if (y.domain()[0] < 0) {
                svg.append('line').attr('class', 'pp-baseline').attr('x1', m.l).attr('x2', w - m.r).attr('y1', y(0)).attr('y2', y(0));
            }
            if (variant === 'A' && basket) {
                const qx = x(basket.vol);
                const qy = y(basket.ann);
                svg.append('line').attr('class', 'pr-quad').attr('x1', qx).attr('x2', qx).attr('y1', m.t).attr('y2', h - m.b);
                svg.append('line').attr('class', 'pr-quad').attr('x1', m.l).attr('x2', w - m.r).attr('y1', qy).attr('y2', qy);
                [['Higher return, smaller swings than your basket', m.l + 6, m.t + 14, 'start'], ['Higher return, bigger swings', w - m.r - 6, m.t + 14, 'end'],
                    ['Lower return, smaller swings', m.l + 6, h - m.b - 8, 'start'], ['Lower return, bigger swings', w - m.r - 6, h - m.b - 8, 'end']]
                    .forEach(([t, tx, ty, a]) => svg.append('text').attr('class', 'pr-quad-label').attr('x', tx).attr('y', ty).attr('text-anchor', a).text(t));
            }
            if (variant === 'C') {
                const rf = d3.mean(all.slice(-36), (mm) => data.tbill[mm] || 0);
                [0.5, 1].forEach((s) => {
                    const x2 = x.domain()[1];
                    svg.append('line').attr('class', 'pr-sharpe').attr('x1', x(0)).attr('y1', y(rf)).attr('x2', x(x2)).attr('y2', y(rf + s * x2));
                    svg.append('text').attr('class', 'pr-quad-label').attr('x', x(x2) - 4).attr('y', y(Math.min(y.domain()[1], rf + s * x2)) + 12).attr('text-anchor', 'end').text(`Sharpe ${s}`);
                });
                const line = d3.line().x((p) => x(p.vol)).y((p) => y(p.ann)).curve(d3.curveCatmullRom);
                trails.forEach((t) => {
                    const c2 = t.r.basket ? BASKET : SLOT[t.r.t];
                    svg.append('path').attr('d', line(t.path)).attr('fill', 'none').attr('stroke', c2).attr('stroke-width', 2).attr('stroke-opacity', 0.6)
                        .attr('marker-end', null);
                    t.path.forEach((p, i) => {
                        svg.append('circle').attr('cx', x(p.vol)).attr('cy', y(p.ann)).attr('r', i === t.path.length - 1 ? 7 : 3.5).attr('fill', c2)
                            .attr('stroke', '#fff').attr('stroke-width', 1.5).attr('tabindex', i === t.path.length - 1 ? 0 : null)
                            .on('pointermove', (e) => tooltip(e, [`${t.r.t} · ${p.l}`, `Annualized return ${pct(p.ann)}`, `Volatility ${p.vol.toFixed(1)}%`, `Sharpe ratio ${p.sharpe.toFixed(2)}`]))
                            .on('pointerleave', () => tooltip(null));
                    });
                    const end = t.path[t.path.length - 1];
                    svg.append('text').attr('class', 'pr-label').attr('x', x(end.vol) + 9).attr('y', y(end.ann)).attr('dy', '0.35em').text(t.r.basket ? 'Your basket' : t.r.t);
                });
                hint.textContent = 'Each fund is a path from its 5-year figures (small dot) through 3 years to the last 12 months (large dot). Diagonal lines mark a Sharpe ratio of 0.5 and 1 against 3-month T-bills.';
                return;
            }
            const area = d3.scaleSqrt().domain([0, d3.max(funds, (r) => r.f.netAssets) || 1]).range([6, 34]);
            const sorted = funds.slice().sort((a, b) => b.f.netAssets - a.f.netAssets);
            const g = svg.append('g');
            sorted.forEach((r) => {
                const node = g.append('g').attr('transform', `translate(${x(r.vol)},${y(r.ann)})`).attr('tabindex', 0).attr('role', 'img').attr('class', 'pr-node')
                    .attr('aria-label', card(r).join('. '));
                node.append('circle').attr('r', area(r.f.netAssets)).attr('fill', SLOT[r.t]).attr('fill-opacity', 0.55).attr('stroke', SLOT[r.t]).attr('stroke-width', 2);
                node.append('text').attr('class', 'pr-label').attr('text-anchor', 'middle').attr('dy', '0.35em').text(r.t);
                node.on('pointermove focus', (e) => {
                    const box = e.type === 'focus' ? node.node().getBoundingClientRect() : null;
                    tooltip(box ? { clientX: box.right, clientY: box.top } : e, card(r));
                }).on('pointerleave blur', () => tooltip(null));
            });
            if (basket) {
                const node = g.append('g').attr('transform', `translate(${x(basket.vol)},${y(basket.ann)})`).attr('tabindex', 0).attr('class', 'pr-node')
                    .attr('aria-label', card(basket).join('. '));
                node.append('path').attr('d', d3.symbol(d3.symbolDiamond, 260)()).attr('fill', BASKET).attr('stroke', '#fff').attr('stroke-width', 2);
                node.append('text').attr('class', 'pr-label').attr('x', 12).attr('dy', '0.35em').text('Your basket');
                node.on('pointermove', (e) => tooltip(e, card(basket))).on('pointerleave', () => tooltip(null));
            }
            hint.textContent = 'Bubble area is the fund\'s net assets. The dashed lines cross at your basket, splitting funds into four descriptive groups. Tab to a bubble for its card. Filter by role with the chips.';
        }

        if (tab) {
            tab.click();
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
            if (e.target.closest && e.target.closest('input, textarea, [contenteditable], svg, button, tr')) {
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
            const response = await fetch('/prototype/risk-return/risk-return.json');
            mount(await response.json());
        } catch (err) {
            document.body.append(el('p', 'pr-hint', `Prototype failed: ${err.message}`));
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
