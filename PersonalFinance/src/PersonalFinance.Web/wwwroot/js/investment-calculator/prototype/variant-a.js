/* global Proto */
/**
 * PROTOTYPE (#587) - Variant A "Studio": mode tabs on top, inputs rail on the left, results on the right, saved
 * scenarios in a slide-out drawer. The layout the spec describes.
 */
(function () {
    'use strict';

    const P = Proto;
    let tab = 'grow';
    let preview = null;
    let compare = [];

    /**
     * Render the variant.
     * @param {HTMLElement} root - Container.
     */
    function render (root) {
        const s = P.state;
        root.innerHTML = `
        <header class="pa-header">
            <div><h1 class="pa-title">Investment Calculator</h1><p class="pa-sub">See how your money could grow, what it takes to reach a goal, and what changes the outcome.</p></div>
            <button type="button" class="btn btn-outline-secondary pa-saved-btn" id="paSavedBtn">Saved scenarios <span class="badge text-bg-secondary">${P.saved.length}</span></button>
        </header>
        <nav class="pa-tabs" role="tablist" aria-label="Calculator mode">
            ${[['grow', 'Grow'], ['goal', 'Reach a goal'], ['compare', 'Compare what-ifs']].map(([k, l]) =>
        `<button type="button" role="tab" class="pa-tab${tab === k ? ' is-active' : ''}" aria-selected="${tab === k}" data-tab="${k}">${l}</button>`).join('')}
        </nav>
        <div class="pa-grid">
            <aside class="pa-rail" aria-label="Your plan">
                <h2 class="pa-h2">Your plan</h2>
                ${tab === 'goal' ? `<div class="pa-goalbox">${P.field({ key: 'goal', label: 'Target amount', min: 10000, max: 3000000, step: 10000, fmt: P.money })}
                    <div class="btn-group w-100 mb-2" role="group" aria-label="Solve for">
                        <input type="radio" class="btn-check" name="paGoalMode" id="paGm1" value="monthly" data-key="goalMode" ${s.goalMode === 'monthly' ? 'checked' : ''}><label class="btn btn-sm btn-outline-primary" for="paGm1">How much per month?</label>
                        <input type="radio" class="btn-check" name="paGoalMode" id="paGm2" value="years" data-key="goalMode" ${s.goalMode === 'years' ? 'checked' : ''}><label class="btn btn-sm btn-outline-primary" for="paGm2">How long?</label>
                    </div></div>` : ''}
                ${P.field({ key: 'initial', label: 'Starting amount', min: 0, max: 500000, step: 1000, fmt: P.money })}
                ${tab === 'goal' && s.goalMode === 'monthly' ? '' : P.field({ key: 'monthly', label: 'Monthly contribution', min: 0, max: 10000, step: 50, fmt: P.money })}
                <div class="pa-presets" role="group" aria-label="Rate presets">${P.PRESETS.map(p => `<button type="button" class="btn btn-sm ${s.rate === p.rate ? 'btn-primary' : 'btn-outline-secondary'}" data-preset="${p.rate}">${p.label} ${p.rate}%</button>`).join('')}</div>
                ${P.field({ key: 'rate', label: 'Assumed yearly return', min: 0, max: 15, step: 0.5, fmt: v => `${v}%` })}
                ${tab === 'goal' && s.goalMode === 'years' ? '' : P.field({ key: 'years', label: 'Years', min: 1, max: 50, step: 1, fmt: v => `${v} yrs` })}
                <details class="pa-adv"><summary>More assumptions</summary>
                    ${P.field({ key: 'increase', label: 'Raise contributions each year', min: 0, max: 10, step: 0.5, fmt: v => `${v}%` })}
                    ${P.field({ key: 'fee', label: 'Yearly fund fee', min: 0, max: 3, step: 0.05, fmt: v => `${v}%` })}
                    ${P.field({ key: 'inflation', label: 'Inflation', min: 0, max: 10, step: 0.1, fmt: v => `${v}%` })}
                </details>
                <button type="button" class="btn btn-primary w-100 mt-3" id="paSave">Save scenario</button>
                <p class="pa-disclaimer">Educational estimate using a fixed assumed return, not financial advice. <a href="/Legal/Disclaimer">Disclaimer</a></p>
            </aside>
            <section class="pa-main" id="paMain" aria-live="polite"></section>
        </div>
        <div class="pa-drawer" id="paDrawer" hidden>
            <div class="pa-drawer__panel" role="dialog" aria-label="Saved scenarios">
                <div class="d-flex justify-content-between align-items-center mb-3"><h2 class="pa-h2 m-0">Saved scenarios</h2><button type="button" class="btn-close" id="paDrawerClose" aria-label="Close"></button></div>
                ${P.saved.map(sc => `<div class="pa-scn"><div><strong>${sc.name}</strong><div class="pa-muted">${P.money(P.project(sc.inputs).final)} · edited ${sc.edited}</div></div>
                    <div class="btn-group btn-group-sm"><button type="button" class="btn btn-outline-primary" data-load="${sc.id}">Load</button><button type="button" class="btn btn-outline-secondary" data-cmp="${sc.id}">Compare</button><button type="button" class="btn btn-outline-secondary" aria-label="More">…</button></div></div>`).join('')}
                <p class="pa-muted small mt-3">Up to 20 scenarios. Stub data: nothing is saved in this prototype.</p>
            </div>
        </div>`;

        P.bindInputs(root);
        root.querySelectorAll('[data-tab]').forEach(b => b.addEventListener('click', () => {
            tab = b.dataset.tab;
            render(root);
        }));
        root.querySelectorAll('[data-preset]').forEach(b => b.addEventListener('click', () => {
            P.set({ rate: parseFloat(b.dataset.preset) });
            render(root);
        }));
        root.querySelectorAll('input[name="paGoalMode"]').forEach(r => r.addEventListener('change', () => render(root)));
        const drawer = root.querySelector('#paDrawer');
        root.querySelector('#paSavedBtn').addEventListener('click', () => {
            drawer.hidden = false;
        });
        root.querySelector('#paDrawerClose').addEventListener('click', () => {
            drawer.hidden = true;
        });
        root.querySelector('#paSave').addEventListener('click', () => {
            const name = window.prompt('Name this scenario', 'My plan');
            if (name) {
                P.saved.unshift({ id: Date.now(), name, inputs: Object.assign({}, P.state), edited: 'just now' });
                render(root);
            }
        });
        root.querySelectorAll('[data-load]').forEach(b => b.addEventListener('click', () => {
            const sc = P.saved.find(x => x.id === Number(b.dataset.load));
            P.set(Object.assign({}, sc.inputs));
            tab = 'grow';
            render(root);
        }));
        root.querySelectorAll('[data-cmp]').forEach(b => b.addEventListener('click', () => {
            const sc = P.saved.find(x => x.id === Number(b.dataset.cmp));
            compare = compare.concat([{ name: sc.name, inputs: sc.inputs }]).slice(-2);
            tab = 'compare';
            render(root);
        }));
        P.reset();
        P.onChange(() => main(root));
        main(root);
    }

    /**
     * Render the results side for the active tab.
     * @param {HTMLElement} root - Container.
     */
    function main (root) {
        const el = root.querySelector('#paMain');
        if (!el) {
            return;
        }
        const s = P.state;
        const c = P.colors();
        const p = P.project(s);
        const realToggle = `<div class="form-check form-switch pa-real"><input class="form-check-input" type="checkbox" role="switch" id="paReal" data-key="real" ${s.real ? 'checked' : ''}><label class="form-check-label" for="paReal">Show in today's money</label></div>`;
        const f = s.real ? Math.pow(1 + s.inflation / 100, s.years) : 1;

        if (tab === 'grow') {
            el.innerHTML = `
            <div class="pa-kpis">
                <div class="pa-kpi pa-kpi--hero"><div class="pa-kpi__label">In ${s.years} years you could have</div><div class="pa-kpi__value">${P.money(p.final / f)}</div><div class="pa-muted">${s.real ? 'in today\'s money' : `≈ ${P.money(p.real)} in today's money`}</div></div>
                <div class="pa-kpi"><div class="pa-kpi__label">You put in</div><div class="pa-kpi__value">${P.money(p.contributed / f)}</div></div>
                <div class="pa-kpi"><div class="pa-kpi__label">Growth</div><div class="pa-kpi__value text-success">${P.money(p.growth / f)}</div></div>
                <div class="pa-kpi"><div class="pa-kpi__label">Fees cost you</div><div class="pa-kpi__value text-danger">${P.money(p.feeCost / f)}</div></div>
            </div>
            <div class="pa-card"><div class="pa-card__head"><h2 class="pa-h2">How it grows</h2>${realToggle}</div>
                <div id="paChart"></div>
                <p class="pa-muted small">${p.milestone ? `From year ${p.milestone}, growth has added more than you put in.` : 'Growth hasn\'t yet overtaken what you put in.'} Dashed line: the balance in today's money.</p></div>
            <div class="pa-row">
                <div class="pa-card"><h2 class="pa-h2">Where the final balance comes from</h2><div class="pa-donut-wrap"><div id="paDonut"></div>
                    <ul class="pa-legend"><li><span class="proto-dot" style="background:${c.contrib}"></span>You put in <strong>${Math.round(p.contributed / p.final * 100)}%</strong></li><li><span class="proto-dot" style="background:${c.growth}"></span>Growth <strong>${Math.round(p.growth / p.final * 100)}%</strong></li></ul></div></div>
                <div class="pa-card"><h2 class="pa-h2">What fees cost</h2><div id="paFee"></div><p class="pa-muted small">A ${s.fee}% yearly fee costs ${P.money(p.feeCost)} over ${s.years} years.</p></div>
            </div>
            <details class="pa-card pa-breakdown" open><summary><h2 class="pa-h2 d-inline">Year by year</h2></summary><div id="paBars"></div>${P.yearTable(p.rows, s.real)}</details>`;
            const markers = p.milestone ? [{ x: p.milestone, label: 'Growth > deposits', color: c.growth }] : [];
            P.lineChart(el.querySelector('#paChart'), { series: P.growthSeries(p, s.real), markers, label: 'Balance over time' });
            P.donut(el.querySelector('#paDonut'), [{ name: 'You put in', value: p.contributed, color: c.contrib }, { name: 'Growth', value: p.growth, color: c.growth }], P.compact(p.final / f));
            P.lineChart(el.querySelector('#paFee'), { height: 200, series: [
                { name: 'Without fees', color: c.growth, values: p.rows.map(r => ({ x: r.year, y: r.noFee })) },
                { name: 'With fees', color: c.value, area: true, values: p.rows.map(r => ({ x: r.year, y: r.balance })) }
            ] });
            P.stackedBars(el.querySelector('#paBars'), p.rows);
        } else if (tab === 'goal') {
            const g = P.solveGoal(s);
            const plan = g.kind === 'monthly' ? Object.assign({}, s, { monthly: g.monthly }) : (g.kind === 'years' ? Object.assign({}, s, { years: g.year }) : Object.assign({}, s, { years: 50 }));
            const gp = P.project(plan);
            const headline = {
                monthly: `You need <strong>${P.money(g.monthly)}/month</strong> to reach ${P.money(s.goal)} in ${s.years} years.`,
                years: `At ${P.money(s.monthly)}/month you reach ${P.money(s.goal)} in <strong>${g.year} years</strong>.`,
                already: `You're already there: your starting amount alone reaches ${P.money(s.goal)} by year ${g.year}.`,
                unreachable: `Not reachable within 50 years. You'd be <strong>${P.money(g.shortfall)}</strong> short.`
            }[g.kind];
            const pct = Math.min(100, Math.round(P.project(s).final / s.goal * 100));
            el.innerHTML = `
            <div class="pa-card pa-goal-answer"><div class="pa-card__head"><h2 class="pa-h2">Your goal</h2>${realToggle.replace('Show in today\'s money', 'Target is in today\'s money').replace('data-key="real"', 'data-key="goalReal"').replace(s.real ? 'checked' : '', s.goalReal ? 'checked' : '')}</div>
                <p class="pa-goal-headline">${headline}</p>
                <div class="pa-progress" role="progressbar" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100" aria-label="Current plan progress toward goal"><div style="width:${pct}%"></div></div>
                <p class="pa-muted small">Your current plan (${P.money(s.monthly)}/mo for ${s.years} years) gets you ${pct}% of the way.</p>
                ${g.kind === 'monthly' ? `<button type="button" class="btn btn-sm btn-outline-primary" id="paUse">Use ${P.money(g.monthly)}/mo in Grow →</button>` : ''}</div>
            <div class="pa-card"><h2 class="pa-h2">Path to your goal</h2><div id="paGoalChart"></div></div>`;
            P.lineChart(el.querySelector('#paGoalChart'), { series: P.growthSeries(gp, false).slice(0, 2), goalLine: s.goal,
                markers: g.year && g.kind !== 'unreachable' ? [{ x: g.year, label: 'Goal reached', color: c.extra }] : [], label: 'Path to goal' });
            const use = el.querySelector('#paUse');
            if (use) {
                use.addEventListener('click', () => {
                    P.set({ monthly: g.monthly });
                    tab = 'grow';
                    render(root);
                });
            }
        } else {
            const base = { name: 'Your plan', inputs: s };
            const lines = [base].concat(compare);
            const colorsList = [c.value, c.extra, c.growth];
            const projections = lines.map(l => P.project(l.inputs));
            const ghost = preview ? P.project(preview.apply(s)) : null;
            el.innerHTML = `
            <div class="pa-card"><h2 class="pa-h2">What if…</h2><p class="pa-muted small">Preview a change as a dashed line. Apply it to your plan, or add it to the comparison.</p>
                <div class="pa-levers">${P.LEVERS.map(l => {
        const d = P.project(l.apply(s)).final - p.final;
        return `<button type="button" class="pa-lever${preview && preview.key === l.key ? ' is-active' : ''}" data-lever="${l.key}"><span>${l.label}</span><strong class="${d >= 0 ? 'text-success' : 'text-danger'}">${d >= 0 ? '+' : '−'}${P.compact(Math.abs(d))}</strong></button>`;
    }).join('')}</div>
                ${preview ? `<div class="pa-preview-actions"><span>${preview.label}: ${P.money(ghost.final)} (${ghost.final >= p.final ? '+' : '−'}${P.money(Math.abs(ghost.final - p.final))})</span><button type="button" class="btn btn-sm btn-primary" id="paApply">Apply to plan</button><button type="button" class="btn btn-sm btn-outline-primary" id="paAddCmp">Add to comparison</button></div>` : ''}</div>
            <div class="pa-card"><h2 class="pa-h2">Comparison</h2>
                <div class="pa-cmp-legend">${lines.map((l, i) => `<div class="pa-cmp-item"><span class="proto-dot" style="background:${colorsList[i]}"></span><div><strong>${l.name}</strong><div>${P.money(projections[i].final)}${i ? ` <span class="${projections[i].final >= projections[0].final ? 'text-success' : 'text-danger'}">(${projections[i].final >= projections[0].final ? '+' : '−'}${P.compact(Math.abs(projections[i].final - projections[0].final))})</span>` : ''}</div></div>${i ? `<button type="button" class="btn-close btn-sm" data-rm="${i - 1}" aria-label="Remove ${l.name}"></button>` : ''}</div>`).join('')}
                ${lines.length < 3 ? '<span class="pa-muted small">Add up to 2 more from the levers or your saved scenarios.</span>' : ''}</div>
                <div id="paCmpChart"></div></div>`;
            const series = lines.map((l, i) => ({ name: l.name, color: colorsList[i], width: i ? 2.5 : 3, values: projections[i].rows.map(r => ({ x: r.year, y: r.balance })) }));
            if (ghost) {
                series.push({ name: `${preview.label} (preview)`, color: c.value, dash: '6 4', values: ghost.rows.map(r => ({ x: r.year, y: r.balance })) });
            }
            P.lineChart(el.querySelector('#paCmpChart'), { series, label: 'Comparison' });
            el.querySelectorAll('[data-lever]').forEach(b => b.addEventListener('click', () => {
                const l = P.LEVERS.find(x => x.key === b.dataset.lever);
                preview = preview && preview.key === l.key ? null : l;
                main(root);
            }));
            el.querySelectorAll('[data-rm]').forEach(b => b.addEventListener('click', () => {
                compare.splice(Number(b.dataset.rm), 1);
                main(root);
            }));
            const apply = el.querySelector('#paApply');
            if (apply) {
                apply.addEventListener('click', () => {
                    P.set(preview.apply(s));
                    preview = null;
                    render(root);
                });
                el.querySelector('#paAddCmp').addEventListener('click', () => {
                    compare = compare.concat([{ name: preview.label, inputs: preview.apply(Object.assign({}, s)) }]).slice(-2);
                    preview = null;
                    main(root);
                });
            }
        }
        P.bindInputs(el);
    }

    window.ProtoVariants.A = { name: 'Studio (tabs + rail)', render };
})();
