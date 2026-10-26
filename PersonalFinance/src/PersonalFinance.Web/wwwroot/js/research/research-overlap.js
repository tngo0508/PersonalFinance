/* global PFI18n */
/**
 * Research page, Funds tab: how much the Fund Basket's funds overlap, from each fund's latest reported holdings.
 *
 * Overlap is by weight: for each security two funds both hold, the smaller of its two weights, added up. The view is
 * a matrix with one row and one column per basket fund (the value printed in each cell, on a single-hue blue scale,
 * the count of securities in common in the tooltip only) and a "Most duplicated holdings in your basket" table:
 * holdings two or more basket funds hold, ranked by their share of the basket (the fund's share of the basket times
 * the holding's weight in the fund, added over the funds that hold it), each with a chip per fund in its fixed colour.
 * Choosing a fund's row label filters the table to the holdings that fund shares; pointing at or focusing a cell
 * highlights that pair's holdings. Callouts name the most overlapping pair, the share of the basket held by two or
 * more of its funds, and the largest duplicated holding. With fewer than two funds the view asks for another.
 *
 * The snapshot holds each pair's overlap, how much of each fund the other covers, and its 25 largest shared holdings
 * by name. It never holds a security identifier. So the table is built from those 25 per pair, and with three or more
 * funds the share held by two or more is a lower bound (the largest cover of each fund by any one other fund).
 *
 * Reads the overlap from the `research-fund-overlap-data` JSON data island and the basket from
 * `window.ResearchBasket`, redrawing on `research:basketchange`.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const DATA_ELEMENT_ID = 'research-fund-overlap-data';
    const PROMPT_ID = 'research-overlap-prompt';
    const VIEW_ID = 'research-overlap-view';
    const CALLOUTS_ID = 'research-overlap-callouts';
    const MATRIX_ID = 'research-overlap-matrix';
    const TABLE_ID = 'research-overlap-table';
    const FILTER_ID = 'research-overlap-filter';
    const FILTER_TEXT_ID = 'research-overlap-filter-text';
    const FILTER_CLEAR_ID = 'research-overlap-filter-clear';
    const TOOLTIP_ID = 'research-overlap-tooltip';
    const TABLE_ROWS = 15;
    // The dataviz reference sequential blue, steps 100 to 700; values print in dark ink up to the fourth step.
    const BLUES = ['#cde2fb', '#9ec5f4', '#6da7ec', '#3987e5', '#256abf', '#184f95', '#0d366b'];
    const FIRST_LIGHT_INK_STEP = 4;

    /**
     * A percentage with one decimal.
     * @param {number} value - The value in percent.
     * @returns {string} E.g. "44.1%".
     */
    function formatPercent (value) {
        return `${value.toFixed(1)}%`;
    }

    /**
     * The blue scale step for an overlap.
     * @param {number} value - The overlap in percent, 0 to 100.
     * @returns {number} The step index into BLUES.
     */
    function stepOf (value) {
        return Math.max(0, Math.min(BLUES.length - 1, Math.floor(value / 100 * BLUES.length)));
    }

    /**
     * A list of tickers in words.
     * @param {string[]} tickers - The tickers.
     * @returns {string} E.g. "SPY, QQQ and VTI".
     */
    function listOf (tickers) {
        return tickers.length < 2 ? tickers.join('') : `${tickers.slice(0, -1).join(', ')} and ${tickers[tickers.length - 1]}`;
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
     * The overlap of the basket's funds: its pairs, its duplicated holdings and the callout figures.
     * @param {Object} data - The fund overlap snapshot.
     * @param {Object[]} basket - The basket's funds (ticker, share, slot), in basket order.
     * @returns {{funds: Object[], missing: string[], pairs: Object[], duplicates: Object[], sharedTotal: number,
     *     sharedExact: boolean}} The model.
     */
    function model (data, basket) {
        const indexOf = new Map(data.funds.map((f, i) => [f.ticker, i]));
        const funds = basket.filter((f) => indexOf.has(f.ticker));
        const missing = basket.filter((f) => !indexOf.has(f.ticker)).map((f) => f.ticker);
        const byKey = new Map(data.pairs.map((p) => [`${p.a}:${p.b}`, p]));

        // Each pair of basket funds, oriented to basket order (first, second).
        const pairs = [];
        for (let i = 0; i < funds.length; i++) {
            for (let j = i + 1; j < funds.length; j++) {
                const a = indexOf.get(funds[i].ticker);
                const b = indexOf.get(funds[j].ticker);
                const pair = byKey.get(`${Math.min(a, b)}:${Math.max(a, b)}`);
                if (pair) {
                    const flipped = a > b;
                    pairs.push({
                        first: funds[i],
                        second: funds[j],
                        overlap: pair.overlap,
                        coveredFirst: flipped ? pair.coveredB : pair.coveredA,
                        coveredSecond: flipped ? pair.coveredA : pair.coveredB,
                        sharedCount: pair.sharedCount,
                        shared: pair.shared.map((s) => ({
                            holding: s.holding,
                            first: flipped ? s.weightB : s.weightA,
                            second: flipped ? s.weightA : s.weightB
                        }))
                    });
                }
            }
        }

        // Holdings two or more basket funds hold, with each fund's weight, from the pairs' shared lists.
        const holdings = new Map();
        pairs.forEach((pair) => {
            pair.shared.forEach((s) => {
                const entry = holdings.get(s.holding) || { name: data.holdings[s.holding], weights: new Map() };
                entry.weights.set(pair.first.ticker, s.first);
                entry.weights.set(pair.second.ticker, s.second);
                holdings.set(s.holding, entry);
            });
        });
        const duplicates = Array.from(holdings.values())
            .map((entry) => ({
                name: entry.name,
                funds: funds.filter((f) => entry.weights.has(f.ticker)),
                weights: entry.weights,
                basketPercent: funds.reduce((sum, f) => sum + (f.share / 100) * (entry.weights.get(f.ticker) || 0), 0)
            }))
            .sort((x, y) => y.basketPercent - x.basketPercent || x.name.localeCompare(y.name));

        // Each fund's share held by another basket fund: exact for two funds, the largest single cover beyond that.
        const covered = new Map(funds.map((f) => [f.ticker, 0]));
        pairs.forEach((pair) => {
            covered.set(pair.first.ticker, Math.max(covered.get(pair.first.ticker), pair.coveredFirst));
            covered.set(pair.second.ticker, Math.max(covered.get(pair.second.ticker), pair.coveredSecond));
        });
        const sharedTotal = funds.reduce((sum, f) => sum + (f.share / 100) * covered.get(f.ticker), 0);

        return { funds, missing, pairs, duplicates, sharedTotal, sharedExact: funds.length === 2 };
    }

    /**
     * The callouts: the most overlapping pair, the share held by two or more funds, the largest duplicated holding.
     * @param {Object} m - The model.
     * @returns {{label: string, value: string, note: string}[]} The callouts.
     */
    function callouts (m) {
        const items = [];
        const top = m.pairs.reduce((best, p) => (!best || p.overlap > best.overlap ? p : best), null);
        if (top) {
            items.push({
                label: 'Most overlapping pair',
                value: formatPercent(top.overlap),
                note: `${top.first.ticker} and ${top.second.ticker}, the most of any pair in this set`
            });
        }
        items.push({
            label: 'Held by two or more of your funds',
            value: `${m.sharedExact ? '' : 'At least '}${formatPercent(m.sharedTotal)}`,
            note: 'of your basket is in securities another of its funds also holds'
        });
        if (m.duplicates.length > 0) {
            const first = m.duplicates[0];
            items.push({
                label: 'Largest duplicated holding',
                value: formatPercent(first.basketPercent),
                note: `of your basket is ${first.name}, held by ${listOf(first.funds.map((f) => f.ticker))}`
            });
        }
        return items;
    }

    /**
     * Wire the overlap view.
     * @param {Object} data - The fund overlap snapshot.
     * @returns {void}
     */
    function createView (data) {
        const prompt = document.getElementById(PROMPT_ID);
        const view = document.getElementById(VIEW_ID);
        const calloutList = document.getElementById(CALLOUTS_ID);
        const matrixHost = document.getElementById(MATRIX_ID);
        const tableHost = document.getElementById(TABLE_ID);
        const filterBar = document.getElementById(FILTER_ID);
        const filterText = document.getElementById(FILTER_TEXT_ID);
        const filterClear = document.getElementById(FILTER_CLEAR_ID);
        const tooltip = page.createTooltip(document.getElementById(TOOLTIP_ID), (line, pair) => [
            line(`${pair.first.ticker} and ${pair.second.ticker}`, 'research-tooltip__name'),
            line(`${formatPercent(pair.overlap)} overlap by weight`, 'research-tooltip__value'),
            line(`${PFI18n.formatNumber(pair.sharedCount)} securities in common`, 'research-tooltip__muted')
        ]);
        let current = null;
        let filterTicker = null;
        // Null until the first draw, so the page load itself is not announced.
        let lastHeadline = null;

        /**
         * Draw the duplicates table for the current filter.
         * @returns {void}
         */
        function drawTable () {
            const rows = current.duplicates
                .filter((d) => !filterTicker || d.weights.has(filterTicker))
                .slice(0, TABLE_ROWS);
            filterBar.hidden = !filterTicker;
            if (filterTicker) {
                filterText.textContent = `Showing the holdings ${filterTicker} shares with your other funds.`;
            }
            const table = element('table', 'research-overlap__duplicates-table');
            table.append(element('caption', null, 'Most duplicated holdings in your basket'));
            const head = element('thead');
            const headRow = element('tr');
            ['Holding', 'Held by', 'Share of your basket'].forEach((text, i) => {
                const th = element('th', i === 2 ? 'research-overlap__number' : null, text);
                th.scope = 'col';
                headRow.append(th);
            });
            head.append(headRow);
            const body = element('tbody');
            if (rows.length === 0) {
                const tr = element('tr');
                const td = element('td', 'research-overlap__none', 'None of the listed shared holdings.');
                td.colSpan = 3;
                tr.append(td);
                body.append(tr);
            }
            rows.forEach((d) => {
                const tr = element('tr');
                tr.dataset.funds = d.funds.map((f) => f.ticker).join(' ');
                const by = element('td');
                const chips = element('div', 'research-overlap__chips');
                d.funds.forEach((f) => chips.append(fundChip(f)));
                by.append(chips);
                tr.append(element('td', null, d.name), by,
                    element('td', 'research-overlap__number', formatPercent(d.basketPercent)));
                body.append(tr);
            });
            table.append(head, body);
            tableHost.replaceChildren(table);
        }

        /**
         * Highlight the table rows a pair of funds both hold, or clear the highlight.
         * @param {Object|null} pair - The pair, or null.
         * @returns {void}
         */
        function highlight (pair) {
            const table = tableHost.querySelector('table');
            if (!table) {
                return;
            }
            table.classList.toggle('research-overlap__duplicates-table--highlighting', Boolean(pair));
            table.querySelectorAll('tbody tr').forEach((tr) => {
                const held = (tr.dataset.funds || '').split(' ');
                tr.classList.toggle('is-highlighted',
                    Boolean(pair) && held.includes(pair.first.ticker) && held.includes(pair.second.ticker));
            });
        }

        /**
         * Filter the table to one fund's shared holdings, or show all when it is already the filter.
         * @param {string|null} ticker - The fund's ticker, or null for all.
         * @returns {void}
         */
        function setFilter (ticker) {
            filterTicker = ticker === filterTicker ? null : ticker;
            matrixHost.querySelectorAll('[data-filter-ticker]').forEach((button) => {
                button.setAttribute('aria-pressed', String(button.dataset.filterTicker === filterTicker));
            });
            drawTable();
        }

        /**
         * A matrix cell for a pair, focusable, with its overlap printed and its tooltip.
         * @param {Object} pair - The pair.
         * @returns {HTMLTableCellElement} The cell.
         */
        function pairCell (pair) {
            const td = element('td', 'research-overlap__cell', `${Math.round(pair.overlap)}%`);
            const step = stepOf(pair.overlap);
            td.style.setProperty('--research-overlap-fill', BLUES[step]);
            if (step >= FIRST_LIGHT_INK_STEP) {
                td.classList.add('research-overlap__cell--light-ink');
            }
            td.tabIndex = 0;
            td.setAttribute('aria-label', `${pair.first.ticker} and ${pair.second.ticker}: ${formatPercent(pair.overlap)} `
                + `overlap by weight, ${PFI18n.formatNumber(pair.sharedCount)} securities in common`);
            td.addEventListener('pointermove', (event) => {
                tooltip.show(event, pair);
                highlight(pair);
            });
            td.addEventListener('pointerleave', () => {
                tooltip.hide();
                highlight(null);
            });
            td.addEventListener('focus', () => {
                const box = td.getBoundingClientRect();
                tooltip.show({ clientX: box.right - 8, clientY: box.bottom - 8 }, pair);
                highlight(pair);
            });
            td.addEventListener('blur', () => {
                tooltip.hide();
                highlight(null);
            });
            return td;
        }

        /**
         * Draw the matrix for the current basket.
         * @returns {void}
         */
        function drawMatrix () {
            const funds = current.funds;
            const pairOf = new Map();
            current.pairs.forEach((p) => {
                pairOf.set(`${p.first.ticker}:${p.second.ticker}`, p);
                pairOf.set(`${p.second.ticker}:${p.first.ticker}`, {
                    ...p, first: p.second, second: p.first
                });
            });
            const table = element('table', 'research-overlap__matrix-table');
            table.append(element('caption', 'visually-hidden', 'Overlap by weight between each pair of your funds'));
            const head = element('thead');
            const headRow = element('tr');
            const corner = element('td', 'research-overlap__corner');
            headRow.append(corner);
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
                const button = element('button', 'research-overlap__row-button');
                button.type = 'button';
                button.dataset.filterTicker = row.ticker;
                button.setAttribute('aria-pressed', String(row.ticker === filterTicker));
                button.setAttribute('aria-label', `${row.ticker}: show only the holdings it shares`);
                button.append(fundChip(row));
                button.addEventListener('click', () => setFilter(row.ticker));
                th.append(button);
                tr.append(th);
                funds.forEach((column) => {
                    if (column.ticker === row.ticker) {
                        const same = element('td', 'research-overlap__cell research-overlap__cell--same');
                        same.append(element('span', 'visually-hidden', 'Same fund'));
                        tr.append(same);
                        return;
                    }
                    const pair = pairOf.get(`${row.ticker}:${column.ticker}`);
                    tr.append(pair ? pairCell(pair) : element('td', 'research-overlap__cell research-overlap__cell--none', '–'));
                });
                body.append(tr);
            });
            table.append(head, body);
            matrixHost.replaceChildren(table, scaleLegend());
        }

        /**
         * The colour scale's legend: the steps from light (little overlap) to dark (a lot).
         * @returns {HTMLElement} The legend.
         */
        function scaleLegend () {
            const legend = element('div', 'research-overlap__legend');
            legend.setAttribute('aria-hidden', 'true');
            const ramp = element('span', 'research-overlap__ramp');
            BLUES.forEach((colour) => {
                const swatch = element('span', 'research-overlap__ramp-step');
                swatch.style.setProperty('--research-overlap-fill', colour);
                ramp.append(swatch);
            });
            legend.append(element('span', null, '0%'), ramp, element('span', null, '100%'));
            return legend;
        }

        /**
         * Redraw for the basket.
         * @returns {void}
         */
        function draw () {
            const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
            current = model(data, basket);
            if (filterTicker && !current.funds.some((f) => f.ticker === filterTicker)) {
                filterTicker = null;
            }
            tooltip.hide();
            const enough = current.funds.length >= 2;
            prompt.hidden = enough;
            view.hidden = !enough;
            prompt.textContent = current.missing.length > 0
                ? `There is no overlap data for ${listOf(current.missing)} yet. Add at least two other funds to see how they overlap.`
                : 'Add at least two funds to your Fund Basket to see how much they overlap.';
            if (!enough) {
                lastHeadline = lastHeadline === null ? null : '';
                return;
            }
            const items = callouts(current);
            if (current.missing.length > 0) {
                items.push({ label: 'Left out', value: listOf(current.missing), note: 'has no overlap data yet' });
            }
            calloutList.replaceChildren(...items.map((c) => {
                const li = element('li', 'research-overlap__callout');
                li.append(element('span', 'research-overlap__callout-label', c.label),
                    element('span', 'research-overlap__callout-value', c.value),
                    element('span', 'research-overlap__callout-note', c.note));
                return li;
            }));
            drawMatrix();
            drawTable();
            const top = current.pairs.reduce((best, p) => (!best || p.overlap > best.overlap ? p : best), null);
            const headline = top
                ? `${top.first.ticker} and ${top.second.ticker} overlap ${formatPercent(top.overlap)} by weight, the most in this set.`
                : '';
            if (headline && lastHeadline !== null && headline !== lastHeadline) {
                page.announce(`Fund overlap: ${headline}`);
            }
            lastHeadline = headline;
        }

        filterClear.addEventListener('click', () => setFilter(null));
        window.addEventListener('scroll', tooltip.hide, { passive: true });
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
        const data = page.readData(DATA_ELEMENT_ID, 'Research fund overlap data');
        if (!data || !Array.isArray(data.funds) || !Array.isArray(data.pairs) || !Array.isArray(data.holdings)) {
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
