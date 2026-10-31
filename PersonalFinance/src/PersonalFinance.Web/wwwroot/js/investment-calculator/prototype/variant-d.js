/* global Proto */
/**
 * PROTOTYPE (#587) - Variant D "Mix": the owner's picks. C's pinned bar at the top (headline numbers, view switch,
 * Today's money, Saved), A's slider rail on the left, C's chart grid as the Overview, A's Compare what-ifs as the
 * second view, A's saved-scenarios drawer. Inputs live once, in the rail; the bar never repeats them.
 */
(function () {
    'use strict';

    const P = Proto;
    let view = 'overview';
    let preview = null;
    let compare = [];

    /**
     * Render the variant.
     * @param {HTMLElement} root - Container.
     */
    function render (root) {
        const s = P.state;
        root.innerHTML = `
        <div class="pd-bar" role="region" aria-label="Plan summary">
            <div class="pd-bar__title"><strong>Investment Calculator</strong>
                <div class="pd-views" role="tablist" aria-label="View">${[['overview', 'Overview'], ['compare', 'Compare what-ifs']].map(([k, l]) =>
        `<button type="button" role="tab" aria-selected="${view === k}" class="pd-view${view === k ? ' is-active' : ''}" data-view="${k}">${l}</button>`).join('')}</div></div>
            <div class="pd-bar__nums" id="pdNums" aria-live="polite"></div>
            <div class="pd-bar__tools">
                <div class="form-check form-switch m-0"><input class="form-check-input" type="checkbox" role="switch" id="pdReal" data-key="real" ${s.real ? 'checked' : ''}><label class="form-check-label small" for="pdReal">Today's money</label></div>
                <button type="button" class="btn btn-sm btn-outline-secondary" id="pdSavedBtn">Saved <span class="badge text-bg-secondary">${P.saved.length}</span></button>
                <button type="button" class="btn btn-sm btn-primary" id="pdSave">Save</button>
            </div>
        </div>
        <div class="pa-grid pd-grid">
            <aside class="pa-rail pd-rail" aria-label="Your plan">
                <h2 class="pa-h2">Your plan</h2>
                ${P.field({ key: 'initial', label: 'Starting amount', min: 0, max: 500000, step: 1000, fmt: P.money })}
                ${P.field({ key: 'monthly', label: 'Monthly contribution', min: 0, max: 10000, step: 50, fmt: P.money })}
                <div class="pa-presets" role="group" aria-label="Rate presets">${P.PRESETS.map(p => `<button type="button" class="btn btn-sm ${s.rate === p.rate ? 'btn-primary' : 'btn-outline-secondary'}" data-preset="${p.rate}">${p.label} ${p.rate}%</button>`).join('')}</div>
                ${P.field({ key: 'rate', label: 'Assumed yearly return', min: 0, max: 15, step: 0.5, fmt: v => `${v}%` })}
                ${P.field({ key: 'years', label: 'Years', min: 1, max: 50, step: 1, fmt: v => `${v} yrs` })}
                <details class="pa-adv"><summary>More assumptions</summary>
                    ${P.field({ key: 'increase', label: 'Raise contributions each year', min: 0, max: 10, step: 0.5, fmt: v => `${v}%` })}
                    ${P.field({ key: 'fee', label: 'Yearly fund fee', min: 0, max: 3, step: 0.05, fmt: v => `${v}%` })}
                    ${P.field({ key: 'inflation', label: 'Inflation', min: 0, max: 10, step: 0.1, fmt: v => `${v}%` })}
                </details>
                <p class="pa-disclaimer">Educational estimate using a fixed assumed return, not financial advice. <a href="/Legal/Disclaimer">Disclaimer</a></p>
            </aside>
            <section class="pd-main" id="pdMain"></section>
        </div>
        <div class="pa-drawer" id="pdDrawer" hidden>
            <div class="pa-drawer__panel" role="dialog" aria-label="Saved scenarios">
                <div class="d-flex justify-content-between align-items-center mb-3"><h2 class="pa-h2 m-0">Saved scenarios</h2><button type="button" class="btn-close" id="pdDrawerClose" aria-label="Close"></button></div>
                ${P.saved.map(sc => `<div class="pa-scn"><div><strong>${sc.name}</strong><div class="pa-muted">${P.money(P.project(sc.inputs).final)} · edited ${sc.edited}</div></div>
                    <div class="btn-group btn-group-sm"><button type="button" class="btn btn-outline-primary" data-load="${sc.id}">Load</button><button type="button" class="btn btn-outline-secondary" data-cmp="${sc.id}">Compare</button><button type="button" class="btn btn-outline-secondary" aria-label="More">…</button></div></div>`).join('')}
                <p class="pa-muted small mt-3">Up to 20 scenarios. Stub data: nothing is saved in this prototype.</p>
            </div>
        </div>`;

        P.bindInputs(root.querySelector('.pd-rail'));
        P.bindInputs(root.querySelector('.pd-bar__tools'));
        root.querySelectorAll('[data-view]').forEach(b => b.addEventListener('click', () => {
            view = b.dataset.view;
            render(root);
        }));
        root.querySelectorAll('[data-preset]').forEach(b => b.addEventListener('click', () => {
            P.set({ rate: parseFloat(b.dataset.preset) });
            render(root);
        }));
        const drawer = root.querySelector('#pdDrawer');
        root.querySelector('#pdSavedBtn').addEventListener('click', () => {
            drawer.hidden = false;
        });
        root.querySelector('#pdDrawerClose').addEventListener('click', () => {
            drawer.hidden = true;
        });
        root.querySelector('#pdSave').addEventListener('click', () => {
            const name = window.prompt('Name this scenario', 'My plan');
            if (name) {
                P.saved.unshift({ id: Date.now(), name, inputs: Object.assign({}, P.state), edited: 'just now' });
                render(root);
            }
        });
        root.querySelectorAll('[data-load]').forEach(b => b.addEventListener('click', () => {
            P.set(Object.assign({}, P.saved.find(x => x.id === Number(b.dataset.load)).inputs));
            render(root);
        }));
        root.querySelectorAll('[data-cmp]').forEach(b => b.addEventListener('click', () => {
            const sc = P.saved.find(x => x.id === Number(b.dataset.cmp));
            compare = compare.concat([{ name: sc.name, inputs: sc.inputs }]).slice(-2);
            view = 'compare';
            render(root);
        }));
        P.reset();
        P.onChange(() => main(root));
        main(root);
    }

    /**
     * Render the bar numbers and the active view.
     * @param {HTMLElement} root - Container.
     */
    function main (root) {
        const s = P.state;
        const c = P.colors();
        const p = P.project(s);
        const f = s.real ? Math.pow(1 + s.inflation / 100, s.years) : 1;
        root.querySelector('#pdNums').innerHTML = `
            <div class="pd-num pd-num--hero"><span>In ${s.years} years</span><strong>${P.money(p.final / f)}</strong></div>
            <div class="pd-num"><span>You put in</span><strong>${P.money(p.contributed / f)}</strong></div>
            <div class="pd-num"><span>Growth</span><strong class="text-success">${P.money(p.growth / f)}</strong></div>
            <div class="pd-num"><span>${s.real ? 'In future money' : "In today's money"}</span><strong>${P.money(s.real ? p.final : p.real)}</strong></div>`;
        const el = root.querySelector('#pdMain');
        if (view === 'overview') {
            const g = P.solveGoal(s);
            el.innerHTML = `<div class="pc-grid pd-tiles">
                <div class="pc-tile pc-tile--wide"><h3>Balance over time</h3><div id="pdGrow"></div>
                    <p class="pa-muted small m-0">${p.milestone ? `From year ${p.milestone}, growth has added more than you put in.` : 'Growth hasn\'t yet overtaken what you put in.'} Dashed: today's money.</p></div>
                <div class="pc-tile"><h3>Where it comes from</h3><div id="pdDonut" class="d-flex justify-content-center"></div>
                    <ul class="pa-legend mt-2"><li><span class="proto-dot" style="background:${c.contrib}"></span>You put in ${Math.round(p.contributed / p.final * 100)}%</li><li><span class="proto-dot" style="background:${c.growth}"></span>Growth ${Math.round(p.growth / p.final * 100)}%</li></ul></div>
                <div class="pc-tile"><h3>Fees cost you <span class="text-danger">${P.money(p.feeCost / f)}</span></h3><div id="pdFee"></div></div>
                <div class="pc-tile"><h3>Added per year</h3><div id="pdBars"></div></div>
                <div class="pc-tile"><h3>Goal <input type="number" class="form-control form-control-sm d-inline-block pc-goal-in" data-key="goal" step="10000" value="${s.goal}" aria-label="Goal amount"></h3>
                    <div class="btn-group btn-group-sm mb-2"><button type="button" class="btn ${s.goalMode === 'monthly' ? 'btn-primary' : 'btn-outline-secondary'}" data-gm="monthly">How much a month?</button><button type="button" class="btn ${s.goalMode === 'years' ? 'btn-primary' : 'btn-outline-secondary'}" data-gm="years">How long?</button></div>
                    <div class="pc-goal-answer">${{ monthly: `${P.money(g.monthly)}<small>/mo for ${s.years} yrs</small>`, years: `${g.year}<small> years at ${P.money(s.monthly)}/mo</small>`, already: 'Already there', unreachable: `Not in 50 yrs<small> (${P.compact(g.shortfall)} short)</small>` }[g.kind]}</div>
                    <div class="pa-progress"><div style="width:${Math.min(100, p.final / s.goal * 100)}%"></div></div><span class="pc-muted small">Current plan: ${Math.round(p.final / s.goal * 100)}% of goal</span>
                    ${g.kind === 'monthly' ? `<button type="button" class="btn btn-sm btn-link p-0 d-block mt-1" id="pdUse">Use ${P.money(g.monthly)}/mo</button>` : ''}</div>
                <details class="pc-tile pc-tile--full"><summary><h3 class="d-inline">Year by year</h3></summary>${P.yearTable(p.rows, s.real)}</details>
            </div>`;
            P.lineChart(el.querySelector('#pdGrow'), { series: P.growthSeries(p, s.real), goalLine: s.real ? null : s.goal,
                markers: p.milestone ? [{ x: p.milestone, label: 'Growth > deposits', color: c.growth }] : [] });
            P.donut(el.querySelector('#pdDonut'), [{ name: 'You put in', value: p.contributed, color: c.contrib }, { name: 'Growth', value: p.growth, color: c.growth }], P.compact(p.final / f));
            P.lineChart(el.querySelector('#pdFee'), { height: 180, series: [
                { name: 'Without fees', color: c.growth, values: p.rows.map(r => ({ x: r.year, y: r.noFee })) },
                { name: 'With fees', color: c.value, area: true, values: p.rows.map(r => ({ x: r.year, y: r.balance })) }
            ] });
            P.stackedBars(el.querySelector('#pdBars'), p.rows, 180);
            el.querySelectorAll('[data-gm]').forEach(b => b.addEventListener('click', () => P.set({ goalMode: b.dataset.gm })));
            const use = el.querySelector('#pdUse');
            if (use) {
                use.addEventListener('click', () => {
                    P.set({ monthly: g.monthly });
                    render(root);
                });
            }
        } else {
            const lines = [{ name: 'Your plan', inputs: s }].concat(compare);
            const colorsList = [c.value, c.extra, c.growth];
            const projections = lines.map(l => P.project(l.inputs));
            const ghost = preview ? P.project(preview.apply(s)) : null;
            el.innerHTML = `
            <div class="pa-card mb-3"><h2 class="pa-h2">What if…</h2><p class="pa-muted small">Preview a change as a dashed line. Apply it to your plan, or add it to the comparison.</p>
                <div class="pa-levers">${P.LEVERS.map(l => {
        const d = P.project(l.apply(s)).final - p.final;
        return `<button type="button" class="pa-lever${preview && preview.key === l.key ? ' is-active' : ''}" data-lever="${l.key}"><span>${l.label}</span><strong class="${d >= 0 ? 'text-success' : 'text-danger'}">${d >= 0 ? '+' : '−'}${P.compact(Math.abs(d))}</strong></button>`;
    }).join('')}</div>
                ${preview ? `<div class="pa-preview-actions"><span>${preview.label}: ${P.money(ghost.final)} (${ghost.final >= p.final ? '+' : '−'}${P.money(Math.abs(ghost.final - p.final))})</span><button type="button" class="btn btn-sm btn-primary" id="pdApply">Apply to plan</button><button type="button" class="btn btn-sm btn-outline-primary" id="pdAddCmp">Add to comparison</button></div>` : ''}</div>
            <div class="pa-card"><h2 class="pa-h2">Comparison</h2>
                <div class="pa-cmp-legend">${lines.map((l, i) => `<div class="pa-cmp-item"><span class="proto-dot" style="background:${colorsList[i]}"></span><div><strong>${l.name}</strong><div>${P.money(projections[i].final)}${i ? ` <span class="${projections[i].final >= projections[0].final ? 'text-success' : 'text-danger'}">(${projections[i].final >= projections[0].final ? '+' : '−'}${P.compact(Math.abs(projections[i].final - projections[0].final))})</span>` : ''}</div></div>${i ? `<button type="button" class="btn-close btn-sm" data-rm="${i - 1}" aria-label="Remove ${l.name}"></button>` : ''}</div>`).join('')}
                ${lines.length < 3 ? '<span class="pa-muted small">Add up to 2 more from the levers or Saved.</span>' : ''}</div>
                <div id="pdCmp"></div></div>`;
            const series = lines.map((l, i) => ({ name: l.name, color: colorsList[i], width: i ? 2.5 : 3, values: projections[i].rows.map(r => ({ x: r.year, y: r.balance })) }));
            if (ghost) {
                series.push({ name: `${preview.label} (preview)`, color: c.value, dash: '6 4', values: ghost.rows.map(r => ({ x: r.year, y: r.balance })) });
            }
            P.lineChart(el.querySelector('#pdCmp'), { series });
            el.querySelectorAll('[data-lever]').forEach(b => b.addEventListener('click', () => {
                const l = P.LEVERS.find(x => x.key === b.dataset.lever);
                preview = preview && preview.key === l.key ? null : l;
                main(root);
            }));
            el.querySelectorAll('[data-rm]').forEach(b => b.addEventListener('click', () => {
                compare.splice(Number(b.dataset.rm), 1);
                main(root);
            }));
            const apply = el.querySelector('#pdApply');
            if (apply) {
                apply.addEventListener('click', () => {
                    P.set(preview.apply(s));
                    preview = null;
                    render(root);
                });
                el.querySelector('#pdAddCmp').addEventListener('click', () => {
                    compare = compare.concat([{ name: preview.label, inputs: preview.apply(Object.assign({}, s)) }]).slice(-2);
                    preview = null;
                    main(root);
                });
            }
        }
        P.bindInputs(el);
    }

    window.ProtoVariants.D = { name: 'Mix (C bar + grid, A rail + Compare + drawer)', render };
})();
