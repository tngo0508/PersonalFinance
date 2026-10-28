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
     * Initialize the analysis dashboard charts, and redraw them in the new colours when the theme changes (#489).
     * @returns {void}
     */
    function init () {
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
        renderWeeklyTrendChart(data.weeklyTrend, palette);
        renderTrendChart(data.trendPoints, palette);
        renderCategoryCharts(data.categories, palette);
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
     * The tooltip and legend colours every chart on the page shares.
     * @param {Object} palette - The chart palette.
     * @returns {{tooltip: Object, legend: Object}} Chart.js plugin options, without callbacks.
     */
    function themedPlugins (palette) {
        return {
            tooltip: {
                backgroundColor: palette.tooltip.background,
                titleColor: palette.tooltip.text,
                bodyColor: palette.tooltip.text,
                borderColor: palette.tooltip.border,
                borderWidth: 1
            },
            legend: { labels: { color: palette.axis } }
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
            grid: { color: palette.grid },
            border: { color: palette.grid },
            ticks: Object.assign({ color: palette.axis }, axis.ticks)
        });
    }

    /**
     * Render the weekly spending trend bar chart.
     * @param {Array} weeklyTrend - Array of weekly spending data.
     * @param {Object} palette - The chart palette.
     * @returns {void}
     */
    function renderWeeklyTrendChart (weeklyTrend, palette) {
        const canvas = document.getElementById('weeklyTrendChart');
        if (!canvas || !weeklyTrend || weeklyTrend.length === 0) {
            return;
        }

        const plugins = themedPlugins(palette);
        const ctx = canvas.getContext('2d');
        keep(new Chart(ctx, {
            type: 'bar',
            data: {
                labels: weeklyTrend.map((w) => {
                    return w.weekLabel;
                }),
                datasets: [{
                    label: PFI18n.t('Analysis.Js.WeeklySpending'),
                    data: weeklyTrend.map((w) => {
                        return w.totalSpent;
                    }),
                    backgroundColor: ChartPalette.withAlpha(palette.series[1], 0.7),
                    borderColor: palette.series[1],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: Object.assign(plugins.tooltip, {
                        callbacks: {
                            label: function (context) {
                                return PFI18n.formatMoney(context.parsed.y, pageCurrency());
                            }
                        }
                    })
                },
                scales: {
                    x: themedScale(palette, {}),
                    y: themedScale(palette, {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return PFI18n.formatMoney(value, pageCurrency(), { whole: true });
                            }
                        }
                    })
                }
            }
        }));
    }

    /**
     * Render the monthly trend line chart.
     * @param {Array} trendPoints - Array of monthly trend data.
     * @param {Object} palette - The chart palette.
     * @returns {void}
     */
    function renderTrendChart (trendPoints, palette) {
        const canvas = document.getElementById('trendChart');
        if (!canvas || !trendPoints || trendPoints.length === 0) {
            return;
        }

        const plugins = themedPlugins(palette);
        const ctx = canvas.getContext('2d');
        keep(new Chart(ctx, {
            type: 'line',
            data: {
                labels: trendPoints.map((t) => {
                    return t.monthName;
                }),
                datasets: [
                    {
                        label: PFI18n.t('Analysis.Js.Income'),
                        data: trendPoints.map((t) => {
                            return t.income;
                        }),
                        borderColor: palette.positive,
                        backgroundColor: ChartPalette.withAlpha(palette.positive, 0.1),
                        tension: 0.3,
                        fill: false
                    },
                    {
                        label: PFI18n.t('Analysis.Js.Expenses'),
                        data: trendPoints.map((t) => {
                            return t.expenses;
                        }),
                        borderColor: palette.negative,
                        backgroundColor: ChartPalette.withAlpha(palette.negative, 0.1),
                        tension: 0.3,
                        fill: false
                    },
                    {
                        label: PFI18n.t('Analysis.Js.NetSavings'),
                        data: trendPoints.map((t) => {
                            return t.netSavings;
                        }),
                        borderColor: palette.series[1],
                        backgroundColor: ChartPalette.withAlpha(palette.series[1], 0.1),
                        tension: 0.3,
                        fill: false,
                        borderDash: [5, 5]
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: plugins.legend,
                    tooltip: Object.assign(plugins.tooltip, {
                        callbacks: {
                            label: function (context) {
                                return PFI18n.t('Analysis.Js.SeriesTooltip', { series: context.dataset.label, amount: PFI18n.formatMoney(context.parsed.y, pageCurrency(), { whole: true }) });
                            }
                        }
                    })
                },
                scales: {
                    x: themedScale(palette, {}),
                    y: themedScale(palette, {
                        ticks: {
                            callback: function (value) {
                                return PFI18n.formatMoney(value, pageCurrency(), { whole: true });
                            }
                        }
                    })
                }
            }
        }));
    }

    /**
     * Render the category pie and bar charts.
     * @param {Array} categories - Array of category insight data.
     * @param {Object} palette - The chart palette.
     * @returns {void}
     */
    function renderCategoryCharts (categories, palette) {
        if (!categories || categories.length === 0) {
            return;
        }

        renderCategoryPieChart(categories, palette);
        renderCategoryBarChart(categories, palette);
    }

    /**
     * Render the category pie chart. Slices take the categorical series in turn; the legend and table name each one.
     * @param {Array} categories - Array of category insight data.
     * @param {Object} palette - The chart palette.
     * @returns {void}
     */
    function renderCategoryPieChart (categories, palette) {
        const canvas = document.getElementById('categoryPieChart');
        if (!canvas) {
            return;
        }

        const plugins = themedPlugins(palette);
        const ctx = canvas.getContext('2d');
        keep(new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: categories.map((c) => {
                    return c.categoryName;
                }),
                datasets: [{
                    data: categories.map((c) => {
                        return c.actualAmount;
                    }),
                    backgroundColor: categories.map((_c, index) => palette.series[index % palette.series.length]),
                    // The gaps between slices in the card's colour
                    borderColor: palette.page
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 12, padding: 10, color: palette.axis }
                    },
                    tooltip: Object.assign(plugins.tooltip, {
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.parsed;
                                const total = context.dataset.data.reduce((a, b) => {
                                    return a + b;
                                }, 0);
                                const percentage = PFI18n.formatNumber(value / total * 100, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
                                return PFI18n.t('Analysis.Js.ShareTooltip', { label: label, amount: PFI18n.formatMoney(value, pageCurrency(), { whole: true }), percent: percentage });
                            }
                        }
                    })
                }
            }
        }));
    }

    /**
     * Render the category bar chart (budgeted vs actual).
     * @param {Array} categories - Array of category insight data.
     * @param {Object} palette - The chart palette.
     * @returns {void}
     */
    function renderCategoryBarChart (categories, palette) {
        const canvas = document.getElementById('categoryBarChart');
        if (!canvas) {
            return;
        }

        const plugins = themedPlugins(palette);
        const ctx = canvas.getContext('2d');
        keep(new Chart(ctx, {
            type: 'bar',
            data: {
                labels: categories.map((c) => {
                    return c.categoryName;
                }),
                datasets: [
                    {
                        label: PFI18n.t('Analysis.Js.Budgeted'),
                        data: categories.map((c) => {
                            return c.budgetedAmount;
                        }),
                        backgroundColor: ChartPalette.withAlpha(palette.axis, 0.5),
                        borderColor: palette.axis,
                        borderWidth: 1
                    },
                    {
                        label: PFI18n.t('Analysis.Js.Actual'),
                        data: categories.map((c) => {
                            return c.actualAmount;
                        }),
                        backgroundColor: ChartPalette.withAlpha(palette.series[1], 0.7),
                        borderColor: palette.series[1],
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: 'y',
                plugins: {
                    legend: plugins.legend,
                    tooltip: Object.assign(plugins.tooltip, {
                        callbacks: {
                            label: function (context) {
                                return PFI18n.t('Analysis.Js.SeriesTooltip', { series: context.dataset.label, amount: PFI18n.formatMoney(context.parsed.x, pageCurrency(), { whole: true }) });
                            }
                        }
                    })
                },
                scales: {
                    x: themedScale(palette, {
                        ticks: {
                            callback: function (value) {
                                return PFI18n.formatMoney(value, pageCurrency(), { whole: true });
                            }
                        }
                    }),
                    y: themedScale(palette, {})
                }
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
