/**
 * Dashboard budget donut.
 * Draws the planned budget split from the "budget" slice of the dashboard data island.
 */
(function () {
    'use strict';

    const CHART_ELEMENT_ID = 'dash-budget-chart';

    // Indigo Ledger tokens (wwwroot/css/site.css): brand first, then the semantic accents and neutrals.
    const COLORS = ['#4F46E5', '#059669', '#D97706', '#DC2626', '#7C3AED', '#6B7280'];

    const CATEGORIES = [
        { key: 'housingPercent', label: 'Housing' },
        { key: 'foodPercent', label: 'Food' },
        { key: 'transportationPercent', label: 'Transport' },
        { key: 'utilitiesPercent', label: 'Utilities' },
        { key: 'savingsPercent', label: 'Savings' },
        { key: 'entertainmentPercent', label: 'Entertainment' }
    ];

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

        const chart = new window.ApexCharts(element, {
            chart: { type: 'donut', height: 220, fontFamily: 'Inter, sans-serif' },
            series: CATEGORIES.map((category) => Number(budget[category.key]) || 0),
            labels: CATEGORIES.map((category) => category.label),
            colors: COLORS,
            legend: { position: 'bottom' },
            dataLabels: { enabled: false },
            stroke: { width: 1, colors: ['#FFFFFF'] },
            tooltip: {
                y: { formatter: (value) => `${value}%` }
            }
        });
        chart.render();
    }

    if (window.PFDashboard) {
        window.PFDashboard.registerSection('budget', init);
    }
})();
