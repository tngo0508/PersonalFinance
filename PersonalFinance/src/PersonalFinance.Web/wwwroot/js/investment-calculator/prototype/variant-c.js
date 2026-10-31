/* global Proto */
/**
 * PROTOTYPE (#587) - Variant C "Cockpit": a sticky control bar across the top, a persistent scenarios sidebar with
 * compare checkboxes, and every chart visible at once in a dense grid. What-ifs are toggles that overlay all charts.
 */
(function () {
    'use strict';

    const P = Proto;
    let lever = null;
    const checked = new Set();

    /**
     * Compact number control.
     * @param {string} key - State key.
     * @param {string} label - Label.
     * @param {number} step - Step.
     * @param {string} [unit] - Unit hint.
     * @returns {string} Markup.
     */
    function ctl (key, label, step, unit) {
        return `<label class="pc-ctl"><span>${label}${unit ? ` <em>${unit}</em>` : ''}</span><input type="number" class="form-control form-control-sm" data-key="${key}" step="${step}" value="${P.state[key]}"></label>`;
    }

    /**
     * Render the variant.
     * @param {HTMLElement} root - Container.
     */
    function render (root) {
        const s = P.state;
        root.innerHTML = `
        <div class="pc-bar" role="group" aria-label="Plan inputs">
            <strong class="pc-brand">Investment Calculator</strong>
            ${ctl('initial', 'Start', 1000, '$')}${ctl('monthly', 'Monthly', 50, '$')}${ctl('rate', 'Return', 0.5, '%')}${ctl('years', 'Years', 1)}
            <span class="pc-sep"></span>
            ${ctl('increase', 'Raise/yr', 0.5, '%')}${ctl('fee', 'Fee', 0.05, '%')}${ctl('inflation', 'Inflation', 0.1, '%')}
            <span class="pc-sep"></span>
            <div class="btn-group btn-group-sm" role="group" aria-label="Rate presets">${P.PRESETS.map(p => `<button type="button" class="btn ${s.rate === p.rate ? 'btn-primary' : 'btn-outline-secondary'}" data-preset="${p.rate}">${p.label.slice(0, 4)}</button>`).join('')}</div>
            <div class="form-check form-switch m-0 ms-auto"><input class="form-check-input" type="checkbox" role="switch" id="pcReal" data-key="real" ${s.real ? 'checked' : ''}><label class="form-check-label small" for="pcReal">Today's $</label></div>
        </div>
        <div class="pc-layout">
            <aside class="pc-side" aria-label="Scenarios">
                <h2 class="pc-h">Scenarios</h2>
                <div class="pc-scn is-current"><strong>Current plan</strong><span id="pcCurFinal"></span></div>
                ${P.saved.map(sc => `<label class="pc-scn"><input type="checkbox" class="form-check-input" data-chk="${sc.id}" ${checked.has(sc.id) ? 'checked' : ''} ${!checked.has(sc.id) && checked.size >= 2 ? 'disabled' : ''}>
                    <span><strong>${sc.name}</strong><span class="pc-muted">${P.compact(P.project(sc.inputs).final)} · ${sc.edited}</span></span>
                    <button type="button" class="btn btn-sm btn-link p-0 ms-auto" data-load="${sc.id}">Load</button></label>`).join('')}
                <button type="button" class="btn btn-sm btn-outline-primary w-100 mt-2" id="pcSave">+ Save current</button>
                <h2 class="pc-h mt-4">What if</h2>
                ${P.LEVERS.map(l => `<label class="pc-lever"><input type="radio" name="pcLever" class="form-check-input" value="${l.key}" ${lever === l.key ? 'checked' : ''}> ${l.label}</label>`).join('')}
                <label class="pc-lever"><input type="radio" name="pcLever" class="form-check-input" value="" ${!lever ? 'checked' : ''}> None</label>
                <button type="button" class="btn btn-sm btn-primary w-100 mt-2" id="pcApply" ${lever ? '' : 'disabled'}>Apply to plan</button>
                <p class="pc-muted small mt-3">Educational estimate, not financial advice. <a href="/Legal/Disclaimer">Disclaimer</a></p>
            </aside>
            <main class="pc-grid" id="pcGrid" aria-live="polite"></main>
        </div>`;
        P.bindInputs(root.querySelector('.pc-bar'));
        root.querySelectorAll('[data-preset]').forEach(b => b.addEventListener('click', () => {
            P.set({ rate: parseFloat(b.dataset.preset) });
            render(root);
        }));
        root.querySelectorAll('[data-chk]').forEach(b => b.addEventListener('change', () => {
            const id = Number(b.dataset.chk);
            if (b.checked) {
                checked.add(id);
            } else {
                checked.delete(id);
            }
            render(root);
        }));
        root.querySelectorAll('[data-load]').forEach(b => b.addEventListener('click', (e) => {
            e.preventDefault();
            P.set(Object.assign({}, P.saved.find(x => x.id === Number(b.dataset.load)).inputs));
            render(root);
        }));
        root.querySelectorAll('input[name="pcLever"]').forEach(r => r.addEventListener('change', () => {
            lever = r.value || null;
            render(root);
        }));
        root.querySelector('#pcApply').addEventListener('click', () => {
            P.set(P.LEVERS.find(l => l.key === lever).apply(P.state));
            lever = null;
            render(root);
        });
        root.querySelector('#pcSave').addEventListener('click', () => {
            const name = window.prompt('Name this scenario', 'My plan');
            if (name) {
                P.saved.unshift({ id: Date.now(), name, inputs: Object.assign({}, P.state), edited: 'just now' });
                render(root);
            }
        });
        P.reset();
        P.onChange(() => grid(root));
        grid(root);
    }

    /**
     * Render the chart grid.
     * @param {HTMLElement} root - Container.
     */
    function grid (root) {
        const s = P.state;
        const c = P.colors();
        const p = P.project(s);
        const f = s.real ? Math.pow(1 + s.inflation / 100, s.years) : 1;
        const lv = P.LEVERS.find(l => l.key === lever);
        const ghost = lv ? P.project(lv.apply(s)) : null;
        const g = P.solveGoal(s);
        root.querySelector('#pcCurFinal').textContent = P.compact(p.final);
        const el = root.querySelector('#pcGrid');
        el.innerHTML = `
        <div class="pc-kpis">
            <div class="pc-kpi"><span>Final balance</span><strong>${P.money(p.final / f)}</strong>${ghost ? `<em class="${ghost.final >= p.final ? 'text-success' : 'text-danger'}">${ghost.final >= p.final ? '+' : '−'}${P.compact(Math.abs(ghost.final - p.final) / f)} with "${lv.label}"</em>` : ''}</div>
            <div class="pc-kpi"><span>Put in</span><strong>${P.money(p.contributed / f)}</strong></div>
            <div class="pc-kpi"><span>Growth</span><strong class="text-success">${P.money(p.growth / f)}</strong></div>
            <div class="pc-kpi"><span>In today's money</span><strong>${P.money(p.real)}</strong></div>
            <div class="pc-kpi"><span>Fees cost</span><strong class="text-danger">${P.money(p.feeCost / f)}</strong></div>
            <div class="pc-kpi"><span>Growth beats deposits</span><strong>${p.milestone ? `Year ${p.milestone}` : '—'}</strong></div>
        </div>
        <div class="pc-tile pc-tile--wide"><h3>Balance over time</h3><div id="pcMain"></div></div>
        <div class="pc-tile"><h3>Mix of final balance</h3><div id="pcDonut" class="d-flex justify-content-center"></div></div>
        <div class="pc-tile"><h3>Fee drag</h3><div id="pcFee"></div></div>
        <div class="pc-tile"><h3>Added per year</h3><div id="pcBars"></div></div>
        <div class="pc-tile"><h3>Goal <input type="number" class="form-control form-control-sm d-inline-block pc-goal-in" data-key="goal" step="10000" value="${s.goal}" aria-label="Goal amount"></h3>
            <div class="btn-group btn-group-sm mb-2"><button type="button" class="btn ${s.goalMode === 'monthly' ? 'btn-primary' : 'btn-outline-secondary'}" data-gm="monthly">Per month</button><button type="button" class="btn ${s.goalMode === 'years' ? 'btn-primary' : 'btn-outline-secondary'}" data-gm="years">How long</button></div>
            <div class="pc-goal-answer">${{ monthly: `${P.money(g.monthly)}<small>/mo for ${s.years} yrs</small>`, years: `${g.year}<small> years at ${P.money(s.monthly)}/mo</small>`, already: 'Already there', unreachable: `Not in 50 yrs<small> (${P.compact(g.shortfall)} short)</small>` }[g.kind]}</div>
            <div class="pa-progress"><div style="width:${Math.min(100, p.final / s.goal * 100)}%"></div></div><span class="pc-muted small">Current plan: ${Math.round(p.final / s.goal * 100)}% of goal</span></div>
        <div class="pc-tile pc-tile--wide"><h3>Compare ${checked.size ? `(${checked.size + 1} plans)` : '<span class="pc-muted small">tick up to 2 scenarios on the left</span>'}</h3><div id="pcCmp"></div></div>
        <div class="pc-tile pc-tile--full"><h3>Year by year</h3>${P.yearTable(p.rows, s.real)}</div>`;

        const series = P.growthSeries(p, s.real);
        if (ghost) {
            series.push({ name: lv.label, color: c.extra, dash: '6 4', values: ghost.rows.map(r => ({ x: r.year, y: r.balance / (s.real ? Math.pow(1 + s.inflation / 100, r.year) : 1) })) });
        }
        P.lineChart(el.querySelector('#pcMain'), { series, goalLine: s.real ? null : s.goal, markers: p.milestone ? [{ x: p.milestone, label: 'Growth > deposits', color: c.growth }] : [] });
        P.donut(el.querySelector('#pcDonut'), [{ name: 'Put in', value: p.contributed, color: c.contrib }, { name: 'Growth', value: p.growth, color: c.growth }], `${Math.round(p.growth / p.final * 100)}% growth`);
        P.lineChart(el.querySelector('#pcFee'), { height: 180, series: [
            { name: 'Without fees', color: c.growth, values: p.rows.map(r => ({ x: r.year, y: r.noFee })) },
            { name: 'With fees', color: c.value, area: true, values: p.rows.map(r => ({ x: r.year, y: r.balance })) }
        ] });
        P.stackedBars(el.querySelector('#pcBars'), p.rows, 180);
        const cmpColors = [c.value, c.extra, c.growth];
        const plans = [{ name: 'Current plan', inputs: s }].concat(P.saved.filter(sc => checked.has(sc.id)));
        P.lineChart(el.querySelector('#pcCmp'), { height: 240, series: plans.map((pl, i) => ({ name: pl.name, color: cmpColors[i], values: P.project(pl.inputs).rows.map(r => ({ x: r.year, y: r.balance })) })) });
        P.bindInputs(el);
        el.querySelectorAll('[data-gm]').forEach(b => b.addEventListener('click', () => P.set({ goalMode: b.dataset.gm })));
    }

    window.ProtoVariants.C = { name: 'Cockpit (everything at once)', render };
})();
