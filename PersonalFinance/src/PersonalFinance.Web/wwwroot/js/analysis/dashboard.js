/* global PFI18n */
(function () {
    'use strict';

    /**
     * Initialize the analysis dashboard charts.
     * @returns {void}
     */
    function init () {
        const data = window.__analysisData;
        if (!data) {
            return;
        }

        renderWeeklyTrendChart(data.weeklyTrend);
        renderTrendChart(data.trendPoints);
        renderCategoryCharts(data.categories);
    }

    /**
     * Render the weekly spending trend bar chart.
     * @param {Array} weeklyTrend - Array of weekly spending data.
     * @returns {void}
     */
    function renderWeeklyTrendChart (weeklyTrend) {
        const canvas = document.getElementById('weeklyTrendChart');
        if (!canvas || !weeklyTrend || weeklyTrend.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: weeklyTrend.map((w) => {
                    return w.weekLabel;
                }),
                datasets: [{
                    label: 'Weekly Spending',
                    data: weeklyTrend.map((w) => {
                        return w.totalSpent;
                    }),
                    backgroundColor: 'rgba(13, 110, 253, 0.7)',
                    borderColor: 'rgba(13, 110, 253, 1)',
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return PFI18n.formatMoney(context.parsed.y, 'USD');
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return PFI18n.formatMoney(value, 'USD', { whole: true });
                            }
                        }
                    }
                }
            }
        });
    }

    /**
     * Render the monthly trend line chart.
     * @param {Array} trendPoints - Array of monthly trend data.
     * @returns {void}
     */
    function renderTrendChart (trendPoints) {
        const canvas = document.getElementById('trendChart');
        if (!canvas || !trendPoints || trendPoints.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        new Chart(ctx, {
            type: 'line',
            data: {
                labels: trendPoints.map((t) => {
                    return t.monthName;
                }),
                datasets: [
                    {
                        label: 'Income',
                        data: trendPoints.map((t) => {
                            return t.income;
                        }),
                        borderColor: 'rgba(25, 135, 84, 1)',
                        backgroundColor: 'rgba(25, 135, 84, 0.1)',
                        tension: 0.3,
                        fill: false
                    },
                    {
                        label: 'Expenses',
                        data: trendPoints.map((t) => {
                            return t.expenses;
                        }),
                        borderColor: 'rgba(220, 53, 69, 1)',
                        backgroundColor: 'rgba(220, 53, 69, 0.1)',
                        tension: 0.3,
                        fill: false
                    },
                    {
                        label: 'Net Savings',
                        data: trendPoints.map((t) => {
                            return t.netSavings;
                        }),
                        borderColor: 'rgba(13, 110, 253, 1)',
                        backgroundColor: 'rgba(13, 110, 253, 0.1)',
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
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return `${context.dataset.label }: ${ PFI18n.formatMoney(context.parsed.y, 'USD', { whole: true })}`;
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        ticks: {
                            callback: function (value) {
                                return PFI18n.formatMoney(value, 'USD', { whole: true });
                            }
                        }
                    }
                }
            }
        });
    }

    /**
     * Render the category pie and bar charts.
     * @param {Array} categories - Array of category insight data.
     * @returns {void}
     */
    function renderCategoryCharts (categories) {
        if (!categories || categories.length === 0) {
            return;
        }

        renderCategoryPieChart(categories);
        renderCategoryBarChart(categories);
    }

    /**
     * Render the category pie chart.
     * @param {Array} categories - Array of category insight data.
     * @returns {void}
     */
    function renderCategoryPieChart (categories) {
        const canvas = document.getElementById('categoryPieChart');
        if (!canvas) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const colors = [
            '#0d6efd', '#198754', '#dc3545', '#ffc107', '#0dcaf0',
            '#6610f2', '#20c997', '#fd7e14', '#6c757d', '#e83e8c',
            '#20c997', '#6f42c1'
        ];

        new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: categories.map((c) => {
                    return c.categoryName;
                }),
                datasets: [{
                    data: categories.map((c) => {
                        return c.actualAmount;
                    }),
                    backgroundColor: colors.slice(0, categories.length)
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 12, padding: 10 }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.parsed;
                                const total = context.dataset.data.reduce((a, b) => {
                                    return a + b;
                                }, 0);
                                const percentage = PFI18n.formatNumber(value / total * 100, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
                                return `${label }: ${ PFI18n.formatMoney(value, 'USD', { whole: true }) } (${ percentage }%)`;
                            }
                        }
                    }
                }
            }
        });
    }

    /**
     * Render the category bar chart (budgeted vs actual).
     * @param {Array} categories - Array of category insight data.
     * @returns {void}
     */
    function renderCategoryBarChart (categories) {
        const canvas = document.getElementById('categoryBarChart');
        if (!canvas) {
            return;
        }

        const ctx = canvas.getContext('2d');
        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: categories.map((c) => {
                    return c.categoryName;
                }),
                datasets: [
                    {
                        label: 'Budgeted',
                        data: categories.map((c) => {
                            return c.budgetedAmount;
                        }),
                        backgroundColor: 'rgba(108, 117, 125, 0.5)',
                        borderColor: 'rgba(108, 117, 125, 1)',
                        borderWidth: 1
                    },
                    {
                        label: 'Actual',
                        data: categories.map((c) => {
                            return c.actualAmount;
                        }),
                        backgroundColor: 'rgba(13, 110, 253, 0.7)',
                        borderColor: 'rgba(13, 110, 253, 1)',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: 'y',
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return `${context.dataset.label }: ${ PFI18n.formatMoney(context.parsed.x, 'USD', { whole: true })}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: {
                            callback: function (value) {
                                return PFI18n.formatMoney(value, 'USD', { whole: true });
                            }
                        }
                    }
                }
            }
        });
    }

    // DOM ready pattern
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
