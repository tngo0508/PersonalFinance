/* global d3, PFI18n */
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
 * table cover it. Muted bands, each labelled, shade the Fed's hiking cycles (derived in the refresh from rises in the
 * federal funds rate) and the configured recessions, and a marker shows the basket's worst fall from a high. The
 * browser only weights and compounds the monthly returns of the snapshots.
 *
 * On the Fund page the card carries `data-performance-fund="TICKER"`: that fund takes the basket's place as the one
 * line drawn, and "Compare with" offers "Your basket" (first, and chosen when the basket holds a fund with returns)
 * besides every other Tracked Fund; the wording names the fund where it would name the basket.
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
    const DEFAULT_COMPARE = 'VT';
    const FUND_ATTRIBUTE = 'data-performance-fund';
    // The "Compare with" value of the basket, on the Fund page; never a ticker, which is upper case.
    const BASKET_COMPARE = 'basket';
    // The keys that name the basket, each with a Fund page twin (Research.Js.Performance.Fund.*) naming the fund.
    const FUND_KEYS = new Set(['BasketMinus', 'DiffTitle', 'DiffTitleNone', 'AllInBasket', 'ChartBelowCompare', 'ChartWorst',
        'LegendWorst', 'GrowthReal', 'TotalReturnLabel']);
    const MIN_MONTHS = 3;
    const MARGIN = { right: 64, left: 48 };
    const MAIN = { top: 16, height: 270 };
    const RATE = { top: 322, height: 64 };
    const DIFF = { top: 420, height: 92 };
    const HEIGHT = DIFF.top + DIFF.height + 26;
    const BRUSH_HEIGHT = 56;
    const LABEL_GAP = 13;
    // Up and down in the difference panel: the dataviz reference blue and orange, never red and green. Tokens with a
    // dark value each (site.css, #486), so the fills follow the theme without a redraw.
    const AHEAD = 'var(--app-research-ahead)';
    const BEHIND = 'var(--app-research-behind)';

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
     * @returns {string} E.g. "June 2026", in the page culture.
     */
    function formatMonth (key) {
        return page.formatMonth(key);
    }

    /**
     * The period between two months in words.
     * @param {string} from - The first month's key.
     * @param {string} to - The last month's key.
     * @returns {string} E.g. "June 2021 to May 2026".
     */
    function formatSpan (from, to) {
        return PFI18n.t('Research.Js.Common.FromTo', { from: formatMonth(from), to: formatMonth(to) });
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
     * @returns {string} E.g. "+12.3%" or "−4.0%", in the page culture.
     */
    function formatPercent (value) {
        const rounded = Math.round(value * 10) / 10;
        if (rounded === 0) {
            return page.formatPercent(0, 1);
        }
        return rounded < 0 ? `−${page.formatPercent(Math.abs(rounded), 1)}` : `+${page.formatPercent(rounded, 1)}`;
    }

    /**
     * Signed points with one decimal.
     * @param {number} value - The difference in points.
     * @returns {string} E.g. "+3.2 points", in the page culture.
     */
    function formatPoints (value) {
        const rounded = Math.round(value * 10) / 10;
        const number = rounded === 0
            ? page.formatFixed(0, 1)
            : `${rounded < 0 ? '−' : '+'}${page.formatFixed(Math.abs(rounded), 1)}`;
        return PFI18n.t('Research.Js.Performance.Points', { value: number });
    }

    /**
     * A time axis tick in the page culture: the year at January, else the short month name (as D3's default does).
     * @param {Date} date - The tick's date (UTC).
     * @returns {string} The label.
     */
    function formatTick (date) {
        return date.getUTCMonth() === 0
            ? PFI18n.formatDate(date, { year: 'numeric', timeZone: 'UTC' })
            : PFI18n.formatDate(date, { month: 'short', timeZone: 'UTC' });
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
     * @returns {{values: number[], total: number, annualized: number, worstFall: number, bottom: string|null,
     *     peakIndex: number, bottomIndex: number}} The line (100 at the base, then one value per month), its total and
     *     annualized return, worst fall from a high and the month that fall bottomed (percent), and the points of that
     *     high and that bottom.
     */
    function growth (returns, deflators, start) {
        const values = [100];
        let level = 1;
        let peak = 1;
        let peakAt = 0;
        let worstFall = 0;
        let bottom = null;
        let peakIndex = 0;
        let bottomIndex = 0;
        returns.forEach((r, i) => {
            level *= 1 + r / 100;
            const shown = deflators ? level / deflators[i] : level;
            values.push(shown * 100);
            if (shown > peak) {
                peak = shown;
                peakAt = i + 1;
            }
            const fall = shown / peak - 1;
            if (fall < worstFall) {
                worstFall = fall;
                bottom = addMonths(start, i);
                peakIndex = peakAt;
                bottomIndex = i + 1;
            }
        });
        const end = values[values.length - 1] / 100;
        return {
            values,
            total: (end - 1) * 100,
            annualized: (Math.pow(end, 12 / returns.length) - 1) * 100,
            worstFall: worstFall * 100,
            bottom,
            peakIndex,
            bottomIndex
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
        const periods = [
            ...((macro && macro.hikingCycles) || []).map((p) => ({ ...p, kind: 'hike' })),
            ...((macro && macro.recessions) || []).map((p) => ({ ...p, kind: 'recession' }))
        ].map((p) => ({ start: monthKey(p.start), end: monthKey(p.end), label: p.label, kind: p.kind }));
        return { months, fundReturns, basketReturns, cpi, canDeflate, rates, returnsOf, series, periods };
    }

    /**
     * The view of a period: each line rebased to 100 at the period's start, the comparison and the rates.
     * @param {Object} full - The full model.
     * @param {{from: number, to: number}} range - The period, as indexes into the full model's months.
     * @param {boolean} real - True to adjust for inflation.
     * @param {string|null} compare - The ticker to compare the basket with, "basket" for the basket on the Fund page, or
     *     null.
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
        const lines = full.subjectLabel
            ? []
            : full.fundReturns.map(({ fund, returns }) => line(fund.ticker, fund.slot, slice(returns), false));
        const basket = line(full.subjectLabel || PFI18n.t('Research.Js.Common.YourBasket'), 0, slice(full.basketReturns), true);
        lines.push(basket);
        let comparison = null;
        const compareReturns = compare === BASKET_COMPARE
            ? full.compareBasket
            : (compare && full.series.has(compare) ? full.returnsOf(compare) : null);
        if (compareReturns) {
            const raw = compareReturns.slice(range.from, range.to + 1);
            if (raw.every((r) => r !== null)) {
                const values = growth(raw, shownDeflators, start).values;
                comparison = { label: compareLabel(compare), values, difference: basket.values.map((v, i) => v - values[i]) };
            }
        }
        const rates = [base, ...months].map((m) => (full.rates.has(m) ? full.rates.get(m) : null));
        // A band covers its months, from the end of the month before its first to the end of its last, cut to the period.
        const bands = full.periods
            .filter((p) => p.end >= start && p.start <= end)
            .map((p) => ({ ...p, from: p.start < start ? base : addMonths(p.start, -1), to: p.end > end ? end : p.end }));
        return { start, end, base, months, lines, inflation, comparison, rates, bands, real: Boolean(shownDeflators) };
    }

    /**
     * The name of a "Compare with" choice.
     * @param {string} compare - A ticker, or "basket".
     * @returns {string} The ticker, or "Your basket".
     */
    function compareLabel (compare) {
        return compare === BASKET_COMPARE ? PFI18n.t('Research.Js.Common.YourBasket') : compare;
    }

    /**
     * The basket's monthly returns over the full model's months, for comparing a fund with it on the Fund page: each
     * month the weighted average of its funds' returns, the weights of a fund without one spread over the rest.
     * @param {Object} full - The full model, of the fund.
     * @param {Object[]} basket - The basket's funds (ticker, share).
     * @returns {(number|null)[]|null} The returns (null for a month none of its funds has), or null when none of its
     *     funds has returns.
     */
    function basketReturnsOver (full, basket) {
        const funds = basket.filter((f) => full.series.has(f.ticker))
            .map((f) => ({ share: f.share, returns: full.returnsOf(f.ticker) }));
        if (funds.length === 0) {
            return null;
        }
        return full.months.map((_, i) => {
            let weight = 0;
            let sum = 0;
            funds.forEach(({ share, returns }) => {
                if (returns[i] !== null) {
                    weight += share;
                    sum += share * returns[i];
                }
            });
            return weight > 0 ? sum / weight : null;
        });
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
        const fundTicker = card.getAttribute(FUND_ATTRIBUTE) || null;
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
         * A text of the chart, naming the fund on the Fund page where it would name the basket.
         * @param {string} key - The key under Research.Js.Performance, e.g. "DiffTitle".
         * @param {Object} [args] - The text's values.
         * @returns {string} The text.
         */
        function say (key, args) {
            return fundTicker && FUND_KEYS.has(key)
                ? PFI18n.t(`Research.Js.Performance.Fund.${key}`, { ...args, fund: fundTicker })
                : PFI18n.t(`Research.Js.Performance.${key}`, args);
        }

        /**
         * Fill the "Compare with" list with the Tracked Funds that have returns and aren't in the basket; on the Fund
         * page, with the basket (when it holds a fund with returns) and every other Tracked Fund.
         * @param {Object[]} basket - The basket's funds.
         * @returns {void}
         */
        function fillCompare (basket) {
            const left = fundTicker ? new Set([fundTicker]) : new Set(basket.map((f) => f.ticker));
            const choices = data.funds.map((f) => f.ticker).filter((t) => !left.has(t));
            const withBasket = Boolean(fundTicker && full.compareBasket);
            if (withBasket) {
                choices.unshift(BASKET_COMPARE);
            }
            if (!choices.includes(compare)) {
                compare = withBasket ? BASKET_COMPARE : (choices.includes(DEFAULT_COMPARE) ? DEFAULT_COMPARE : (choices[0] || null));
            }
            compareSelect.replaceChildren(...choices.map((ticker) => {
                const option = element('option', null, ticker === BASKET_COMPARE
                    ? compareLabel(ticker)
                    : (names.has(ticker) ? `${ticker}, ${names.get(ticker)}` : ticker));
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
            const rows = [current.real
                ? PFI18n.t('Research.Js.Performance.MonthReal', { month: formatMonth(month) })
                : formatMonth(month)];
            const valueRow = (label, value) =>
                PFI18n.t('Research.Js.Common.LabelValue', { label, value: page.formatFixed(value, 1) });
            [...current.lines].sort((a, b) => Number(b.isBasket) - Number(a.isBasket)).forEach((l) => {
                rows.push(valueRow(l.label, l.values[index]));
            });
            if (current.comparison) {
                rows.push(valueRow(current.comparison.label, current.comparison.values[index]));
                rows.push(say('BasketMinus',
                    { ticker: current.comparison.label, value: formatPoints(current.comparison.difference[index]) }));
            }
            const rate = current.rates[index];
            rows.push(rate === null
                ? PFI18n.t('Research.Js.Performance.RateNone')
                : PFI18n.t('Research.Js.Performance.Rate', { value: page.formatPercent(rate, 2) }));
            current.bands.filter((b) => month >= b.start && month <= b.end)
                .forEach((b) => rows.push(PFI18n.t('Research.Js.Performance.Shaded', { label: b.label })));
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

            // Shaded periods, under every panel, each labelled at the top of the main panel.
            const bands = svg.append('g').attr('class', 'research-performance__bands');
            current.bands.forEach((b) => {
                const x0 = x(monthEnd(b.from));
                const x1 = x(monthEnd(b.to));
                bands.append('rect').attr('class', `research-performance__band research-performance__band--${b.kind}`)
                    .attr('data-band', b.label).attr('x', x0).attr('width', Math.max(1, x1 - x0))
                    .attr('y', MAIN.top).attr('height', DIFF.top + DIFF.height - MAIN.top);
                bands.append('text').attr('class', 'research-performance__band-label')
                    .attr('x', x0 + 4).attr('y', MAIN.top + 11).text(b.label);
            });

            // Main panel: growth of 100.
            grid(svg, y, 5, (t) => PFI18n.formatNumber(t), width);
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
                .text((d) => PFI18n.t('Research.Js.Performance.EndLabel', {
                    label: d.line.isBasket && !fundTicker ? PFI18n.t('Research.Js.Performance.Basket') : d.line.label,
                    value: PFI18n.formatNumber(Math.round(d.line.values[d.line.values.length - 1]))
                }));

            // The basket's worst fall from a high: its high and its bottom, joined, with the fall written by the bottom.
            const basket = current.lines.find((l) => l.isBasket);
            if (basket.bottom) {
                const marker = svg.append('g').attr('class', 'research-performance__worst');
                const px = at(basket.peakIndex);
                const py = y(basket.values[basket.peakIndex]);
                const bx = at(basket.bottomIndex);
                const by = y(basket.values[basket.bottomIndex]);
                marker.append('line').attr('x1', px).attr('y1', py).attr('x2', bx).attr('y2', by);
                marker.append('circle').attr('cx', px).attr('cy', py).attr('r', 3.5);
                marker.append('circle').attr('cx', bx).attr('cy', by).attr('r', 3.5);
                const below = Math.min(by + 16, MAIN.top + MAIN.height - 4);
                marker.append('text').attr('class', 'research-performance__worst-label')
                    .attr('x', Math.min(bx, width - MARGIN.right - 4)).attr('y', below)
                    .attr('text-anchor', bx > width - MARGIN.right - 90 ? 'end' : 'middle')
                    .text(PFI18n.t('Research.Js.Performance.WorstFallLabel', { value: formatPercent(basket.worstFall) }));
            }

            // Rate panel: the effective federal funds rate.
            title(PFI18n.t('Research.Js.Performance.RateTitle'), RATE.top);
            grid(svg, yRate, 3, (t) => PFI18n.formatNumber(t), width);
            const ratePath = d3.line().defined((r) => r !== null).x((_, i) => at(i)).y((r) => yRate(r));
            svg.append('path').attr('class', 'research-performance__rate').attr('d', ratePath(current.rates));

            // Difference panel: the basket minus the chosen fund, blue above zero and orange below.
            title(current.comparison
                ? say('DiffTitle', { ticker: current.comparison.label })
                : say('DiffTitleNone'), DIFF.top);
            if (current.comparison) {
                grid(svg, yDiff, 3, (t) => (t > 0 ? `+${PFI18n.formatNumber(t)}` : PFI18n.formatNumber(t)), width);
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
                    .text(compare
                        ? PFI18n.t('Research.Js.Performance.NoReturns', { ticker: compareLabel(compare) })
                        : say('AllInBasket'));
            }

            svg.append('g').attr('class', 'research-performance__axis')
                .attr('transform', `translate(0,${DIFF.top + DIFF.height})`)
                .call(d3.axisBottom(x).ticks(Math.max(2, Math.floor(width / 110))).tickFormat(formatTick).tickSizeOuter(0));

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

            const words = current.lines.map((l) => PFI18n.t('Research.Js.Performance.EndLabel',
                { label: l.label, value: PFI18n.formatNumber(Math.round(l.values[l.values.length - 1])) })).join(', ');
            const shaded = current.bands.map((b) => PFI18n.t('Research.Js.Performance.ShadedSpan',
                { label: b.label, from: formatMonth(b.start), to: formatMonth(b.end) })).join('; ');
            const sentences = [
                PFI18n.t(current.real ? 'Research.Js.Performance.ChartLabelReal' : 'Research.Js.Performance.ChartLabel',
                    { from: formatMonth(current.start), to: formatMonth(current.end), lines: words }),
                current.comparison
                    ? say('ChartBelowCompare', { ticker: current.comparison.label })
                    : PFI18n.t('Research.Js.Performance.ChartBelow')
            ];
            if (shaded) {
                sentences.push(PFI18n.t('Research.Js.Performance.ChartShaded', { shaded }));
            }
            if (basket.bottom) {
                sentences.push(say('ChartWorst',
                    { value: formatPercent(basket.worstFall), month: formatMonth(basket.bottom) }));
            }
            chartHost.setAttribute('aria-label', sentences.join(' '));
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
                .call(d3.axisBottom(brushX).ticks(Math.max(2, Math.floor(width / 110))).tickFormat(formatTick).tickSizeOuter(0));
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
            const kinds = new Map(current.bands.map((b) => [b.label, b.kind]));
            kinds.forEach((kind, label) => {
                const item = element('li', 'research-performance__legend-item');
                const swatch = element('span', `research-performance__band-swatch research-performance__band-swatch--${kind}`);
                swatch.setAttribute('aria-hidden', 'true');
                item.append(swatch, PFI18n.t('Research.Js.Performance.LegendShaded', { label }));
                legend.append(item);
            });
            if (current.lines.find((l) => l.isBasket).bottom) {
                const item = element('li', 'research-performance__legend-item');
                const swatch = element('span', 'research-performance__worst-swatch');
                swatch.setAttribute('aria-hidden', 'true');
                item.append(swatch, say('LegendWorst'));
                legend.append(item);
            }
        }

        /**
         * Draw the callouts: the basket's total and annualized return, its worst fall, and inflation.
         * @returns {void}
         */
        function drawCallouts () {
            const basket = current.lines.find((l) => l.isBasket);
            const period = formatSpan(current.start, current.end);
            const items = [
                {
                    label: say(current.real ? 'GrowthReal' : 'TotalReturnLabel'),
                    value: formatPercent(basket.total),
                    note: PFI18n.t('Research.Js.Performance.PerYear', { value: formatPercent(basket.annualized), period })
                },
                {
                    label: PFI18n.t('Research.Js.Performance.WorstFall'),
                    value: formatPercent(basket.worstFall),
                    note: basket.bottom
                        ? PFI18n.t('Research.Js.Performance.WorstFallNote', { month: formatMonth(basket.bottom) })
                        : PFI18n.t('Research.Js.Performance.WorstFallNone')
                }
            ];
            if (current.inflation !== null) {
                items.push({
                    label: PFI18n.t('Research.Js.Performance.Inflation'),
                    value: formatPercent(current.inflation),
                    note: PFI18n.t('Research.Js.Performance.InflationNote')
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
            table.append(element('caption', null, PFI18n.t('Research.Js.Performance.TableCaption',
                { from: formatMonth(current.start), to: formatMonth(current.end) })));
            const head = element('thead');
            const headRow = element('tr');
            [
                '',
                PFI18n.t('Research.Js.Common.AnnualizedReturn'),
                PFI18n.t('Research.Js.Performance.TotalReturn'),
                PFI18n.t('Research.Js.Performance.WorstFall'),
                PFI18n.t('Research.Js.Performance.AfterInflation')
            ].forEach((text, i) => {
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
                page.announce(PFI18n.t('Research.Js.Performance.ShowingPeriod', { period: formatSpan(current.start, current.end) }));
            }
        }

        /**
         * Rebuild for the basket (on the Fund page, for the fund, compared with the basket): its months, the comparison
         * list and the whole period.
         * @returns {void}
         */
        function draw () {
            viewButtons.forEach((button) => button.setAttribute('aria-pressed', String((button.dataset.performanceView === 'real') === real)));
            const basket = window.ResearchBasket ? window.ResearchBasket.get() : [];
            full = fundTicker
                ? fullModel(data, macro, [{ ticker: fundTicker, share: 1, slot: 0 }])
                : fullModel(data, macro, basket);
            if (full && fundTicker) {
                full.subjectLabel = fundTicker;
                full.compareBasket = basketReturnsOver(full, basket);
            }
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
                page.announce(PFI18n.t(real ? 'Research.Js.Performance.ShowingReal' : 'Research.Js.Performance.ShowingNominal'));
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
