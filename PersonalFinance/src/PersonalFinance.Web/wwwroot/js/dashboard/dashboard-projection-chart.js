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

    // Indigo Ledger tokens (wwwroot/css/site.css).
    const COLORS = {
        base: '#4F46E5', // --app-brand
        conservative: '#6B7280', // --app-text-muted (neutral)
        aggressive: '#059669', // --app-accent-positive
        fiTarget: '#111827', // --app-text
        grid: '#E5E7EB', // --app-border
        axis: '#6B7280', // --app-text-muted
        surface: '#FFFFFF' // --app-bg
    };

    const SERIES = [
        { key: 'base', name: 'Base', color: COLORS.base, width: 3 },
        { key: 'conservative', name: 'Conservative', color: COLORS.conservative, width: 2 },
        { key: 'aggressive', name: 'Aggressive', color: COLORS.aggressive, width: 2 }
    ];

    const currencyFormatter = new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        maximumFractionDigits: 0
    });

    const compactFormatter = new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        notation: 'compact',
        maximumFractionDigits: 1
    });

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
     * @returns {Array<Object>} ApexCharts y-axis annotations.
     */
    function buildFiAnnotation (fiTarget) {
        if (!(fiTarget > 0)) {
            return [];
        }
        return [{
            y: fiTarget,
            borderColor: COLORS.fiTarget,
            strokeDashArray: 6,
            label: {
                text: `FI Target ${compactFormatter.format(fiTarget)}`,
                position: 'left',
                textAnchor: 'start',
                offsetX: 4,
                borderColor: 'transparent',
                style: { color: COLORS.fiTarget, background: COLORS.surface, fontSize: '11px' }
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
        return point ? `${point.year} · Age ${point.age}` : '';
    }

    /**
     * Render the projection chart when projection points exist and the container is on the page.
     * @param {Object} data - The dashboard data island.
     * @returns {void}
     */
    function init (data) {
        const element = document.getElementById(CHART_ELEMENT_ID);
        const netWorth = data && data.netWorth;
        const points = (netWorth && Array.isArray(netWorth.projectionPoints)) ? netWorth.projectionPoints : [];
        if (!element || points.length === 0 || typeof window.ApexCharts !== 'function') {
            return;
        }

        const fiTarget = Number(netWorth.fiTarget) || 0;
        const bounds = yAxisBounds(points, fiTarget);
        const animate = !window.PFDashboard.prefersReducedMotion();
        const chart = new window.ApexCharts(element, {
            chart: {
                type: 'area',
                height: chartHeight(),
                fontFamily: 'Inter, sans-serif',
                toolbar: { show: false },
                zoom: { enabled: false },
                animations: { enabled: animate }
            },
            series: SERIES.map((series) => ({
                name: series.name,
                data: points.map((point) => ({ x: point.age, y: Number(point[series.key]) || 0 }))
            })),
            colors: SERIES.map((series) => series.color),
            stroke: { curve: 'straight', width: SERIES.map((series) => series.width) },
            fill: {
                type: 'gradient',
                gradient: { opacityFrom: 0.18, opacityTo: 0, stops: [0, 100] }
            },
            dataLabels: { enabled: false },
            markers: { size: 0, hover: { size: 5 }, strokeColors: COLORS.surface, strokeWidth: 2 },
            legend: { position: 'top', horizontalAlign: 'left', fontSize: '12px' },
            grid: { borderColor: COLORS.grid, strokeDashArray: 0, xaxis: { lines: { show: false } } },
            xaxis: {
                type: 'numeric',
                tickAmount: Math.min(points.length - 1, 8),
                title: { text: 'Age', style: { color: COLORS.axis, fontWeight: 500 } },
                labels: {
                    style: { colors: COLORS.axis },
                    formatter: (value) => `${Math.round(value)}`
                },
                axisBorder: { color: COLORS.grid },
                axisTicks: { color: COLORS.grid },
                tooltip: { enabled: false }
            },
            yaxis: {
                min: bounds.min,
                max: bounds.max,
                forceNiceScale: true,
                labels: {
                    style: { colors: COLORS.axis },
                    formatter: (value) => compactFormatter.format(value)
                }
            },
            annotations: { yaxis: buildFiAnnotation(fiTarget) },
            tooltip: {
                shared: true,
                intersect: false,
                x: {
                    formatter: (_value, options) => tooltipTitle(points, options ? options.dataPointIndex : -1)
                },
                y: { formatter: (value) => currencyFormatter.format(value) }
            },
            responsive: [{
                breakpoint: 576,
                options: { legend: { position: 'bottom', horizontalAlign: 'center' } }
            }]
        });
        chart.render();
    }

    if (window.PFDashboard) {
        window.PFDashboard.registerSection('projection', init);
    }
})();
