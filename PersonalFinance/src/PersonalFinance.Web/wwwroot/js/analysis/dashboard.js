/* global PFI18n, ChartPalette */
(function () {
    'use strict';

    /** The charts drawn on the page, so a theme switch can redraw them. */
    const charts = [];

    /**
     * The currency of every amount on the page: the Account's Reporting Currency (ADR 0016), from the page root.
     * @returns {string} ISO 4217 code.
     */
    function pageCurrency () {
        const page = document.querySelector('.analysis-page');
        return (page && page.dataset.currency) || 'USD';
    }

    /**
     * Format an amount as whole currency units in the page culture, e.g. "$1,800".
     * @param {number} amount - The amount.
     * @returns {string} The formatted amount.
     */
    function money (amount) {
        return PFI18n.formatMoney(amount, pageCurrency(), { whole: true });
    }

    /**
     * Scroll the month strip so the selected month is in view; on a phone it would otherwise start off screen.
     * @returns {void}
     */
    function revealSelectedMonth () {
        const strip = document.querySelector('.analysis-period__months');
        const current = strip && strip.querySelector('[aria-current="page"]');
        if (!current || strip.scrollWidth <= strip.clientWidth) {
            return;
        }
        const chip = current.getBoundingClientRect();
        const box = strip.getBoundingClientRect();
        if (chip.right > box.right || chip.left < box.left) {
            strip.scrollLeft += chip.left - box.left - (box.width - chip.width) / 2;
        }
    }

    /**
     * Initialize the analysis dashboard charts, and redraw them in the new colours when the theme changes (#489).
     * @returns {void}
     */
    function init () {
        revealSelectedMonth();
        const data = window.__analysisData;
        if (!data) {
            return;
        }

        renderAll(data);
        window.ChartPalette.subscribe(() => {
            charts.splice(0).forEach((chart) => chart.destroy());
            renderAll(data);
        });
    }

    /**
     * Draw every chart in the colours of the theme in effect, from the shared chart palette (core/chart-palette.js).
     * @param {Object} data - The page data island.
     * @returns {void}
     */
    function renderAll (data) {
        const palette = window.ChartPalette.read();
        const extra = window.ChartPalette.readTokens(['--app-money-flow-spending', '--app-accent-target', '--app-text']);
        palette.expenses = extra[0];
        palette.net = extra[1];
        palette.text = extra[2];
        palette.tone = { over: palette.negative, ahead: palette.warning, ok: palette.positive };

        const selected = selectedIndex(data);
        renderSparklines(data.trendPoints, selected, palette);
        renderPaceChart(data.pace, data.weeklyTrend, palette);
        renderTrendCharts(data.trendPoints, selected, palette);
    }

    /**
     * The position of the selected month in the trend points, or the last point for a whole-year view.
     * @param {Object} data - The page data island.
     * @returns {number} The index, or -1 without trend points.
     */
    function selectedIndex (data) {
        const points = data.trendPoints || [];
        const index = points.findIndex((p) => p.monthNumber === data.selectedMonth);
        return index >= 0 ? index : points.length - 1;
    }

    /**
     * Keeps a drawn chart, so a theme switch can redraw it.
     * @param {Chart} chart - The chart.
     * @returns {void}
     */
    function keep (chart) {
        charts.push(chart);
    }

    /**
     * The tooltip colours every chart on the page shares.
     * @param {Object} palette - The chart palette.
     * @returns {Object} Chart.js tooltip options, without callbacks.
     */
    function themedTooltip (palette) {
        return {
            backgroundColor: palette.tooltip.background,
            titleColor: palette.tooltip.text,
            bodyColor: palette.tooltip.text,
            borderColor: palette.tooltip.border,
            borderWidth: 1
        };
    }

    /**
     * An axis in the palette's gridline and axis-text colours.
     * @param {Object} palette - The chart palette.
     * @param {Object} axis - The axis' own options.
     * @returns {Object} Chart.js scale options.
     */
    function themedScale (palette, axis) {
        return Object.assign({}, axis, {
            grid: Object.assign({ color: palette.grid }, axis.grid),
            border: { color: palette.grid },
            ticks: Object.assign({ color: palette.axis }, axis.ticks)
        });
    }

    /**
     * A money axis whose labels are whole currency units.
     * @param {Object} palette - The chart palette.
     * @param {Object} [axis] - The axis' own options.
     * @returns {Object} Chart.js scale options.
     */
    function moneyScale (palette, axis) {
        return themedScale(palette, Object.assign({}, axis, {
            ticks: { maxTicksLimit: 5, callback: (value) => money(value) }
        }));
    }

    /**
     * A small line of each summary figure across the year, with the selected month marked.
     * @param {Array} points - The monthly trend points.
     * @param {number} selected - The selected point's index.
     * @param {Object} palette - The chart palette.
     * @returns {void}
     */
    function renderSparklines (points, selected, palette) {
        if (!points || points.length < 2) {
            return;
        }

        const colours = { income: palette.positive, expenses: palette.expenses, netSavings: palette.net, savingsRate: palette.net };
        document.querySelectorAll('canvas[data-spark]').forEach((canvas) => {
            const series = canvas.dataset.spark;
            const colour = colours[series] || palette.axis;
            keep(new Chart(canvas.getContext('2d'), {
                type: 'line',
                data: {
                    labels: points.map((p) => p.monthName),
                    datasets: [{
                        data: points.map((p) => p[series]),
                        borderColor: colour,
                        borderWidth: 1.75,
                        tension: 0.3,
                        pointRadius: points.map((_p, i) => (i === selected ? 3 : 0)),
                        pointBackgroundColor: colour,
                        pointBorderWidth: 0
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    events: [],
                    layout: { padding: 4 },
                    plugins: { legend: { display: false }, tooltip: { enabled: false } },
                    scales: { x: { display: false }, y: { display: false } }
                }
            }));
        });
    }

    /**
     * Draws a vertical "today" line across the chart area at the x value in the plugin options.
     * @type {Object}
     */
    const todayLine = {
        id: 'analysisTodayLine',
        afterDatasetsDraw: function (chart, _args, options) {
            if (options.day === null || options.day === undefined) {
                return;
            }
            const x = chart.scales.x.getPixelForValue(options.day);
            const area = chart.chartArea;
            const ctx = chart.ctx;
            ctx.save();
            ctx.strokeStyle = options.color;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(x, area.top);
            ctx.lineTo(x, area.bottom);
            ctx.stroke();
            ctx.fillStyle = options.color;
            ctx.font = `600 11px ${Chart.defaults.font.family}`;
            ctx.textAlign = 'right';
            ctx.fillText(options.label, x - 6, area.bottom - 6);
            ctx.restore();
        }
    };

    /**
     * Cumulative spending against an even spread of the budget, with the projection to month end while the month is
     * open. The spending line steps through the weekly totals (days 1-7, 8-14, ...); without transactions it shows only
     * today's total rather than inventing a path to it.
     * @param {Object|null} pace - The velocity figures from the page model.
     * @param {Array} weeks - The weekly spending totals, in order.
     * @param {Object} palette - The chart palette.
     * @returns {void}
     */
    function renderPaceChart (pace, weeks, palette) {
        const canvas = document.getElementById('paceChart');
        if (!canvas || !pace) {
            return;
        }

        const tone = palette.tone[pace.tone] || palette.positive;
        const elapsed = pace.daysElapsed;
        let spent = [];
        if (weeks && weeks.length > 0) {
            let running = 0;
            spent.push({ x: 0, y: 0 });
            weeks.forEach((week, index) => {
                const endDay = Math.min((index + 1) * 7, pace.daysInMonth, Math.ceil(elapsed));
                if (endDay > spent[spent.length - 1].x) {
                    running += week.totalSpent;
                    spent.push({ x: Math.min(endDay, elapsed), y: running });
                }
            });
        } else {
            spent = [{ x: elapsed, y: pace.spentSoFar }];
        }

        const datasets = [
            {
                label: PFI18n.t('Analysis.Js.SpentSoFar'),
                data: spent,
                borderColor: tone,
                backgroundColor: ChartPalette.withAlpha(tone, 0.12),
                fill: spent.length > 1 ? 'origin' : false,
                borderWidth: 2.5,
                pointRadius: spent.map((_p, i) => (i === spent.length - 1 ? 4 : 0)),
                pointBackgroundColor: tone,
                tension: 0
            },
            {
                label: PFI18n.t('Analysis.Js.BudgetPace'),
                data: [{ x: 0, y: 0 }, { x: pace.daysInMonth, y: pace.budget }],
                borderColor: palette.axis,
                borderDash: [5, 4],
                borderWidth: 1.5,
                pointRadius: 0
            }
        ];
        if (pace.open) {
            datasets.push({
                label: PFI18n.t('Analysis.Js.Projection'),
                data: [{ x: elapsed, y: pace.spentSoFar }, { x: pace.daysInMonth, y: pace.projected }],
                borderColor: tone,
                borderDash: [3, 4],
                borderWidth: 2,
                pointRadius: [0, 3],
                pointBackgroundColor: tone
            });
        }

        keep(new Chart(canvas.getContext('2d'), {
            type: 'line',
            data: { datasets: datasets },
            plugins: [todayLine],
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'nearest', intersect: false },
                plugins: {
                    legend: { position: 'bottom', labels: { color: palette.axis, boxWidth: 18, boxHeight: 2 } },
                    tooltip: Object.assign(themedTooltip(palette), {
                        callbacks: {
                            title: (items) => PFI18n.t('Analysis.Js.Day', { day: Math.round(items[0].parsed.x) }),
                            label: (context) => PFI18n.t('Analysis.Js.SeriesTooltip', { series: context.dataset.label, amount: money(context.parsed.y) })
                        }
                    }),
                    analysisTodayLine: { day: pace.open ? elapsed : null, color: palette.axis, label: PFI18n.t('Analysis.Js.Today') }
                },
                scales: {
                    x: themedScale(palette, {
                        type: 'linear',
                        min: 0,
                        max: pace.daysInMonth,
                        grid: { display: false },
                        ticks: { stepSize: 7, includeBounds: false, callback: (value) => PFI18n.t('Analysis.Js.Day', { day: value === 0 ? 1 : value }) }
                    }),
                    y: moneyScale(palette, { beginAtZero: true })
                }
            }
        }));
    }

    /**
     * Writes each bar's label above it: the savings rate on the net savings bars.
     * @type {Object}
     */
    const barLabels = {
        id: 'analysisBarLabels',
        afterDatasetsDraw: function (chart, _args, options) {
            const meta = chart.getDatasetMeta(0);
            const ctx = chart.ctx;
            ctx.save();
            ctx.textAlign = 'center';
            meta.data.forEach((bar, index) => {
                const value = chart.data.datasets[0].data[index];
                ctx.fillStyle = index === options.selected ? options.strong : options.color;
                ctx.font = `${index === options.selected ? '600 ' : ''}11px ${Chart.defaults.font.family}`;
                ctx.textBaseline = value < 0 ? 'top' : 'bottom';
                ctx.fillText(options.labels[index], bar.x, value < 0 ? bar.y + 4 : bar.y - 4);
            });
            ctx.restore();
        }
    };

    /**
     * The monthly trend in two charts on one month axis: income and expenses as lines with the kept gap shaded
     * (red where expenses passed income), then net savings as bars labelled with the savings rate. Clicking a month
     * opens it.
     * @param {Array} points - The monthly trend points.
     * @param {number} selected - The selected point's index.
     * @param {Object} palette - The chart palette.
     * @returns {void}
     */
    function renderTrendCharts (points, selected, palette) {
        const trendCanvas = document.getElementById('trendChart');
        const netCanvas = document.getElementById('netSavingsChart');
        if (!trendCanvas || !points || points.length < 2) {
            return;
        }

        const section = trendCanvas.closest('[data-month-url]');
        const labels = points.map((p) => p.monthName);
        const onClick = function (_event, elements) {
            if (section && elements.length > 0) {
                const point = points[elements[0].index];
                window.location.href = `${section.dataset.monthUrl}&month=${point.monthNumber}`;
            }
        };
        const onHover = function (event, elements) {
            event.native.target.style.cursor = elements.length > 0 ? 'pointer' : 'default';
        };
        const radius = points.map((_p, i) => (i === selected ? 5 : 3));
        const xAxis = themedScale(palette, {
            grid: { display: false },
            ticks: {
                color: (context) => (context.index === selected ? palette.text : palette.axis),
                font: (context) => (context.index === selected ? { weight: '600' } : {})
            }
        });
        const tooltip = Object.assign(themedTooltip(palette), {
            callbacks: {
                label: (context) => PFI18n.t('Analysis.Js.SeriesTooltip', { series: context.dataset.label, amount: money(context.parsed.y) })
            }
        });

        keep(new Chart(trendCanvas.getContext('2d'), {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: PFI18n.t('Analysis.Js.Income'),
                        data: points.map((p) => p.income),
                        borderColor: palette.positive,
                        backgroundColor: palette.positive,
                        borderWidth: 2.5,
                        pointRadius: radius,
                        tension: 0.25,
                        fill: false
                    },
                    {
                        label: PFI18n.t('Analysis.Js.Expenses'),
                        data: points.map((p) => p.expenses),
                        borderColor: palette.expenses,
                        backgroundColor: palette.expenses,
                        borderWidth: 2.5,
                        pointRadius: radius,
                        tension: 0.25,
                        fill: { target: 0, above: ChartPalette.withAlpha(palette.negative, 0.16), below: ChartPalette.withAlpha(palette.positive, 0.14) }
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                onClick: onClick,
                onHover: onHover,
                plugins: { legend: { display: false }, tooltip: tooltip },
                scales: { x: xAxis, y: moneyScale(palette, { grace: '5%' }) }
            }
        }));

        if (!netCanvas) {
            return;
        }
        const nets = points.map((p) => p.netSavings);
        keep(new Chart(netCanvas.getContext('2d'), {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: PFI18n.t('Analysis.Js.NetSavings'),
                    data: nets,
                    backgroundColor: nets.map((v, i) => ChartPalette.withAlpha(v < 0 ? palette.negative : palette.net, i === selected ? 1 : 0.55)),
                    borderRadius: 3,
                    maxBarThickness: 40
                }]
            },
            plugins: [barLabels],
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: { padding: { top: 18, bottom: 4 } },
                onClick: onClick,
                onHover: onHover,
                plugins: {
                    legend: { display: false },
                    tooltip: tooltip,
                    analysisBarLabels: {
                        selected: selected,
                        color: palette.axis,
                        strong: palette.text,
                        labels: points.map((p) => PFI18n.t('Analysis.Js.Percent', { value: PFI18n.formatNumber(p.savingsRate, { maximumFractionDigits: 0 }) }))
                    }
                },
                scales: { x: xAxis, y: moneyScale(palette, { beginAtZero: true, grace: '10%' }) }
            }
        }));
    }

    // DOM ready pattern
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
