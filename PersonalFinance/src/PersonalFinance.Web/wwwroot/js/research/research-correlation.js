/* global PFI18n */
/**
 * Research page, Funds tab: how closely the funds' monthly returns moved together.
 *
 * A matrix with one row and one column per fund shows the Pearson correlation of each pair's monthly returns over a
 * 1-, 3- or 5-year window (3 years by default), on a diverging scale (blue for moving together, red for moving
 * opposite, grey near zero) with the value printed in every cell. It shows the Fund Basket's funds, or every Tracked
 * Fund with "All tracked funds". Callouts name the most similar and the least correlated pair in the set. Cells are
 * focusable, and their tooltip gives the window's months.
 *
 * Every figure is precomputed by the refresh: this script only selects values from the `research-fund-returns-data`
 * JSON data island. It reads the basket from `window.ResearchBasket` and redraws on `research:basketchange`.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const DATA_ELEMENT_ID = 'research-fund-returns-data';
    const CARD_ID = 'research-correlation';
    const PROMPT_ID = 'research-correlation-prompt';
    const VIEW_ID = 'research-correlation-view';
    const CALLOUTS_ID = 'research-correlation-callouts';
    const MATRIX_ID = 'research-correlation-matrix';
    const ALL_ID = 'research-correlation-all';
    const TOOLTIP_ID = 'research-correlation-tooltip';
    const DEFAULT_YEARS = 3;
    // The dataviz reference diverging pair: blue (steps 200 to 600) and a red arm stepped to the same lightness, with
    // the reference grey midpoint. Values print in light ink from the fourth step of either arm. The colours are the
    // app tokens (site.css), the same in both themes since each cell carries its own ink (#486).
    const BLUES = [2, 3, 4, 5, 6].map((step) => `var(--app-research-blue-${step})`);
    const REDS = [1, 2, 3, 4, 5].map((step) => `var(--app-research-red-${step})`);
    const NEUTRAL = 'var(--app-research-heat-neutral)';
    const FIRST_LIGHT_INK_STEP = 4;

    /**
     * A correlation for display, with two decimals and a true minus sign.
     * @param {number} value - The correlation, -1 to 1.
     * @returns {string} E.g. "0.94" or "−0.12", in the page culture.
     */
    function formatCorrelation (value) {
        return value < 0 ? `−${page.formatFixed(Math.abs(value), 2)}` : page.formatFixed(value, 2);
    }

    /**
     * A month for display.
     * @param {string} isoDate - The month's first day, e.g. "2026-06-01".
     * @returns {string} E.g. "June 2026", in the page culture.
     */
    function formatMonth (isoDate) {
        return page.formatMonth(isoDate);
    }

    /**
     * The diverging scale step for a correlation: 0 is the neutral midpoint, 1 to 5 the steps of its arm.
     * @param {number} value - The correlation, -1 to 1.
     * @returns {number} The step.
     */
    function stepOf (value) {
        const size = Math.abs(value);
        return size < 0.05 ? 0 : Math.min(BLUES.length, Math.ceil(size / 0.2));
    }

    /**
     * The fill for a correlation.
     * @param {number} value - The correlation, -1 to 1.
     * @returns {string} The colour.
     */
    function fillOf (value) {
        const step = stepOf(value);
        if (step === 0) {
            return NEUTRAL;
        }
        return (value > 0 ? BLUES : REDS)[step - 1];
    }

    /**
     * Create an element with a class and text.
     * @param {string} tag - The tag name.
     * @param {string} [className] - The class.
     * @param {string} [text] - The text content.
     * @returns {HTMLElement} The element.
     */
    function element (tag, className, text) {
        const node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (text !== undefined) {
            node.textContent = text;
        }
        return node;
    }

    /**
     * A fund chip: its colour swatch and ticker.
     * @param {{ticker: string, slot: number}} fund - The fund.
     * @returns {HTMLElement} The chip.
     */
    function fundChip (fund) {
        const chip = element('span', 'research-overlap__fund');
        const swatch = element('span', 'research-fund-swatch');
        swatch.dataset.fundSlot = String(fund.slot);
        swatch.setAttribute('aria-hidden', 'true');
        chip.append(swatch, fund.ticker);
        return chip;
    }

    /**
     * Wire the correlation view.
     * @param {Object} data - The fund returns snapshot.
     * @returns {void}
     */
    function createView (data) {
        const card = document.getElementById(CARD_ID);
        const prompt = document.getElementById(PROMPT_ID);
        const view = document.getElementById(VIEW_ID);
        const calloutList = document.getElementById(CALLOUTS_ID);
        const matrixHost = document.getElementById(MATRIX_ID);
        const allButton = document.getElementById(ALL_ID);
        const yearButtons = Array.from(card.querySelectorAll('[data-correlation-years]'));
        const indexOf = new Map(data.funds.map((f, i) => [f.ticker, i]));
        const slotOf = (ticker) => (window.ResearchBasket && window.ResearchBasket.slotOf
            ? window.ResearchBasket.slotOf(ticker)
            : 0);
        const tooltip = page.createTooltip(document.getElementById(TOOLTIP_ID), (line, cell) => [
            line(PFI18n.t('Research.Js.Common.Pair', { a: cell.row.ticker, b: cell.column.ticker }), 'research-tooltip__name'),
            line(PFI18n.t('Research.Js.Correlation.Value', { value: formatCorrelation(cell.value) }), 'research-tooltip__value'),
            line(PFI18n.t('Research.Js.Correlation.Months',
                { count: PFI18n.formatNumber(cell.years * 12), month: formatMonth(cell.end) }), 'research-tooltip__muted')
        ]);
        // The Funds page opens on its window and on every tracked fund (data-correlation-all); the tabbed page on
        // 3 years and the basket's funds.
        const pressed = yearButtons.find((button) => button.getAttribute('aria-pressed') === 'true');
        let years = pressed ? Number(pressed.dataset.correlationYears) : DEFAULT_YEARS;
        let all = card.dataset.correlationAll === 'true';

        /**
         * The funds to show, in their order: every fund with returns, or the basket's that have them.
         * @returns {{ticker: string, slot: number, index: number}[]} The funds.
         */
        function fundsShown () {
            const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
            const tickers = all ? data.funds.map((f) => f.ticker) : basket.map((f) => f.ticker).filter((t) => indexOf.has(t));
            return tickers.map((ticker) => ({ ticker, slot: slotOf(ticker), index: indexOf.get(ticker) }));
        }

        /**
         * The window's correlation of two funds, or null when the window has none for them.
         * @param {Object} windowData - The window.
         * @param {number} a - One fund's index.
         * @param {number} b - The other's.
         * @returns {{value: number, end: string}|null} The correlation and the window's last month.
         */
        function correlationOf (windowData, a, b) {
            const low = Math.min(a, b);
            const high = Math.max(a, b);
            const found = windowData.correlations.find((c) => c.a === low && c.b === high);
            return found ? { value: found.correlation, end: found.end } : null;
        }

        /**
         * The pairs of the shown funds that have a correlation in the window.
         * @param {Object} windowData - The window.
         * @param {Object[]} funds - The shown funds.
         * @returns {{first: Object, second: Object, value: number}[]} The pairs.
         */
        function pairsOf (windowData, funds) {
            const pairs = [];
            for (let i = 0; i < funds.length; i++) {
                for (let j = i + 1; j < funds.length; j++) {
                    const found = correlationOf(windowData, funds[i].index, funds[j].index);
                    if (found) {
                        pairs.push({ first: funds[i], second: funds[j], value: found.value });
                    }
                }
            }
            return pairs;
        }

        /**
         * A matrix cell: the pair's correlation printed on its fill, focusable, with a tooltip.
         * @param {Object} cell - The row and column funds, the value, the window's years and its last month.
         * @returns {HTMLTableCellElement} The cell.
         */
        function correlationCell (cell) {
            const td = element('td', 'research-overlap__cell', formatCorrelation(cell.value));
            td.style.setProperty('--research-overlap-fill', fillOf(cell.value));
            if (stepOf(cell.value) >= FIRST_LIGHT_INK_STEP) {
                td.classList.add('research-overlap__cell--light-ink');
            }
            td.tabIndex = 0;
            td.setAttribute('aria-label', PFI18n.t('Research.Js.Correlation.CellLabel', {
                a: cell.row.ticker,
                b: cell.column.ticker,
                value: formatCorrelation(cell.value),
                count: PFI18n.formatNumber(cell.years * 12),
                month: formatMonth(cell.end)
            }));
            td.addEventListener('pointermove', (event) => tooltip.show(event, cell));
            td.addEventListener('pointerleave', tooltip.hide);
            td.addEventListener('focus', () => {
                const box = td.getBoundingClientRect();
                tooltip.show({ clientX: box.right - 8, clientY: box.bottom - 8 }, cell);
            });
            td.addEventListener('blur', tooltip.hide);
            return td;
        }

        /**
         * Draw the matrix.
         * @param {Object} windowData - The window.
         * @param {Object[]} funds - The shown funds.
         * @returns {void}
         */
        function drawMatrix (windowData, funds) {
            const table = element('table', 'research-overlap__matrix-table research-correlation__table');
            table.append(element('caption', 'visually-hidden',
                PFI18n.t('Research.Js.Correlation.Caption', { span: page.formatYears(years) })));
            const head = element('thead');
            const headRow = element('tr');
            headRow.append(element('td', 'research-overlap__corner'));
            funds.forEach((f) => {
                const th = element('th', 'research-overlap__column');
                th.scope = 'col';
                th.append(fundChip(f));
                headRow.append(th);
            });
            head.append(headRow);
            const body = element('tbody');
            funds.forEach((row) => {
                const tr = element('tr');
                const th = element('th', 'research-overlap__row');
                th.scope = 'row';
                th.append(fundChip(row));
                tr.append(th);
                funds.forEach((column) => {
                    if (column.ticker === row.ticker) {
                        const same = element('td', 'research-overlap__cell research-overlap__cell--same');
                        same.append(element('span', 'visually-hidden', PFI18n.t('Research.Js.Common.SameFund')));
                        tr.append(same);
                        return;
                    }
                    const found = correlationOf(windowData, row.index, column.index);
                    if (found) {
                        tr.append(correlationCell({ row, column, value: found.value, end: found.end, years }));
                    } else {
                        const none = element('td', 'research-overlap__cell research-overlap__cell--none', '–');
                        none.setAttribute('aria-label', PFI18n.t('Research.Js.Correlation.NoneLabel',
                            { a: row.ticker, b: column.ticker, span: page.formatYears(years) }));
                        tr.append(none);
                    }
                });
                body.append(tr);
            });
            table.append(head, body);
            matrixHost.replaceChildren(table, scaleLegend());
        }

        /**
         * The diverging scale's legend, from −1 (red) through 0 (grey) to 1 (blue).
         * @returns {HTMLElement} The legend.
         */
        function scaleLegend () {
            const legend = element('div', 'research-overlap__legend');
            legend.setAttribute('aria-hidden', 'true');
            const ramp = element('span', 'research-overlap__ramp');
            [...REDS.slice().reverse(), NEUTRAL, ...BLUES].forEach((colour) => {
                const swatch = element('span', 'research-overlap__ramp-step');
                swatch.style.setProperty('--research-overlap-fill', colour);
                ramp.append(swatch);
            });
            legend.append(element('span', null, PFI18n.t('Research.Js.Correlation.LegendOpposite', { value: formatCorrelation(-1) })),
                ramp, element('span', null, PFI18n.t('Research.Js.Correlation.LegendInStep', { value: formatCorrelation(1) })));
            return legend;
        }

        /**
         * The callouts: the most similar and the least correlated pair in the set.
         * @param {Object[]} pairs - The shown pairs.
         * @returns {{label: string, value: string, note: string}[]} The callouts.
         */
        function callouts (pairs) {
            if (pairs.length === 0) {
                return [];
            }
            const most = pairs.reduce((best, p) => (p.value > best.value ? p : best));
            const least = pairs.reduce((best, p) => (p.value < best.value ? p : best));
            const items = [{
                label: PFI18n.t('Research.Js.Correlation.Most'),
                value: formatCorrelation(most.value),
                note: PFI18n.t('Research.Js.Correlation.MostNote',
                    { a: most.first.ticker, b: most.second.ticker, span: page.formatYears(years) })
            }];
            if (pairs.length > 1) {
                items.push({
                    label: PFI18n.t('Research.Js.Correlation.Least'),
                    value: formatCorrelation(least.value),
                    note: PFI18n.t('Research.Js.Correlation.LeastNote',
                        { a: least.first.ticker, b: least.second.ticker, span: page.formatYears(years) })
                });
            }
            return items;
        }

        /**
         * Redraw for the basket, the window and the switch.
         * @returns {void}
         */
        function draw () {
            tooltip.hide();
            yearButtons.forEach((button) => button.setAttribute('aria-pressed', String(Number(button.dataset.correlationYears) === years)));
            allButton.setAttribute('aria-pressed', String(all));
            const funds = fundsShown();
            const windowData = data.windows.find((w) => w.years === years);
            const enough = funds.length >= 2 && Boolean(windowData);
            prompt.hidden = enough;
            view.hidden = !enough;
            if (!enough) {
                return;
            }
            const pairs = pairsOf(windowData, funds);
            calloutList.replaceChildren(...callouts(pairs).map((c) => {
                const li = element('li', 'research-overlap__callout');
                li.append(element('span', 'research-overlap__callout-label', c.label),
                    element('span', 'research-overlap__callout-value', c.value),
                    element('span', 'research-overlap__callout-note', c.note));
                return li;
            }));
            drawMatrix(windowData, funds);
        }

        yearButtons.forEach((button) => button.addEventListener('click', () => {
            years = Number(button.dataset.correlationYears);
            draw();
            if (page.announce) {
                page.announce(PFI18n.t('Research.Js.Correlation.Announce', { span: page.formatYears(years) }));
            }
        }));
        allButton.addEventListener('click', () => {
            all = !all;
            draw();
        });
        window.addEventListener('scroll', tooltip.hide, { passive: true });
        matrixHost.addEventListener('scroll', tooltip.hide, { passive: true });
        document.addEventListener('research:basketchange', draw);
        draw();
    }

    /**
     * Wire the view when its data and the page helpers are present.
     * @returns {void}
     */
    function init () {
        if (!page || !document.getElementById(VIEW_ID)) {
            return;
        }
        const data = page.readData(DATA_ELEMENT_ID, 'Research fund returns data');
        if (!data || !Array.isArray(data.funds) || !Array.isArray(data.windows)) {
            return;
        }
        createView(data);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
