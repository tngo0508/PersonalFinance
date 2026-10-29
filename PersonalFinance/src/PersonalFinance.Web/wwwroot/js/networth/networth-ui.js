/* global PFI18n */
/**
 * Net Worth UI module: writes the headline, the levers, the milestones and the year-by-year table from a projection,
 * in the chosen view (as projected, or in today's money), and keeps the chart, the table and the milestones pointing
 * at the same age.
 * Depends on: networth-utils.js, networth-runway.js (window.NW)
 */
(function () {
    'use strict';

    const NW = window.NW;
    const SCENARIOS = ['Conservative', 'Base', 'Aggressive'];

    // The view: amounts as projected or in today's money, and every fifth year or every year in the table.
    const view = { mode: 'projected', density: 'five' };

    /**
     * Create an element with an optional class and text.
     * @param {string} tag - The tag name.
     * @param {string} [className] - The class attribute.
     * @param {string} [text] - Text content.
     * @returns {HTMLElement} The new element.
     */
    function el (tag, className, text) {
        const node = document.createElement(tag);
        if (className) { node.className = className; }
        if (text !== undefined) { node.textContent = text; }
        return node;
    }

    /**
     * Safely set text content on an element if it exists.
     * @param {string} id - Element id.
     * @param {string} text - Text to set.
     */
    function setText (id, text) {
        const node = document.getElementById(id);
        if (node) { node.textContent = text; }
    }

    /**
     * A scenario of the projection by name.
     * @param {Object} data - The projection.
     * @param {string} name - Base, Conservative or Aggressive.
     * @returns {Object} The scenario.
     */
    function scenarioOf (data, name) {
        return data[NW.scenarioMap[name]];
    }

    /**
     * A scenario's FI age in the current view, or null.
     * @param {Object} scenario - A scenario, or a lever.
     * @returns {number|null} The age.
     */
    function fiAgeOf (scenario) {
        const age = view.mode === 'real' ? scenario.realFiReachedAge : scenario.fiReachedAge;
        return age === undefined ? null : age;
    }

    /**
     * A scenario's final net worth in the current view.
     * @param {Object} scenario - A scenario, or a lever.
     * @returns {number} The amount.
     */
    function finalOf (scenario) {
        return view.mode === 'real' ? scenario.realFinalNetWorth : scenario.finalNetWorth;
    }

    /**
     * A year's balance in the current view.
     * @param {Object} year - A projection year.
     * @returns {number} The ending balance.
     */
    function balanceOf (year) {
        return view.mode === 'real' ? year.realEndingBalance : year.endingBalance;
    }

    /**
     * A count of years in words: "1 year", "14 years".
     * @param {number} count - The number of years.
     * @returns {string} The phrase.
     */
    function years (count) {
        return PFI18n.plural('NetWorth.Js.Years', count);
    }

    /**
     * The answer first: when the money could support you, or how far it gets by retirement.
     * @param {Object} data - The projection.
     */
    function updateHeadline (data) {
        const base = data.base;
        const fiAge = fiAgeOf(base);
        let lede;
        let sub = '';

        if (!(data.fiTarget > 0)) {
            lede = PFI18n.t('NetWorth.Js.Headline.NoTarget');
        } else if (fiAge !== null && fiAge <= data.currentAge) {
            lede = PFI18n.t('NetWorth.Js.Headline.Already');
            sub = PFI18n.t('NetWorth.Js.Sub.Already', {
                balance: NW.formatCurrency(data.startingBalance),
                spending: NW.formatCurrency(data.annualSpending)
            });
        } else if (fiAge !== null) {
            const early = data.retirementAge - fiAge;
            lede = PFI18n.t('NetWorth.Js.Headline.Free', { age: fiAge });
            sub = early > 0
                ? PFI18n.t('NetWorth.Js.Sub.Free', { years: years(fiAge - data.currentAge), early: years(early) })
                : PFI18n.t('NetWorth.Js.Sub.FreeAtRetirement', { years: years(fiAge - data.currentAge) });
        } else {
            const final = finalOf(base);
            lede = PFI18n.t('NetWorth.Js.Headline.NotYet', {
                percent: PFI18n.formatNumber(Math.max(0, Math.floor(final / data.fiTarget * 100))),
                age: data.retirementAge
            });
            sub = PFI18n.t('NetWorth.Js.Sub.NotYet', {
                target: NW.formatCurrency(data.fiTarget),
                rate: NW.formatRate(base.roiPercent),
                final: NW.formatCurrency(final)
            });
        }

        setText('nwHeadline', lede);
        setText('nwHeadlineSub', sub);

        const caption = document.getElementById('nwViewCaption');
        if (caption) {
            caption.hidden = view.mode !== 'real';
            caption.textContent = PFI18n.t('NetWorth.Js.View.Caption', { rate: NW.formatRate(data.inflationPercent) });
        }
    }

    /**
     * The chart legend and the table headers carry each scenario's return.
     * @param {Object} data - The projection.
     */
    function updateLabels (data) {
        const label = (name) => PFI18n.t('NetWorth.Js.Legend.Scenario', {
            name: NW.scenarioName(name),
            rate: NW.formatRate(scenarioOf(data, name).roiPercent)
        });
        setText('nwLegendBase', label('Base'));
        SCENARIOS.forEach((name) => {
            const header = document.querySelector(`[data-nw-col="${name}"]`);
            if (header) { header.textContent = label(name); }
        });
        const fi = document.querySelector('.nw-fi-target-value');
        if (fi) { fi.textContent = NW.formatCurrency(data.fiTarget); }
    }

    /**
     * What a lever does to the FI age (or, when the age doesn't move, to the money at retirement).
     * @param {Object} data - The projection.
     * @param {Object} lever - A lever from the projection.
     * @returns {{text: string, better: boolean}} The effect and whether it helps.
     */
    function leverEffect (data, lever) {
        const before = fiAgeOf(data.base);
        const after = fiAgeOf(lever);
        if (before !== null && after !== null && before !== after) {
            const diff = before - after;
            return diff > 0
                ? { text: PFI18n.plural('NetWorth.Js.Lever.Sooner', diff), better: true }
                : { text: PFI18n.plural('NetWorth.Js.Lever.Later', -diff), better: false };
        }
        if (before === null && after !== null) {
            return { text: PFI18n.t('NetWorth.Js.Lever.Reaches', { age: after }), better: true };
        }
        if (before !== null && after === null) {
            return { text: PFI18n.t('NetWorth.Js.Lever.Misses', { age: data.retirementAge }), better: false };
        }
        const gain = finalOf(lever) - finalOf(data.base);
        return gain >= 0
            ? { text: PFI18n.t('NetWorth.Js.Lever.More', { amount: NW.formatCompact(gain), age: data.retirementAge }), better: true }
            : { text: PFI18n.t('NetWorth.Js.Lever.Less', { amount: NW.formatCompact(-gain), age: data.retirementAge }), better: false };
    }

    /**
     * The step a lever applies to the inputs, in the currency the inputs are typed in.
     * @returns {number} 5,000 US dollars scaled by the selected currency's bound scale.
     */
    function inputMoneyStep () {
        const select = document.getElementById('nwCurrency');
        const option = select && select.selectedOptions ? select.selectedOptions[0] : null;
        const scale = option ? Number(option.getAttribute('data-bound-scale')) : 1;
        return 5000 * (Number.isFinite(scale) && scale > 0 ? scale : 1);
    }

    /**
     * Apply a lever to the inputs, so the page recalculates with it.
     * @param {string} key - The lever's key.
     */
    function applyLever (key) {
        const step = inputMoneyStep();
        const value = (id) => NW.getSliderValue(id);
        const changes = {
            SaveMore: { nwAnnualSavings: value('nwAnnualSavings') + step },
            SpendLess: { nwAnnualSpending: Math.max(0, value('nwAnnualSpending') - step) },
            ReturnUp: { nwBaseRoi: Math.round((value('nwBaseRoi') + 1) * 10) / 10 },
            ReturnDown: { nwBaseRoi: Math.max(0, Math.round((value('nwBaseRoi') - 1) * 10) / 10) }
        };
        if (changes[key]) { NW.applyPreset(changes[key]); }
    }

    /**
     * The levers: one change each, and what it does to the date.
     * @param {Object} data - The projection.
     */
    function updateLevers (data) {
        const list = document.getElementById('nwLevers');
        if (!list) { return; }
        const items = (data.levers || []).filter((lever) => lever.change > 0).map((lever) => {
            const effect = leverEffect(data, lever);
            const amount = lever.key === 'ReturnUp' || lever.key === 'ReturnDown'
                ? NW.formatRate(lever.change)
                : NW.formatCompact(lever.change);
            const button = el('button', `nw-lever${effect.better ? '' : ' nw-lever--worse'}`);
            button.type = 'button';
            button.append(
                el('span', 'nw-lever__effect', effect.text),
                el('span', 'nw-lever__label', PFI18n.t(`NetWorth.Js.Lever.${lever.key}`, { amount: amount }))
            );
            button.addEventListener('click', () => applyLever(lever.key));
            return button;
        });
        list.replaceChildren(...items);
    }

    /**
     * The moments that matter on the Base path, in age order.
     * @param {Object} data - The projection.
     * @returns {Array<{age: number, text: string, fi: boolean}>} The milestones.
     */
    function milestonesOf (data) {
        const base = data.base;
        const list = base.years;
        if (!list.length) { return []; }
        const target = data.fiTarget;
        const fiAge = fiAgeOf(base);
        const real = (amount, year) => (view.mode === 'real' ? amount / year.inflationFactor : amount);
        const found = [{ age: data.currentAge, text: PFI18n.t('NetWorth.Js.Milestone.Start', { amount: NW.formatCurrency(data.startingBalance) }), fi: false }];

        const first = (test) => list.find(test);
        const halfway = target > 0 && data.startingBalance < target / 2 ? first((y) => balanceOf(y) >= target / 2) : null;
        const crossover = base.annualSavings > 0 ? first((y) => y.roi >= y.savings) : null;
        const snowball = first((y) => y.endingBalance - y.totalContributions >= y.totalContributions && y.totalContributions > 0);

        if (halfway && (fiAge === null || halfway.age < fiAge)) {
            found.push({ age: halfway.age, text: PFI18n.t('NetWorth.Js.Milestone.Halfway'), fi: false });
        }
        if (crossover) {
            found.push({ age: crossover.age, text: PFI18n.t('NetWorth.Js.Milestone.Crossover'), fi: false });
        }
        if (snowball) {
            found.push({ age: snowball.age, text: PFI18n.t('NetWorth.Js.Milestone.Snowball'), fi: false });
        }
        if (fiAge !== null && fiAge > data.currentAge) {
            found.push({ age: fiAge, text: PFI18n.t('NetWorth.Js.Milestone.Fi', { amount: NW.formatCurrency(target) }), fi: true });
        }
        const last = list[list.length - 1];
        found.push({ age: last.age, text: PFI18n.t('NetWorth.Js.Milestone.Retire', { amount: NW.formatCurrency(real(last.endingBalance, last)) }), fi: false });

        return found.sort((a, b) => a.age - b.age);
    }

    /**
     * The milestone list; picking one moves the chart and the table to its age.
     * @param {Array} milestones - From milestonesOf.
     */
    function updateMilestones (milestones) {
        const list = document.getElementById('nwMilestones');
        if (!list) { return; }
        list.replaceChildren(...milestones.map((milestone) => {
            const item = el('li', `nw-milestone${milestone.fi ? ' nw-milestone--fi' : ''}`);
            const button = el('button', 'nw-milestone__btn');
            button.type = 'button';
            button.dataset.age = String(milestone.age);
            button.append(el('span', 'nw-milestone__age', String(milestone.age)), el('span', 'nw-milestone__text', milestone.text));
            button.addEventListener('click', () => NW.setRunwayAge(milestone.age));
            item.append(button);
            return item;
        }));
    }

    /**
     * The bar showing how much of a balance was put in and how much growth added.
     * @param {Object} year - A Base projection year.
     * @returns {HTMLElement} The bar.
     */
    function compositionBar (year) {
        const bar = el('div', 'nw-mix');
        const ending = year.endingBalance;
        if (!(ending > 0)) { return bar; }
        const putIn = Math.min(year.totalContributions, ending);
        const growth = Math.max(0, ending - putIn);
        const factor = view.mode === 'real' ? year.inflationFactor : 1;
        bar.setAttribute('role', 'img');
        bar.setAttribute('aria-label', PFI18n.t('NetWorth.Js.Table.Mix', {
            putIn: NW.formatCompact(putIn / factor),
            growth: NW.formatCompact(growth / factor)
        }));
        const put = el('span', 'nw-mix__put');
        put.style.width = `${(putIn / ending * 100).toFixed(1)}%`;
        const grown = el('span', 'nw-mix__grown');
        grown.style.width = `${(growth / ending * 100).toFixed(1)}%`;
        bar.append(put, grown);
        return bar;
    }

    /**
     * The year-by-year table: every scenario side by side, at every fifth age (plus the milestones and the last
     * year) or at every age. The first year each scenario reaches the FI Target is marked.
     * @param {Object} data - The projection.
     * @param {Array} milestones - From milestonesOf.
     */
    function updateTable (data, milestones) {
        const tbody = document.getElementById('nwTableBody');
        if (!tbody) { return; }
        const base = data.base.years;
        const lastAge = base.length ? base[base.length - 1].age : data.currentAge;
        const marked = new Set(milestones.map((m) => m.age));
        const fiAges = {};
        SCENARIOS.forEach((name) => { fiAges[name] = fiAgeOf(scenarioOf(data, name)); });

        const rows = base.filter((year) => view.density === 'all'
            || (year.age - data.currentAge) % 5 === 0 || year.age === lastAge || marked.has(year.age) || year.age === fiAges.Base)
            .map((year) => {
                // The scenarios share their ages, so a year's position in Base is its position in each.
                const index = base.indexOf(year);
                const row = el('tr', marked.has(year.age) ? 'nw-row--milestone' : '');
                row.dataset.age = String(year.age);

                const head = el('th');
                head.scope = 'row';
                const button = el('button', 'nw-age-btn');
                button.type = 'button';
                button.append(el('span', 'nw-age-btn__age', String(year.age)), el('span', 'nw-age-btn__year', String(year.year)));
                button.addEventListener('click', () => NW.setRunwayAge(year.age));
                head.append(button);
                row.append(head);

                SCENARIOS.forEach((name) => {
                    const scenarioYear = scenarioOf(data, name).years[index];
                    const cell = el('td', `nw-num${name === 'Base' ? ' nw-table__base' : ''}`);
                    cell.append(document.createTextNode(scenarioYear ? NW.formatCurrency(balanceOf(scenarioYear)) : ''));
                    if (scenarioYear && fiAges[name] === year.age) {
                        cell.classList.add('nw-cell--fi');
                        cell.prepend(el('span', 'nw-fi-dot'));
                        cell.append(el('span', 'visually-hidden', ` ${PFI18n.t('NetWorth.Js.Table.FiReached')}`));
                    }
                    row.append(cell);
                });

                const mix = el('td', 'nw-table__mix');
                mix.append(compositionBar(year));
                row.append(mix);
                return row;
            });

        tbody.replaceChildren(...rows);
    }

    /**
     * Mark the table row and milestone at an age, the one the chart marker is on.
     * @param {number} age - The age.
     */
    function highlightAge (age) {
        document.querySelectorAll('#nwTableBody tr').forEach((row) => {
            row.classList.toggle('is-current', row.dataset.age === String(age));
        });
        document.querySelectorAll('.nw-milestone__btn').forEach((button) => {
            button.setAttribute('aria-current', button.dataset.age === String(age) ? 'true' : 'false');
        });
    }

    /**
     * The caption under the chart: which rate converted the inputs into the Reporting Currency, or that none could.
     * @param {Object|null|undefined} conversion - The projection's conversion.
     */
    function updateConversionCaption (conversion) {
        const box = document.querySelector('[data-nw-conversion-caption]');
        if (!box) { return; }
        const text = window.PFCurrency ? window.PFCurrency.captionText(conversion) : '';
        box.textContent = text;
        box.hidden = text === '';
    }

    /**
     * Write the whole page from a projection in the current view.
     * @param {Object} data - The projection.
     */
    function render (data) {
        if (!data || !data.base) { return; }
        const milestones = milestonesOf(data);
        updateHeadline(data);
        updateLabels(data);
        updateLevers(data);
        updateMilestones(milestones);
        updateTable(data, milestones);
        NW.renderRunway(data, view.mode);
    }

    /**
     * Wire a two-button toggle group: pressing one sets the view property and re-renders.
     * @param {string} attribute - The buttons' data attribute naming the value.
     * @param {string} property - The view property it sets.
     */
    function initToggle (attribute, property) {
        const buttons = document.querySelectorAll(`[${attribute}]`);
        buttons.forEach((button) => {
            button.addEventListener('click', () => {
                view[property] = button.getAttribute(attribute);
                buttons.forEach((b) => b.setAttribute('aria-pressed', b === button ? 'true' : 'false'));
                render(window.nwProjectionData);
            });
        });
    }

    /**
     * Wire the today's-money and table density toggles.
     */
    function initViews () {
        initToggle('data-nw-view', 'mode');
        initToggle('data-nw-density', 'density');
    }

    window.NW = Object.assign(window.NW || {}, {
        render: render,
        initViews: initViews,
        highlightAge: highlightAge,
        updateConversionCaption: updateConversionCaption
    });

})();
