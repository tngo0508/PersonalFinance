/* global d3, PFI18n, ChartPalette */
/**
 * PROTOTYPE (#587) - throwaway. Shared state, math, D3 chart primitives and stub saved scenarios for the three
 * Investment Calculator variants. Variants register on window.ProtoVariants. Nothing is persisted.
 */
(function () {
    'use strict';

    const DEFAULTS = {
        initial: 10000, monthly: 500, rate: 7, years: 20,
        increase: 0, inflation: 2.5, fee: 0.1,
        real: false, goal: 500000, goalMode: 'monthly', goalReal: false
    };

    const PRESETS = [
        { key: 'conservative', label: 'Conservative', rate: 4 },
        { key: 'base', label: 'Base', rate: 7 },
        { key: 'aggressive', label: 'Aggressive', rate: 10 }
    ];

    const LEVERS = [
        { key: 'more', label: '+$100/mo', apply: s => Object.assign({}, s, { monthly: s.monthly + 100 }) },
        { key: 'rate', label: '+1% return', apply: s => Object.assign({}, s, { rate: s.rate + 1 }) },
        { key: 'later', label: 'Start 5 years later', apply: s => Object.assign({}, s, { delay: 5 }) },
        { key: 'stop', label: 'Stop adding at year 10', apply: s => Object.assign({}, s, { stopAt: 10 }) },
        { key: 'fee', label: 'Fee +0.5%', apply: s => Object.assign({}, s, { fee: s.fee + 0.5 }) }
    ];

    const state = Object.assign({}, DEFAULTS);
    const listeners = [];

    // Stub saved scenarios: in memory only.
    const saved = [
        { id: 1, name: 'Retire at 60', note: 'Base case', inputs: Object.assign({}, DEFAULTS, { monthly: 800, years: 25 }), edited: '2 days ago' },
        { id: 2, name: 'House deposit', note: '', inputs: Object.assign({}, DEFAULTS, { initial: 5000, monthly: 1200, rate: 4, years: 5 }), edited: 'last week' },
        { id: 3, name: 'Aggressive growth', note: 'If I max it out', inputs: Object.assign({}, DEFAULTS, { monthly: 1500, rate: 10, years: 30 }), edited: 'Oct 12' }
    ];

    /**
     * Project a plan month by month.
     * @param {object} s - Inputs.
     * @returns {object} Yearly rows and totals.
     */
    function project (s) {
        const years = s.years;
        const r = (s.rate - s.fee) / 100 / 12;
        const r0 = s.rate / 100 / 12;
        const delay = s.delay || 0;
        let bal = delay ? 0 : s.initial;
        let balNoFee = bal;
        let contributed = bal;
        const rows = [{ year: 0, contributedYear: bal, growthYear: 0, contributed, balance: bal, real: bal, noFee: bal }];
        let milestone = null;
        for (let y = 1; y <= years; y++) {
            let cy = 0;
            const startBal = bal;
            for (let m = 0; m < 12; m++) {
                let c = 0;
                if (y > delay) {
                    if (y === delay + 1 && m === 0 && delay) {
                        c += s.initial;
                    }
                    if (!s.stopAt || y <= s.stopAt) {
                        c += s.monthly * Math.pow(1 + s.increase / 100, y - 1 - delay);
                    }
                }
                bal = bal * (1 + r) + c;
                balNoFee = balNoFee * (1 + r0) + c;
                cy += c;
            }
            contributed += cy;
            const growthYear = bal - startBal - cy;
            const real = bal / Math.pow(1 + s.inflation / 100, y);
            rows.push({ year: y, contributedYear: cy, growthYear, contributed, balance: bal, real, noFee: balNoFee });
            if (milestone === null && bal - contributed > contributed && contributed > 0) {
                milestone = y;
            }
        }
        const last = rows[rows.length - 1];
        return {
            rows,
            final: last.balance,
            real: last.real,
            contributed: last.contributed,
            growth: last.balance - last.contributed,
            feeCost: last.noFee - last.balance,
            milestone
        };
    }

    /**
     * Solve the goal.
     * @param {object} s - Inputs (goal, goalMode, goalReal).
     * @returns {object} Outcome.
     */
    function solveGoal (s) {
        const target = y => s.goalReal ? s.goal * Math.pow(1 + s.inflation / 100, y) : s.goal;
        if (s.goalMode === 'monthly') {
            const zero = project(Object.assign({}, s, { monthly: 0 }));
            if (zero.final >= target(s.years)) {
                const hit = zero.rows.find(row => row.balance >= target(row.year));
                return { kind: 'already', year: hit ? hit.year : s.years };
            }
            let lo = 0;
            let hi = Math.max(1000, s.goal);
            for (let i = 0; i < 60; i++) {
                const mid = (lo + hi) / 2;
                if (project(Object.assign({}, s, { monthly: mid })).final >= target(s.years)) {
                    hi = mid;
                } else {
                    lo = mid;
                }
            }
            return { kind: 'monthly', monthly: Math.ceil(hi), year: s.years };
        }
        const long = project(Object.assign({}, s, { years: 50 }));
        const hit = long.rows.find(row => row.year > 0 && row.balance >= target(row.year));
        if (!hit) {
            return { kind: 'unreachable', shortfall: target(50) - long.final };
        }
        return { kind: 'years', year: hit.year };
    }

    /**
     * Money formatting.
     * @param {number} v - Amount.
     * @returns {string} Whole-unit money.
     */
    function money (v) {
        return PFI18n.formatMoney(v, null, { whole: true });
    }

    /**
     * Compact money formatting.
     * @param {number} v - Amount.
     * @returns {string} Compact money.
     */
    function compact (v) {
        return PFI18n.formatCompactMoney(v);
    }

    /**
     * Update state and notify.
     * @param {object} patch - Fields to change.
     */
    function set (patch) {
        Object.assign(state, patch);
        listeners.forEach(fn => fn(state));
    }

    /**
     * Subscribe to state changes.
     * @param {Function} fn - Callback.
     */
    function onChange (fn) {
        listeners.push(fn);
    }

    /**
     * Colours from the shared palette.
     * @returns {object} Palette plus named series.
     */
    function colors () {
        const p = ChartPalette.read();
        return Object.assign(p, { value: p.series[0], contrib: p.series[1], growth: p.series[2], extra: p.series[3] });
    }

    let tip = null;

    /**
     * Shared hover tooltip element.
     * @returns {HTMLElement} The tooltip.
     */
    function tooltip () {
        if (!tip) {
            tip = document.createElement('div');
            tip.className = 'proto-tip';
            tip.hidden = true;
            document.body.appendChild(tip);
        }
        return tip;
    }

    /**
     * Multi-series line/area chart.
     * @param {HTMLElement} el - Container.
     * @param {object} opts - series [{name,color,values:[{x,y}],dash,area,width}], markers [{x,label,color}], height, yMax.
     */
    function lineChart (el, opts) {
        if (!el) {
            return;
        }
        el.innerHTML = '';
        const c = colors();
        const width = Math.max(260, el.clientWidth);
        const height = opts.height || Math.round(Math.min(380, Math.max(220, width * 0.5)));
        const m = { top: 16, right: 16, bottom: 28, left: 56 };
        const allX = opts.series.flatMap(s => s.values.map(v => v.x));
        const allY = opts.series.flatMap(s => s.values.map(v => v.y));
        const x = d3.scaleLinear().domain([0, d3.max(allX) || 1]).range([m.left, width - m.right]);
        const y = d3.scaleLinear().domain([0, opts.yMax || d3.max(allY) || 1]).nice().range([height - m.bottom, m.top]);
        const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${width} ${height}`).attr('width', width).attr('height', height)
            .attr('role', 'img').attr('aria-label', opts.label || 'Projection chart');
        svg.append('g').attr('transform', `translate(0,${height - m.bottom})`)
            .call(d3.axisBottom(x).ticks(Math.min(8, d3.max(allX) || 1)).tickFormat(v => v === 0 ? 'Now' : `Yr ${v}`))
            .call(g => g.selectAll('text').attr('fill', c.axis)).call(g => g.selectAll('line,path').attr('stroke', c.grid));
        svg.append('g').attr('transform', `translate(${m.left},0)`)
            .call(d3.axisLeft(y).ticks(5).tickSize(-(width - m.left - m.right)).tickFormat(compact))
            .call(g => g.selectAll('text').attr('fill', c.axis)).call(g => g.select('.domain').remove())
            .call(g => g.selectAll('line').attr('stroke', c.grid).attr('stroke-dasharray', '3 3'));
        opts.series.forEach(s => {
            if (s.area) {
                svg.append('path').datum(s.values).attr('fill', ChartPalette.withAlpha(s.color, 0.14))
                    .attr('d', d3.area().x(d => x(d.x)).y0(y(0)).y1(d => y(d.y)).curve(d3.curveMonotoneX));
            }
            svg.append('path').datum(s.values).attr('fill', 'none').attr('stroke', s.color).attr('stroke-width', s.width || 2.5)
                .attr('stroke-dasharray', s.dash || null).attr('opacity', s.opacity || 1)
                .attr('d', d3.line().x(d => x(d.x)).y(d => y(d.y)).curve(d3.curveMonotoneX));
        });
        (opts.markers || []).forEach(mk => {
            const g = svg.append('g');
            g.append('line').attr('x1', x(mk.x)).attr('x2', x(mk.x)).attr('y1', m.top).attr('y2', height - m.bottom)
                .attr('stroke', mk.color || c.axis).attr('stroke-dasharray', '4 4');
            const nearRight = x(mk.x) > width * 0.75;
            g.append('text').attr('x', x(mk.x) + (nearRight ? -4 : 4)).attr('text-anchor', nearRight ? 'end' : 'start').attr('y', m.top + 10).attr('fill', mk.color || c.axis)
                .attr('font-size', 11).attr('font-weight', 600).text(mk.label);
        });
        if (opts.goalLine) {
            svg.append('line').attr('x1', m.left).attr('x2', width - m.right).attr('y1', y(opts.goalLine)).attr('y2', y(opts.goalLine))
                .attr('stroke', c.extra).attr('stroke-width', 1.5).attr('stroke-dasharray', '6 3');
        }
        const guide = svg.append('line').attr('stroke', c.axis).attr('stroke-dasharray', '3 3').attr('y1', m.top).attr('y2', height - m.bottom).style('display', 'none');
        svg.append('rect').attr('x', m.left).attr('y', m.top).attr('width', width - m.left - m.right).attr('height', height - m.top - m.bottom)
            .attr('fill', 'transparent')
            .on('mousemove', (event) => {
                const [px] = d3.pointer(event);
                const yr = Math.round(x.invert(px));
                guide.style('display', null).attr('x1', x(yr)).attr('x2', x(yr));
                const t = tooltip();
                t.hidden = false;
                t.innerHTML = `<strong>${yr === 0 ? 'Today' : `Year ${yr}`}</strong>${ opts.series.map(s => {
                    const v = s.values.find(p => p.x === yr);
                    return v ? `<div><span class="proto-dot" style="background:${s.color}"></span>${s.name}: ${money(v.y)}</div>` : '';
                }).join('')}`;
                t.style.left = `${event.pageX + 14}px`;
                t.style.top = `${event.pageY - 10}px`;
            })
            .on('mouseleave', () => {
                guide.style('display', 'none');
                tooltip().hidden = true;
            });
    }

    /**
     * Donut chart.
     * @param {HTMLElement} el - Container.
     * @param {Array} parts - [{name, value, color}].
     * @param {string} centre - Centre text.
     */
    function donut (el, parts, centre) {
        if (!el) {
            return;
        }
        el.innerHTML = '';
        const size = Math.min(220, Math.max(140, el.clientWidth));
        const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${size} ${size}`).attr('width', size).attr('height', size)
            .attr('role', 'img').attr('aria-label', parts.map(p => `${p.name} ${money(p.value)}`).join(', '));
        const g = svg.append('g').attr('transform', `translate(${size / 2},${size / 2})`);
        const arc = d3.arc().innerRadius(size * 0.32).outerRadius(size * 0.48).cornerRadius(3).padAngle(0.02);
        g.selectAll('path').data(d3.pie().sort(null).value(d => Math.max(0, d.value))(parts)).join('path')
            .attr('d', arc).attr('fill', d => d.data.color);
        g.append('text').attr('text-anchor', 'middle').attr('dy', '0.35em').attr('font-weight', 700)
            .attr('fill', colors().axis).attr('font-size', 14).text(centre || '');
    }

    /**
     * Stacked bars of contributions and growth per year.
     * @param {HTMLElement} el - Container.
     * @param {Array} rows - Projection rows.
     * @param {number} [h] - Height.
     */
    function stackedBars (el, rows, h) {
        if (!el) {
            return;
        }
        el.innerHTML = '';
        const c = colors();
        const data = rows.filter(r => r.year > 0);
        const width = Math.max(260, el.clientWidth);
        const height = h || 220;
        const m = { top: 10, right: 10, bottom: 24, left: 52 };
        const x = d3.scaleBand().domain(data.map(d => d.year)).range([m.left, width - m.right]).padding(0.18);
        const max = d3.max(data, d => d.contributedYear + Math.max(0, d.growthYear)) || 1;
        const y = d3.scaleLinear().domain([0, max]).nice().range([height - m.bottom, m.top]);
        const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${width} ${height}`).attr('width', width).attr('height', height)
            .attr('role', 'img').attr('aria-label', 'Contributions and growth added each year');
        svg.append('g').attr('transform', `translate(0,${height - m.bottom})`)
            .call(d3.axisBottom(x).tickValues(x.domain().filter(v => v % Math.ceil(data.length / 8) === 0)))
            .call(g => g.selectAll('text').attr('fill', c.axis)).call(g => g.selectAll('line,path').attr('stroke', c.grid));
        svg.append('g').attr('transform', `translate(${m.left},0)`).call(d3.axisLeft(y).ticks(4).tickFormat(compact))
            .call(g => g.selectAll('text').attr('fill', c.axis)).call(g => g.select('.domain').remove()).call(g => g.selectAll('line').attr('stroke', c.grid));
        const bars = svg.append('g').selectAll('g').data(data).join('g');
        bars.append('rect').attr('x', d => x(d.year)).attr('width', x.bandwidth())
            .attr('y', d => y(d.contributedYear)).attr('height', d => y(0) - y(d.contributedYear)).attr('fill', c.contrib)
            .append('title').text(d => `Year ${d.year}: contributed ${money(d.contributedYear)}`);
        bars.append('rect').attr('x', d => x(d.year)).attr('width', x.bandwidth())
            .attr('y', d => y(d.contributedYear + Math.max(0, d.growthYear))).attr('height', d => y(0) - y(Math.max(0, d.growthYear)))
            .attr('fill', c.growth).append('title').text(d => `Year ${d.year}: growth ${money(d.growthYear)}`);
    }

    /**
     * Year-by-year table HTML.
     * @param {Array} rows - Projection rows.
     * @param {boolean} real - Today's dollars.
     * @returns {string} Table markup.
     */
    function yearTable (rows, real) {
        const body = rows.filter(r => r.year > 0).map(r => {
            const f = real ? Math.pow(1 + state.inflation / 100, r.year) : 1;
            return `<tr><td>${r.year}</td><td>${money(r.contributedYear / f)}</td><td>${money(r.growthYear / f)}</td><td><strong>${money(r.balance / f)}</strong></td><td>${money(r.real)}</td></tr>`;
        }).join('');
        return `<div class="proto-table-wrap"><table class="table table-sm proto-table"><thead><tr><th scope="col">Year</th><th scope="col">Added</th><th scope="col">Growth</th><th scope="col">Balance</th><th scope="col">In today's money</th></tr></thead><tbody>${body}</tbody></table></div>`;
    }

    /**
     * Series for the main chart from a projection.
     * @param {object} p - Projection.
     * @param {boolean} real - Today's dollars.
     * @returns {Array} Series.
     */
    function growthSeries (p, real) {
        const c = colors();
        const f = r => real ? Math.pow(1 + state.inflation / 100, r.year) : 1;
        const series = [
            { name: 'Balance', color: c.value, area: true, width: 3, values: p.rows.map(r => ({ x: r.year, y: r.balance / f(r) })) },
            { name: 'You put in', color: c.contrib, values: p.rows.map(r => ({ x: r.year, y: r.contributed / f(r) })) }
        ];
        if (!real) {
            series.push({ name: "In today's money", color: c.value, dash: '6 4', width: 2, opacity: 0.6, values: p.rows.map(r => ({ x: r.year, y: r.real })) });
        }
        return series;
    }

    /**
     * Build a labelled slider + number field pair.
     * @param {object} o - key, label, min, max, step, fmt.
     * @returns {string} Markup.
     */
    const FMT = {};

    function field (o) {
        const v = state[o.key];
        FMT[o.key] = o.fmt;
        return `<div class="proto-field"><div class="proto-field__head"><label for="pf-${o.key}">${o.label}</label><output id="pf-${o.key}-out">${o.fmt ? o.fmt(v) : v}</output></div>
            <input type="range" class="form-range" data-key="${o.key}" min="${o.min}" max="${o.max}" step="${o.step}" value="${v}" aria-label="${o.label}">
            <input type="number" class="form-control form-control-sm" id="pf-${o.key}" data-key="${o.key}" min="${o.min}" max="${o.max}" step="${o.step}" value="${v}"></div>`;
    }

    /**
     * Wire every [data-key] input inside root to state.
     * @param {HTMLElement} root - Container.
     */
    function bindInputs (root) {
        root.querySelectorAll('[data-key]').forEach(inp => {
            inp.addEventListener('input', () => {
                const key = inp.dataset.key;
                const val = inp.type === 'checkbox' ? inp.checked : (inp.type === 'radio' ? inp.value : parseFloat(inp.value) || 0);
                root.querySelectorAll(`[data-key="${key}"]`).forEach(o => {
                    if (o !== inp && o.type !== 'radio' && o.type !== 'checkbox') {
                        o.value = val;
                    }
                });
                const out = root.querySelector(`#pf-${key}-out`);
                if (out) {
                    out.textContent = FMT[key] ? FMT[key](val) : val;
                }
                set({ [key]: val });
                if (!document.body.contains(inp)) {
                    const again = document.querySelector(`[data-key="${key}"][type="${inp.type}"]`);
                    if (again && inp.type !== 'radio') {
                        again.focus();
                    }
                }
            });
        });
    }

    window.Proto = {
        DEFAULTS, PRESETS, LEVERS, state, saved, project, solveGoal, money, compact, set, onChange, colors,
        lineChart, donut, stackedBars, yearTable, growthSeries, field, bindInputs,
        reset: () => {
            listeners.length = 0;
        }
    };
    window.ProtoVariants = {};
})();
