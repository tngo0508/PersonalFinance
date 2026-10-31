/* global PFI18n, InvestmentMath, InvestmentCharts */
/**
 * Investment Calculator, Compare what-ifs (#591): the Overview / Compare view switch in the summary bar, the what-if
 * levers, the preview of one lever as a dashed line, and the comparison of the plan with up to two variants.
 *
 * A variant is the plan's inputs with one change (InvestmentMath.lever). Added to the comparison it keeps that change
 * and follows the plan, so it always differs from the plan in one input, which can be edited there. The differences
 * are always against the current plan.
 * Nothing is saved: the comparison lives until the page is left.
 *
 * calculator.js creates the controller (window.InvestmentCompare.create) and calls update() with every new plan and
 * draw() with every chart frame. add() is the way in for other sources of entries, such as saved scenarios (#592).
 */
(function () {
    'use strict';

    /** At most this many variants beside the plan: three lines on the chart. */
    const MAX_VARIANTS = 2;
    const STATUS_DELAY_MS = 600;
    /**
     * Every lever, by the plan input it changes (InvestmentMath.LEVERS gives each lever key's field): the suffix of its
     * Js.Lever / Js.Variant / Js.Edit keys, the arguments of its button label (from the step and the variant) and of
     * its variant name (from the variant's inputs), and its editor's step and bounds on the comparison.
     */
    const LEVERS = {
        monthly: {
            name: 'Monthly',
            lever: (ctx) => ({ amount: ctx.deps.money(ctx.monthlyStep) }),
            variant: (deps, inputs) => ({ amount: deps.money(inputs.monthly) }),
            step: 'any',
            min: () => 0,
            max: (deps) => deps.monthlyMax()
        },
        rate: {
            name: 'Rate',
            lever: (ctx) => ({ points: ctx.deps.percent(InvestmentMath.LEVER_STEPS.rate) }),
            variant: (deps, inputs) => ({ rate: deps.percent(inputs.rate) }),
            step: '0.5',
            min: () => InvestmentMath.LIMITS.rate.min,
            max: () => InvestmentMath.LIMITS.rate.max
        },
        delay: {
            name: 'Delay',
            lever: () => ({ years: PFI18n.plural('InvestmentCalculator.Js.Years', InvestmentMath.LEVER_STEPS.delay) }),
            variant: (deps, inputs) => ({ year: PFI18n.formatNumber(inputs.delay) }),
            step: '1',
            min: () => 0,
            max: (deps, inputs) => inputs.years - 1
        },
        stopAt: {
            name: 'Stop',
            lever: (ctx) => ({ year: PFI18n.formatNumber(ctx.variant.inputs.stopAt) }),
            variant: (deps, inputs) => ({ year: PFI18n.formatNumber(inputs.stopAt) }),
            step: '1',
            // Stopping in or before the delay's last year would mean no deposits at all.
            min: (deps, inputs) => Math.min(inputs.delay + 1, inputs.years),
            max: (deps, inputs) => inputs.years
        },
        fee: {
            name: 'Fee',
            lever: (ctx) => ({ points: ctx.deps.percent(InvestmentMath.LEVER_STEPS.fee) }),
            variant: (deps, inputs) => ({ fee: deps.percent(inputs.fee) }),
            step: '0.05',
            min: () => InvestmentMath.LIMITS.fee.min,
            max: () => InvestmentMath.LIMITS.fee.max
        }
    };

    /**
     * The Compare controller for one page.
     * @param {HTMLElement} root - The calculator root ([data-investment-calculator]).
     * @param {object} deps - What the page shares.
     * @param {Function} deps.money - Formats an amount as whole money.
     * @param {Function} deps.compact - Formats an amount compactly.
     * @param {Function} deps.percent - Formats a rate in percent.
     * @param {Function} deps.colours - The chart palette (ChartPalette.read()).
     * @param {Function} deps.yearFormats - The year formatters {formatX, titleX}.
     * @param {Function} deps.shown - A projection in the money the Today's money switch shows.
     * @param {Function} deps.monthlyMax - The largest monthly contribution the rail takes.
     * @param {Function} deps.applyPlan - Makes the given inputs the plan (writes the rail and updates the page).
     * @param {Function} deps.onView - Called after the view changes, with 'overview' or 'compare'.
     * @returns {{update: Function, draw: Function, add: Function, view: Function}} The controller.
     */
    function create (root, deps) {
        const byId = (id) => document.getElementById(id);
        const leversHost = byId('icLevers');
        const list = byId('icCmpList');
        const preview = byId('icPreview');
        const monthlyStep = leversHost ? parseFloat(leversHost.dataset.monthlyStep) : NaN;
        let plan = null;
        let previewKey = null;
        let entries = [];
        let nextId = 1;
        let statusTimer = null;

        /**
         * Write a sentence to the Compare view's live region.
         * @param {string} text - The sentence.
         * @param {boolean} [later] - Wait until the input settles, for typing.
         */
        function announce (text, later) {
            clearTimeout(statusTimer);
            const status = byId('icCompareStatus');
            if (!status) {
                return;
            }
            if (later) {
                statusTimer = setTimeout(() => {
                    status.textContent = text;
                }, STATUS_DELAY_MS);
            } else {
                status.textContent = text;
            }
        }

        /**
         * A lever applied to the current plan.
         * @param {string} key - A lever key.
         * @returns {{key: string, field: string, inputs: object}|null} The variant.
         */
        function variantOf (key) {
            return InvestmentMath.lever(plan, key, { monthlyStep: Number.isFinite(monthlyStep) ? monthlyStep : undefined });
        }

        /**
         * A lever's button label.
         * @param {string} key - A lever key.
         * @returns {string} E.g. +$100 a month.
         */
        function leverLabel (key) {
            const variant = variantOf(key);
            const lever = variant ? LEVERS[variant.field] : null;
            if (!lever) {
                return '';
            }
            const step = Number.isFinite(monthlyStep) ? monthlyStep : InvestmentMath.LEVER_STEPS.monthly;
            return PFI18n.t(`InvestmentCalculator.Js.Lever.${lever.name}`, lever.lever({ deps, monthlyStep: step, variant }));
        }

        /**
         * A variant's name from the one input it changes, e.g. "$600 a month".
         * @param {string|null} field - The changed input, or null.
         * @param {object} inputs - The variant's inputs.
         * @returns {string} The name.
         */
        function variantName (field, inputs) {
            const lever = field ? LEVERS[field] : null;
            return lever ? PFI18n.t(`InvestmentCalculator.Js.Variant.${lever.name}`, lever.variant(deps, inputs)) : '';
        }

        /**
         * The final balance of some inputs, in the money the switch shows.
         * @param {object} inputs - The inputs.
         * @returns {object} The shown projection.
         */
        function shownOf (inputs) {
            return deps.shown(InvestmentMath.project(inputs));
        }

        /**
         * The difference of a final balance from the plan's, as words: "$52,093 more than your plan".
         * @param {number} difference - Variant minus plan.
         * @returns {string} The sentence part.
         */
        function differenceText (difference) {
            const rounded = Math.round(difference);
            if (rounded === 0) {
                return PFI18n.t('InvestmentCalculator.Js.Diff.Same');
            }
            return PFI18n.t(rounded > 0 ? 'InvestmentCalculator.Js.Diff.More' : 'InvestmentCalculator.Js.Diff.Less',
                { amount: deps.money(Math.abs(rounded)) });
        }

        /**
         * A signed compact difference for a lever button: +$52K or −$40K.
         * @param {number} difference - Variant minus plan.
         * @returns {string} The text.
         */
        function signed (difference) {
            const rounded = Math.round(difference);
            if (rounded === 0) {
                return '±0';
            }
            return `${rounded > 0 ? '+' : '−'}${deps.compact(Math.abs(rounded))}`;
        }

        /**
         * The colours of the comparison's lines: the plan, then each variant slot.
         * @returns {string[]} Three colours.
         */
        function lineColours () {
            const palette = deps.colours();
            return [palette.series[0], palette.series[1], palette.series[4]];
        }

        /**
         * Show or hide the preview panel and write its sentence.
         */
        function showPreview () {
            const variant = previewKey ? variantOf(previewKey) : null;
            if (preview) {
                preview.hidden = !variant;
            }
            if (leversHost) {
                leversHost.querySelectorAll('[data-lever]').forEach((button) => {
                    button.setAttribute('aria-pressed', String(button.dataset.lever === previewKey));
                });
            }
            if (!variant) {
                return;
            }
            const base = shownOf(plan);
            const result = shownOf(variant.inputs);
            const text = byId('icPreviewText');
            if (text) {
                text.textContent = PFI18n.t('InvestmentCalculator.Js.Preview.Text', {
                    name: variantName(variant.field, variant.inputs),
                    final: deps.money(result.final),
                    years: PFI18n.plural('InvestmentCalculator.Js.Years', variant.inputs.years),
                    difference: differenceText(result.final - base.final)
                });
            }
            // A full comparison already shows three lines: the preview is not drawn and cannot be added.
            const full = entries.length >= MAX_VARIANTS;
            const add = byId('icPreviewAdd');
            if (add) {
                add.disabled = full;
            }
            const fullNote = byId('icPreviewFull');
            if (fullNote) {
                fullNote.hidden = !full;
            }
        }

        /**
         * Write each lever's label and its difference from the plan.
         */
        function showLevers () {
            if (!leversHost) {
                return;
            }
            const base = shownOf(plan).final;
            leversHost.querySelectorAll('[data-lever]').forEach((button) => {
                const key = button.dataset.lever;
                const variant = variantOf(key);
                if (!variant) {
                    return;
                }
                const difference = shownOf(variant.inputs).final - base;
                const label = button.querySelector('.ic-lever__label');
                const diff = button.querySelector('.ic-lever__diff');
                if (label) {
                    label.textContent = leverLabel(key);
                }
                if (diff) {
                    diff.textContent = signed(difference);
                    diff.classList.toggle('ic-lever__diff--up', Math.round(difference) > 0);
                    diff.classList.toggle('ic-lever__diff--down', Math.round(difference) < 0);
                }
                button.title = differenceText(difference);
            });
        }

        /**
         * Build one comparison row: swatch, name, final balance, difference, the editor and the remove button.
         * @param {object|null} entry - The entry, or null for the plan.
         * @param {number} slot - The line's position (0 = the plan).
         * @returns {HTMLLIElement} The row.
         */
        function rowFor (entry, slot) {
            const li = document.createElement('li');
            li.className = `ic-cmp__item ic-cmp__item--${slot}`;
            const swatch = document.createElement('span');
            swatch.className = 'ic-cmp__swatch';
            swatch.setAttribute('aria-hidden', 'true');
            const body = document.createElement('div');
            body.className = 'ic-cmp__body';
            const name = document.createElement('strong');
            name.className = 'ic-cmp__name';
            const final = document.createElement('span');
            final.className = 'ic-cmp__final';
            body.append(name, final);
            li.append(swatch, body);
            if (!entry) {
                return li;
            }
            li.dataset.entryId = String(entry.id);
            const diff = document.createElement('span');
            diff.className = 'ic-cmp__diff';
            body.appendChild(diff);

            const editor = entry.field ? LEVERS[entry.field] : null;
            if (editor) {
                const id = `icCmpEdit${entry.id}`;
                const wrap = document.createElement('div');
                wrap.className = 'ic-cmp__edit';
                const label = document.createElement('label');
                label.htmlFor = id;
                label.textContent = PFI18n.t(`InvestmentCalculator.Js.Edit.${editor.name}`);
                const input = document.createElement('input');
                input.type = 'number';
                input.id = id;
                input.className = 'form-control form-control-sm ic-cmp__input';
                input.inputMode = 'decimal';
                input.step = editor.step;
                input.addEventListener('input', () => {
                    const value = parseFloat(input.value);
                    if (!Number.isFinite(value)) {
                        return;
                    }
                    entry.value = withinEditor(entry.field, value);
                    refresh();
                    announce(`${entryName(entry)}: ${final.textContent}, ${diff.textContent}`, true);
                });
                // On leaving the field, show the value the variant uses.
                input.addEventListener('change', () => {
                    input.value = String(inputsOf(entry)[entry.field]);
                });
                wrap.append(label, input);
                body.appendChild(wrap);
            }

            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'ic-cmp__remove';
            const cross = document.createElement('span');
            cross.setAttribute('aria-hidden', 'true');
            cross.textContent = '×';
            remove.appendChild(cross);
            remove.addEventListener('click', () => removeEntry(entry.id));
            li.appendChild(remove);
            return li;
        }

        /**
         * Rebuild the comparison rows (after an entry is added or removed); refresh() fills them.
         */
        function buildList () {
            if (!list) {
                return;
            }
            list.replaceChildren(rowFor(null, 0), ...entries.map((entry, i) => rowFor(entry, i + 1)));
            entries.forEach((entry) => {
                const input = byId(`icCmpEdit${entry.id}`);
                if (input) {
                    input.value = String(inputsOf(entry)[entry.field]);
                }
            });
        }

        /**
         * Fill the comparison rows' text and the room hint from the current plan.
         */
        function showList () {
            if (!list) {
                return;
            }
            const base = shownOf(plan);
            const rows = list.querySelectorAll('.ic-cmp__item');
            [{ name: PFI18n.t('InvestmentCalculator.Js.Compare.Plan'), inputs: plan }].concat(entries).forEach((entry, i) => {
                const row = rows[i];
                if (!row) {
                    return;
                }
                const result = i === 0 ? base : shownOf(inputsOf(entry));
                const name = i === 0 ? entry.name : entryName(entry);
                row.querySelector('.ic-cmp__name').textContent = name;
                row.querySelector('.ic-cmp__final').textContent = deps.money(result.final);
                const diff = row.querySelector('.ic-cmp__diff');
                if (diff) {
                    const difference = Math.round(result.final - base.final);
                    diff.textContent = differenceText(difference);
                    diff.classList.toggle('ic-cmp__diff--up', difference > 0);
                    diff.classList.toggle('ic-cmp__diff--down', difference < 0);
                }
                const input = row.querySelector('.ic-cmp__input');
                if (input) {
                    const editor = LEVERS[entry.field];
                    input.min = String(editor.min(deps, plan));
                    const max = editor.max(deps, plan);
                    input.max = Number.isFinite(max) ? String(max) : '';
                }
                const remove = row.querySelector('.ic-cmp__remove');
                if (remove) {
                    remove.setAttribute('aria-label', PFI18n.t('InvestmentCalculator.Js.Compare.Remove', { name }));
                }
            });
            const keys = ['Room', 'RoomOne', 'Full'];
            const room = byId('icCmpRoom');
            if (room) {
                room.textContent = PFI18n.t(`InvestmentCalculator.Js.Compare.${keys[Math.min(entries.length, MAX_VARIANTS)]}`);
            }
        }

        /**
         * An entry's name: its own, or the one its changed input gives it.
         * @param {object} entry - The entry.
         * @returns {string} The name.
         */
        function entryName (entry) {
            return entry.name || variantName(entry.field, inputsOf(entry));
        }

        /**
         * An entry's inputs: the current plan with its one change, or its own fixed inputs.
         * @param {object} entry - The entry.
         * @returns {object} The normalized inputs.
         */
        function inputsOf (entry) {
            return entry.field
                ? InvestmentMath.normalize(Object.assign({}, plan, { [entry.field]: withinEditor(entry.field, entry.value) }))
                : entry.inputs;
        }

        /**
         * A lever input's value within its editor's bounds for the current plan (e.g. a stop year after the delay).
         * @param {string} field - The changed input.
         * @param {number} value - The value.
         * @returns {number} The bounded value.
         */
        function withinEditor (field, value) {
            const lever = LEVERS[field];
            const min = lever.min(deps, plan);
            const max = lever.max(deps, plan);
            return Math.max(min, Number.isFinite(max) ? Math.min(value, max) : value);
        }

        /**
         * Refresh the levers, the preview, the rows and the chart.
         */
        function refresh () {
            if (!plan) {
                return;
            }
            showLevers();
            showPreview();
            showList();
            draw();
        }

        /**
         * Add an entry to the comparison.
         * Either a change to the plan, which follows the plan and whose one input can be edited on the comparison
         * ({field, value}: a what-if), or fixed inputs with a name ({inputs, name}: e.g. a saved scenario, #592).
         * @param {{field?: string, value?: number, inputs?: object, name?: string, source?: string}} entry - The change
         *     (an InvestmentMath input key and its value) or the fixed inputs; a name (by default the change's); where
         *     it came from ('lever', or e.g. 'saved').
         * @returns {object|null} The added entry, or null when the comparison is full or the entry is neither.
         */
        function add (entry) {
            const change = entry && entry.field && LEVERS[entry.field] && Number.isFinite(Number(entry.value));
            if (entries.length >= MAX_VARIANTS || !entry || (!change && !entry.inputs)) {
                return null;
            }
            const added = {
                id: nextId++,
                field: change ? entry.field : null,
                value: change ? Number(entry.value) : null,
                inputs: change ? null : InvestmentMath.normalize(entry.inputs),
                name: entry.name || '',
                source: entry.source || (change ? 'lever' : 'other')
            };
            entries.push(added);
            buildList();
            refresh();
            return added;
        }

        /**
         * Remove an entry and move focus somewhere sensible.
         * @param {number} id - The entry id.
         */
        function removeEntry (id) {
            const entry = entries.find((candidate) => candidate.id === id);
            if (!entry) {
                return;
            }
            const name = entryName(entry);
            entries = entries.filter((candidate) => candidate.id !== id);
            buildList();
            refresh();
            const focus = list && list.querySelector('.ic-cmp__remove');
            const fallback = leversHost && leversHost.querySelector('[data-lever]');
            (focus || fallback || root).focus();
            announce(PFI18n.t('InvestmentCalculator.Js.Compare.Removed', { name }));
        }

        /**
         * Draw the comparison chart: the plan, the variants and the previewed lever, dashed.
         */
        function draw () {
            const host = byId('icCompareChart');
            if (!plan || !host || host.closest('[hidden]')) {
                return;
            }
            const colours = lineColours();
            const palette = deps.colours();
            const values = (result) => result.rows.map((row) => ({ x: row.year, y: row.balance }));
            const lines = [{ name: PFI18n.t('InvestmentCalculator.Js.Compare.Plan'), result: shownOf(plan) }]
                .concat(entries.map((entry) => ({ name: entryName(entry), result: shownOf(inputsOf(entry)) })));
            const series = lines.map((line, i) => ({ name: line.name, color: colours[i], width: i === 0 ? 3 : 2.5, values: values(line.result) }));
            // At most three lines: a full comparison draws no preview (the preview panel says so).
            const variant = previewKey && entries.length < MAX_VARIANTS ? variantOf(previewKey) : null;
            if (variant) {
                series.push({
                    name: PFI18n.t('InvestmentCalculator.Js.Preview.Series', { name: variantName(variant.field, variant.inputs) }),
                    // The colour it would take in the comparison.
                    color: colours[entries.length + 1],
                    dash: '6 4',
                    width: 2.5,
                    values: values(shownOf(variant.inputs))
                });
            }
            const years = Math.max(...lines.map((line) => line.result.rows.length - 1));
            InvestmentCharts.line(host, Object.assign({ palette, formatY: deps.compact, formatValue: deps.money }, deps.yearFormats(), {
                label: [PFI18n.t('InvestmentCalculator.Js.Chart.Compare', { years: PFI18n.plural('InvestmentCalculator.Js.Years', years) })]
                    .concat(lines.map((line) => PFI18n.t('InvestmentCalculator.Js.Chart.CompareLine', {
                        name: line.name, final: deps.money(line.result.final)
                    }))).join(' '),
                series
            }));
        }

        /**
         * Take a new plan: recompute the levers' differences, the preview and the comparison.
         * @param {object} inputs - The normalized plan.
         */
        function update (inputs) {
            plan = inputs;
            showLevers();
            showPreview();
            showList();
        }

        /**
         * The view the page shows.
         * @returns {string} 'overview' or 'compare'.
         */
        function view () {
            const tab = root.querySelector('[data-ic-view][aria-selected="true"]');
            return tab ? tab.dataset.icView : 'overview';
        }

        /**
         * Show a view: select its tab, show its panel, keep ?view= in the query string.
         * @param {string} name - 'overview' or 'compare'.
         * @param {boolean} [focus] - Move focus to the tab (keyboard switching).
         */
        function select (name, focus) {
            root.querySelectorAll('[data-ic-view]').forEach((tab) => {
                const on = tab.dataset.icView === name;
                tab.setAttribute('aria-selected', String(on));
                tab.tabIndex = on ? 0 : -1;
                const panel = byId(tab.getAttribute('aria-controls'));
                if (panel) {
                    panel.hidden = !on;
                }
                if (on && focus) {
                    tab.focus();
                }
            });
            if (window.history && window.history.replaceState) {
                const url = new URL(window.location.href);
                if (name === 'compare') {
                    url.searchParams.set('view', 'compare');
                } else {
                    url.searchParams.delete('view');
                }
                window.history.replaceState(window.history.state, '', url);
            }
            deps.onView(name);
        }

        /**
         * Wire the view tabs: click, and the arrow keys, Home and End between them.
         */
        function bindViews () {
            const tabs = Array.from(root.querySelectorAll('[data-ic-view]'));
            tabs.forEach((tab, i) => {
                tab.addEventListener('click', () => select(tab.dataset.icView));
                tab.addEventListener('keydown', (event) => {
                    const moves = { ArrowRight: i + 1, ArrowLeft: i - 1, Home: 0, End: tabs.length - 1 };
                    if (!(event.key in moves)) {
                        return;
                    }
                    event.preventDefault();
                    const next = tabs[(moves[event.key] + tabs.length) % tabs.length];
                    select(next.dataset.icView, true);
                });
            });
        }

        /**
         * Wire the levers and the preview's Apply and Add to comparison.
         */
        function bindLevers () {
            if (leversHost) {
                leversHost.querySelectorAll('[data-lever]').forEach((button) => {
                    button.addEventListener('click', () => {
                        previewKey = previewKey === button.dataset.lever ? null : button.dataset.lever;
                        refresh();
                        const text = byId('icPreviewText');
                        announce(previewKey && text ? text.textContent : '');
                    });
                });
            }
            const focusLever = (key) => {
                const button = leversHost && leversHost.querySelector(`[data-lever="${key}"]`);
                if (button) {
                    button.focus();
                }
            };
            const apply = byId('icPreviewApply');
            if (apply) {
                apply.addEventListener('click', () => {
                    const variant = previewKey ? variantOf(previewKey) : null;
                    if (!variant) {
                        return;
                    }
                    const name = variantName(variant.field, variant.inputs);
                    previewKey = null;
                    deps.applyPlan(variant.inputs);
                    refresh();
                    focusLever(variant.key);
                    announce(PFI18n.t('InvestmentCalculator.Js.Compare.Applied', { name, final: deps.money(shownOf(plan).final) }));
                });
            }
            const addButton = byId('icPreviewAdd');
            if (addButton) {
                addButton.addEventListener('click', () => {
                    const variant = previewKey ? variantOf(previewKey) : null;
                    if (!variant) {
                        return;
                    }
                    const key = variant.key;
                    previewKey = null;
                    const added = add({ field: variant.field, value: variant.inputs[variant.field], source: 'lever' });
                    if (!added) {
                        refresh();
                        return;
                    }
                    focusLever(key);
                    announce(PFI18n.t('InvestmentCalculator.Js.Compare.Added', { name: entryName(added) }));
                });
            }
        }

        bindViews();
        bindLevers();
        buildList();

        return { update, draw, add, view };
    }

    window.InvestmentCompare = Object.freeze({ create, MAX_VARIANTS });
})();
