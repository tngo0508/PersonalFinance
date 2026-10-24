/* global d3 */
/**
 * Research page, Risk vs return tab: each Tracked Fund's annualized return against its volatility over a 1-, 3- or
 * 5-year window (3 years by default).
 *
 * Fund points use the figures the refresh precomputed (the fund returns snapshot's windows: annualized return,
 * volatility, Sharpe ratio and the Treasury bill rate it used). Bubble area follows each fund's net assets, from the
 * fund facts snapshot. "Your basket" is a dark diamond the browser computes from its funds' monthly returns over the
 * same months: rebalanced monthly to its weights, annualized return, volatility (sample standard deviation of the
 * monthly returns times √12) and a Sharpe ratio against the average 3-month Treasury bill (from the macro snapshot).
 *
 * Every point is focusable and opens a card with its return, volatility, Sharpe ratio, expense ratio and net assets;
 * "Show as table" lists the funds and the basket sorted by Sharpe ratio, with inline bars. Reads the
 * `research-fund-returns-data`, `research-fund-facts-data`, `research-macro-data` and `research-funds-data` islands,
 * and the basket from `window.ResearchBasket`, redrawing on `research:basketchange`.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const RETURNS_ID = 'research-fund-returns-data';
    const FACTS_ID = 'research-fund-facts-data';
    const MACRO_ID = 'research-macro-data';
    const FUNDS_ID = 'research-funds-data';
    const CARD_ID = 'research-risk';
    const CHART_ID = 'research-risk-chart';
    const LEGEND_ID = 'research-risk-legend';
    const NOTE_ID = 'research-risk-note';
    const TABLE_ID = 'research-risk-table';
    const TABLE_TOGGLE_ID = 'research-risk-table-toggle';
    const TOOLTIP_ID = 'research-risk-card';
    const BASKET_LABEL = 'Your basket';
    const DEFAULT_YEARS = 3;
    const HEIGHT = 420;
    const MARGIN = { top: 20, right: 28, bottom: 44, left: 56 };
    const RADIUS = [6, 26];
    const DEFAULT_RADIUS = 8;
    const DIAMOND = 9;

    /**
     * A month's key.
     * @param {string} isoDate - The month's first day, e.g. "2026-06-01".
     * @returns {string} The key, e.g. "2026-06".
     */
    function monthKey (isoDate) {
        return isoDate.slice(0, 7);
    }

    /**
     * The month a number of months after another.
     * @param {string} key - The month.
     * @param {number} count - How many months later (negative for earlier).
     * @returns {string} The month's key.
     */
    function addMonths (key, count) {
        const [year, month] = key.split('-').map(Number);
        const index = year * 12 + (month - 1) + count;
        return `${Math.floor(index / 12)}-${String(index % 12 + 1).padStart(2, '0')}`;
    }

    /**
     * How many months from one month to another.
     * @param {string} from - The first month.
     * @param {string} to - The second month.
     * @returns {number} The months between them.
     */
    function monthsBetween (from, to) {
        const [y1, m1] = from.split('-').map(Number);
        const [y2, m2] = to.split('-').map(Number);
        return (y2 - y1) * 12 + (m2 - m1);
    }

    /**
     * A number with a true minus sign and fixed decimals.
     * @param {number} value - The value.
     * @param {number} digits - The decimals.
     * @returns {string} E.g. "−0.42".
     */
    function signed (value, digits) {
        const text = Math.abs(value).toFixed(digits);
        return value < 0 && Number(text) !== 0 ? `−${text}` : text;
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
     * The basket's point for a window: its funds' monthly returns over the window's months, weighted, then the same
     * figures the refresh computes for a fund.
     * @param {Object} data - The fund returns snapshot.
     * @param {Object|null} macro - The macro snapshot.
     * @param {Object[]} basket - The basket's funds (ticker, share).
     * @param {number} years - The window's length in years.
     * @returns {Object|null} The point, or null when a basket fund lacks a month of the window.
     */
    function basketPoint (data, macro, basket, years) {
        const series = new Map(data.funds.map((f) => [f.ticker, f]));
        const funds = basket.filter((f) => series.has(f.ticker));
        if (funds.length === 0) {
            return null;
        }
        const months = years * 12;
        const end = funds.map((f) => monthKey(series.get(f.ticker).lastMonth)).sort()[0];
        const start = addMonths(end, 1 - months);
        const returns = [];
        for (let i = 0; i < months; i++) {
            const month = addMonths(start, i);
            let sum = 0;
            let weight = 0;
            for (const fund of funds) {
                const s = series.get(fund.ticker);
                const value = s.returns[monthsBetween(monthKey(s.firstMonth), month)];
                if (value === undefined || value === null) {
                    return null;
                }
                sum += fund.share * value;
                weight += fund.share;
            }
            returns.push(sum / weight);
        }
        const growth = returns.reduce((g, r) => g * (1 + r / 100), 1);
        const annualized = (Math.pow(growth, 12 / months) - 1) * 100;
        const mean = returns.reduce((a, r) => a + r, 0) / months;
        const volatility = Math.sqrt(returns.reduce((a, r) => a + (r - mean) * (r - mean), 0) / (months - 1)) * Math.sqrt(12);
        const bills = new Map((macro ? macro.months : [])
            .filter((m) => m.treasuryBill !== null && m.treasuryBill !== undefined)
            .map((m) => [monthKey(m.month), m.treasuryBill]));
        const windowBills = Array.from({ length: months }, (_, i) => bills.get(addMonths(start, i))).filter((b) => b !== undefined);
        const treasuryBill = windowBills.length ? windowBills.reduce((a, b) => a + b, 0) / windowBills.length : null;
        return {
            ticker: BASKET_LABEL,
            label: BASKET_LABEL,
            isBasket: true,
            annualizedReturn: annualized,
            volatility,
            sharpe: treasuryBill === null || volatility <= 0 ? null : (annualized - treasuryBill) / volatility,
            treasuryBill,
            start,
            end
        };
    }

    /**
     * Wire the Risk vs return view.
     * @param {Object} data - The fund returns snapshot.
     * @param {Object|null} facts - The fund facts snapshot.
     * @param {Object|null} macro - The macro snapshot.
     * @param {Map<string, string>} names - Each Tracked Fund's name by ticker.
     * @returns {void}
     */
    function createView (data, facts, macro, names) {
        const card = document.getElementById(CARD_ID);
        const chartHost = document.getElementById(CHART_ID);
        const legend = document.getElementById(LEGEND_ID);
        const note = document.getElementById(NOTE_ID);
        const tableHost = document.getElementById(TABLE_ID);
        const tableToggle = document.getElementById(TABLE_TOGGLE_ID);
        const yearButtons = Array.from(card.querySelectorAll('[data-risk-years]'));
        const factsOf = new Map((facts ? facts.funds : []).map((f) => [f.ticker, f]));
        const slotOf = (ticker) => (window.ResearchBasket && window.ResearchBasket.slotOf ? window.ResearchBasket.slotOf(ticker) : 0);
        const tooltip = page.createTooltip(document.getElementById(TOOLTIP_ID), (line, point) => cardRows(point)
            .map((row, i) => line(row, i === 0 ? 'research-tooltip__name' : 'research-tooltip__muted')));
        let years = DEFAULT_YEARS;
        let points = [];
        let lastWidth = 0;

        /**
         * The card's rows for a point.
         * @param {Object} point - The point.
         * @returns {string[]} The rows, its name first.
         */
        function cardRows (point) {
            const rows = [point.isBasket ? BASKET_LABEL : `${point.ticker}${names.has(point.ticker) ? `, ${names.get(point.ticker)}` : ''}`,
                `Annualized return: ${signed(point.annualizedReturn, 1)}% a year`,
                `Volatility: ${point.volatility.toFixed(1)}%`,
                point.sharpe === null || point.sharpe === undefined
                    ? 'Sharpe ratio: n/a'
                    : `Sharpe ratio: ${signed(point.sharpe, 2)} (Treasury bill ${point.treasuryBill.toFixed(2)}%)`];
            if (!point.isBasket) {
                const fact = factsOf.get(point.ticker);
                rows.push(fact && fact.expenseRatio !== null && fact.expenseRatio !== undefined
                    ? `Expense ratio: ${fact.expenseRatio.toFixed(2)}%`
                    : 'Expense ratio: n/a');
                rows.push(fact && fact.netAssets ? `Net assets: ${page.formatMoney(fact.netAssets)}` : 'Net assets: n/a');
            }
            return rows;
        }

        /**
         * The points for the window: every fund with figures, then the basket.
         * @returns {Object[]} The points.
         */
        function pointsFor () {
            const windowData = data.windows.find((w) => w.years === years);
            const funds = (windowData ? windowData.funds : []).map((f) => ({
                ...f,
                label: f.ticker,
                isBasket: false,
                slot: slotOf(f.ticker),
                netAssets: factsOf.has(f.ticker) ? factsOf.get(f.ticker).netAssets : null
            }));
            const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
            const basketFigures = basketPoint(data, macro, basket, years);
            note.textContent = basket.length === 0
                ? 'Add funds to your Fund Basket to see where it sits.'
                : basketFigures ? '' : `A fund in your basket has less than ${years} ${years === 1 ? 'year' : 'years'} of returns, so the basket is not shown.`;
            return basketFigures ? [...funds, basketFigures] : funds;
        }

        /**
         * Show the card for a point, beside it.
         * @param {Object} point - The point.
         * @param {SVGElement} node - The point's element.
         * @returns {void}
         */
        function showCard (point, node) {
            const box = node.getBoundingClientRect();
            tooltip.show({ clientX: box.right, clientY: box.top }, point);
        }

        /**
         * Draw the scatter at the host's width.
         * @returns {void}
         */
        function drawChart () {
            const width = Math.max(280, chartHost.clientWidth || 640);
            lastWidth = width;
            const xMax = d3.max(points, (p) => p.volatility) || 1;
            const yValues = points.map((p) => p.annualizedReturn);
            const x = d3.scaleLinear().domain([0, xMax * 1.08]).nice().range([MARGIN.left, width - MARGIN.right]);
            const y = d3.scaleLinear().domain([Math.min(0, d3.min(yValues)), Math.max(0, d3.max(yValues))]).nice()
                .range([HEIGHT - MARGIN.bottom, MARGIN.top]);
            const assets = points.filter((p) => !p.isBasket && p.netAssets).map((p) => p.netAssets);
            const radius = assets.length
                ? d3.scaleSqrt().domain([0, d3.max(assets)]).range([0, RADIUS[1]])
                : null;
            const radiusOf = (p) => (radius && p.netAssets ? Math.max(RADIUS[0], radius(p.netAssets)) : DEFAULT_RADIUS);

            d3.select(chartHost).selectAll('svg').remove();
            const svg = d3.select(chartHost).append('svg').attr('class', 'research-risk__svg')
                .attr('viewBox', `0 0 ${width} ${HEIGHT}`);
            const grid = svg.append('g').attr('class', 'research-performance__grid');
            y.ticks(6).forEach((t) => {
                grid.append('line').attr('x1', MARGIN.left).attr('x2', width - MARGIN.right).attr('y1', y(t)).attr('y2', y(t));
                grid.append('text').attr('x', MARGIN.left - 6).attr('y', y(t)).attr('dy', '0.32em').attr('text-anchor', 'end')
                    .text(`${signed(t, 0)}%`);
            });
            if (y.domain()[0] < 0) {
                svg.append('line').attr('class', 'research-performance__zero')
                    .attr('x1', MARGIN.left).attr('x2', width - MARGIN.right).attr('y1', y(0)).attr('y2', y(0));
            }
            svg.append('g').attr('class', 'research-performance__axis')
                .attr('transform', `translate(0,${HEIGHT - MARGIN.bottom})`)
                .call(d3.axisBottom(x).ticks(Math.max(3, Math.floor(width / 90))).tickFormat((t) => `${t}%`).tickSizeOuter(0));
            svg.append('text').attr('class', 'research-performance__panel-title').attr('x', width - MARGIN.right)
                .attr('y', HEIGHT - 8).attr('text-anchor', 'end').text('Volatility, % a year →');
            svg.append('text').attr('class', 'research-performance__panel-title').attr('x', MARGIN.left)
                .attr('y', MARGIN.top - 8).text('↑ Annualized return, % a year');

            const funds = points.filter((p) => !p.isBasket).sort((a, b) => radiusOf(b) - radiusOf(a));
            const marks = svg.append('g');
            funds.forEach((p) => {
                const g = marks.append('g').attr('class', 'research-risk__point').attr('tabindex', 0)
                    .attr('data-ticker', p.ticker)
                    .attr('transform', `translate(${x(p.volatility)},${y(p.annualizedReturn)})`)
                    .attr('aria-label', cardRows(p).join('. '));
                g.append('circle').attr('class', 'research-risk__bubble').attr('data-fund-slot', String(p.slot)).attr('r', radiusOf(p));
                g.append('text').attr('class', 'research-risk__label').attr('x', radiusOf(p) + 4).attr('dy', '0.32em').text(p.ticker);
            });
            const basket = points.find((p) => p.isBasket);
            if (basket) {
                const g = marks.append('g').attr('class', 'research-risk__point research-risk__point--basket').attr('tabindex', 0)
                    .attr('data-ticker', BASKET_LABEL)
                    .attr('transform', `translate(${x(basket.volatility)},${y(basket.annualizedReturn)})`)
                    .attr('aria-label', cardRows(basket).join('. '));
                g.append('path').attr('class', 'research-risk__diamond')
                    .attr('d', `M0,${-DIAMOND}L${DIAMOND},0L0,${DIAMOND}L${-DIAMOND},0Z`);
                g.append('text').attr('class', 'research-risk__label research-risk__label--basket').attr('x', DIAMOND + 4)
                    .attr('dy', '0.32em').text(BASKET_LABEL);
            }
            marks.selectAll('.research-risk__point').each(function () {
                const node = this;
                const ticker = node.getAttribute('data-ticker');
                const point = points.find((p) => (p.isBasket ? BASKET_LABEL : p.ticker) === ticker);
                node.addEventListener('pointerenter', () => showCard(point, node));
                node.addEventListener('pointerleave', tooltip.hide);
                node.addEventListener('focus', () => showCard(point, node));
                node.addEventListener('blur', tooltip.hide);
            });
            chartHost.setAttribute('aria-label', `Risk vs return over ${years} ${years === 1 ? 'year' : 'years'}: `
                + `${funds.length} funds${basket ? ' and your basket' : ''}. Tab to a point for its figures.`);
        }

        /**
         * Draw the legend: what the marks mean.
         * @returns {void}
         */
        function drawLegend () {
            const items = [
                ['research-risk__legend-bubble', `Fund: bubble area follows net assets${facts ? '' : ' (not loaded)'}`],
                ['research-risk__legend-diamond', BASKET_LABEL]
            ];
            legend.replaceChildren(...items.map(([cls, text]) => {
                const item = element('li', 'research-performance__legend-item');
                const swatch = element('span', cls);
                swatch.setAttribute('aria-hidden', 'true');
                item.append(swatch, text);
                return item;
            }));
        }

        /**
         * Draw the table: funds and basket sorted by Sharpe ratio, with inline bars for return and volatility.
         * @returns {void}
         */
        function drawTable () {
            const sorted = [...points].sort((a, b) => (b.sharpe ?? -Infinity) - (a.sharpe ?? -Infinity));
            const maxReturn = d3.max(sorted, (p) => Math.abs(p.annualizedReturn)) || 1;
            const maxVolatility = d3.max(sorted, (p) => p.volatility) || 1;
            const table = element('table', 'research-overlap__duplicates-table research-risk__table');
            table.append(element('caption', null, `Risk and return over ${years} ${years === 1 ? 'year' : 'years'}, by Sharpe ratio`));
            const head = element('thead');
            const headRow = element('tr');
            ['', 'Annualized return', 'Volatility', 'Sharpe ratio', 'Expense ratio'].forEach((text, i) => {
                const th = element('th', i === 0 ? null : 'research-overlap__number', text);
                th.scope = 'col';
                headRow.append(th);
            });
            head.append(headRow);
            const body = element('tbody');
            const bar = (value, max, cls) => {
                const cell = element('td', 'research-overlap__number research-risk__bar-cell');
                const track = element('span', `research-risk__bar ${cls}${value < 0 ? ' research-risk__bar--negative' : ''}`);
                track.style.width = `${Math.round(Math.abs(value) / max * 100)}%`;
                track.setAttribute('aria-hidden', 'true');
                cell.append(track, element('span', 'research-risk__bar-value', `${signed(value, 1)}%`));
                return cell;
            };
            sorted.forEach((p) => {
                const tr = element('tr');
                tr.dataset.ticker = p.isBasket ? BASKET_LABEL : p.ticker;
                const name = element('th', null, p.isBasket ? BASKET_LABEL : p.ticker);
                name.scope = 'row';
                const fact = p.isBasket ? null : factsOf.get(p.ticker);
                tr.append(name,
                    bar(p.annualizedReturn, maxReturn, 'research-risk__bar--return'),
                    bar(p.volatility, maxVolatility, 'research-risk__bar--volatility'),
                    element('td', 'research-overlap__number', p.sharpe === null || p.sharpe === undefined ? 'n/a' : signed(p.sharpe, 2)),
                    element('td', 'research-overlap__number', p.isBasket ? '–'
                        : fact && fact.expenseRatio !== null && fact.expenseRatio !== undefined ? `${fact.expenseRatio.toFixed(2)}%` : 'n/a'));
                body.append(tr);
            });
            table.append(head, body);
            tableHost.replaceChildren(table);
        }

        /**
         * Redraw for the window and the basket.
         * @returns {void}
         */
        function draw () {
            tooltip.hide();
            yearButtons.forEach((button) => button.setAttribute('aria-pressed', String(Number(button.dataset.riskYears) === years)));
            points = pointsFor();
            drawLegend();
            drawTable();
            if (chartHost.clientWidth > 0) {
                drawChart();
            }
        }

        yearButtons.forEach((button) => button.addEventListener('click', () => {
            years = Number(button.dataset.riskYears);
            draw();
            if (page.announce) {
                page.announce(`Risk vs return over ${years} ${years === 1 ? 'year' : 'years'}.`);
            }
        }));
        tableToggle.addEventListener('click', () => {
            const show = tableHost.hidden;
            tableHost.hidden = !show;
            tableToggle.setAttribute('aria-pressed', String(show));
            tableToggle.textContent = show ? 'Hide table' : 'Show as table';
        });
        window.addEventListener('scroll', () => tooltip.hide(), { passive: true });
        document.addEventListener('research:basketchange', draw);
        document.addEventListener('research:tabchange', () => {
            if (chartHost.clientWidth > 0) {
                drawChart();
            }
        });
        if (typeof ResizeObserver === 'function') {
            new ResizeObserver(() => {
                if (chartHost.clientWidth > 0 && Math.abs(chartHost.clientWidth - lastWidth) > 4) {
                    drawChart();
                }
            }).observe(chartHost);
        }
        draw();
    }

    /**
     * Wire the view when its data, D3 and the page helpers are present.
     * @returns {void}
     */
    function init () {
        if (!page || typeof d3 === 'undefined' || !document.getElementById(CHART_ID)) {
            return;
        }
        const data = page.readData(RETURNS_ID, 'Research fund returns data');
        if (!data || !Array.isArray(data.windows)) {
            return;
        }
        const funds = page.readData(FUNDS_ID, 'Research funds data');
        const names = new Map((funds && Array.isArray(funds.funds) ? funds.funds : []).map((f) => [f.ticker, f.name]));
        createView(data, page.readData(FACTS_ID, 'Research fund facts data'), page.readData(MACRO_ID, 'Research macro data'), names);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
