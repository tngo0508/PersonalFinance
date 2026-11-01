/* global d3, PFI18n */
/**
 * Research page, Risk vs return tab: each Tracked Fund's annualized return against its volatility over a 1-, 3- or
 * 5-year window (3 years by default).
 *
 * Fund points use the figures the refresh precomputed (the fund returns snapshot's windows: annualized return,
 * volatility, Sharpe ratio and the Treasury bill rate it used). Bubble area follows each fund's net assets, from the
 * fund facts snapshot. "Your basket" is a dark diamond the browser computes (research-basket-math.js) from its funds'
 * monthly returns over the same months: rebalanced monthly to its weights, annualized return, volatility (sample
 * standard deviation of the monthly returns times √12) and a Sharpe ratio against the average 3-month Treasury bill
 * (from the macro snapshot).
 *
 * Every point is focusable and opens a card with its return, volatility, Sharpe ratio, expense ratio and net assets;
 * "Show as table" lists the funds and the basket sorted by Sharpe ratio, with inline bars. Role chips narrow the funds
 * shown, keeping the basket and every fund's colour; dashed lines cross at the basket, with descriptive corner labels
 * (never a "better" zone); a label that would overlap another moves aside on a short leader line. Reads the
 * `research-fund-returns-data`, `research-fund-facts-data`, `research-macro-data` and `research-funds-data` islands,
 * and the basket from `window.ResearchBasket`, redrawing on `research:basketchange`. On the Fund page the card's
 * `data-risk-highlight="TICKER"` rings that fund's bubble and bolds its label, and the role chips never hide it.
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
    const ROLES_ID = 'research-risk-roles';
    const LABEL_HEIGHT = 13;
    const QUADRANTS = [
        { x: 'left', y: 'top', key: 'Research.Js.Risk.Quadrant.HigherSmaller' },
        { x: 'right', y: 'top', key: 'Research.Js.Risk.Quadrant.HigherBigger' },
        { x: 'left', y: 'bottom', key: 'Research.Js.Risk.Quadrant.LowerSmaller' },
        { x: 'right', y: 'bottom', key: 'Research.Js.Risk.Quadrant.LowerBigger' }
    ];
    // The basket's id in data attributes; what the page shows is its translated label.
    const BASKET_ID = 'Your basket';
    const DEFAULT_YEARS = 3;
    const HEIGHT = 420;
    const MARGIN = { top: 20, right: 28, bottom: 44, left: 56 };
    const RADIUS = [6, 26];
    const DEFAULT_RADIUS = 8;
    const DIAMOND = 9;

    /**
     * A number with a true minus sign and fixed decimals.
     * @param {number} value - The value.
     * @param {number} digits - The decimals.
     * @returns {string} E.g. "−0.42", in the page culture.
     */
    function signed (value, digits) {
        const rounded = Number(Math.abs(value).toFixed(digits));
        const text = page.formatFixed(rounded, digits);
        return value < 0 && rounded !== 0 ? `−${text}` : text;
    }

    /**
     * The basket's label, in the page language.
     * @returns {string} "Your basket".
     */
    function basketLabel () {
        return PFI18n.t('Research.Js.Common.YourBasket');
    }

    /**
     * A window in words.
     * @param {number} years - The window's length in years.
     * @returns {string} E.g. "3 years".
     */
    function span (years) {
        return page.formatYears(years);
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
     * The basket's point for a window, from the shared basket figures (research-basket-math.js).
     * @param {Object} data - The fund returns snapshot.
     * @param {Object|null} macro - The macro snapshot.
     * @param {Object[]} basket - The basket's funds (ticker, share).
     * @param {number} years - The window's length in years.
     * @returns {Object|null} The point, or null when a basket fund lacks a month of the window.
     */
    function basketPoint (data, macro, basket, years) {
        const figures = window.ResearchBasketMath.point(data, macro, basket, years);
        return figures ? { ...figures, ticker: BASKET_ID, label: basketLabel(), isBasket: true } : null;
    }

    /**
     * Whether two boxes overlap.
     * @param {{x: number, y: number, w: number, h: number}} a - One box.
     * @param {{x: number, y: number, w: number, h: number}} b - The other.
     * @returns {boolean} True when they overlap.
     */
    function overlaps (a, b) {
        return a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
    }

    /**
     * Place each label at the first spot around its mark that overlaps no placed label and no mark, nearest first,
     * keeping inside the plot; a label placed away from its mark gets a leader line.
     * @param {{cx: number, cy: number, r: number, w: number}[]} labels - Each mark's centre, radius and label width.
     * @param {{x: number, y: number, w: number, h: number}} plot - The plot area.
     * @returns {{x: number, y: number, leader: boolean}[]} Each label's top-left corner and whether it needs a leader.
     */
    function placeLabels (labels, plot) {
        const marks = labels.map((l) => ({ x: l.cx - l.r, y: l.cy - l.r, w: l.r * 2, h: l.r * 2 }));
        const placed = [];
        return labels.map((label, index) => {
            const h = LABEL_HEIGHT;
            const near = [
                [label.r + 4, -h / 2], [-label.r - 4 - label.w, -h / 2], [-label.w / 2, -label.r - 4 - h], [-label.w / 2, label.r + 4]
            ];
            const far = [];
            for (let step = 1; step <= 6; step++) {
                const d = label.r + 10 + step * 12;
                far.push([d, -h / 2 - step * 10], [d, -h / 2 + step * 10], [-d - label.w, -h / 2 - step * 10],
                    [-d - label.w, -h / 2 + step * 10], [-label.w / 2, -d - h], [-label.w / 2, d]);
            }
            const candidates = [...near.map((c) => [...c, false]), ...far.map((c) => [...c, true])];
            const fits = (box) => box.x >= plot.x && box.x + box.w <= plot.x + plot.w && box.y >= plot.y && box.y + box.h <= plot.y + plot.h
                && !placed.some((p) => overlaps(p, box)) && !marks.some((m, i) => i !== index && overlaps(m, box));
            let chosen = null;
            for (const [dx, dy, leader] of candidates) {
                const box = { x: label.cx + dx, y: label.cy + dy, w: label.w, h, leader };
                if (fits(box)) {
                    chosen = box;
                    break;
                }
            }
            if (!chosen) {
                chosen = { x: label.cx + near[0][0], y: label.cy + near[0][1], w: label.w, h, leader: false };
            }
            placed.push(chosen);
            return chosen;
        });
    }

    /**
     * Wire the Risk vs return view.
     * @param {Object} data - The fund returns snapshot.
     * @param {Object|null} facts - The fund facts snapshot.
     * @param {Object|null} macro - The macro snapshot.
     * @param {Map<string, string>} names - Each Tracked Fund's name by ticker.
     * @param {Map<string, string>} roles - Each Tracked Fund's role by ticker.
     * @returns {void}
     */
    function createView (data, facts, macro, names, roles) {
        const card = document.getElementById(CARD_ID);
        const chartHost = document.getElementById(CHART_ID);
        const legend = document.getElementById(LEGEND_ID);
        const note = document.getElementById(NOTE_ID);
        const tableHost = document.getElementById(TABLE_ID);
        const tableToggle = document.getElementById(TABLE_TOGGLE_ID);
        const yearButtons = Array.from(card.querySelectorAll('[data-risk-years]'));
        const rolesHost = document.getElementById(ROLES_ID);
        const roleNames = [...new Set(data.funds.map((f) => roles.get(f.ticker)).filter(Boolean))];
        const activeRoles = new Set();
        const highlight = (card.getAttribute('data-risk-highlight') || '').toUpperCase();
        const factsOf = new Map((facts ? facts.funds : []).map((f) => [f.ticker, f]));
        const slotOf = (ticker) => (window.ResearchBasket && window.ResearchBasket.slotOf ? window.ResearchBasket.slotOf(ticker) : 0);
        const tooltip = page.createTooltip(document.getElementById(TOOLTIP_ID), (line, point) => cardRows(point)
            .map((row, i) => line(row, i === 0 ? 'research-tooltip__name' : 'research-tooltip__muted')));
        // The window the page marked as pressed (the Funds page's ?window=), else 3 years.
        const pressed = yearButtons.find((button) => button.getAttribute('aria-pressed') === 'true');
        let years = pressed ? Number(pressed.dataset.riskYears) : DEFAULT_YEARS;
        let points = [];
        let allPoints = [];
        let lastWidth = 0;

        /**
         * The card's rows for a point.
         * @param {Object} point - The point.
         * @returns {string[]} The rows, its name first.
         */
        function cardRows (point) {
            const rows = [point.isBasket ? basketLabel() : `${point.ticker}${names.has(point.ticker) ? `, ${names.get(point.ticker)}` : ''}`,
                PFI18n.t('Research.Js.Risk.CardReturn', { value: `${signed(point.annualizedReturn, 1)}%` }),
                PFI18n.t('Research.Js.Risk.CardVolatility', { value: page.formatPercent(point.volatility, 1) }),
                point.sharpe === null || point.sharpe === undefined
                    ? PFI18n.t('Research.Js.Risk.CardSharpeNone')
                    : PFI18n.t('Research.Js.Risk.CardSharpe',
                        { value: signed(point.sharpe, 2), bill: page.formatPercent(point.treasuryBill, 2) })];
            if (!point.isBasket) {
                const fact = factsOf.get(point.ticker);
                rows.push(fact && fact.expenseRatio !== null && fact.expenseRatio !== undefined
                    ? PFI18n.t('Research.Js.Risk.CardExpense', { value: page.formatPercent(fact.expenseRatio, 2) })
                    : PFI18n.t('Research.Js.Risk.CardExpenseNone'));
                rows.push(fact && fact.netAssets
                    ? PFI18n.t('Research.Js.Risk.CardNetAssets', { value: page.formatMoney(fact.netAssets) })
                    : PFI18n.t('Research.Js.Risk.CardNetAssetsNone'));
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
                ? PFI18n.t('Research.Js.Risk.NoteEmpty')
                : basketFigures ? '' : PFI18n.t('Research.Js.Risk.NoteShort', { span: span(years) });
            allPoints = basketFigures ? [...funds, basketFigures] : funds;
            const shown = funds.filter((f) => activeRoles.size === 0 || activeRoles.has(roles.get(f.ticker)) || f.ticker === highlight);
            return basketFigures ? [...shown, basketFigures] : shown;
        }

        /**
         * Draw the role chips: one per role, pressed when it is among those shown.
         * @returns {void}
         */
        function drawRoles () {
            rolesHost.hidden = roleNames.length < 2;
            rolesHost.replaceChildren(...roleNames.map((role) => {
                const chip = element('button', 'research-chip research-risk__role', role);
                chip.type = 'button';
                chip.setAttribute('aria-pressed', String(activeRoles.has(role)));
                chip.addEventListener('click', () => {
                    if (activeRoles.has(role)) {
                        activeRoles.delete(role);
                    } else {
                        activeRoles.add(role);
                    }
                    drawRoles();
                    draw();
                    if (page.announce) {
                        page.announce(activeRoles.size === 0
                            ? PFI18n.t('Research.Js.Risk.ShowingAll')
                            : PFI18n.t('Research.Js.Risk.ShowingRoles', { roles: [...activeRoles].join(', ') }));
                    }
                });
                return chip;
            }));
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
            // The axes cover every fund, shown or not, so filtering never moves a point.
            const xMax = d3.max(allPoints, (p) => p.volatility) || 1;
            const yValues = allPoints.map((p) => p.annualizedReturn);
            const x = d3.scaleLinear().domain([0, xMax * 1.08]).nice().range([MARGIN.left, width - MARGIN.right]);
            const y = d3.scaleLinear().domain([Math.min(0, d3.min(yValues)), Math.max(0, d3.max(yValues))]).nice()
                .range([HEIGHT - MARGIN.bottom, MARGIN.top]);
            const assets = allPoints.filter((p) => !p.isBasket && p.netAssets).map((p) => p.netAssets);
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
                .call(d3.axisBottom(x).ticks(Math.max(3, Math.floor(width / 90)))
                    .tickFormat((t) => `${PFI18n.formatNumber(t)}%`).tickSizeOuter(0));
            svg.append('text').attr('class', 'research-performance__panel-title').attr('x', width - MARGIN.right)
                .attr('y', HEIGHT - 8).attr('text-anchor', 'end').text(PFI18n.t('Research.Js.Risk.AxisX'));
            svg.append('text').attr('class', 'research-performance__panel-title').attr('x', MARGIN.left)
                .attr('y', MARGIN.top - 8).text(PFI18n.t('Research.Js.Risk.AxisY'));

            const basketPoint = points.find((p) => p.isBasket);
            if (basketPoint) {
                // Dashed lines through the basket, and each corner described against it.
                const bx = x(basketPoint.volatility);
                const by = y(basketPoint.annualizedReturn);
                const quadrants = svg.append('g').attr('class', 'research-risk__quadrants');
                quadrants.append('line').attr('x1', bx).attr('x2', bx).attr('y1', MARGIN.top).attr('y2', HEIGHT - MARGIN.bottom);
                quadrants.append('line').attr('x1', MARGIN.left).attr('x2', width - MARGIN.right).attr('y1', by).attr('y2', by);
                QUADRANTS.forEach((q) => {
                    quadrants.append('text').attr('class', 'research-risk__quadrant')
                        .attr('x', q.x === 'left' ? MARGIN.left + 6 : width - MARGIN.right - 6)
                        .attr('y', q.y === 'top' ? MARGIN.top + 12 : HEIGHT - MARGIN.bottom - 8)
                        .attr('text-anchor', q.x === 'left' ? 'start' : 'end')
                        .text(PFI18n.t(q.key));
                });
            }

            const funds = points.filter((p) => !p.isBasket).sort((a, b) => radiusOf(b) - radiusOf(a));
            const marks = svg.append('g');
            funds.forEach((p) => {
                const g = marks.append('g')
                    .attr('class', p.ticker === highlight ? 'research-risk__point research-risk__point--highlight' : 'research-risk__point')
                    .attr('tabindex', 0)
                    .attr('data-ticker', p.ticker)
                    .attr('transform', `translate(${x(p.volatility)},${y(p.annualizedReturn)})`)
                    .attr('aria-label', cardRows(p).join('. '));
                g.append('circle').attr('class', 'research-risk__bubble').attr('data-fund-slot', String(p.slot)).attr('r', radiusOf(p));
            });
            const basket = points.find((p) => p.isBasket);
            if (basket) {
                const g = marks.append('g').attr('class', 'research-risk__point research-risk__point--basket').attr('tabindex', 0)
                    .attr('data-ticker', BASKET_ID)
                    .attr('transform', `translate(${x(basket.volatility)},${y(basket.annualizedReturn)})`)
                    .attr('aria-label', cardRows(basket).join('. '));
                g.append('path').attr('class', 'research-risk__diamond')
                    .attr('d', `M0,${-DIAMOND}L${DIAMOND},0L0,${DIAMOND}L${-DIAMOND},0Z`);
            }

            // Labels: measured, then placed clear of each other and of the marks, with leaders where they moved away.
            const labelled = [...funds.map((p) => ({ point: p, r: radiusOf(p), text: p.ticker })),
                ...(basket ? [{ point: basket, r: DIAMOND, text: basketLabel() }] : [])];
            const labelLayer = svg.append('g').attr('class', 'research-risk__labels');
            const texts = labelled.map((l) => labelLayer.append('text')
                .attr('class', l.point.isBasket ? 'research-risk__label research-risk__label--basket'
                    : (l.point.ticker === highlight ? 'research-risk__label research-risk__label--highlight' : 'research-risk__label'))
                .attr('data-label', l.text).text(l.text));
            const spots = placeLabels(labelled.map((l, i) => ({
                cx: x(l.point.volatility),
                cy: y(l.point.annualizedReturn),
                r: l.r,
                w: texts[i].node().getComputedTextLength() || l.text.length * 6.5
            })), { x: MARGIN.left, y: MARGIN.top, w: width - MARGIN.left - MARGIN.right, h: HEIGHT - MARGIN.top - MARGIN.bottom });
            spots.forEach((spot, i) => {
                texts[i].attr('x', spot.x).attr('y', spot.y + LABEL_HEIGHT - 3);
                if (spot.leader) {
                    const cx = x(labelled[i].point.volatility);
                    const cy = y(labelled[i].point.annualizedReturn);
                    const tx = Math.max(spot.x, Math.min(cx, spot.x + spot.w));
                    const ty = Math.max(spot.y, Math.min(cy, spot.y + spot.h));
                    labelLayer.insert('line', 'text').attr('class', 'research-risk__leader')
                        .attr('x1', cx).attr('y1', cy).attr('x2', tx).attr('y2', ty);
                }
            });
            marks.selectAll('.research-risk__point').each(function () {
                const node = this;
                const ticker = node.getAttribute('data-ticker');
                const point = points.find((p) => (p.isBasket ? BASKET_ID : p.ticker) === ticker);
                node.addEventListener('pointerenter', () => showCard(point, node));
                node.addEventListener('pointerleave', tooltip.hide);
                node.addEventListener('focus', () => showCard(point, node));
                node.addEventListener('blur', tooltip.hide);
            });
            const fundsText = PFI18n.plural('Research.Js.Funds.FundCount', funds.length)
                + (activeRoles.size ? ` (${[...activeRoles].join(', ')})` : '');
            chartHost.setAttribute('aria-label', PFI18n.t(basket ? 'Research.Js.Risk.ChartLabelBasket' : 'Research.Js.Risk.ChartLabel',
                { span: span(years), funds: fundsText }));
        }

        /**
         * Draw the legend: what the marks mean.
         * @returns {void}
         */
        function drawLegend () {
            const items = [
                ['research-risk__legend-bubble', PFI18n.t(facts ? 'Research.Js.Risk.LegendFund' : 'Research.Js.Risk.LegendFundNoFacts')],
                ['research-risk__legend-diamond', basketLabel()]
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
            table.append(element('caption', null, PFI18n.t('Research.Js.Risk.TableCaption', { span: span(years) })));
            const head = element('thead');
            const headRow = element('tr');
            [
                '',
                PFI18n.t('Research.Js.Common.AnnualizedReturn'),
                PFI18n.t('Research.Js.Risk.Volatility'),
                PFI18n.t('Research.Js.Risk.Sharpe'),
                PFI18n.t('Research.Js.Risk.Expense')
            ].forEach((text, i) => {
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
                tr.dataset.ticker = p.isBasket ? BASKET_ID : p.ticker;
                const name = element('th', null, p.isBasket ? basketLabel() : p.ticker);
                name.scope = 'row';
                const fact = p.isBasket ? null : factsOf.get(p.ticker);
                tr.append(name,
                    bar(p.annualizedReturn, maxReturn, 'research-risk__bar--return'),
                    bar(p.volatility, maxVolatility, 'research-risk__bar--volatility'),
                    element('td', 'research-overlap__number', p.sharpe === null || p.sharpe === undefined
                        ? PFI18n.t('Research.Js.Common.NotAvailable')
                        : signed(p.sharpe, 2)),
                    element('td', 'research-overlap__number', p.isBasket ? '–'
                        : fact && fact.expenseRatio !== null && fact.expenseRatio !== undefined
                            ? page.formatPercent(fact.expenseRatio, 2)
                            : PFI18n.t('Research.Js.Common.NotAvailable')));
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

        drawRoles();
        yearButtons.forEach((button) => button.addEventListener('click', () => {
            years = Number(button.dataset.riskYears);
            draw();
            if (page.announce) {
                page.announce(PFI18n.t('Research.Js.Risk.Announce', { span: span(years) }));
            }
        }));
        tableToggle.addEventListener('click', () => {
            const show = tableHost.hidden;
            tableHost.hidden = !show;
            tableToggle.setAttribute('aria-pressed', String(show));
            tableToggle.textContent = PFI18n.t(show ? 'Research.Js.Common.HideTable' : 'Research.Js.Common.ShowTable');
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
        if (!page || typeof d3 === 'undefined' || !window.ResearchBasketMath || !document.getElementById(CHART_ID)) {
            return;
        }
        const data = page.readData(RETURNS_ID, 'Research fund returns data');
        if (!data || !Array.isArray(data.windows)) {
            return;
        }
        const funds = page.readData(FUNDS_ID, 'Research funds data');
        const tracked = funds && Array.isArray(funds.funds) ? funds.funds : [];
        const names = new Map(tracked.map((f) => [f.ticker, f.name]));
        const roles = new Map(tracked.filter((f) => f.role).map((f) => [f.ticker, f.role]));
        createView(data, page.readData(FACTS_ID, 'Research fund facts data'), page.readData(MACRO_ID, 'Research macro data'),
            names, roles);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
