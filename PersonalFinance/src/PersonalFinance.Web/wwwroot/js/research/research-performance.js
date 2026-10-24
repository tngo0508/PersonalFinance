/* global d3 */
/**
 * Research page, Performance tab: how 100 put into the Fund Basket and into each of its funds grew, month by month.
 *
 * Three panels share one time axis. The main panel draws growth-of-100 lines for each basket fund and "Your basket"
 * (rebalanced to its weights every month: its return for a month is the weighted average of its funds' returns). The
 * rate panel draws the effective federal funds rate (H.15, from the macro snapshot). The difference panel draws the
 * basket minus a chosen Tracked Fund (by default VT), in points of the growth of 100: blue where the basket is ahead,
 * orange where it is behind. "Adjusted for inflation" divides the growth lines by how much CPI-U rose since the start.
 *
 * One crosshair spans the three panels; it follows the pointer, or the arrow keys (Home and End too) when the chart
 * has focus, and its tooltip lists every series for the month and the rate. A brush strip under the chart, with 1Y, 3Y,
 * 5Y and Max presets, chooses the period: every line starts at 100 again at the period's start, and the callouts and
 * table cover it. The browser only weights and compounds the monthly returns of the snapshots.
 *
 * Reads the fund returns (`research-fund-returns-data`), macro (`research-macro-data`) and funds (`research-funds-data`,
 * for names) data islands, and the basket from `window.ResearchBasket`, redrawing on `research:basketchange`.
 */
(function () {
    'use strict';

    const page = window.ResearchPage;
    const RETURNS_ID = 'research-fund-returns-data';
    const MACRO_ID = 'research-macro-data';
    const FUNDS_ID = 'research-funds-data';
    const CARD_ID = 'research-performance';
    const PROMPT_ID = 'research-performance-prompt';
    const VIEW_ID = 'research-performance-view';
    const CALLOUTS_ID = 'research-performance-callouts';
    const CHART_ID = 'research-performance-chart';
    const LEGEND_ID = 'research-performance-legend';
    const TABLE_ID = 'research-performance-table';
    const BRUSH_ID = 'research-performance-brush';
    const COMPARE_ID = 'research-performance-compare';
    const TOOLTIP_ID = 'research-performance-tooltip';
    const READOUT_ID = 'research-performance-readout';
    const BASKET_LABEL = 'Your basket';
    const DEFAULT_COMPARE = 'VT';
    const MIN_MONTHS = 3;
    const MARGIN = { right: 64, left: 48 };
    const MAIN = { top: 16, height: 270 };
    const RATE = { top: 322, height: 64 };
    const DIFF = { top: 420, height: 92 };
    const HEIGHT = DIFF.top + DIFF.height + 26;
    const BRUSH_HEIGHT = 56;
    const LABEL_GAP = 13;
    // Up and down in the difference panel: the dataviz reference blue and orange, never red and green.
    const AHEAD = '#2a78d6';
    const BEHIND = '#eb6834';
    const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September',
        'October', 'November', 'December'];

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
     * The date at the end of a month, where its point sits.
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
     * Signed points with one decimal.
     * @param {number} value - The difference in points.
     * @returns {string} E.g. "+3.2 points".
     */
    function formatPoints (value) {
        const rounded = Math.round(value * 10) / 10;
        if (rounded === 0) {
            return '0.0 points';
        }
        return rounded < 0 ? `−${Math.abs(rounded).toFixed(1)} points` : `+${rounded.toFixed(1)} points`;
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
     * The basket over all the months its funds share: each fund's and the basket's monthly returns, and the CPI.
     * @param {Object} data - The fund returns snapshot.
     * @param {Object|null} macro - The macro snapshot.
     * @param {Object[]} basket - The basket's funds (ticker, share, slot).
     * @returns {Object|null} The full model, or null when the funds share no month.
     */
    function fullModel (data, macro, basket) {
        const series = new Map(data.funds.map((f) => [f.ticker, f]));
        const funds = basket.filter((f) => series.has(f.ticker));
        if (funds.length === 0) {
            return null;
        }
        const start = funds.map((f) => monthKey(series.get(f.ticker).firstMonth)).sort().pop();
        let end = funds.map((f) => monthKey(series.get(f.ticker).lastMonth)).sort()[0];
        const cpi = consumerPrices(macro);
        const canDeflate = cpi.has(addMonths(start, -1));
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
            return months.map((_, i) => {
                const value = offset + i >= 0 ? s.returns[offset + i] : undefined;
                return value === undefined ? null : value;
            });
        };
        const fundReturns = funds.map((f) => ({ fund: f, returns: returnsOf(f.ticker) }));
        // The weights of any fund with no return for a month are spread over the rest.
        const basketReturns = months.map((_, i) => {
            let weight = 0;
            let sum = 0;
            fundReturns.forEach(({ fund, returns }) => {
                if (returns[i] !== null) {
                    weight += fund.share;
                    sum += fund.share * returns[i];
                }
            });
            return weight > 0 ? sum / weight : 0;
        });
        const rates = new Map((macro ? macro.months : [])
            .filter((m) => m.fedFunds !== null && m.fedFunds !== undefined)
            .map((m) => [monthKey(m.month), m.fedFunds]));
        return { months, fundReturns, basketReturns, cpi, canDeflate, rates, returnsOf, series };
    }

    /**
     * The view of a period: each line rebased to 100 at the period's start, the comparison and the rates.
     * @param {Object} full - The full model.
     * @param {{from: number, to: number}} range - The period, as indexes into the full model's months.
     * @param {boolean} real - True to adjust for inflation.
     * @param {string|null} compare - The ticker to compare the basket with, or null.
     * @returns {Object} The view.
     */
    function periodView (full, range, real, compare) {
        const months = full.months.slice(range.from, range.to + 1);
        const start = months[0];
        const end = months[months.length - 1];
        const base = addMonths(start, -1);
        const deflators = full.canDeflate && full.cpi.has(base)
            ? months.map((m) => full.cpi.get(m) / full.cpi.get(base))
            : null;
        const shownDeflators = real && deflators ? deflators : null;
        const inflation = deflators ? (deflators[deflators.length - 1] - 1) * 100 : null;
        const slice = (returns) => returns.slice(range.from, range.to + 1).map((r) => (r === null ? 0 : r));
        const line = (label, slot, returns, isBasket) => {
            const shown = growth(returns, shownDeflators, start);
            const nominal = growth(returns, null, start);
            return {
                label,
                slot,
                isBasket,
                ...shown,
                nominalTotal: nominal.total,
                nominalAnnualized: nominal.annualized,
                nominalWorstFall: nominal.worstFall,
                afterInflation: inflation === null ? null : ((1 + nominal.total / 100) / (1 + inflation / 100) - 1) * 100
            };
        };
        const lines = full.fundReturns.map(({ fund, returns }) => line(fund.ticker, fund.slot, slice(returns), false));
        const basket = line(BASKET_LABEL, 0, slice(full.basketReturns), true);
        lines.push(basket);
        let comparison = null;
        if (compare && full.series.has(compare)) {
            const raw = full.returnsOf(compare).slice(range.from, range.to + 1);
            if (raw.every((r) => r !== null)) {
                const values = growth(raw, shownDeflators, start).values;
                comparison = { label: compare, values, difference: basket.values.map((v, i) => v - values[i]) };
            }
        }
        const rates = [base, ...months].map((m) => (full.rates.has(m) ? full.rates.get(m) : null));
        return { start, end, base, months, lines, inflation, comparison, rates, real: Boolean(shownDeflators) };
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
     * @param {Map<string, string>} names - Each Tracked Fund's name by ticker.
     * @returns {void}
     */
    function createView (data, macro, names) {
        const card = document.getElementById(CARD_ID);
        const prompt = document.getElementById(PROMPT_ID);
        const view = document.getElementById(VIEW_ID);
        const calloutList = document.getElementById(CALLOUTS_ID);
        const chartHost = document.getElementById(CHART_ID);
        const legend = document.getElementById(LEGEND_ID);
        const tableHost = document.getElementById(TABLE_ID);
        const brushHost = document.getElementById(BRUSH_ID);
        const compareSelect = document.getElementById(COMPARE_ID);
        const readout = document.getElementById(READOUT_ID);
        const viewButtons = Array.from(card.querySelectorAll('[data-performance-view]'));
        const rangeButtons = Array.from(card.querySelectorAll('[data-performance-range]'));
        const tooltip = page.createTooltip(document.getElementById(TOOLTIP_ID), (line, rows) => rows.map((r, i) =>
            line(r, i === 0 ? 'research-tooltip__name' : 'research-tooltip__muted')));
        let real = false;
        let full = null;
        let current = null;
        let range = null;
        let compare = null;
        let crosshair = null;
        let lastWidth = 0;
        let x = null;
        let crossYs = [];
        let brush = null;
        let brushX = null;
        let settingBrush = false;

        /**
         * Fill the "Compare with" list with the Tracked Funds that have returns and aren't in the basket.
         * @param {Object[]} basket - The basket's funds.
         * @returns {void}
         */
        function fillCompare (basket) {
            const inBasket = new Set(basket.map((f) => f.ticker));
            const choices = data.funds.map((f) => f.ticker).filter((t) => !inBasket.has(t));
            if (!choices.includes(compare)) {
                compare = choices.includes(DEFAULT_COMPARE) ? DEFAULT_COMPARE : (choices[0] || null);
            }
            compareSelect.replaceChildren(...choices.map((ticker) => {
                const option = element('option', null, names.has(ticker) ? `${ticker}, ${names.get(ticker)}` : ticker);
                option.value = ticker;
                option.selected = ticker === compare;
                return option;
            }));
            compareSelect.disabled = choices.length === 0;
        }

        /**
         * The tooltip rows for a point of the period.
         * @param {number} index - The point's index (0 is the base month).
         * @returns {string[]} The rows, the month first.
         */
        function rowsAt (index) {
            const month = index === 0 ? current.base : current.months[index - 1];
            const rows = [`${formatMonth(month)}${current.real ? ', adjusted for inflation' : ''}`];
            [...current.lines].sort((a, b) => Number(b.isBasket) - Number(a.isBasket)).forEach((l) => {
                rows.push(`${l.label}: ${l.values[index].toFixed(1)}`);
            });
            if (current.comparison) {
                rows.push(`${current.comparison.label}: ${current.comparison.values[index].toFixed(1)}`);
                rows.push(`Basket minus ${current.comparison.label}: ${formatPoints(current.comparison.difference[index])}`);
            }
            const rate = current.rates[index];
            rows.push(rate === null ? 'Federal funds rate: not available' : `Federal funds rate: ${rate.toFixed(2)}%`);
            return rows;
        }

        /**
         * Move the crosshair to a point and show its tooltip, or hide it.
         * @param {number|null} index - The point's index (0 is the base month), or null to hide.
         * @param {boolean} announce - True to read the month out (keyboard moves).
         * @returns {void}
         */
        function showCrosshair (index, announce) {
            crosshair = index;
            const group = d3.select(chartHost).select('.research-performance__crosshair');
            if (index === null || !current || group.empty()) {
                group.attr('hidden', true);
                tooltip.hide();
                return;
            }
            const points = [current.base, ...current.months];
            const cx = x(monthEnd(points[index]));
            group.attr('hidden', null).attr('data-index', index);
            group.select('line').attr('x1', cx).attr('x2', cx);
            group.selectAll('circle').each(function (_, i) {
                d3.select(this).attr('cx', cx).attr('cy', crossYs[i](index));
            });
            const rows = rowsAt(index);
            const box = chartHost.getBoundingClientRect();
            const scale = box.width / lastWidth;
            tooltip.show({ clientX: box.left + cx * scale, clientY: box.top + MAIN.top * scale + 24 }, rows);
            if (announce) {
                readout.textContent = rows.join('. ');
            }
        }

        /**
         * Draw a panel's recessive grid with its y labels.
         * @param {Object} svg - The D3 selection of the chart.
         * @param {Function} scale - The panel's y scale.
         * @param {number} ticks - About how many ticks.
         * @param {function(number): string} format - The label of a tick.
         * @param {number} width - The chart's width.
         * @returns {void}
         */
        function grid (svg, scale, ticks, format, width) {
            const g = svg.append('g').attr('class', 'research-performance__grid');
            scale.ticks(ticks).forEach((t) => {
                g.append('line').attr('x1', MARGIN.left).attr('x2', width - MARGIN.right).attr('y1', scale(t)).attr('y2', scale(t));
                g.append('text').attr('x', MARGIN.left - 6).attr('y', scale(t)).attr('dy', '0.32em')
                    .attr('text-anchor', 'end').text(format(t));
            });
        }

        /**
         * Draw the three panels into the chart host at its width.
         * @returns {void}
         */
        function drawChart () {
            const width = Math.max(280, chartHost.clientWidth || 640);
            lastWidth = width;
            const points = [current.base, ...current.months];
            x = d3.scaleUtc().domain([monthEnd(current.base), monthEnd(current.end)]).range([MARGIN.left, width - MARGIN.right]);
            const all = current.lines.flatMap((l) => l.values);
            const y = d3.scaleLinear()
                .domain([Math.min(100, d3.min(all)), Math.max(100, d3.max(all))]).nice()
                .range([MAIN.top + MAIN.height, MAIN.top]);
            const knownRates = current.rates.filter((r) => r !== null);
            const yRate = d3.scaleLinear().domain([0, Math.max(1, d3.max(knownRates) || 1)]).nice()
                .range([RATE.top + RATE.height, RATE.top]);
            const difference = current.comparison ? current.comparison.difference : [];
            const extent = Math.max(1, d3.max(difference, (d) => Math.abs(d)) || 1);
            const yDiff = d3.scaleLinear().domain([-extent, extent]).nice().range([DIFF.top + DIFF.height, DIFF.top]);
            const at = (i) => x(monthEnd(points[i]));

            d3.select(chartHost).selectAll('svg').remove();
            const svg = d3.select(chartHost).append('svg').attr('class', 'research-performance__svg')
                .attr('viewBox', `0 0 ${width} ${HEIGHT}`).attr('aria-hidden', 'true');
            const title = (text, top) => svg.append('text').attr('class', 'research-performance__panel-title')
                .attr('x', MARGIN.left).attr('y', top - 8).text(text);

            // Main panel: growth of 100.
            grid(svg, y, 5, (t) => String(t), width);
            svg.append('line').attr('class', 'research-performance__baseline')
                .attr('x1', MARGIN.left).attr('x2', width - MARGIN.right).attr('y1', y(100)).attr('y2', y(100));
            const path = d3.line().x((_, i) => at(i)).y((v) => y(v));
            const ordered = [...current.lines].sort((a, b) => Number(a.isBasket) - Number(b.isBasket));
            svg.append('g').selectAll('path').data(ordered).join('path')
                .attr('class', (l) => (l.isBasket ? 'research-performance__line research-performance__line--basket' : 'research-performance__line'))
                .attr('data-fund-slot', (l) => (l.isBasket ? null : String(l.slot)))
                .attr('data-line', (l) => l.label)
                .attr('d', (l) => path(l.values));
            const labels = current.lines.map((l) => ({ line: l, y: y(l.values[l.values.length - 1]) }));
            spreadLabels(labels, MAIN.top, MAIN.top + MAIN.height);
            svg.append('g').selectAll('text').data(labels).join('text')
                .attr('class', (d) => (d.line.isBasket ? 'research-performance__end research-performance__end--basket' : 'research-performance__end'))
                .attr('x', width - MARGIN.right + 6).attr('y', (d) => d.labelY).attr('dy', '0.32em')
                .text((d) => `${d.line.isBasket ? 'Basket' : d.line.label} ${Math.round(d.line.values[d.line.values.length - 1])}`);

            // Rate panel: the effective federal funds rate.
            title('Effective federal funds rate, %', RATE.top);
            grid(svg, yRate, 3, (t) => String(t), width);
            const ratePath = d3.line().defined((r) => r !== null).x((_, i) => at(i)).y((r) => yRate(r));
            svg.append('path').attr('class', 'research-performance__rate').attr('d', ratePath(current.rates));

            // Difference panel: the basket minus the chosen fund, blue above zero and orange below.
            title(current.comparison ? `Basket minus ${current.comparison.label}, points` : 'Basket minus another fund', DIFF.top);
            if (current.comparison) {
                grid(svg, yDiff, 3, (t) => (t > 0 ? `+${t}` : String(t)), width);
                const area = d3.area().x((_, i) => at(i)).y0(yDiff(0)).y1((d) => yDiff(d));
                const defs = svg.append('defs');
                defs.append('clipPath').attr('id', 'research-performance-clip-above').append('rect')
                    .attr('x', MARGIN.left).attr('width', width - MARGIN.left - MARGIN.right)
                    .attr('y', DIFF.top).attr('height', yDiff(0) - DIFF.top);
                defs.append('clipPath').attr('id', 'research-performance-clip-below').append('rect')
                    .attr('x', MARGIN.left).attr('width', width - MARGIN.left - MARGIN.right)
                    .attr('y', yDiff(0)).attr('height', DIFF.top + DIFF.height - yDiff(0));
                svg.append('path').attr('class', 'research-performance__difference research-performance__difference--ahead')
                    .attr('fill', AHEAD).attr('clip-path', 'url(#research-performance-clip-above)').attr('d', area(difference));
                svg.append('path').attr('class', 'research-performance__difference research-performance__difference--behind')
                    .attr('fill', BEHIND).attr('clip-path', 'url(#research-performance-clip-below)').attr('d', area(difference));
                svg.append('line').attr('class', 'research-performance__zero')
                    .attr('x1', MARGIN.left).attr('x2', width - MARGIN.right).attr('y1', yDiff(0)).attr('y2', yDiff(0));
            } else {
                svg.append('text').attr('class', 'research-performance__empty').attr('x', MARGIN.left)
                    .attr('y', DIFF.top + DIFF.height / 2)
                    .text(compare ? `${compare} has no return for every month of this period.` : 'Every tracked fund is in your basket.');
            }

            svg.append('g').attr('class', 'research-performance__axis')
                .attr('transform', `translate(0,${DIFF.top + DIFF.height})`)
                .call(d3.axisBottom(x).ticks(Math.max(2, Math.floor(width / 110))).tickSizeOuter(0));

            // The crosshair: one line through every panel, and a dot on each series.
            const cross = svg.append('g').attr('class', 'research-performance__crosshair').attr('hidden', true);
            cross.append('line').attr('y1', MAIN.top).attr('y2', DIFF.top + DIFF.height);
            crossYs = [];
            const dot = (yOf, cls, slot) => {
                crossYs.push(yOf);
                cross.append('circle').attr('r', 4).attr('class', cls).attr('data-fund-slot', slot);
            };
            ordered.forEach((l) => dot((i) => y(l.values[i]),
                l.isBasket ? 'research-performance__dot research-performance__dot--basket' : 'research-performance__dot',
                l.isBasket ? null : String(l.slot)));
            dot((i) => (current.rates[i] === null ? -10 : yRate(current.rates[i])), 'research-performance__dot research-performance__dot--rate', null);
            if (current.comparison) {
                dot((i) => yDiff(difference[i]), 'research-performance__dot research-performance__dot--difference', null);
            }
            svg.append('rect').attr('class', 'research-performance__overlay')
                .attr('x', MARGIN.left).attr('width', width - MARGIN.left - MARGIN.right)
                .attr('y', MAIN.top).attr('height', DIFF.top + DIFF.height - MAIN.top)
                .on('pointermove', (event) => {
                    const [px] = d3.pointer(event);
                    const date = x.invert(px);
                    showCrosshair(d3.bisector((p) => monthEnd(p)).center(points, date), false);
                })
                .on('pointerleave', () => showCrosshair(null, false));

            const words = current.lines.map((l) => `${l.label} ${Math.round(l.values[l.values.length - 1])}`).join(', ');
            chartHost.setAttribute('aria-label', `Growth of 100${current.real ? ', adjusted for inflation,' : ''} from `
                + `${formatMonth(current.start)} to ${formatMonth(current.end)}: ${words}. Below it, the federal funds rate`
                + `${current.comparison ? ` and your basket minus ${current.comparison.label}` : ''}.`);
            if (crosshair !== null && crosshair <= current.months.length) {
                showCrosshair(crosshair, false);
            }
        }

        /**
         * Put the brush selection on the period, without firing a range change.
         * @returns {void}
         */
        function moveBrush () {
            if (!brush || !brushX) {
                return;
            }
            const points = [addMonths(full.months[0], -1), ...full.months];
            settingBrush = true;
            d3.select(brushHost).select('.research-performance__brush-area')
                .call(brush.move, [brushX(monthEnd(points[range.from])), brushX(monthEnd(points[range.to + 1]))]);
            settingBrush = false;
        }

        /**
         * Draw the brush strip: the basket's nominal growth over all its months, with the period selected.
         * @returns {void}
         */
        function drawBrush () {
            const width = Math.max(280, brushHost.clientWidth || 640);
            const values = growth(full.basketReturns, null, full.months[0]).values;
            const points = [addMonths(full.months[0], -1), ...full.months];
            brushX = d3.scaleUtc().domain([monthEnd(points[0]), monthEnd(points[points.length - 1])])
                .range([MARGIN.left, width - MARGIN.right]);
            const y = d3.scaleLinear().domain(d3.extent(values)).range([BRUSH_HEIGHT - 18, 4]);
            d3.select(brushHost).selectAll('svg').remove();
            const svg = d3.select(brushHost).append('svg').attr('class', 'research-performance__brush-svg')
                .attr('viewBox', `0 0 ${width} ${BRUSH_HEIGHT}`);
            svg.append('path').attr('class', 'research-performance__brush-line')
                .attr('d', d3.line().x((_, i) => brushX(monthEnd(points[i]))).y((v) => y(v))(values));
            svg.append('g').attr('class', 'research-performance__axis')
                .attr('transform', `translate(0,${BRUSH_HEIGHT - 16})`)
                .call(d3.axisBottom(brushX).ticks(Math.max(2, Math.floor(width / 110))).tickSizeOuter(0));
            brush = d3.brushX().extent([[MARGIN.left, 2], [width - MARGIN.right, BRUSH_HEIGHT - 16]])
                .on('brush end', (event) => {
                    if (settingBrush) {
                        return;
                    }
                    if (!event.selection) {
                        // A click without a drag clears the selection: back to the whole period.
                        if (event.type === 'end') {
                            setRange({ from: 0, to: full.months.length - 1 }, true);
                            moveBrush();
                        }
                        return;
                    }
                    // Each point is a month's end: the period runs from the month after the first point to the last.
                    const [x0, x1] = event.selection.map((p) => brushX.invert(p));
                    const nearest = (date) => d3.bisector((p) => monthEnd(p)).center(points, date);
                    let from = Math.max(0, nearest(x0));
                    let to = Math.min(full.months.length - 1, nearest(x1) - 1);
                    if (to - from + 1 < MIN_MONTHS) {
                        to = Math.min(full.months.length - 1, from + MIN_MONTHS - 1);
                        from = Math.max(0, to - MIN_MONTHS + 1);
                    }
                    setRange({ from, to }, event.type === 'end');
                    if (event.type === 'end') {
                        moveBrush();
                    }
                });
            svg.append('g').attr('class', 'research-performance__brush-area').call(brush);
            moveBrush();
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
                items.push({ label: 'Inflation over the period', value: formatPercent(current.inflation), note: 'rise in consumer prices (CPI-U)' });
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
            table.append(element('caption', null, `Returns from ${formatMonth(current.start)} to ${formatMonth(current.end)}`));
            const head = element('thead');
            const headRow = element('tr');
            ['', 'Annualized return', 'Total return', 'Worst fall from a high', 'Return after inflation'].forEach((text, i) => {
                const th = element('th', i === 0 ? null : 'research-overlap__number', text);
                th.scope = 'col';
                headRow.append(th);
            });
            head.append(headRow);
            const body = element('tbody');
            [...current.lines].sort((a, b) => Number(b.isBasket) - Number(a.isBasket)).forEach((l) => {
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
         * Mark the preset whose period is the one shown, if any.
         * @returns {void}
         */
        function markPreset () {
            const length = range.to - range.from + 1;
            const atEnd = range.to === full.months.length - 1;
            const whole = range.from === 0 && atEnd;
            rangeButtons.forEach((button) => {
                const preset = button.dataset.performanceRange;
                const matches = preset === 'max' ? whole : atEnd && length === Number(preset);
                button.setAttribute('aria-pressed', String(matches));
            });
        }

        /**
         * Recompute and redraw the period's view.
         * @returns {void}
         */
        function render () {
            current = periodView(full, range, real, compare);
            markPreset();
            drawCallouts();
            drawLegend();
            drawTable();
            if (chartHost.clientWidth > 0) {
                drawChart();
            }
        }

        /**
         * Choose the period.
         * @param {{from: number, to: number}} next - The period, as indexes into the months.
         * @param {boolean} final - True when the choice is done (the brush let go, a preset chosen), to announce it.
         * @returns {void}
         */
        function setRange (next, final) {
            const same = range && next.from === range.from && next.to === range.to;
            if (!same) {
                range = next;
                if (crosshair !== null && crosshair > range.to - range.from + 1) {
                    crosshair = null;
                }
                render();
            }
            if (final && page.announce) {
                page.announce(`Showing ${formatMonth(current.start)} to ${formatMonth(current.end)}.`);
            }
        }

        /**
         * Rebuild for the basket: its months, the comparison list and the whole period.
         * @returns {void}
         */
        function draw () {
            viewButtons.forEach((button) => button.setAttribute('aria-pressed', String((button.dataset.performanceView === 'real') === real)));
            const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
            full = fullModel(data, macro, basket);
            prompt.hidden = Boolean(full);
            view.hidden = !full;
            crosshair = null;
            tooltip.hide();
            if (!full) {
                return;
            }
            fillCompare(basket);
            range = { from: 0, to: full.months.length - 1 };
            render();
            if (brushHost.clientWidth > 0) {
                drawBrush();
            }
        }

        viewButtons.forEach((button) => button.addEventListener('click', () => {
            if (button.disabled || !full) {
                return;
            }
            real = button.dataset.performanceView === 'real';
            viewButtons.forEach((b) => b.setAttribute('aria-pressed', String((b.dataset.performanceView === 'real') === real)));
            render();
            if (page.announce) {
                page.announce(real ? 'Showing growth adjusted for inflation.' : 'Showing nominal growth.');
            }
        }));
        rangeButtons.forEach((button) => button.addEventListener('click', () => {
            if (!full) {
                return;
            }
            const preset = button.dataset.performanceRange;
            const last = full.months.length - 1;
            const length = preset === 'max' ? full.months.length : Math.min(Number(preset), full.months.length);
            setRange({ from: last - length + 1, to: last }, true);
            moveBrush();
        }));
        compareSelect.addEventListener('change', () => {
            compare = compareSelect.value || null;
            if (full) {
                render();
            }
        });
        chartHost.addEventListener('keydown', (event) => {
            if (!current) {
                return;
            }
            const last = current.months.length;
            const moves = {
                ArrowLeft: () => Math.max(0, (crosshair === null ? last : crosshair) - 1),
                ArrowRight: () => Math.min(last, (crosshair === null ? 0 : crosshair) + 1),
                Home: () => 0,
                End: () => last
            };
            if (moves[event.key]) {
                event.preventDefault();
                showCrosshair(moves[event.key](), true);
            } else if (event.key === 'Escape') {
                showCrosshair(null, false);
            }
        });
        chartHost.addEventListener('focus', () => {
            if (current && crosshair === null) {
                showCrosshair(current.months.length, true);
            }
        });
        chartHost.addEventListener('blur', () => showCrosshair(null, false));
        window.addEventListener('scroll', () => tooltip.hide(), { passive: true });
        document.addEventListener('research:basketchange', draw);
        document.addEventListener('research:tabchange', () => {
            if (full && chartHost.clientWidth > 0) {
                drawChart();
                drawBrush();
            }
        });
        if (typeof ResizeObserver === 'function') {
            new ResizeObserver(() => {
                if (full && chartHost.clientWidth > 0 && Math.abs(chartHost.clientWidth - lastWidth) > 4) {
                    drawChart();
                    drawBrush();
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
        if (!page || typeof d3 === 'undefined' || !document.getElementById(VIEW_ID)) {
            return;
        }
        const data = page.readData(RETURNS_ID, 'Research fund returns data');
        if (!data || !Array.isArray(data.funds)) {
            return;
        }
        const funds = page.readData(FUNDS_ID, 'Research funds data');
        const names = new Map((funds && Array.isArray(funds.funds) ? funds.funds : []).map((f) => [f.ticker, f.name]));
        createView(data, page.readData(MACRO_ID, 'Research macro data'), names);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
