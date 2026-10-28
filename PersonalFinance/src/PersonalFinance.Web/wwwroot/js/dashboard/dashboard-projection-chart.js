/* global PFI18n */
/**
 * Dashboard net worth projection.
 * Draws the Base, Conservative, and Aggressive scenarios from current age to retirement age, with the FI Target as
 * a dashed reference line. Points come from the "netWorth.projectionPoints" slice of the dashboard data island,
 * which the server builds with the same calculation as the Net Worth page.
 */
(function () {
    'use strict';

    const CHART_ELEMENT_ID = 'dash-projection-chart';
    const Y_AXIS_HEADROOM = 0.1;

    const SERIES = [
        { key: 'base', nameKey: 'Dashboard.Js.Scenario.Base', width: 3 },
        { key: 'conservative', nameKey: 'Dashboard.Js.Scenario.Conservative', width: 2 },
        { key: 'aggressive', nameKey: 'Dashboard.Js.Scenario.Aggressive', width: 2 }
    ];

    /**
     * The chart's colours of the theme in effect, from the shared chart palette (core/chart-palette.js, #489): the
     * same scenario colours as the Net Worth page (Base the first series, Conservative the neutral axis colour,
     * Aggressive the second series) and the FI Target in the third, brass, series.
     * @returns {{base: string, conservative: string, aggressive: string, fiTarget: string, grid: string, axis: string,
     *     surface: string, mode: string}} mode: ApexCharts' 'light' or 'dark', for its legend and tooltip.
     */
    function themeColors () {
        const palette = window.ChartPalette.read();
        return {
            base: palette.series[0],
            conservative: palette.axis,
            aggressive: palette.series[1],
            fiTarget: palette.series[2],
            grid: palette.grid,
            axis: palette.axis,
            surface: palette.page,
            mode: document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light'
        };
    }

    // Separators from the page culture, symbol from the currency (PFI18n, spec §2.2). The amounts are in the
    // projection's currency: the Reporting Currency (ADR 0016), read from the data island.
    let currentCurrency = 'USD';
    const currencyFormatter = { format: (value) => PFI18n.formatMoney(value, currentCurrency, { whole: true }) };

    const compactFormatter = { format: (value) => PFI18n.formatCompactMoney(value, currentCurrency) };

    /**
     * Chart height that scales with the viewport so the plot stays legible on phones and desktops.
     * @returns {number} Height in pixels.
     */
    function chartHeight () {
        return window.innerWidth < 576 ? 240 : 300;
    }

    /**
     * Build the dashed FI Target reference line, or none when there is no FI Target.
     * @param {number} fiTarget - The FI Target amount.
     * @param {Object} colors - From themeColors.
     * @returns {Array<Object>} ApexCharts y-axis annotations.
     */
    function buildFiAnnotation (fiTarget, colors) {
        if (!(fiTarget > 0)) {
            return [];
        }
        return [{
            y: fiTarget,
            borderColor: colors.fiTarget,
            strokeDashArray: 6,
            label: {
                text: PFI18n.t('Dashboard.Js.FiTargetLine', { amount: compactFormatter.format(fiTarget) }),
                position: 'left',
                textAnchor: 'start',
                offsetX: 4,
                borderColor: 'transparent',
                style: { color: colors.fiTarget, background: colors.surface, fontSize: '11px' }
            }
        }];
    }

    /**
     * Y-axis bounds that always include zero, every scenario balance (negative balances are not clipped), and the
     * FI Target, so the dashed FI line stays visible even when every scenario ends below it.
     * @param {Array<Object>} points - Projection points.
     * @param {number} fiTarget - The FI Target amount (0 when there is none).
     * @returns {{min: number, max: number}} The y-axis bounds.
     */
    function yAxisBounds (points, fiTarget) {
        const values = points.flatMap((point) => SERIES.map((series) => Number(point[series.key]) || 0));
        const minPoint = Math.min(0, ...values);
        const maxPoint = Math.max(0, ...values, fiTarget > 0 ? fiTarget : 0);
        // Headroom (10% of the range) beyond the extremes so no line or the FI label is drawn on the plot edge.
        const span = maxPoint - minPoint;
        const headroom = span > 0 ? span * Y_AXIS_HEADROOM : 1;
        return {
            min: minPoint < 0 ? minPoint - headroom : 0,
            max: maxPoint + headroom
        };
    }

    /**
     * Tooltip title for a hovered point: the calendar year and the user's age.
     * @param {Array<Object>} points - Projection points.
     * @param {number} index - Hovered data-point index.
     * @returns {string} The tooltip title.
     */
    function tooltipTitle (points, index) {
        const point = points[index];
        return point ? PFI18n.t('Dashboard.Js.TooltipTitle', { year: point.year, age: point.age }) : '';
    }

    /**
     * Render the projection chart when projection points exist and the container is on the page.
     * @param {Object} data - The dashboard data island.
     * @returns {void}
     */
    function init (data) {
        const element = document.getElementById(CHART_ELEMENT_ID);
        const netWorth = data && data.netWorth;
        currentCurrency = (netWorth && netWorth.currencyCode) || 'USD';
        const points = (netWorth && Array.isArray(netWorth.projectionPoints)) ? netWorth.projectionPoints : [];
        if (!element || points.length === 0 || typeof window.ApexCharts !== 'function') {
            return;
        }

        const fiTarget = Number(netWorth.fiTarget) || 0;
        const animate = !window.PFDashboard.prefersReducedMotion();
        const chart = new window.ApexCharts(element, Object.assign({
            chart: {
                type: 'area',
                height: chartHeight(),
                fontFamily: 'Inter, sans-serif',
                // The card shows through, in either theme
                background: 'transparent',
                toolbar: { show: false },
                zoom: { enabled: false },
                animations: { enabled: animate }
            },
            series: SERIES.map((series) => ({
                name: PFI18n.t(series.nameKey),
                data: points.map((point) => ({ x: point.age, y: Number(point[series.key]) || 0 }))
            })),
            stroke: { curve: 'straight', width: SERIES.map((series) => series.width) },
            fill: {
                type: 'gradient',
                gradient: { opacityFrom: 0.18, opacityTo: 0, stops: [0, 100] }
            },
            dataLabels: { enabled: false },
            legend: { position: 'top', horizontalAlign: 'left', fontSize: '12px' },
            responsive: [{
                breakpoint: 576,
                options: { legend: { position: 'bottom', horizontalAlign: 'center' } }
            }]
        }, themedOptions(points, fiTarget, themeColors())));
        chart.render();

        // A theme switch restyles the chart without a reload (#489)
        window.ChartPalette.subscribe(() => {
            chart.updateOptions(themedOptions(points, fiTarget, themeColors()), false, false);
        });
    }

    /**
     * The options that carry a colour, so a theme switch can apply them again.
     * @param {Array<Object>} points - Projection points.
     * @param {number} fiTarget - The FI Target amount (0 when there is none).
     * @param {Object} colors - From themeColors.
     * @returns {Object} ApexCharts options.
     */
    function themedOptions (points, fiTarget, colors) {
        const bounds = yAxisBounds(points, fiTarget);
        return {
            theme: { mode: colors.mode },
            colors: [colors.base, colors.conservative, colors.aggressive],
            markers: { size: 0, hover: { size: 5 }, strokeColors: colors.surface, strokeWidth: 2 },
            grid: { borderColor: colors.grid, strokeDashArray: 0, xaxis: { lines: { show: false } } },
            xaxis: {
                type: 'numeric',
                tickAmount: Math.min(points.length - 1, 8),
                title: { text: PFI18n.t('Dashboard.Js.Age'), style: { color: colors.axis, fontWeight: 500 } },
                labels: {
                    style: { colors: colors.axis },
                    formatter: (value) => `${Math.round(value)}`
                },
                axisBorder: { color: colors.grid },
                axisTicks: { color: colors.grid },
                tooltip: { enabled: false }
            },
            yaxis: {
                min: bounds.min,
                max: bounds.max,
                forceNiceScale: true,
                labels: {
                    style: { colors: colors.axis },
                    formatter: (value) => compactFormatter.format(value)
                }
            },
            annotations: { yaxis: buildFiAnnotation(fiTarget, colors) },
            tooltip: {
                theme: colors.mode,
                shared: true,
                intersect: false,
                x: {
                    formatter: (_value, options) => tooltipTitle(points, options ? options.dataPointIndex : -1)
                },
                y: { formatter: (value) => currencyFormatter.format(value) }
            }
        };
    }

    if (window.PFDashboard) {
        window.PFDashboard.registerSection('projection', init);
    }
})();
