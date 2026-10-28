/* global PFI18n */
/**
 * Dashboard budget donut.
 * Draws the planned budget split from the "budget" slice of the dashboard data island
 * (allocations and Unallocated are computed by the Dashboard service; nothing is recalculated here).
 * Each category's colour comes from its --dash-budget-<key> token (key supplied by the service) in css/dashboard.css, so a category
 * keeps the same colour whichever categories are drawn. The legend list in _Budget.cshtml is the
 * text alternative; the chart itself is decorative to assistive technology. Slice names are read from that legend,
 * so they are in the page's language without a second copy of the category names.
 */
(function () {
    'use strict';

    const CHART_ELEMENT_ID = 'dash-budget-chart';
    const UNALLOCATED_KEY = 'unallocated';

    /**
     * Read a category's colour token from the card, for the theme in effect.
     * @param {Element} element - Element inside the budget card.
     * @param {string} slug - Category slug.
     * @param {Object} palette - The chart palette (core/chart-palette.js); its axis grey stands in for a missing token.
     * @returns {string} The colour value.
     */
    function colorFor (element, slug, palette) {
        const value = window.getComputedStyle(element).getPropertyValue(`--dash-budget-${slug}`).trim();
        return value || palette.axis;
    }

    /**
     * The options that carry a colour, so a theme switch can apply them again (#489).
     * @param {Element} element - The chart container.
     * @param {Array<{slug: string}>} slices - The drawn slices.
     * @returns {Object} ApexCharts options.
     */
    function themedOptions (element, slices) {
        const palette = window.ChartPalette.read();
        const mode = document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
        return {
            theme: { mode: mode },
            colors: slices.map((slice) => colorFor(element, slice.slug, palette)),
            // 2px gap in the card's colour between segments so neighbours never touch.
            stroke: { width: 2, colors: [palette.page] },
            tooltip: { theme: mode }
        };
    }

    /**
     * The translated name the server-rendered legend shows for a category key.
     * @param {string} key - Category key, or 'unallocated'.
     * @param {string} fallback - Name to use when the legend has no entry for the key.
     * @returns {string} The label.
     */
    function legendLabel (key, fallback) {
        const items = document.querySelectorAll('.dash-budget__legend [data-budget-key]');
        const item = Array.from(items).find((element) => element.dataset.budgetKey === key);
        const name = item && item.querySelector('.dash-budget__name');
        return (name && name.textContent.trim()) || fallback;
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
                label: legendLabel(String(allocation.key || ''), allocation.name),
                percent: Number(allocation.percent) || 0,
                // Category key comes from the Dashboard service, the single source for colour-token names.
                slug: String(allocation.key || '')
            }))
            .filter((slice) => slice.percent > 0);

        const unallocated = Number(budget.unallocatedPercent) || 0;
        if (unallocated > 0) {
            slices.push({
                label: legendLabel(UNALLOCATED_KEY, UNALLOCATED_KEY),
                percent: unallocated,
                slug: UNALLOCATED_KEY
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

        const themed = themedOptions(element, slices);
        const chart = new window.ApexCharts(element, Object.assign({}, themed, {
            chart: {
                type: 'donut',
                height: 220,
                fontFamily: 'Inter, sans-serif',
                // The card shows through, in either theme
                background: 'transparent',
                animations: { enabled: !window.PFDashboard.prefersReducedMotion() }
            },
            series: slices.map((slice) => slice.percent),
            labels: slices.map((slice) => slice.label),
            // The server-rendered legend list carries names, percents, and amounts.
            legend: { show: false },
            dataLabels: { enabled: false },
            plotOptions: { pie: { donut: { size: '68%' } } },
            states: { active: { filter: { type: 'none' } } },
            tooltip: Object.assign({}, themed.tooltip, {
                y: {
                    formatter: (value) => PFI18n.t('Dashboard.Js.PercentOfIncome', { percent: percent.format(value) })
                }
            })
        }));
        chart.render();

        // A theme switch recolours the slices without a reload (#489)
        window.ChartPalette.subscribe(() => {
            chart.updateOptions(themedOptions(element, slices), false, false);
        });
    }

    if (window.PFDashboard) {
        window.PFDashboard.registerSection('budget', init);
    }
})();
