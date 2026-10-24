/* global d3 */
/**
 * Research page, Performance tab: how 100 put into the Fund Basket and into each of its funds grew, month by month.
 *
 * Every line starts at 100 at the end of the month before the first month all the basket's funds have a return, and
 * ends at the last month they all have. Each fund's line compounds its monthly total returns (N-PORT Item B.5, from
 * the `research-fund-returns-data` island). "Your basket" is rebalanced to its weights every month: its return for a
 * month is the weighted average of its funds' returns. "Adjusted for inflation" divides every line by how much CPI-U
 * (from the `research-macro-data` island) rose since the start; there is no CPI line and no second axis.
 *
 * Callouts give the basket's total and annualized return, its worst fall from a high and the month it bottomed, and
 * inflation over the period. A table gives the annualized return, total return, worst fall from a high and return
 * after inflation of the basket and each fund. The browser only weights and compounds: no figure comes from
 * anywhere but the snapshots. Reads the basket from `window.ResearchBasket` and redraws on `research:basketchange`.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const RETURNS_ID = 'research-fund-returns-data';
    const MACRO_ID = 'research-macro-data';
    const CARD_ID = 'research-performance';
    const PROMPT_ID = 'research-performance-prompt';
    const VIEW_ID = 'research-performance-view';
    const CALLOUTS_ID = 'research-performance-callouts';
    const CHART_ID = 'research-performance-chart';
    const LEGEND_ID = 'research-performance-legend';
    const TABLE_ID = 'research-performance-table';
    const BASKET_LABEL = 'Your basket';
    const HEIGHT = 320;
    const MARGIN = { top: 16, right: 64, bottom: 28, left: 44 };
    const LABEL_GAP = 13;
    const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September',
        'October', 'November', 'December'];

    /**
     * A month as a key and a date.
     * @param {string} isoDate - The month's first day, e.g. "2026-06-01".
     * @returns {string} The key, e.g. "2026-06".
     */
    function monthKey (isoDate) {
        return isoDate.slice(0, 7);
    }

    /**
     * The month a number of months after another.
     * @param {string} key - The month, e.g. "2026-06".
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
     * A month for display.
     * @param {string} key - The month, e.g. "2026-06".
     * @returns {string} E.g. "June 2026".
     */
    function formatMonth (key) {
        const [year, month] = key.split('-').map(Number);
        return `${MONTH_NAMES[month - 1]} ${year}`;
    }

    /**
     * The date at the end of a month, where its line point sits.
     * @param {string} key - The month.
     * @returns {Date} The month's last day.
     */
    function monthEnd (key) {
        const [year, month] = key.split('-').map(Number);
        return new Date(Date.UTC(year, month, 0));
    }

    /**
     * A signed percentage with one decimal.
     * @param {number} value - The value in percent.
     * @returns {string} E.g. "+12.3%" or "−4.0%".
     */
    function formatPercent (value) {
        const rounded = Math.round(value * 10) / 10;
        if (rounded === 0) {
            return '0.0%';
        }
        return rounded < 0 ? `−${Math.abs(rounded).toFixed(1)}%` : `+${rounded.toFixed(1)}%`;
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
     * CPI-U by month, with a month BLS did not publish (such as October 2025) filled in between its neighbours.
     * @param {Object|null} macro - The macro snapshot.
     * @returns {Map<string, number>} The index by month key.
     */
    function consumerPrices (macro) {
        const known = (macro ? macro.months : [])
            .filter((m) => m.consumerPrices !== null && m.consumerPrices !== undefined)
            .map((m) => [monthKey(m.month), m.consumerPrices]);
        const prices = new Map(known);
        for (let i = 1; i < known.length; i++) {
            const [fromKey, from] = known[i - 1];
            const [toKey, to] = known[i];
            const gap = monthsBetween(fromKey, toKey);
            for (let k = 1; k < gap; k++) {
                prices.set(addMonths(fromKey, k), from * Math.pow(to / from, k / gap));
            }
        }
        return prices;
    }

    /**
     * Compound monthly returns into a growth-of-100 line and its figures.
     * @param {number[]} returns - The monthly returns in percent, oldest first.
     * @param {number[]|null} deflators - CPI at each month divided by CPI at the base month, or null for nominal.
     * @param {string} start - The first month.
     * @returns {{values: number[], total: number, annualized: number, worstFall: number, bottom: string|null}} The
     *     line (100 at the base, then one value per month) and its total and annualized return, worst fall from a
     *     high and the month that fall bottomed, all in percent.
     */
    function growth (returns, deflators, start) {
        const values = [100];
        let level = 1;
        let peak = 1;
        let worstFall = 0;
        let bottom = null;
        returns.forEach((r, i) => {
            level *= 1 + r / 100;
            const shown = deflators ? level / deflators[i] : level;
            values.push(shown * 100);
            peak = Math.max(peak, shown);
            const fall = shown / peak - 1;
            if (fall < worstFall) {
                worstFall = fall;
                bottom = addMonths(start, i);
            }
        });
        const end = values[values.length - 1] / 100;
        return {
            values,
            total: (end - 1) * 100,
            annualized: (Math.pow(end, 12 / returns.length) - 1) * 100,
            worstFall: worstFall * 100,
            bottom
        };
    }

    /**
     * The basket's model: the common months, each fund's and the basket's lines, figures and inflation.
     * @param {Object} data - The fund returns snapshot.
     * @param {Object|null} macro - The macro snapshot.
     * @param {Object[]} basket - The basket's funds (ticker, share, slot).
     * @param {boolean} real - True to adjust for inflation.
     * @returns {Object|null} The model, or null when the funds share no month.
     */
    function model (data, macro, basket, real) {
        const series = new Map(data.funds.map((f) => [f.ticker, f]));
        const funds = basket.filter((f) => series.has(f.ticker));
        if (funds.length === 0) {
            return null;
        }
        const start = funds.map((f) => monthKey(series.get(f.ticker).firstMonth)).sort().pop();
        let end = funds.map((f) => monthKey(series.get(f.ticker).lastMonth)).sort()[0];
        const cpi = consumerPrices(macro);
        const base = addMonths(start, -1);
        const canDeflate = cpi.has(base);
        if (canDeflate) {
            // The inflation figures need CPI for every month shown; the last CPI month may come before the returns'.
            while (end >= start && !cpi.has(end)) {
                end = addMonths(end, -1);
            }
        }
        const count = monthsBetween(start, end) + 1;
        if (count < 1) {
            return null;
        }
        const months = Array.from({ length: count }, (_, i) => addMonths(start, i));
        const returnsOf = (ticker) => {
            const s = series.get(ticker);
            const offset = monthsBetween(monthKey(s.firstMonth), start);
            return months.map((_, i) => s.returns[offset + i]);
        };
        const fundReturns = funds.map((f) => ({ fund: f, returns: returnsOf(f.ticker) }));
        // The basket's monthly return: its funds' returns weighted by their shares (the weights of any fund with no
        // return for the month spread over the rest).
        const basketReturns = months.map((_, i) => {
            let weight = 0;
            let sum = 0;
            fundReturns.forEach(({ fund, returns }) => {
                if (returns[i] !== null && returns[i] !== undefined) {
                    weight += fund.share;
                    sum += fund.share * returns[i];
                }
            });
            return weight > 0 ? sum / weight : 0;
        });
        const deflators = canDeflate ? months.map((m) => cpi.get(m) / cpi.get(base)) : null;
        const shownDeflators = real && deflators ? deflators : null;
        const inflation = deflators ? (deflators[deflators.length - 1] - 1) * 100 : null;
        const afterInflation = (total) => (inflation === null ? null : ((1 + total / 100) / (1 + inflation / 100) - 1) * 100);
        const line = (label, slot, returns, isBasket) => {
            const clean = returns.map((r) => (r === null || r === undefined ? 0 : r));
            const shown = growth(clean, shownDeflators, start);
            const nominal = growth(clean, null, start);
            return { label, slot, isBasket, ...shown, nominalTotal: nominal.total, nominalAnnualized: nominal.annualized,
                nominalWorstFall: nominal.worstFall, afterInflation: afterInflation(nominal.total) };
        };
        const lines = fundReturns.map(({ fund, returns }) => line(fund.ticker, fund.slot, returns, false));
        lines.push(line(BASKET_LABEL, 0, basketReturns, true));
        return { start, end, base, months, lines, inflation, real: Boolean(shownDeflators), canDeflate };
    }

    /**
     * Spread end labels apart so none overlaps the one above it.
     * @param {{y: number}[]} labels - The labels, each with its line's end y.
     * @param {number} top - The highest y allowed.
     * @param {number} bottom - The lowest y allowed.
     * @returns {void}
     */
    function spreadLabels (labels, top, bottom) {
        labels.sort((a, b) => a.y - b.y);
        labels.forEach((label, i) => {
            label.labelY = Math.max(label.y, i === 0 ? top : labels[i - 1].labelY + LABEL_GAP);
        });
        for (let i = labels.length - 1; i >= 0; i--) {
            const limit = i === labels.length - 1 ? bottom : labels[i + 1].labelY - LABEL_GAP;
            labels[i].labelY = Math.min(labels[i].labelY, limit);
        }
    }

    /**
     * Wire the Performance view.
     * @param {Object} data - The fund returns snapshot.
     * @param {Object|null} macro - The macro snapshot.
     * @returns {void}
     */
    function createView (data, macro) {
        const card = document.getElementById(CARD_ID);
        const prompt = document.getElementById(PROMPT_ID);
        const view = document.getElementById(VIEW_ID);
        const calloutList = document.getElementById(CALLOUTS_ID);
        const chartHost = document.getElementById(CHART_ID);
        const legend = document.getElementById(LEGEND_ID);
        const tableHost = document.getElementById(TABLE_ID);
        const viewButtons = Array.from(card.querySelectorAll('[data-performance-view]'));
        let real = false;
        let current = null;
        let lastWidth = 0;

        /**
         * Draw the chart into its host at the host's width.
         * @param {boolean} animate - True to move the lines to their new place (never under reduced motion).
         * @returns {void}
         */
        function drawChart (animate) {
            const width = Math.max(280, chartHost.clientWidth || 640);
            lastWidth = width;
            const x = d3.scaleUtc()
                .domain([monthEnd(current.base), monthEnd(current.end)])
                .range([MARGIN.left, width - MARGIN.right]);
            const all = current.lines.flatMap((l) => l.values);
            const y = d3.scaleLinear()
                .domain([Math.min(100, d3.min(all)), Math.max(100, d3.max(all))])
                .nice()
                .range([HEIGHT - MARGIN.bottom, MARGIN.top]);
            const points = [current.base, ...current.months];
            const path = d3.line().x((_, i) => x(monthEnd(points[i]))).y((v) => y(v));
            let svg = d3.select(chartHost).select('svg');
            if (svg.empty()) {
                svg = d3.select(chartHost).append('svg').attr('class', 'research-performance__svg');
                svg.append('g').attr('class', 'research-performance__grid');
                svg.append('g').attr('class', 'research-performance__axis research-performance__axis--x');
                svg.append('line').attr('class', 'research-performance__baseline');
                svg.append('g').attr('class', 'research-performance__lines');
                svg.append('g').attr('class', 'research-performance__labels');
            }
            svg.attr('viewBox', `0 0 ${width} ${HEIGHT}`).attr('aria-hidden', 'true');
            const yTicks = y.ticks(5);
            svg.select('.research-performance__grid')
                .selectAll('g')
                .data(yTicks)
                .join((enter) => {
                    const g = enter.append('g');
                    g.append('line');
                    g.append('text');
                    return g;
                })
                .call((g) => {
                    g.select('line').attr('x1', MARGIN.left).attr('x2', width - MARGIN.right)
                        .attr('y1', (t) => y(t)).attr('y2', (t) => y(t));
                    g.select('text').attr('x', MARGIN.left - 6).attr('y', (t) => y(t)).attr('dy', '0.32em')
                        .attr('text-anchor', 'end').text((t) => t);
                });
            svg.select('.research-performance__axis--x')
                .attr('transform', `translate(0,${HEIGHT - MARGIN.bottom})`)
                .call(d3.axisBottom(x).ticks(Math.max(2, Math.floor(width / 110))).tickSizeOuter(0));
            svg.select('.research-performance__baseline')
                .attr('x1', MARGIN.left).attr('x2', width - MARGIN.right).attr('y1', y(100)).attr('y2', y(100));
            const duration = animate && !page.prefersReducedMotion() ? 300 : 0;
            const lines = svg.select('.research-performance__lines')
                .selectAll('path')
                .data(current.lines, (l) => l.label)
                .join('path')
                .attr('class', (l) => (l.isBasket ? 'research-performance__line research-performance__line--basket' : 'research-performance__line'))
                .attr('data-fund-slot', (l) => (l.isBasket ? null : String(l.slot)))
                .attr('data-line', (l) => l.label);
            // The basket is drawn last, on top.
            lines.sort((a, b) => Number(a.isBasket) - Number(b.isBasket));
            if (duration > 0) {
                lines.transition().duration(duration).attr('d', (l) => path(l.values));
            } else {
                lines.interrupt().attr('d', (l) => path(l.values));
            }
            const labels = current.lines.map((l) => ({ line: l, y: y(l.values[l.values.length - 1]) }));
            spreadLabels(labels, MARGIN.top, HEIGHT - MARGIN.bottom);
            svg.select('.research-performance__labels')
                .selectAll('text')
                .data(labels, (d) => d.line.label)
                .join('text')
                .attr('class', (d) => (d.line.isBasket ? 'research-performance__end research-performance__end--basket' : 'research-performance__end'))
                .attr('x', width - MARGIN.right + 6)
                .attr('y', (d) => d.labelY)
                .attr('dy', '0.32em')
                .text((d) => `${d.line.isBasket ? 'Basket' : d.line.label} ${Math.round(d.line.values[d.line.values.length - 1])}`);
            const words = current.lines.map((l) => `${l.label} ${Math.round(l.values[l.values.length - 1])}`).join(', ');
            chartHost.setAttribute('aria-label', `Growth of 100${current.real ? ', adjusted for inflation,' : ''} from `
                + `${formatMonth(current.start)} to ${formatMonth(current.end)}: ${words}.`);
        }

        /**
         * Draw the legend: every line with its swatch, the basket first.
         * @returns {void}
         */
        function drawLegend () {
            const ordered = [...current.lines].sort((a, b) => Number(b.isBasket) - Number(a.isBasket));
            legend.replaceChildren(...ordered.map((l) => {
                const item = element('li', 'research-performance__legend-item');
                const swatch = element('span', l.isBasket ? 'research-performance__swatch research-performance__swatch--basket' : 'research-fund-swatch');
                if (!l.isBasket) {
                    swatch.dataset.fundSlot = String(l.slot);
                }
                swatch.setAttribute('aria-hidden', 'true');
                item.append(swatch, l.label);
                return item;
            }));
        }

        /**
         * Draw the callouts: the basket's total and annualized return, its worst fall, and inflation.
         * @returns {void}
         */
        function drawCallouts () {
            const basket = current.lines.find((l) => l.isBasket);
            const period = `${formatMonth(current.start)} to ${formatMonth(current.end)}`;
            const items = [
                {
                    label: current.real ? 'Basket growth after inflation' : 'Basket total return',
                    value: formatPercent(basket.total),
                    note: `${formatPercent(basket.annualized)} a year, ${period}`
                },
                {
                    label: 'Worst fall from a high',
                    value: formatPercent(basket.worstFall),
                    note: basket.bottom ? `at month ends, bottoming in ${formatMonth(basket.bottom)}` : 'no fall at month ends in this period'
                }
            ];
            if (current.inflation !== null) {
                items.push({
                    label: 'Inflation over the period',
                    value: formatPercent(current.inflation),
                    note: 'rise in consumer prices (CPI-U)'
                });
            }
            calloutList.replaceChildren(...items.map((c) => {
                const li = element('li', 'research-overlap__callout');
                li.append(element('span', 'research-overlap__callout-label', c.label),
                    element('span', 'research-overlap__callout-value', c.value),
                    element('span', 'research-overlap__callout-note', c.note));
                return li;
            }));
        }

        /**
         * Draw the table: each line's annualized and total return, worst fall and return after inflation.
         * @returns {void}
         */
        function drawTable () {
            const table = element('table', 'research-overlap__duplicates-table research-performance__table');
            table.append(element('caption', null,
                `Returns from ${formatMonth(current.start)} to ${formatMonth(current.end)}`));
            const head = element('thead');
            const headRow = element('tr');
            ['', 'Annualized return', 'Total return', 'Worst fall from a high', 'Return after inflation'].forEach((text, i) => {
                const th = element('th', i === 0 ? null : 'research-overlap__number', text);
                th.scope = 'col';
                headRow.append(th);
            });
            head.append(headRow);
            const body = element('tbody');
            const ordered = [...current.lines].sort((a, b) => Number(b.isBasket) - Number(a.isBasket));
            ordered.forEach((l) => {
                const tr = element('tr');
                tr.dataset.line = l.label;
                const name = element('th', null, l.label);
                name.scope = 'row';
                tr.append(name,
                    element('td', 'research-overlap__number', formatPercent(l.nominalAnnualized)),
                    element('td', 'research-overlap__number', formatPercent(l.nominalTotal)),
                    element('td', 'research-overlap__number', formatPercent(l.nominalWorstFall)),
                    element('td', 'research-overlap__number', l.afterInflation === null ? '–' : formatPercent(l.afterInflation)));
                body.append(tr);
            });
            table.append(head, body);
            tableHost.replaceChildren(table);
        }

        /**
         * Redraw for the basket and the switch.
         * @param {boolean} animate - True when the switch changed, so the lines move.
         * @returns {void}
         */
        function draw (animate) {
            viewButtons.forEach((button) => button.setAttribute('aria-pressed', String((button.dataset.performanceView === 'real') === real)));
            const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
            current = model(data, macro, basket, real);
            prompt.hidden = Boolean(current);
            view.hidden = !current;
            if (!current) {
                return;
            }
            drawCallouts();
            drawLegend();
            drawTable();
            // A hidden panel has no width: the chart is drawn when its tab is shown.
            if (chartHost.clientWidth > 0) {
                drawChart(animate);
            }
        }

        viewButtons.forEach((button) => button.addEventListener('click', () => {
            if (button.disabled) {
                return;
            }
            real = button.dataset.performanceView === 'real';
            draw(true);
            if (page.announce && current) {
                page.announce(real ? 'Showing growth adjusted for inflation.' : 'Showing nominal growth.');
            }
        }));
        document.addEventListener('research:basketchange', () => draw(false));
        document.addEventListener('research:tabchange', () => {
            if (current && chartHost.clientWidth > 0) {
                drawChart(false);
            }
        });
        if (typeof ResizeObserver === 'function') {
            new ResizeObserver(() => {
                if (current && chartHost.clientWidth > 0 && Math.abs(chartHost.clientWidth - lastWidth) > 4) {
                    drawChart(false);
                }
            }).observe(chartHost);
        }
        draw(false);
    }

    /**
     * Wire the view when its data, D3 and the page helpers are present.
     * @returns {void}
     */
    function init () {
        if (!page || typeof d3 === 'undefined' || !document.getElementById(VIEW_ID)) {
            return;
        }
        const data = page.readData(RETURNS_ID, 'Research fund returns data');
        if (!data || !Array.isArray(data.funds)) {
            return;
        }
        createView(data, page.readData(MACRO_ID, 'Research macro data'));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
