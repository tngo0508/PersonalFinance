/* global PFI18n, ChartPalette, InvestmentMath, InvestmentCharts */
/**
 * Investment Calculator page (#587): reads the slider rail, projects the plan with investment-math.js and hands the
 * result to the summary bar and the Overview tiles (investment-charts.js). Every amount is in the Reporting Currency
 * the view names on [data-investment-calculator]. Nothing is sent to the server.
 *
 * The Today's money switch shows the bar's numbers, the tiles' text, the donut and the year-by-year table in today's
 * money (the projection's real half); the line and bar charts stay in future money, with today's money dashed.
 *
 * The Reach a goal tile (#590) holds its own goal amount and solves it with InvestmentMath.solveGoal, in today's money
 * when the switch is on; the balance chart draws the goal as a horizontal line with a marker at the year it is reached.
 */
(function () {
    'use strict';

    const KEYS = ['initial', 'monthly', 'rate', 'years', 'increase', 'fee', 'inflation'];
    const STATUS_DELAY_MS = 600;
    /** Where the year-by-year table starts open: wide enough for its five columns without a squeeze. */
    const WIDE_TABLE_QUERY = '(min-width: 48em)';

    let root = null;
    let currency = 'USD';
    let projection = null;
    let frame = null;
    let statusTimer = null;
    let todays = false;
    /** The goal tile's last answer (InvestmentMath.solveGoal) and target, for the balance chart. */
    let goal = null;
    let goalTarget = 0;
    /** Set by the goal tile's own controls, so only they announce its answer. */
    let announceGoal = false;

    /**
     * The element with this id, or null.
     * @param {string} id - The element id.
     * @returns {HTMLElement|null} The element.
     */
    function byId (id) {
        return document.getElementById(id);
    }

    /**
     * Set an element's text when it exists.
     * @param {string} id - The element id.
     * @param {string} text - The text.
     */
    function setText (id, text) {
        const el = byId(id);
        if (el) {
            el.textContent = text;
        }
    }

    /**
     * The field id stem of an input key: initial → Initial.
     * @param {string} key - The input key.
     * @returns {string} The stem used in the element ids.
     */
    function stem (key) {
        return key.charAt(0).toUpperCase() + key.slice(1);
    }

    /**
     * An amount as whole money in the Reporting Currency.
     * @param {number} amount - The amount.
     * @returns {string} E.g. $300,851.
     */
    function money (amount) {
        return PFI18n.formatMoney(amount, currency, { whole: true });
    }

    /**
     * An amount in compact form for chart axes.
     * @param {number} amount - The amount.
     * @returns {string} E.g. $300K.
     */
    function compact (amount) {
        return PFI18n.formatCompactMoney(amount, currency);
    }

    /**
     * A rate in percent.
     * @param {number} value - The rate, e.g. 7.5.
     * @returns {string} E.g. 7.5%.
     */
    function percent (value) {
        return `${PFI18n.formatNumber(value, { maximumFractionDigits: 2 })}%`;
    }

    /**
     * How an input's value reads beside its label.
     * @param {string} key - The input key.
     * @param {number} value - The value.
     * @returns {string} The display text.
     */
    function display (key, value) {
        if (key === 'initial' || key === 'monthly') {
            return money(value);
        }
        if (key === 'years') {
            return PFI18n.plural('InvestmentCalculator.Js.Years', value);
        }
        return percent(value);
    }

    /**
     * The plan as the number fields hold it, normalized to the math module's bounds.
     * @returns {object} The inputs.
     */
    function readInputs () {
        const raw = {};
        KEYS.forEach((key) => {
            const field = byId(`ic${stem(key)}`);
            raw[key] = field ? parseFloat(field.value) : NaN;
        });
        const plan = InvestmentMath.normalize(raw);
        ['initial', 'monthly'].forEach((key) => {
            const field = byId(`ic${stem(key)}`);
            const max = field ? parseFloat(field.max) : NaN;
            if (Number.isFinite(max)) {
                plan[key] = Math.min(plan[key], max);
            }
        });
        return plan;
    }

    /**
     * Update one input's label value and slider text, and the presets' pressed state for the rate.
     * @param {string} key - The input key.
     * @param {number} value - The normalized value.
     */
    function showValue (key, value) {
        const text = display(key, value);
        setText(`ic${stem(key)}Value`, text);
        const range = byId(`ic${stem(key)}Range`);
        if (range) {
            range.setAttribute('aria-valuetext', text);
        }
        if (key === 'rate') {
            root.querySelectorAll('[data-rate-preset]').forEach((button) => {
                button.setAttribute('aria-pressed', String(parseFloat(button.dataset.ratePreset) === value));
            });
        }
    }

    /**
     * The colours of the page's series from the chart palette.
     * @returns {object} The palette plus balance, contributed and growth.
     */
    function colours () {
        const palette = ChartPalette.read();
        return Object.assign(palette, {
            balance: palette.series[0], contributed: palette.series[1], growth: palette.series[2], goal: palette.series[4]
        });
    }

    /**
     * The year labels for the charts' axis and readout.
     * @returns {{formatX: Function, titleX: Function}} The formatters.
     */
    function yearFormats () {
        return {
            formatX: (year) => (year === 0
                ? PFI18n.t('InvestmentCalculator.Js.Axis.Now')
                : PFI18n.t('InvestmentCalculator.Js.Axis.Year', { year: PFI18n.formatNumber(year) })),
            titleX: (year) => (year === 0
                ? PFI18n.t('InvestmentCalculator.Js.Tooltip.Today')
                : PFI18n.t('InvestmentCalculator.Js.Tooltip.Year', { year: PFI18n.formatNumber(year) }))
        };
    }

    /**
     * The projection as the Today's money switch shows it: the real half when on.
     * @param {object} p - The projection.
     * @returns {object} rows, final, contributed, growth, noFee, feeCost in the chosen money.
     */
    function shown (p) {
        return todays ? p.real : p;
    }

    /**
     * The goal amount as its field holds it: at least 0 and at most the field's max.
     * @returns {number} The target.
     */
    function readTarget () {
        const field = byId('icGoal');
        const value = field ? parseFloat(field.value) : NaN;
        const max = field ? parseFloat(field.max) : NaN;
        const target = Number.isFinite(value) ? Math.max(0, value) : 0;
        return Number.isFinite(max) ? Math.min(target, max) : target;
    }

    /**
     * The goal tile's chosen question: 'monthly' (How much a month?) or 'years' (How long?).
     * @returns {string} The mode.
     */
    function goalMode () {
        const years = byId('icGoalModeYears');
        return years && years.checked ? 'years' : 'monthly';
    }

    /**
     * The sentence the balance chart's description adds about the goal.
     * @returns {string} The sentence, or empty without a goal.
     */
    function goalSentence () {
        if (!goal || goalTarget <= 0) {
            return '';
        }
        return goal.reachedYear === null
            ? PFI18n.t('InvestmentCalculator.Js.Chart.GoalNotReached', { target: money(goalTarget) })
            : PFI18n.t('InvestmentCalculator.Js.Chart.GoalReached', {
                target: money(goalTarget), year: PFI18n.formatNumber(goal.reachedYear)
            });
    }

    /**
     * Solve the goal for the plan and fill the goal tile: the answer, the progress bar, the today's-money note and
     * the "Use this amount" button.
     * @param {object} plan - The normalized inputs.
     * @returns {string} The answer as one sentence pair, for the goal's live region.
     */
    function updateGoal (plan) {
        goalTarget = readTarget();
        goal = InvestmentMath.solveGoal(plan, { target: goalTarget, mode: goalMode(), todays });
        const target = money(goalTarget);
        const yearsText = (count) => PFI18n.plural('InvestmentCalculator.Js.Years', count);
        setText('icGoalValue', target);

        const monthlyField = byId('icMonthly');
        const monthlyMax = monthlyField ? parseFloat(monthlyField.max) : NaN;
        const tooHigh = goal.kind === 'monthly' && Number.isFinite(monthlyMax) && goal.monthly > monthlyMax;
        let headline = '';
        let detail = '';
        if (goal.kind === 'monthly') {
            headline = PFI18n.t('InvestmentCalculator.Js.Goal.Monthly', { amount: money(goal.monthly) });
            detail = PFI18n.t('InvestmentCalculator.Js.Goal.MonthlyDetail', { target, years: yearsText(goal.year) });
            if (tooHigh) {
                detail = `${detail} ${PFI18n.t('InvestmentCalculator.Js.Goal.TooHigh', { max: money(monthlyMax) })}`;
            }
        } else if (goal.kind === 'years') {
            headline = yearsText(goal.year);
            detail = PFI18n.t('InvestmentCalculator.Js.Goal.YearsDetail', { target, monthly: money(plan.monthly) });
        } else if (goal.kind === 'already') {
            headline = PFI18n.t('InvestmentCalculator.Js.Goal.Already');
            detail = goal.year === 0
                ? PFI18n.t('InvestmentCalculator.Js.Goal.AlreadyNow', { target })
                : PFI18n.t('InvestmentCalculator.Js.Goal.AlreadyYear', { target, year: PFI18n.formatNumber(goal.year) });
        } else {
            const horizon = InvestmentMath.LIMITS.years.max;
            headline = PFI18n.t('InvestmentCalculator.Js.Goal.Unreachable', { years: yearsText(horizon) });
            detail = PFI18n.t('InvestmentCalculator.Js.Goal.Shortfall', {
                monthly: money(plan.monthly), amount: money(goal.shortfall), year: PFI18n.formatNumber(horizon)
            });
        }
        setText('icGoalAnswer', headline);
        setText('icGoalDetail', detail);

        const filled = Math.max(0, Math.min(1, goal.progress));
        const asPercent = (value) => PFI18n.formatNumber(value, { style: 'percent', maximumFractionDigits: 0 });
        const bar = byId('icGoalProgress');
        if (bar) {
            bar.setAttribute('aria-valuenow', String(Math.round(filled * 100)));
            bar.setAttribute('aria-valuetext', PFI18n.t('InvestmentCalculator.Js.Goal.ProgressText', { percent: asPercent(goal.progress) }));
        }
        const fill = byId('icGoalProgressFill');
        if (fill) {
            fill.style.width = `${filled * 100}%`;
        }
        setText('icGoalProgressLabel', goal.reachedYear === null
            ? PFI18n.t('InvestmentCalculator.Js.Goal.Progress', { percent: asPercent(goal.progress), years: yearsText(plan.years) })
            : PFI18n.t(goal.reachedYear === 0 ? 'InvestmentCalculator.Js.Goal.ProgressNow' : 'InvestmentCalculator.Js.Goal.ProgressReached',
                { year: PFI18n.formatNumber(goal.reachedYear) }));

        const note = byId('icGoalNote');
        if (note) {
            const year = goal.kind === 'years' ? goal.year : plan.years;
            note.hidden = !todays || goalTarget <= 0;
            note.textContent = note.hidden
                ? ''
                : PFI18n.t('InvestmentCalculator.Js.Goal.Today', {
                    amount: money(goalTarget * Math.pow(1 + plan.inflation / 100, year)), year: PFI18n.formatNumber(year)
                });
        }

        const use = byId('icGoalUse');
        if (use) {
            use.hidden = goal.kind !== 'monthly' || tooHigh || goal.monthly === plan.monthly;
            use.textContent = goal.kind === 'monthly'
                ? PFI18n.t('InvestmentCalculator.Js.Goal.Use', { amount: money(goal.monthly) })
                : '';
        }
        return `${headline}. ${detail}`;
    }

    /**
     * Fill the year-by-year table: the chosen money in the first columns, the other money in the last.
     */
    function drawTable () {
        const body = byId('icYearRows');
        if (!projection || !body) {
            return;
        }
        const view = shown(projection);
        const other = todays ? projection.rows : projection.real.rows;
        const { titleX } = yearFormats();
        setText('icYearCaption', PFI18n.t(todays ? 'InvestmentCalculator.Js.Table.CaptionToday' : 'InvestmentCalculator.Js.Table.Caption'));
        setText('icYearOther', PFI18n.t(todays ? 'InvestmentCalculator.Js.Money.Future' : 'InvestmentCalculator.Js.Money.Today'));
        body.replaceChildren(...view.rows.map((row, i) => {
            const tr = document.createElement('tr');
            const th = document.createElement('th');
            th.scope = 'row';
            th.textContent = titleX(row.year);
            tr.appendChild(th);
            [row.contributedYear, row.growthYear, row.balance, other[i].balance].forEach((amount, column) => {
                const td = document.createElement('td');
                td.textContent = money(amount);
                if (column === 2) {
                    td.className = 'ic-table__balance';
                }
                tr.appendChild(td);
            });
            return tr;
        }));
    }

    /**
     * Draw the charts from the current projection.
     */
    function drawCharts () {
        if (!projection) {
            return;
        }
        const p = projection;
        const view = shown(p);
        const c = colours();
        const years = p.rows.length - 1;
        const common = Object.assign({ palette: c, formatY: compact, formatValue: money }, yearFormats());
        const name = (key) => PFI18n.t(`InvestmentCalculator.Js.Series.${key}`);

        InvestmentCharts.line(byId('icBalanceChart'), Object.assign({}, common, {
            label: [
                PFI18n.t('InvestmentCalculator.Js.Chart.BalanceToday', {
                    start: money(p.rows[0].balance), final: money(p.final), years: PFI18n.formatNumber(years),
                    real: money(p.real.final), contributed: money(p.contributed)
                }),
                goalSentence()
            ].join(' ').trim(),
            series: [
                { name: name('Balance'), color: c.balance, area: true, width: 3, values: p.rows.map((r) => ({ x: r.year, y: r.balance })) },
                { name: name('PutIn'), color: c.contributed, values: p.rows.map((r) => ({ x: r.year, y: r.contributed })) },
                { name: name('TodaysMoney'), color: c.balance, dash: '6 4', width: 2, values: p.real.rows.map((r) => ({ x: r.year, y: r.balance })) }
            ],
            // With Today's money on, the goal is today's money: its line reads against the dashed line.
            goal: goalTarget > 0 ? { y: goalTarget, color: c.goal, label: name('Goal') } : null,
            markers: [
                p.milestoneYear === null
                    ? null
                    : { x: p.milestoneYear, color: c.growth, label: PFI18n.t('InvestmentCalculator.Js.Milestone.Marker') },
                !goal || goal.reachedYear === null || goalTarget <= 0
                    ? null
                    : { x: goal.reachedYear, color: c.goal, label: PFI18n.t('InvestmentCalculator.Js.Goal.Marker') }
            ].filter(Boolean)
        }));

        InvestmentCharts.line(byId('icFeeChart'), Object.assign({}, common, {
            heightEm: 11,
            label: PFI18n.t('InvestmentCalculator.Js.Chart.Fee', {
                years: PFI18n.formatNumber(years), withFees: money(p.final), withoutFees: money(p.noFee)
            }),
            // The no-fee line is dashed and drawn last, so a small fee's gap still shows above the balance.
            series: [
                { name: name('WithFees'), color: c.balance, area: true, values: p.rows.map((r) => ({ x: r.year, y: r.balance })) },
                { name: name('WithoutFees'), color: c.growth, dash: '6 4', values: p.rows.map((r) => ({ x: r.year, y: r.noFee })) }
            ]
        }));

        const growth = Math.max(0, view.growth);
        InvestmentCharts.donut(byId('icDonut'), {
            palette: c,
            centre: compact(view.final),
            label: PFI18n.t('InvestmentCalculator.Js.Chart.Donut', { contributed: money(view.contributed), growth: money(growth) }),
            parts: [
                { name: name('PutIn'), value: view.contributed, color: c.contributed },
                { name: name('Growth'), value: growth, color: c.growth }
            ]
        });

        const last = p.rows[years];
        InvestmentCharts.bars(byId('icBarsChart'), {
            palette: c,
            formatY: compact,
            formatX: (year) => PFI18n.formatNumber(year),
            label: PFI18n.t('InvestmentCalculator.Js.Chart.Bars', {
                years: PFI18n.formatNumber(years), contributed: money(last.contributedYear), growth: money(last.growthYear)
            }),
            bars: p.rows.slice(1).map((r) => ({
                x: r.year,
                title: PFI18n.t('InvestmentCalculator.Js.Bars.Year', {
                    year: PFI18n.formatNumber(r.year), contributed: money(r.contributedYear), growth: money(r.growthYear)
                }),
                parts: [{ value: r.contributedYear, color: c.contributed }, { value: r.growthYear, color: c.growth }]
            }))
        });
    }

    /**
     * Schedule a chart redraw on the next frame, once however many changes arrive.
     */
    function scheduleCharts () {
        if (frame) {
            cancelAnimationFrame(frame);
        }
        frame = requestAnimationFrame(() => {
            frame = null;
            drawCharts();
            drawTable();
        });
    }

    /**
     * Project the plan and update the summary bar, the tiles' text and the charts.
     */
    function update () {
        const plan = readInputs();
        KEYS.forEach((key) => showValue(key, plan[key]));
        projection = InvestmentMath.project(plan);
        const p = shown(projection);
        const label = PFI18n.plural(todays ? 'InvestmentCalculator.Js.Bar.InYearsToday' : 'InvestmentCalculator.Js.Bar.InYears', plan.years);

        setText('icFinalLabel', label);
        setText('icFinal', money(p.final));
        setText('icContributed', money(p.contributed));
        setText('icGrowth', money(p.growth));
        setText('icOtherLabel', PFI18n.t(todays ? 'InvestmentCalculator.Js.Money.Future' : 'InvestmentCalculator.Js.Money.Today'));
        setText('icOther', money(todays ? projection.final : projection.real.final));

        const milestone = projection.milestoneYear;
        setText('icMilestone', milestone === null
            ? PFI18n.t('InvestmentCalculator.Js.Milestone.NotYet')
            : PFI18n.t('InvestmentCalculator.Js.Milestone.Reached', { year: PFI18n.formatNumber(milestone) }));

        const growth = Math.max(0, p.growth);
        const whole = p.contributed + growth;
        const share = (part) => PFI18n.formatNumber(whole > 0 ? part / whole : 0, { style: 'percent', maximumFractionDigits: 0 });
        setText('icSplitContributed', PFI18n.t('InvestmentCalculator.Js.Split.Value', { amount: money(p.contributed), percent: share(p.contributed) }));
        setText('icSplitGrowth', PFI18n.t('InvestmentCalculator.Js.Split.Value', { amount: money(growth), percent: share(growth) }));

        setText('icFeeCost', money(p.feeCost));
        setText('icFeeNote', plan.fee > 0
            ? PFI18n.t('InvestmentCalculator.Js.Fee.Note', { fee: percent(plan.fee), amount: money(p.noFee) })
            : PFI18n.t('InvestmentCalculator.Js.Fee.None'));

        const goalText = updateGoal(plan);

        // The goal tile's own controls announce its answer; everything else announces the plan.
        const goalOnly = announceGoal;
        announceGoal = false;
        clearTimeout(statusTimer);
        statusTimer = setTimeout(() => {
            if (goalOnly) {
                setText('icGoalStatus', goalText);
                return;
            }
            setText('icStatus', PFI18n.t('InvestmentCalculator.Js.Status', {
                label, final: money(p.final), contributed: money(p.contributed), growth: money(p.growth)
            }));
        }, STATUS_DELAY_MS);

        scheduleCharts();
    }

    /**
     * Keep a slider and its number field together: moving one sets the other, then the page updates.
     * @param {string} key - The input key.
     */
    function bindField (key) {
        const range = byId(`ic${stem(key)}Range`);
        const field = byId(`ic${stem(key)}`);
        if (!range || !field) {
            return;
        }
        range.addEventListener('input', () => {
            field.value = range.value;
            update();
        });
        field.addEventListener('input', () => {
            const value = parseFloat(field.value);
            if (Number.isFinite(value)) {
                range.value = String(value);
            }
            update();
        });
        // On leaving the field, show the value the projection used (e.g. 50 years for 80).
        field.addEventListener('change', () => {
            const value = readInputs()[key];
            field.value = String(value);
            range.value = String(value);
            update();
        });
    }

    /**
     * Wire the rate presets: each sets the assumed return.
     */
    function bindPresets () {
        root.querySelectorAll('[data-rate-preset]').forEach((button) => {
            button.addEventListener('click', () => {
                const rate = button.dataset.ratePreset;
                const field = byId('icRate');
                const range = byId('icRateRange');
                if (field) {
                    field.value = rate;
                }
                if (range) {
                    range.value = rate;
                }
                update();
            });
        });
    }

    /**
     * Wire the Today's money switch, and open the year-by-year table where there is room for it.
     */
    function bindTodaysMoney () {
        const toggle = byId('icTodaysMoney');
        if (toggle) {
            todays = toggle.checked;
            toggle.addEventListener('change', () => {
                todays = toggle.checked;
                update();
            });
        }
        const yearly = byId('icYearly');
        if (yearly && window.matchMedia && window.matchMedia(WIDE_TABLE_QUERY).matches) {
            yearly.open = true;
        }
    }

    /**
     * Start the goal from one of the Account's Savings Goals (#593): its target becomes the goal amount and, for a
     * dated goal, its years to the target date become the plan's years, with the tile asking "How much a month?".
     * Nothing is written back to the Savings Goal.
     * @param {HTMLOptionElement|undefined} option - The chosen option; the empty "Choose a goal" option does nothing.
     */
    function startFromSavingsGoal (option) {
        const field = byId('icGoal');
        const target = option ? parseFloat(option.dataset.target) : NaN;
        if (!field || !Number.isFinite(target)) {
            return;
        }
        field.value = String(target);
        const years = parseInt(option.dataset.years, 10);
        const hasYears = Number.isFinite(years);
        if (hasYears) {
            const yearsField = byId('icYears');
            const yearsRange = byId('icYearsRange');
            if (yearsField) {
                yearsField.value = String(years);
            }
            if (yearsRange) {
                yearsRange.value = String(years);
            }
            const monthly = byId('icGoalModeMonthly');
            if (monthly) {
                monthly.checked = true;
            }
        }
        update();
        const values = { amount: money(readTarget()), name: option.dataset.name || option.textContent };
        if (hasYears) {
            values.years = PFI18n.plural('InvestmentCalculator.Js.Years', readInputs().years);
        }
        clearTimeout(statusTimer);
        setText('icGoalStatus', PFI18n.t(hasYears ? 'InvestmentCalculator.Js.Goal.FromYears' : 'InvestmentCalculator.Js.Goal.From', values));
    }

    /**
     * Wire the goal tile: its amount and question re-solve the goal; "Use this amount" puts the answer in the rail's
     * monthly contribution.
     */
    function bindGoal () {
        const field = byId('icGoal');
        const from = byId('icGoalFrom');
        if (field) {
            field.addEventListener('input', () => {
                // A typed amount is no longer the chosen Savings Goal's.
                if (from) {
                    from.value = '';
                }
                announceGoal = true;
                update();
            });
            // On leaving the field, show the amount the goal used (e.g. 0 for a negative entry).
            field.addEventListener('change', () => {
                field.value = String(readTarget());
                announceGoal = true;
                update();
            });
        }
        root.querySelectorAll('input[name="icGoalMode"]').forEach((radio) => {
            radio.addEventListener('change', () => {
                announceGoal = true;
                update();
            });
        });
        if (from) {
            from.addEventListener('change', () => startFromSavingsGoal(from.selectedOptions[0]));
        }
        const use = byId('icGoalUse');
        if (use) {
            use.addEventListener('click', () => {
                if (!goal || goal.kind !== 'monthly') {
                    return;
                }
                const amount = String(goal.monthly);
                const monthly = byId('icMonthly');
                const range = byId('icMonthlyRange');
                if (monthly) {
                    monthly.value = amount;
                }
                if (range) {
                    range.value = amount;
                }
                const used = PFI18n.t('InvestmentCalculator.Js.Goal.Used', { amount: money(goal.monthly) });
                update();
                // The button hides once the plan uses the amount; keep focus in the tile on the chosen question.
                const checked = root.querySelector('input[name="icGoalMode"]:checked');
                if (checked) {
                    checked.focus();
                }
                clearTimeout(statusTimer);
                setText('icGoalStatus', used);
            });
        }
    }

    /**
     * Start the page.
     */
    function init () {
        root = document.querySelector('[data-investment-calculator]');
        if (!root || typeof InvestmentMath === 'undefined' || typeof InvestmentCharts === 'undefined') {
            return;
        }
        currency = root.dataset.currency || 'USD';
        KEYS.forEach(bindField);
        bindPresets();
        bindTodaysMoney();
        bindGoal();
        update();

        const tiles = root.querySelector('.ic-tiles');
        const bar = root.querySelector('.ic-bar');
        if (typeof ResizeObserver !== 'undefined') {
            if (tiles) {
                new ResizeObserver(scheduleCharts).observe(tiles);
            }
            // The rail pins below the bar, whose height changes as its numbers wrap.
            if (bar) {
                new ResizeObserver(() => root.style.setProperty('--ic-bar-height', `${bar.offsetHeight}px`)).observe(bar);
            }
        }
        ChartPalette.subscribe(scheduleCharts);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
