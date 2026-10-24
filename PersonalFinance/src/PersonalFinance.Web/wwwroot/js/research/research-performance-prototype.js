/* global d3 */
/**
 * PROTOTYPE, throwaway (wayfinder ticket #239, "Performance tab: charts, overlays and annotations at monthly
 * resolution"). Never merge. Draws into the prototype-only Performance tab panel, switched by ?variant=A|B|C and the
 * floating bar. Data: /prototype/performance/performance.json (N-PORT B.5 monthly NAV returns for eight funds, BLS
 * CPI-U, Fed H.15 effective fed funds). Monthly resolution only; one y-axis per chart.
 *
 *   A  Lines + brush + rate panel: base-100 lines (funds + basket), a brush strip to pick the range, a fed funds panel
 *      below sharing the crosshair, an "Adjust for inflation" toggle, shaded hiking cycles and drawdown markers.
 *   B  Basket growth story: growth of $10,000 as a stacked area by fund, a difference chart against a chosen benchmark
 *      fund, and range presets (1Y, 3Y, 5Y, Max) instead of a brush.
 *   C  Small multiples: one base-100 panel per fund with the basket as a grey reference line, shared crosshair, and a
 *      summary table (annualized return, worst drawdown, return after inflation).
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    if (!page || typeof d3 === 'undefined') {
        return;
    }
    const VARIANTS = [
        { key: 'A', name: 'Lines + brush + rate panel' },
        { key: 'B', name: 'Basket growth + difference' },
        { key: 'C', name: 'Small multiples + table' }
    ];
    const SLOT = { VTI: '#2a78d6', VT: '#eb6834', VXUS: '#1baf7a', BND: '#eda100', QQQ: '#e87ba4', SCHD: '#008300', VGT: '#6250d6', IWM: '#e34948' };
    const BASKET = '#111827';
    const params = new URLSearchParams(window.location.search);
    const variant = (VARIANTS.find((v) => v.key === (params.get('variant') || 'A').toUpperCase()) || VARIANTS[0]).key;
    const parse = d3.utcParse('%Y-%m');
    const fmtMonth = d3.utcFormat('%b %Y');
    const pct = (v, d = 1) => `${v >= 0 ? '+' : ''}${v.toFixed(d)}%`;
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

    /** Common months (all basket funds and the benchmark have a return), oldest first. */
    function commonMonths (data, tickers) {
        const sets = tickers.map((t) => new Set(Object.keys(data.funds[t].returns)));
        return Object.keys(data.funds[tickers[0]].returns).filter((m) => sets.every((s) => s.has(m))).sort();
    }

    /** Base-100 index from monthly % returns over the given months; the value before the first month is 100. */
    function index (returns, months, deflate) {
        let v = 100;
        const out = [{ m: prevMonth(months[0]), v }];
        months.forEach((m) => {
            v *= 1 + returns[m] / 100;
            out.push({ m, v: deflate ? v / deflate(m) : v });
        });
        return out;
    }
    function prevMonth (m) {
        const d = parse(m);
        return d3.utcFormat('%Y-%m')(d3.utcMonth.offset(d, -1));
    }
    function basketReturns (data, basket, months) {
        const total = d3.sum(basket, (b) => b.w);
        return Object.fromEntries(months.map((m) => [m, d3.sum(basket, (b) => (b.w / total) * data.funds[b.t].returns[m])]));
    }
    function stats (series) {
        const first = series[0].v;
        const last = series[series.length - 1].v;
        const years = (series.length - 1) / 12;
        let peak = -Infinity;
        let worst = 0;
        let trough = null;
        series.forEach((p) => {
            peak = Math.max(peak, p.v);
            const dd = (p.v / peak - 1) * 100;
            if (dd < worst) {
                worst = dd;
                trough = p.m;
            }
        });
        return { total: (last / first - 1) * 100, cagr: (Math.pow(last / first, 1 / years) - 1) * 100, worst, trough };
    }
    function hikingCycles (ff) {
        const months = Object.keys(ff).sort();
        const out = [];
        let cur = null;
        months.forEach((m, i) => {
            const up = i > 0 && ff[m] - ff[months[i - 1]] >= 0.2;
            if (up && !cur) {
                cur = { start: months[i - 1], end: m };
            } else if (up) {
                cur.end = m;
            } else if (cur && i > 0 && ff[m] - ff[months[i - 1]] < 0.05 && months.indexOf(cur.end) < i - 2) {
                out.push(cur);
                cur = null;
            }
        });
        if (cur) {
            out.push(cur);
        }
        return out.map((c) => ({ ...c, kind: 'hike', label: `Fed hiking cycle: ${ff[c.start].toFixed(2)}% to ${ff[c.end].toFixed(2)}%` }));
    }

    // ---------- shared drawing ----------

    function frame (host, h, margin) {
        const w = host.clientWidth || 800;
        const svg = d3.select(host).append('svg').attr('viewBox', [0, 0, w, h]).attr('class', 'pp-svg');
        return { svg, w, h, inner: { x0: margin.l, x1: w - margin.r, y0: margin.t, y1: h - margin.b } };
    }
    function drawEvents (g, events, x, y0, y1) {
        g.selectAll('rect.pp-band').data(events).join('rect').attr('class', (d) => `pp-band pp-band--${d.kind}`)
            .attr('x', (d) => x(parse(d.start))).attr('width', (d) => Math.max(2, x(parse(d.end)) - x(parse(d.start))))
            .attr('y', y0).attr('height', y1 - y0)
            .append('title').text((d) => d.label);
        g.selectAll('text.pp-band-label').data(events).join('text').attr('class', 'pp-band-label')
            .attr('x', (d) => x(parse(d.start)) + 3).attr('y', y0 + 11).text((d) => (d.kind === 'hike' ? 'Rate hikes' : 'Recession'));
    }
    function legend (host, items) {
        const ul = el('ul', 'pp-legend');
        items.forEach((it) => {
            const li = el('li');
            const sw = el('span', `pp-swatch${it.dashed ? ' pp-swatch--dashed' : ''}`);
            sw.style.setProperty('--c', it.c);
            li.append(sw, it.label);
            ul.append(li);
        });
        host.append(ul);
    }

    /** A crosshair shared by several charts: each registers (date) => void; one moves them all. */
    function crosshairHub () {
        const subs = [];
        return { on: (fn) => subs.push(fn), move: (d) => subs.forEach((fn) => fn(d)) };
    }

    function lineChart (host, cfg) {
        const margin = { t: 16, r: 64, b: 24, l: 44 };
        const f = frame(host, cfg.h || 340, margin);
        const x = d3.scaleUtc().domain(cfg.domain).range([f.inner.x0, f.inner.x1]);
        const all = cfg.series.flatMap((s) => s.points.filter((p) => parse(p.m) >= cfg.domain[0] && parse(p.m) <= cfg.domain[1]));
        const y = d3.scaleLinear().domain(cfg.yDomain || d3.extent(all, (p) => p.v)).nice().range([f.inner.y1, f.inner.y0]);
        const g = f.svg.append('g');
        g.append('g').attr('class', 'pp-grid').attr('transform', `translate(${f.inner.x0},0)`)
            .call(d3.axisLeft(y).ticks(5).tickSize(-(f.inner.x1 - f.inner.x0)).tickFormat(cfg.yFormat || ((v) => v)));
        g.append('g').attr('class', 'pp-axis').attr('transform', `translate(0,${f.inner.y1})`).call(d3.axisBottom(x).ticks(6).tickSizeOuter(0));
        if (cfg.baseline !== undefined) {
            g.append('line').attr('class', 'pp-baseline').attr('x1', f.inner.x0).attr('x2', f.inner.x1).attr('y1', y(cfg.baseline)).attr('y2', y(cfg.baseline));
        }
        if (cfg.events) {
            drawEvents(g.append('g'), cfg.events, x, f.inner.y0, f.inner.y1);
        }
        const line = d3.line().x((p) => x(parse(p.m))).y((p) => y(p.v)).defined((p) => parse(p.m) >= cfg.domain[0] && parse(p.m) <= cfg.domain[1]);
        cfg.series.forEach((s) => {
            g.append('path').attr('class', 'pp-line').attr('fill', 'none').attr('stroke', s.c).attr('stroke-width', s.basket ? 3 : 2)
                .attr('stroke-dasharray', s.dashed ? '5 3' : null).attr('d', line(s.points));
            const lastP = s.points.filter((p) => parse(p.m) <= cfg.domain[1]).pop();
            if (cfg.endLabels && lastP) {
                g.append('text').attr('class', 'pp-end').attr('x', f.inner.x1 + 4).attr('y', y(lastP.v)).attr('dy', '0.35em').attr('fill', 'currentColor').text(s.label);
            }
        });
        if (cfg.markers) {
            g.selectAll('g.pp-dd').data(cfg.markers).join('g').attr('class', 'pp-dd')
                .attr('transform', (d) => `translate(${x(parse(d.m))},${y(d.v)})`)
                .call((s) => {
                    s.append('path').attr('d', d3.symbol(d3.symbolTriangle, 60)()).attr('transform', 'rotate(180)').attr('fill', '#e34948');
                    s.append('title').text((d) => d.label);
                });
        }
        // Crosshair
        const cross = g.append('g').attr('class', 'pp-cross').style('display', 'none');
        cross.append('line').attr('y1', f.inner.y0).attr('y2', f.inner.y1);
        const dots = cross.selectAll('circle').data(cfg.series).join('circle').attr('r', 4).attr('fill', (s) => s.c).attr('stroke', '#fff').attr('stroke-width', 1.5);
        const months = cfg.series[0].points.map((p) => p.m);
        const show = (date) => {
            if (!date) {
                cross.style('display', 'none');
                return;
            }
            const m = d3.utcFormat('%Y-%m')(date);
            if (date < cfg.domain[0] || date > cfg.domain[1] || !months.includes(m)) {
                cross.style('display', 'none');
                return;
            }
            cross.style('display', null).select('line').attr('x1', x(date)).attr('x2', x(date));
            dots.attr('cx', x(date)).attr('cy', (s) => {
                const p = s.points.find((q) => q.m === m);
                return p ? y(p.v) : -99;
            });
        };
        if (cfg.hub) {
            cfg.hub.on(show);
        }
        const overlay = f.svg.append('rect').attr('class', 'pp-overlay').attr('x', f.inner.x0).attr('y', f.inner.y0)
            .attr('width', f.inner.x1 - f.inner.x0).attr('height', f.inner.y1 - f.inner.y0).attr('fill', 'transparent')
            .attr('tabindex', 0).attr('aria-label', cfg.aria || 'Chart. Use the left and right arrow keys to move through months.');
        let keyIdx = months.length - 1;
        const emit = (date, e) => {
            if (cfg.hub) {
                cfg.hub.move(date);
            } else {
                show(date);
            }
            if (cfg.onMove) {
                cfg.onMove(date, e);
            }
        };
        overlay.on('pointermove', (e) => {
            const [px] = d3.pointer(e);
            emit(d3.utcMonth.round(x.invert(px)), e);
        }).on('pointerleave', () => emit(null)).on('keydown', (e) => {
            if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
                e.preventDefault();
                keyIdx = Math.max(0, Math.min(months.length - 1, keyIdx + (e.key === 'ArrowRight' ? 1 : -1)));
                const date = parse(months[keyIdx]);
                const box = overlay.node().getBoundingClientRect();
                emit(date, { clientX: box.left + (x(date) - f.inner.x0) * box.width / (f.inner.x1 - f.inner.x0), clientY: box.top + 20 });
            }
        }).on('blur', () => emit(null));
        return { x, y, svg: f.svg, inner: f.inner };
    }

    // ---------- mount ----------

    function mount (data) {
        const panel = document.getElementById('research-panel-performance');
        const tab = document.getElementById('research-tab-performance');
        if (!panel) {
            return;
        }
        const card = el('div', 'research-card pp-card');
        card.append(el('h2', 'research-card__title', 'How your funds have performed'),
            el('p', 'research-card__subtitle', 'Monthly total return of each fund\'s net asset value, as the funds report it to the SEC, from mid-2019. Past performance, for information only.'));
        const picker = el('div', 'pp-picker');
        const controls = el('div', 'pp-controls');
        const statsEl = el('div', 'pp-stats');
        const stage = el('div', 'pp-stage');
        const hint = el('p', 'pp-hint');
        card.append(picker, controls, statsEl, stage, hint, el('p', 'research-card__source', `Sources: ${data.source}`));
        panel.append(card);
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
            tip.style.left = `${Math.min(e.clientX + 16, window.innerWidth - 280)}px`;
            tip.style.top = `${e.clientY + 16}px`;
        };
        const state = { basket: [{ t: 'VTI', w: 60 }, { t: 'VXUS', w: 30 }, { t: 'BND', w: 10 }], compare: ['QQQ'], real: false, bench: 'VT', range: 'Max', brush: null };
        const hikes = hikingCycles(data.fedFunds);
        const events = data.events.concat(hikes);

        function renderPicker () {
            const inBasket = (t) => state.basket.find((b) => b.t === t);
            picker.replaceChildren(el('span', 'po-picker__label', 'Fund Basket (stub):'), ...Object.keys(data.funds).map((t) => {
                const item = inBasket(t);
                const b = el('button', `po-fund${item ? ' po-fund--on' : ''}`, item ? `${t} ${item.w}%` : t);
                b.type = 'button';
                b.style.setProperty('--c', SLOT[t]);
                b.setAttribute('aria-pressed', String(Boolean(item)));
                b.title = data.funds[t].name;
                b.disabled = !item && state.basket.length >= 5;
                b.addEventListener('click', () => {
                    const next = item ? state.basket.filter((x) => x.t !== t) : state.basket.concat({ t, w: 0 });
                    const each = Math.round(100 / Math.max(1, next.length));
                    state.basket = next.map((x) => ({ t: x.t, w: each }));
                    state.brush = null;
                    draw();
                });
                return b;
            }));
        }

        function seg (label, options, value, onPick) {
            const wrap = el('div', 'pp-seg');
            wrap.setAttribute('role', 'group');
            wrap.setAttribute('aria-label', label);
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

        function renderControls () {
            controls.replaceChildren();
            if (variant === 'A' || variant === 'C') {
                controls.append(seg('Values', [[false, 'Nominal'], [true, 'Adjusted for inflation']], state.real, (v) => { state.real = v; draw(); }));
            }
            if (variant === 'B') {
                controls.append(seg('Range', [['1Y', '1Y'], ['3Y', '3Y'], ['5Y', '5Y'], ['Max', 'Max']], state.range, (v) => { state.range = v; draw(); }));
                controls.append(seg('Compare with', Object.keys(data.funds).filter((t) => !state.basket.some((b) => b.t === t)).slice(0, 5).map((t) => [t, t]), state.bench, (v) => { state.bench = v; draw(); }));
            }
        }

        function statCards (items) {
            statsEl.replaceChildren(...items.map((c) => {
                const d = el('div', 'po-stat');
                d.append(el('span', 'po-stat__label', c.label), el('span', 'po-stat__value', c.value), el('span', 'po-stat__note', c.note));
                return d;
            }));
        }

        function draw () {
            renderPicker();
            renderControls();
            stage.replaceChildren();
            if (!state.basket.length) {
                statsEl.replaceChildren();
                stage.append(el('p', 'pp-hint', 'Add a fund to your basket.'));
                return;
            }
            const tickers = Array.from(new Set(state.basket.map((b) => b.t).concat(state.bench, state.compare)));
            const months = commonMonths(data, tickers);
            const cpiBase = data.cpi[prevMonth(months[0])] || data.cpi[months[0]];
            const deflate = state.real ? (m) => (data.cpi[m] || data.cpi[Object.keys(data.cpi).sort().pop()]) / cpiBase : null;
            const bRet = basketReturns(data, state.basket, months);
            const basketLabel = `Your basket (${state.basket.map((b) => `${b.t} ${b.w}%`).join(', ')})`;
            const basketSeries = { label: 'Your basket', c: BASKET, basket: true, points: index(bRet, months, deflate) };
            const bs = stats(basketSeries.points);
            const firstDate = parse(basketSeries.points[0].m);
            const lastDate = parse(months[months.length - 1]);
            const cpiChange = ((data.cpi[months[months.length - 1]] || cpiBase) / cpiBase - 1) * 100;

            if (variant === 'A') {
                const domain = state.brush || [firstDate, lastDate];
                const fundSeries = state.basket.map((b) => ({ label: b.t, c: SLOT[b.t], points: index(data.funds[b.t].returns, months, deflate) }));
                const series = [basketSeries, ...fundSeries];
                const inRange = basketSeries.points.filter((p) => parse(p.m) >= domain[0] && parse(p.m) <= domain[1]);
                const rs = stats(inRange);
                statCards([
                    { label: `Basket, ${fmtMonth(domain[0])} – ${fmtMonth(domain[1])}`, value: pct(rs.total, 0), note: `${pct(rs.cagr)} a year${state.real ? ', after inflation' : ''}` },
                    { label: 'Worst fall from a high', value: `${rs.worst.toFixed(1)}%`, note: rs.trough ? `bottomed in ${fmtMonth(parse(rs.trough))}` : '' },
                    { label: 'Inflation over the period', value: pct(cpiChange, 0), note: 'CPI-U, BLS' }
                ]);
                const hub = crosshairHub();
                legend(stage, series.map((s) => ({ label: s.basket ? basketLabel : `${s.label} · ${data.funds[s.label].name}`, c: s.c })));
                const main = el('div', 'pp-main');
                stage.append(main);
                const ddMarkers = [{ m: bs.trough, v: basketSeries.points.find((p) => p.m === bs.trough).v, label: `Basket's worst fall: ${bs.worst.toFixed(1)}% (to ${fmtMonth(parse(bs.trough))})` }];
                lineChart(main, {
                    series, domain, events, baseline: 100, endLabels: true, hub, markers: ddMarkers,
                    aria: `Growth of 100, ${state.real ? 'after inflation' : 'nominal'}. Use the arrow keys to move through months.`,
                    onMove: (date, e) => {
                        if (!date) {
                            tooltip(null);
                            return;
                        }
                        const m = d3.utcFormat('%Y-%m')(date);
                        tooltip(e, [fmtMonth(date), ...series.map((s) => {
                            const p = s.points.find((q) => q.m === m);
                            return p ? `${s.label}: ${p.v.toFixed(1)}` : null;
                        }).filter(Boolean), `Fed funds: ${(data.fedFunds[m] || 0).toFixed(2)}%`]);
                    }
                });
                const rate = el('div', 'pp-rate');
                rate.append(el('h3', 'pp-h', 'Effective fed funds rate (%)'));
                stage.append(rate);
                const ffPoints = months.map((m) => ({ m, v: data.fedFunds[m] })).filter((p) => p.v !== undefined);
                lineChart(rate, { series: [{ label: 'Fed funds', c: '#6b7280', points: ffPoints }], domain, events: hikes, h: 130, hub, yFormat: (v) => `${v}%` });
                // Brush strip
                const ctx = el('div', 'pp-context');
                ctx.append(el('h3', 'pp-h', 'Drag to choose a range'));
                stage.append(ctx);
                const c = lineChart(ctx, { series: [basketSeries], domain: [firstDate, lastDate], h: 80 });
                const brush = d3.brushX().extent([[c.inner.x0, c.inner.y0], [c.inner.x1, c.inner.y1]]).on('end', (e) => {
                    if (!e.sourceEvent) {
                        return;
                    }
                    state.brush = e.selection ? e.selection.map(c.x.invert).map(d3.utcMonth.round) : null;
                    draw();
                });
                const bg = c.svg.append('g').attr('class', 'pp-brush').call(brush);
                if (state.brush) {
                    bg.call(brush.move, state.brush.map(c.x));
                }
                hint.textContent = 'Hover or Tab into a chart and use ←/→ to move the crosshair; the rate panel follows. Drag across the bottom strip to zoom both charts to a range; click the strip to reset. Grey bands: recession; amber bands: Fed hiking cycles. The red marker is the basket\'s worst fall.';
            } else if (variant === 'B') {
                const n = { '1Y': 12, '3Y': 36, '5Y': 60, Max: months.length }[state.range];
                const sub = months.slice(-Math.min(n, months.length));
                const total = d3.sum(state.basket, (b) => b.w);
                // Growth of $10,000, split by fund, rebalanced monthly to the weights.
                let value = 10000;
                const rows = [{ m: prevMonth(sub[0]), ...Object.fromEntries(state.basket.map((b) => [b.t, value * b.w / total])) }];
                sub.forEach((m) => {
                    const parts = Object.fromEntries(state.basket.map((b) => [b.t, value * b.w / total * (1 + data.funds[b.t].returns[m] / 100)]));
                    value = d3.sum(Object.values(parts));
                    rows.push({ m, ...parts });
                });
                const benchIdx = index(data.funds[state.bench].returns, sub);
                const basketIdx = index(bRet, sub);
                const gap = basketIdx.map((p, i) => ({ m: p.m, v: p.v - benchIdx[i].v }));
                const bsub = stats(basketIdx);
                const bench = stats(benchIdx);
                statCards([
                    { label: `$10,000 became (${state.range})`, value: `$${Math.round(value).toLocaleString('en-US')}`, note: `${pct(bsub.total, 0)}, ${pct(bsub.cagr)} a year` },
                    { label: `${state.bench} over the same months`, value: pct(bench.total, 0), note: data.funds[state.bench].name },
                    { label: 'Difference at the end', value: `${gap[gap.length - 1].v >= 0 ? '+' : ''}${gap[gap.length - 1].v.toFixed(1)} pts`, note: `basket minus ${state.bench}, on a base of 100` }
                ]);
                const margin = { t: 16, r: 20, b: 24, l: 64 };
                const f = frame(stage, 300, margin);
                const keys = state.basket.map((b) => b.t);
                const stack = d3.stack().keys(keys)(rows);
                const x = d3.scaleUtc().domain(d3.extent(rows, (r) => parse(r.m))).range([f.inner.x0, f.inner.x1]);
                const y = d3.scaleLinear().domain([0, d3.max(stack[stack.length - 1], (d) => d[1])]).nice().range([f.inner.y1, f.inner.y0]);
                f.svg.append('g').attr('class', 'pp-grid').attr('transform', `translate(${f.inner.x0},0)`).call(d3.axisLeft(y).ticks(5).tickSize(-(f.inner.x1 - f.inner.x0)).tickFormat((v) => `$${d3.format(',')(v)}`));
                f.svg.append('g').attr('class', 'pp-axis').attr('transform', `translate(0,${f.inner.y1})`).call(d3.axisBottom(x).ticks(6).tickSizeOuter(0));
                const area = d3.area().x((d) => x(parse(d.data.m))).y0((d) => y(d[0])).y1((d) => y(d[1]));
                f.svg.append('g').selectAll('path').data(stack).join('path').attr('d', area).attr('fill', (d) => SLOT[d.key]).attr('fill-opacity', 0.85)
                    .attr('stroke', 'var(--bs-body-bg, #fff)').attr('stroke-width', 1).append('title').text((d) => d.key);
                legend(stage, keys.map((k) => ({ label: `${k} · ${data.funds[k].name}`, c: SLOT[k] })));
                stage.append(el('h3', 'pp-h', `Basket minus ${state.bench}, in points of a base of 100`));
                const g2 = frame(stage, 160, { t: 10, r: 20, b: 24, l: 64 });
                const x2 = d3.scaleUtc().domain(d3.extent(gap, (p) => parse(p.m))).range([g2.inner.x0, g2.inner.x1]);
                const ext = d3.max(gap, (p) => Math.abs(p.v)) || 1;
                const y2 = d3.scaleLinear().domain([-ext, ext]).nice().range([g2.inner.y1, g2.inner.y0]);
                g2.svg.append('g').attr('class', 'pp-grid').attr('transform', `translate(${g2.inner.x0},0)`).call(d3.axisLeft(y2).ticks(4).tickSize(-(g2.inner.x1 - g2.inner.x0)));
                g2.svg.append('g').attr('class', 'pp-axis').attr('transform', `translate(0,${g2.inner.y1})`).call(d3.axisBottom(x2).ticks(6).tickSizeOuter(0));
                const above = d3.area().x((p) => x2(parse(p.m))).y0(y2(0)).y1((p) => y2(Math.max(0, p.v)));
                const below = d3.area().x((p) => x2(parse(p.m))).y0(y2(0)).y1((p) => y2(Math.min(0, p.v)));
                g2.svg.append('path').attr('d', above(gap)).attr('fill', '#2a78d6').attr('fill-opacity', 0.75);
                g2.svg.append('path').attr('d', below(gap)).attr('fill', '#eb6834').attr('fill-opacity', 0.75);
                legend(stage, [{ label: `Basket ahead of ${state.bench}`, c: '#2a78d6' }, { label: `Basket behind ${state.bench}`, c: '#eb6834' }]);
                hint.textContent = 'Range presets replace a brush. The difference chart uses blue above zero and orange below, never red and green. No crosshair in this variant.';
            } else {
                const fundStats = state.basket.map((b) => {
                    const s = index(data.funds[b.t].returns, months, deflate);
                    return { t: b.t, s, st: stats(s) };
                });
                statCards([
                    { label: 'Basket, full period', value: pct(bs.total, 0), note: `${pct(bs.cagr)} a year${state.real ? ', after inflation' : ''}` },
                    { label: 'Highest annualized return', value: [...fundStats].sort((a, b) => b.st.cagr - a.st.cagr)[0].t, note: 'among the funds in your basket, over this period' },
                    { label: 'Inflation over the period', value: pct(cpiChange, 0), note: 'CPI-U, BLS' }
                ]);
                const grid = el('div', 'pp-multiples');
                stage.append(grid);
                const hub = crosshairHub();
                const yAll = d3.extent(fundStats.flatMap((f) => f.s).concat(basketSeries.points), (p) => p.v);
                const cells = fundStats.map((fs) => {
                    const cell = el('div', 'pp-mini');
                    cell.append(el('h3', 'pp-h', `${fs.t} · ${pct(fs.st.cagr)} a year`));
                    grid.append(cell);
                    return cell;
                });
                fundStats.forEach((fs, i) => {
                    const cell = cells[i];
                    lineChart(cell, {
                        series: [{ label: 'Basket', c: '#9ca3af', points: basketSeries.points, dashed: true }, { label: fs.t, c: SLOT[fs.t], points: fs.s }],
                        domain: [firstDate, lastDate], h: 170, hub, baseline: 100, events: data.events, yDomain: yAll,
                        onMove: (date, e) => {
                            if (!date) {
                                tooltip(null);
                                return;
                            }
                            const m = d3.utcFormat('%Y-%m')(date);
                            const p = fs.s.find((q) => q.m === m);
                            const bp = basketSeries.points.find((q) => q.m === m);
                            tooltip(e, p && bp ? [fmtMonth(date), `${fs.t}: ${p.v.toFixed(1)}`, `Basket: ${bp.v.toFixed(1)}`] : null);
                        }
                    });
                });
                const table = el('table', 'po-table');
                const head = el('thead');
                const hr = el('tr');
                ['Fund', 'Annualized return', 'Total return', 'Worst fall from a high', state.real ? '(after inflation)' : 'Return after inflation'].forEach((h, i) => hr.append(el('th', i ? 'num' : null, h)));
                head.append(hr);
                const tb = el('tbody');
                const real = (s) => ((s[s.length - 1].v / s[0].v) / (1 + cpiChange / 100) - 1) * 100;
                [{ t: 'Your basket', s: basketSeries.points, st: bs }, ...fundStats].forEach((r) => {
                    const tr = el('tr');
                    tr.append(el('td', null, r.t), el('td', 'num', pct(r.st.cagr)), el('td', 'num', pct(r.st.total, 0)), el('td', 'num', `${r.st.worst.toFixed(1)}%`), el('td', 'num', state.real ? '—' : pct(real(r.s), 0)));
                    tb.append(tr);
                });
                table.append(el('caption', null, `Mid-2019 to ${fmtMonth(lastDate)}`), head, tb);
                stage.append(table);
                hint.textContent = 'Each panel is one fund (colour) against your basket (grey dashes), on the same scale. The crosshair is shared across panels; Tab into a panel and use ←/→.';
            }
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
            const response = await fetch('/prototype/performance/performance.json');
            mount(await response.json());
        } catch (err) {
            document.body.append(el('p', 'pp-hint', `Prototype failed: ${err.message} | ${String(err.stack || '').slice(0, 300)}`));
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
