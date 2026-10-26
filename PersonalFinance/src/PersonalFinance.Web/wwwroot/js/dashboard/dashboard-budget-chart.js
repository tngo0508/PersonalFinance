/* global PFI18n */
/**
 * Dashboard budget donut.
 * Draws the planned budget split from the "budget" slice of the dashboard data island
 * (allocations and Unallocated are computed by the Dashboard service; nothing is recalculated here).
 * Each category's colour comes from its --dash-budget-<key> token (key supplied by the service) in css/dashboard.css, so a category
 * keeps the same colour whichever categories are drawn. The legend list in _Budget.cshtml is the
 * text alternative; the chart itself is decorative to assistive technology.
 */
(function () {
    'use strict';

    const CHART_ELEMENT_ID = 'dash-budget-chart';
    const UNALLOCATED_LABEL = 'Unallocated';
    const FALLBACK_COLOR = '#6B7280';

    /**
     * Read a category's colour token from the card.
     * @param {Element} element - Element inside the budget card.
     * @param {string} slug - Category slug.
     * @returns {string} The colour value.
     */
    function colorFor (element, slug) {
        const value = window.getComputedStyle(element).getPropertyValue(`--dash-budget-${slug}`).trim();
        return value || FALLBACK_COLOR;
    }

    /**
     * Build the drawable slices: non-zero categories in service order, then Unallocated when present.
     * @param {Object} budget - The budget summary.
     * @returns {Array<{label: string, percent: number, slug: string}>} The slices.
     */
    function toSlices (budget) {
        const allocations = Array.isArray(budget.allocations) ? budget.allocations : [];
        const slices = allocations
            .map((allocation) => ({
                label: allocation.name,
                percent: Number(allocation.percent) || 0,
                // Category key comes from the Dashboard service, the single source for colour-token names.
                slug: String(allocation.key || '')
            }))
            .filter((slice) => slice.percent > 0);

        const unallocated = Number(budget.unallocatedPercent) || 0;
        if (unallocated > 0) {
            slices.push({
                label: UNALLOCATED_LABEL,
                percent: unallocated,
                slug: 'unallocated'
            });
        }
        return slices;
    }

    /**
     * Render the budget donut when a budget exists and its container is on the page.
     * @param {Object} data - The dashboard data island.
     * @returns {void}
     */
    function init (data) {
        const element = document.getElementById(CHART_ELEMENT_ID);
        const budget = data && data.budget;
        if (!element || !budget || typeof window.ApexCharts !== 'function') {
            return;
        }

        const slices = toSlices(budget);
        if (slices.length === 0) {
            return;
        }

        // Dollar amounts stay in the server-rendered legend so currency formatting has one source.
        const percent = new Intl.NumberFormat(PFI18n.culture, { maximumFractionDigits: 1 });

        const chart = new window.ApexCharts(element, {
            chart: {
                type: 'donut',
                height: 220,
                fontFamily: 'Inter, sans-serif',
                animations: { enabled: !window.PFDashboard.prefersReducedMotion() }
            },
            series: slices.map((slice) => slice.percent),
            labels: slices.map((slice) => slice.label),
            colors: slices.map((slice) => colorFor(element, slice.slug)),
            // The server-rendered legend list carries names, percents, and amounts.
            legend: { show: false },
            dataLabels: { enabled: false },
            // 2px surface gap between segments so neighbours never touch.
            stroke: { width: 2, colors: ['#FFFFFF'] },
            plotOptions: { pie: { donut: { size: '68%' } } },
            states: { active: { filter: { type: 'none' } } },
            tooltip: {
                y: {
                    formatter: (value) => `${percent.format(value)}% of income`
                }
            }
        });
        chart.render();
    }

    if (window.PFDashboard) {
        window.PFDashboard.registerSection('budget', init);
    }
})();
