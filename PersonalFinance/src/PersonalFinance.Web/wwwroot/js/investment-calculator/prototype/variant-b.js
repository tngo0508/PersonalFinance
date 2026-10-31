/* global Proto */
/**
 * PROTOTYPE (#587) - Variant B "Story": no tabs, no sidebar. The plan is a sentence with inline inputs; the page then
 * reads top to bottom as chapters (grow, today's money, fees, year by year, goal, what if), like the Analysis page.
 */
(function () {
    'use strict';

    const P = Proto;
    let ghostKey = null;

    /**
     * Inline number input for the sentence.
     * @param {string} key - State key.
     * @param {number} step - Step.
     * @param {string} [prefix] - Text before.
     * @param {string} [suffix] - Text after.
     * @returns {string} Markup.
     */
    function inline (key, step, prefix, suffix) {
        return `<span class="pb-inline">${prefix || ''}<input type="number" data-key="${key}" step="${step}" value="${P.state[key]}" aria-label="${key}" style="width:${String(P.state[key]).length + 2}ch">${suffix || ''}</span>`;
    }

    /**
     * Render the variant.
     * @param {HTMLElement} root - Container.
     */
    function render (root) {
        const s = P.state;
        root.innerHTML = `
        <section class="pb-hero">
            <p class="pb-eyebrow">Investment Calculator</p>
            <p class="pb-sentence">If I start with ${inline('initial', 1000, '$')} and add ${inline('monthly', 50, '$')} a month,
                at an assumed ${inline('rate', 0.5, '', '%')} a year for ${inline('years', 1, '', ' years')}…</p>
            <div class="pb-presets">Not sure about the return? ${P.PRESETS.map(p => `<button type="button" class="pb-chip${s.rate === p.rate ? ' is-active' : ''}" data-preset="${p.rate}">${p.label} ${p.rate}%</button>`).join('')}</div>
            <div class="pb-answer" id="pbAnswer" aria-live="polite"></div>
            <div class="pb-actions"><button type="button" class="btn btn-primary" id="pbSave">Save this plan</button>
                <div class="pb-saved">${P.saved.map(sc => `<button type="button" class="pb-chip" data-load="${sc.id}" title="Edited ${sc.edited}">${sc.name}</button>`).join('')}</div></div>
        </section>
        <div id="pbChapters"></div>
        <p class="pb-disclaimer">Educational estimate using a fixed assumed return, not financial advice. <a href="/Legal/Disclaimer">Disclaimer</a></p>`;
        P.bindInputs(root);
        root.querySelectorAll('.pb-inline input').forEach(i => i.addEventListener('input', () => {
            i.style.width = `${i.value.length + 2}ch`;
        }));
        root.querySelectorAll('[data-preset]').forEach(b => b.addEventListener('click', () => {
            P.set({ rate: parseFloat(b.dataset.preset) });
            render(root);
        }));
        root.querySelectorAll('[data-load]').forEach(b => b.addEventListener('click', () => {
            P.set(Object.assign({}, P.saved.find(x => x.id === Number(b.dataset.load)).inputs));
            render(root);
        }));
        root.querySelector('#pbSave').addEventListener('click', () => {
            const name = window.prompt('Name this plan', 'My plan');
            if (name) {
                P.saved.unshift({ id: Date.now(), name, inputs: Object.assign({}, P.state), edited: 'just now' });
                render(root);
            }
        });
        P.reset();
        P.onChange(() => chapters(root));
        chapters(root);
    }

    /**
     * Render the answer and every chapter.
     * @param {HTMLElement} root - Container.
     */
    function chapters (root) {
        const s = P.state;
        const c = P.colors();
        const p = P.project(s);
        root.querySelector('#pbAnswer').innerHTML = `<span class="pb-answer__lead">…I could have</span> <span class="pb-answer__value">${P.money(p.final)}</span>
            <span class="pb-answer__split">${P.money(p.contributed)} from me · <span class="text-success">${P.money(p.growth)} from growth</span></span>`;
        const g = P.solveGoal(s);
        const goalText = {
            monthly: `you'd need to put in <strong>${P.money(g.monthly)} a month</strong>.`,
            years: `you'd get there in <strong>${g.year} years</strong>.`,
            already: `you're already there: your starting amount gets you there by year ${g.year}.`,
            unreachable: `you won't get there within 50 years (about ${P.money(g.shortfall)} short).`
        }[g.kind];
        const ghostLever = P.LEVERS.find(l => l.key === ghostKey);
        const ghost = ghostLever ? P.project(ghostLever.apply(s)) : null;
        const el = root.querySelector('#pbChapters');
        el.innerHTML = `
        <section class="pb-chapter"><span class="pb-num">1</span><div class="pb-body">
            <h2>How it grows</h2><p class="pb-lede">${p.milestone ? `By <strong>year ${p.milestone}</strong>, growth has added more than you put in. After that, compounding does most of the work.` : 'Over this period, what you put in stays bigger than the growth.'}</p>
            <div id="pbGrow"></div>
            ${ghost ? `<p class="pb-ghost-note">Dashed: <strong>${ghostLever.label}</strong> → ${P.money(ghost.final)} <button type="button" class="btn btn-link btn-sm p-0" id="pbApply">Make this my plan</button> · <button type="button" class="btn btn-link btn-sm p-0" id="pbClear">Clear</button></p>` : ''}
        </div></section>
        <section class="pb-chapter"><span class="pb-num">2</span><div class="pb-body">
            <h2>What that's worth today</h2>
            <p class="pb-lede">With prices rising ${inline('inflation', 0.1, '', '%')} a year, ${P.money(p.final)} in ${s.years} years buys what <strong>${P.money(p.real)}</strong> buys today.</p>
            <div class="pb-compare-bars"><div class="pb-bar"><div style="width:100%;background:${c.value}"></div><span>Future money ${P.money(p.final)}</span></div><div class="pb-bar"><div style="width:${p.real / p.final * 100}%;background:${c.contrib}"></div><span>Today's money ${P.money(p.real)}</span></div></div>
        </div></section>
        <section class="pb-chapter"><span class="pb-num">3</span><div class="pb-body">
            <h2>What fees take</h2>
            <p class="pb-lede">A fund fee of ${inline('fee', 0.05, '', '%')} a year quietly costs you <strong class="text-danger">${P.money(p.feeCost)}</strong>. Raising your contribution ${inline('increase', 0.5, '', '%')} each year would add up too.</p>
            <div id="pbFee"></div>
        </div></section>
        <section class="pb-chapter"><span class="pb-num">4</span><div class="pb-body">
            <h2>Year by year</h2><p class="pb-lede">Each bar is one year: what you added (bottom) and what grew (top).</p>
            <div id="pbBars"></div>
            <details><summary>Show the table</summary>${P.yearTable(p.rows, false)}</details>
        </div></section>
        <section class="pb-chapter"><span class="pb-num">5</span><div class="pb-body">
            <h2>Reaching a goal</h2>
            <p class="pb-lede">To have ${inline('goal', 10000, '$')} ${s.goalMode === 'monthly' ? `in ${s.years} years` : `adding ${P.money(s.monthly)} a month`}, ${goalText}</p>
            <div class="pb-presets"><button type="button" class="pb-chip${s.goalMode === 'monthly' ? ' is-active' : ''}" data-gm="monthly">How much a month?</button><button type="button" class="pb-chip${s.goalMode === 'years' ? ' is-active' : ''}" data-gm="years">How long?</button></div>
            <div id="pbGoal"></div>
        </div></section>
        <section class="pb-chapter"><span class="pb-num">6</span><div class="pb-body">
            <h2>What if…</h2><p class="pb-lede">Tap one to see it on the chart in chapter 1.</p>
            <div class="pb-whatifs">${P.LEVERS.map(l => {
        const d = P.project(l.apply(s)).final - p.final;
        return `<button type="button" class="pb-whatif${ghostKey === l.key ? ' is-active' : ''}" data-ghost="${l.key}"><span>${l.label}</span><strong class="${d >= 0 ? 'text-success' : 'text-danger'}">${d >= 0 ? '+' : '−'}${P.money(Math.abs(d))}</strong></button>`;
    }).join('')}</div>
        </div></section>`;

        const series = P.growthSeries(p, false);
        if (ghost) {
            series.push({ name: ghostLever.label, color: c.extra, dash: '6 4', values: ghost.rows.map(r => ({ x: r.year, y: r.balance })) });
        }
        P.lineChart(el.querySelector('#pbGrow'), { series, markers: p.milestone ? [{ x: p.milestone, label: 'Growth > deposits', color: c.growth }] : [] });
        P.lineChart(el.querySelector('#pbFee'), { height: 200, series: [
            { name: 'Without fees', color: c.growth, values: p.rows.map(r => ({ x: r.year, y: r.noFee })) },
            { name: 'With fees', color: c.value, area: true, values: p.rows.map(r => ({ x: r.year, y: r.balance })) }
        ] });
        P.stackedBars(el.querySelector('#pbBars'), p.rows);
        const gp = P.project(g.kind === 'monthly' ? Object.assign({}, s, { monthly: g.monthly }) : Object.assign({}, s, { years: g.year || 50 }));
        P.lineChart(el.querySelector('#pbGoal'), { height: 220, series: P.growthSeries(gp, false).slice(0, 1), goalLine: s.goal,
            markers: g.year && g.kind !== 'unreachable' ? [{ x: g.year, label: 'Goal', color: c.extra }] : [] });

        P.bindInputs(el);
        el.querySelectorAll('[data-gm]').forEach(b => b.addEventListener('click', () => P.set({ goalMode: b.dataset.gm })));
        el.querySelectorAll('[data-ghost]').forEach(b => b.addEventListener('click', () => {
            ghostKey = ghostKey === b.dataset.ghost ? null : b.dataset.ghost;
            chapters(root);
            root.querySelector('#pbGrow').scrollIntoView({ behavior: 'smooth', block: 'center' });
        }));
        const apply = el.querySelector('#pbApply');
        if (apply) {
            apply.addEventListener('click', () => {
                P.set(ghostLever.apply(s));
                ghostKey = null;
                render(root);
            });
            el.querySelector('#pbClear').addEventListener('click', () => {
                ghostKey = null;
                chapters(root);
            });
        }
    }

    window.ProtoVariants.B = { name: 'Story (sentence + chapters)', render };
})();
