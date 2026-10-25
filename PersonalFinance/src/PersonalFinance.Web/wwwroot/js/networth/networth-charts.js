/**
 * Net Worth chart rendering module.
 * Renders the growth line chart and comparison bar chart using Chart.js.
 * Depends on: networth-utils.js (window.NW)
 */
(function () {
    'use strict';

    const NW = window.NW;
    const colors = NW.colors;

    // Chart instances
    let growthChart = null;
    let comparisonChart = null;

    /**
     * Create a gradient fill for a line chart.
     * @param {CanvasRenderingContext2D} ctx - Canvas context
     * @param {string} colorHex - Hex color for the gradient
     * @returns {CanvasGradient} The gradient object
     */
    function createLineGradient (ctx, colorHex) {
        const gradient = ctx.createLinearGradient(0, 0, 0, NW.GROWTH_CHART_HEIGHT);
        gradient.addColorStop(0, NW.hexToRgba(colorHex, 0.35));
        gradient.addColorStop(0.5, NW.hexToRgba(colorHex, 0.12));
        gradient.addColorStop(1, NW.hexToRgba(colorHex, 0.0));
        return gradient;
    }

    /**
     * Create a gradient fill for a bar chart.
     * @param {CanvasRenderingContext2D} ctx - Canvas context
     * @param {string} colorHex - Hex color for the gradient
     * @param {number} height - Canvas height
     * @returns {CanvasGradient} The gradient object
     */
    function createBarGradient (ctx, colorHex, height) {
        const gradient = ctx.createLinearGradient(0, 0, 0, height);
        gradient.addColorStop(0, colorHex);
        gradient.addColorStop(1, NW.hexToRgba(colorHex, 0.4));
        return gradient;
    }

    /**
     * Common tooltip configuration for all charts.
     * @returns {Object} Chart.js tooltip plugin options
     */
    function tooltipConfig () {
        return {
            backgroundColor: colors.tooltipBg,
            borderColor: colors.tooltipBorder,
            borderWidth: 1,
            titleColor: '#14283B',
            bodyColor: '#3D4F5F',
            titleFont: { size: 13, weight: '600', family: '"IBM Plex Sans", "Segoe UI", system-ui, sans-serif' },
            bodyFont: { size: 12, family: '"IBM Plex Sans", "Segoe UI", system-ui, sans-serif' },
            padding: 12,
            cornerRadius: 4,
            displayColors: true,
            boxPadding: 4,
            usePointStyle: true
        };
    }

    /**
     * Common grid configuration for chart axes.
     * @returns {Object} Grid options
     */
    function gridConfig () {
        return { color: colors.gridColor, drawBorder: false };
    }

    /**
     * Common tick configuration for chart axes.
     * @returns {Object} Tick options
     */
    function tickConfig () {
        return { font: { size: 11, weight: '400', family: '"Segoe UI Variable", "Segoe UI", system-ui, sans-serif' }, color: colors.tickColor };
    }

    /**
     * Build a line chart dataset for a scenario.
     * @param {string} name - Scenario name (e.g., 'Base')
     * @param {number} roiPercent - ROI percentage
     * @param {Array} years - Array of year data objects
     * @param {string} colorHex - Hex color
     * @param {CanvasGradient} gradient - Gradient fill
     * @param {number} borderWidth - Line width
     * @returns {Object} Chart.js dataset
     */
    function buildLineDataset (name, roiPercent, years, colorHex, gradient, borderWidth) {
        return {
            label: `${name } (${ roiPercent }%)`,
            data: years.map((y) => { return y.endingBalance; }),
            borderColor: colorHex,
            backgroundColor: gradient,
            fill: true,
            tension: 0.4,
            borderWidth: borderWidth,
            borderCapStyle: 'round',
            pointRadius: 0,
            pointHoverRadius: 6,
            pointHoverBackgroundColor: colorHex,
            pointHoverBorderColor: '#fff',
            pointHoverBorderWidth: 2
        };
    }

    /**
     * Render the growth chart showing all three scenarios over time.
     * @param {Object} data - The projection data object.
     */
    function renderGrowthChart (data) {
        const ctxEl = document.getElementById('nwGrowthChart');
        if (!ctxEl) { return; }

        if (growthChart) {
            growthChart.destroy();
        }

        const ctx = ctxEl.getContext('2d');
        const labels = data.base.years.map((y) => { return y.year; });
        const fiTarget = data.fiTarget;

        // Build gradient fills
        const baseGrad = createLineGradient(ctx, colors.base);
        const consGrad = createLineGradient(ctx, colors.conservative);
        const aggGrad = createLineGradient(ctx, colors.aggressive);

        const datasets = [
            buildLineDataset('Base', data.base.roiPercent, data.base.years, colors.base, baseGrad, 3),
            buildLineDataset('Conservative', data.conservative.roiPercent, data.conservative.years, colors.conservative, consGrad, 2),
            buildLineDataset('Aggressive', data.aggressive.roiPercent, data.aggressive.years, colors.aggressive, aggGrad, 3),
            {
                label: 'FI Target',
                data: Array(labels.length).fill(fiTarget),
                borderColor: colors.fi,
                backgroundColor: 'transparent',
                borderDash: [8, 5],
                borderWidth: 1.5,
                pointRadius: 0,
                fill: false
            }
        ];

        growthChart = new Chart(ctx, {
            type: 'line',
            data: { labels: labels, datasets: datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: { display: false },
                    tooltip: Object.assign({}, tooltipConfig(), {
                        callbacks: {
                            title: function (items) { return `\u{1f4c5} ${ items[0].label}`; },
                            label: function (context) {
                                return ` ${ context.dataset.label }:  ${ NW.formatCurrency(context.parsed.y)}`;
                            }
                        }
                    })
                },
                scales: {
                    x: {
                        grid: gridConfig(),
                        ticks: Object.assign({}, tickConfig(), { maxTicksLimit: 15 }),
                        border: { display: false }
                    },
                    y: {
                        grid: gridConfig(),
                        ticks: Object.assign({}, tickConfig(), {
                            callback: function (value) { return NW.formatCompact(value); }
                        }),
                        border: { display: false }
                    }
                },
                animation: {
                    duration: 1200,
                    easing: 'easeOutQuart'
                }
            }
        });
    }

    /**
     * Render the comparison bar chart showing final net worth across scenarios.
     * @param {Object} data - The projection data object.
     */
    function renderComparisonChart (data) {
        const ctxEl = document.getElementById('nwComparisonChart');
        if (!ctxEl) { return; }

        if (comparisonChart) {
            comparisonChart.destroy();
        }

        const ctx = ctxEl.getContext('2d');

        // Use ending balance from the last year of each scenario for comparison
        const baseLastYear = data.base.years[data.base.years.length - 1];
        const conservativeLastYear = data.conservative.years[data.conservative.years.length - 1];
        const aggressiveLastYear = data.aggressive.years[data.aggressive.years.length - 1];

        // Create gradient backgrounds for bars
        const baseBarGrad = createBarGradient(ctx, colors.base, NW.COMPARISON_CHART_HEIGHT);
        const consBarGrad = createBarGradient(ctx, colors.conservative, NW.COMPARISON_CHART_HEIGHT);
        const aggBarGrad = createBarGradient(ctx, colors.aggressive, NW.COMPARISON_CHART_HEIGHT);

        comparisonChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: ['Base', 'Conservative', 'Aggressive'],
                datasets: [{
                    label: 'Final Net Worth',
                    data: [
                        baseLastYear ? baseLastYear.endingBalance : 0,
                        conservativeLastYear ? conservativeLastYear.endingBalance : 0,
                        aggressiveLastYear ? aggressiveLastYear.endingBalance : 0
                    ],
                    backgroundColor: [baseBarGrad, consBarGrad, aggBarGrad],
                    borderColor: [colors.base, colors.conservative, colors.aggressive],
                    borderWidth: 1.5,
                    borderRadius: 10,
                    borderSkipped: false,
                    barThickness: 52,
                    hoverBackgroundColor: [colors.base, colors.conservative, colors.aggressive],
                    hoverBorderColor: ['#fff', '#fff', '#fff'],
                    hoverBorderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: { display: false },
                    tooltip: Object.assign({}, tooltipConfig(), {
                        callbacks: {
                            title: function (items) { return `\u{1f4ca} ${ items[0].label } Scenario`; },
                            label: function (context) {
                                return ` Final Net Worth:  ${ NW.formatCurrency(context.parsed.y)}`;
                            }
                        }
                    })
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { font: { size: 12, weight: '600', family: '"Segoe UI Variable", "Segoe UI", system-ui, sans-serif' }, color: colors.tickColor },
                        border: { display: false }
                    },
                    y: {
                        grid: gridConfig(),
                        ticks: Object.assign({}, tickConfig(), {
                            callback: function (value) { return NW.formatCompact(value); }
                        }),
                        border: { display: false }
                    }
                },
                animation: {
                    duration: 1000,
                    easing: 'easeOutQuart'
                }
            }
        });
    }

    // Expose chart functions
    window.NW = Object.assign(window.NW || {}, {
        renderGrowthChart: renderGrowthChart,
        renderComparisonChart: renderComparisonChart
    });

})();
