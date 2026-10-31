/* global PFI18n, ChartPalette, InvestmentMath, InvestmentCharts */
/**
 * Investment Calculator page (#587): reads the slider rail, projects the plan with investment-math.js and hands the
 * result to the summary bar and the Overview tiles (investment-charts.js). Every amount is in the Reporting Currency
 * the view names on [data-investment-calculator]. Nothing is sent to the server.
 */
(function () {
    'use strict';

    const KEYS = ['initial', 'monthly', 'rate', 'years', 'increase', 'fee'];
    const STATUS_DELAY_MS = 600;

    let root = null;
    let currency = 'USD';
    let projection = null;
    let frame = null;
    let statusTimer = null;

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
        return Object.assign(palette, { balance: palette.series[0], contributed: palette.series[1], growth: palette.series[2] });
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
     * Draw the three charts from the current projection.
     */
    function drawCharts () {
        if (!projection) {
            return;
        }
        const p = projection;
        const c = colours();
        const years = p.rows.length - 1;
        const common = Object.assign({ palette: c, formatY: compact, formatValue: money }, yearFormats());
        const name = (key) => PFI18n.t(`InvestmentCalculator.Js.Series.${key}`);

        InvestmentCharts.line(byId('icBalanceChart'), Object.assign({}, common, {
            label: PFI18n.t('InvestmentCalculator.Js.Chart.Balance', {
                start: money(p.rows[0].balance), final: money(p.final), years: PFI18n.formatNumber(years),
                contributed: money(p.contributed)
            }),
            series: [
                { name: name('Balance'), color: c.balance, area: true, width: 3, values: p.rows.map((r) => ({ x: r.year, y: r.balance })) },
                { name: name('PutIn'), color: c.contributed, values: p.rows.map((r) => ({ x: r.year, y: r.contributed })) }
            ],
            markers: p.milestoneYear === null
                ? []
                : [{ x: p.milestoneYear, color: c.growth, label: PFI18n.t('InvestmentCalculator.Js.Milestone.Marker') }]
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

        const growth = Math.max(0, p.growth);
        InvestmentCharts.donut(byId('icDonut'), {
            palette: c,
            centre: compact(p.final),
            label: PFI18n.t('InvestmentCalculator.Js.Chart.Donut', { contributed: money(p.contributed), growth: money(growth) }),
            parts: [
                { name: name('PutIn'), value: p.contributed, color: c.contributed },
                { name: name('Growth'), value: growth, color: c.growth }
            ]
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
        });
    }

    /**
     * Project the plan and update the summary bar, the tiles' text and the charts.
     */
    function update () {
        const plan = readInputs();
        KEYS.forEach((key) => showValue(key, plan[key]));
        projection = InvestmentMath.project(plan);
        const p = projection;
        const label = PFI18n.plural('InvestmentCalculator.Js.Bar.InYears', plan.years);

        setText('icFinalLabel', label);
        setText('icFinal', money(p.final));
        setText('icContributed', money(p.contributed));
        setText('icGrowth', money(p.growth));

        setText('icMilestone', p.milestoneYear === null
            ? PFI18n.t('InvestmentCalculator.Js.Milestone.NotYet')
            : PFI18n.t('InvestmentCalculator.Js.Milestone.Reached', { year: PFI18n.formatNumber(p.milestoneYear) }));

        const growth = Math.max(0, p.growth);
        const whole = p.contributed + growth;
        const share = (part) => PFI18n.formatNumber(whole > 0 ? part / whole : 0, { style: 'percent', maximumFractionDigits: 0 });
        setText('icSplitContributed', PFI18n.t('InvestmentCalculator.Js.Split.Value', { amount: money(p.contributed), percent: share(p.contributed) }));
        setText('icSplitGrowth', PFI18n.t('InvestmentCalculator.Js.Split.Value', { amount: money(growth), percent: share(growth) }));

        setText('icFeeCost', money(p.feeCost));
        setText('icFeeNote', plan.fee > 0
            ? PFI18n.t('InvestmentCalculator.Js.Fee.Note', { fee: percent(plan.fee), amount: money(p.noFee) })
            : PFI18n.t('InvestmentCalculator.Js.Fee.None'));

        clearTimeout(statusTimer);
        statusTimer = setTimeout(() => {
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
