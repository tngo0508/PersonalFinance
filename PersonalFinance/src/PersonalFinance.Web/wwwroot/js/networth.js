/**
 * Net Worth Projection page JavaScript
 * Handles chart rendering, scenario tab switching, and AJAX recalculation.
 */

(function () {
    'use strict';

    // Chart instances
    let growthChart = null;
    let comparisonChart = null;

    // Color palette
    const colors = {
        base: '#1a73e8',
        conservative: '#9334e6',
        aggressive: '#0f9d58',
        fi: '#0369a1',
        baseAlpha: 'rgba(26, 115, 232, 0.1)',
        conservativeAlpha: 'rgba(147, 52, 230, 0.1)',
        aggressiveAlpha: 'rgba(15, 157, 88, 0.1)'
    };

    /**
     * Format currency for display
     */
    function formatCurrency(value) {
        return '$' + Math.round(value).toLocaleString('en-US');
    }

    /**
     * Format compact currency (e.g., $1.2M)
     */
    function formatCompact(value) {
        if (value >= 1_000_000) {
            return '$' + (value / 1_000_000).toFixed(1) + 'M';
        }
        if (value >= 1_000) {
            return '$' + (value / 1_000).toFixed(0) + 'K';
        }
        return '$' + Math.round(value).toLocaleString('en-US');
    }

    /**
     * Render the growth chart showing all three scenarios over time
     */
    function renderGrowthChart(data) {
        const ctx = document.getElementById('nwGrowthChart');
        if (!ctx) return;

        if (growthChart) {
            growthChart.destroy();
        }

        const labels = data.base.years.map(y => y.year);
        const fiTarget = data.fiTarget;

        const datasets = [
            {
                label: 'Base (' + data.base.roiPercent + '%)',
                data: data.base.years.map(y => y.endingBalance),
                borderColor: colors.base,
                backgroundColor: colors.baseAlpha,
                fill: false,
                tension: 0.3,
                pointRadius: 0,
                pointHoverRadius: 5,
                borderWidth: 2.5
            },
            {
                label: 'Conservative (' + data.conservative.roiPercent + '%)',
                data: data.conservative.years.map(y => y.endingBalance),
                borderColor: colors.conservative,
                backgroundColor: colors.conservativeAlpha,
                fill: false,
                tension: 0.3,
                pointRadius: 0,
                pointHoverRadius: 5,
                borderWidth: 2
            },
            {
                label: 'Aggressive (' + data.aggressive.roiPercent + '%)',
                data: data.aggressive.years.map(y => y.endingBalance),
                borderColor: colors.aggressive,
                backgroundColor: colors.aggressiveAlpha,
                fill: false,
                tension: 0.3,
                pointRadius: 0,
                pointHoverRadius: 5,
                borderWidth: 2
            },
            {
                label: 'FI Target',
                data: Array(labels.length).fill(fiTarget),
                borderColor: colors.fi,
                borderDash: [6, 4],
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
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: 'rgba(31, 41, 55, 0.95)',
                        titleFont: { size: 13, weight: '600' },
                        bodyFont: { size: 12 },
                        padding: 12,
                        cornerRadius: 8,
                        callbacks: {
                            label: function (context) {
                                return context.dataset.label + ': ' + formatCurrency(context.parsed.y);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            font: { size: 11 },
                            color: '#64748b',
                            maxTicksLimit: 15
                        }
                    },
                    y: {
                        grid: {
                            color: 'rgba(226, 232, 240, 0.5)',
                            drawBorder: false
                        },
                        ticks: {
                            font: { size: 11 },
                            color: '#64748b',
                            callback: function (value) {
                                return formatCompact(value);
                            }
                        }
                    }
                }
            }
        });
    }

    /**
     * Render the comparison bar chart showing final net worth across scenarios
     */
    function renderComparisonChart(data) {
        const ctx = document.getElementById('nwComparisonChart');
        if (!ctx) return;

        if (comparisonChart) {
            comparisonChart.destroy();
        }

        comparisonChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: ['Base', 'Conservative', 'Aggressive'],
                datasets: [{
                    label: 'Final Net Worth',
                    data: [
                        data.base.finalNetWorth,
                        data.conservative.finalNetWorth,
                        data.aggressive.finalNetWorth
                    ],
                    backgroundColor: [colors.base, colors.conservative, colors.aggressive],
                    borderRadius: 8,
                    borderSkipped: false,
                    barThickness: 48
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: 'rgba(31, 41, 55, 0.95)',
                        padding: 12,
                        cornerRadius: 8,
                        callbacks: {
                            label: function (context) {
                                return formatCurrency(context.parsed.y);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: {
                            font: { size: 12, weight: '600' },
                            color: '#64748b'
                        }
                    },
                    y: {
                        grid: {
                            color: 'rgba(226, 232, 240, 0.5)',
                            drawBorder: false
                        },
                        ticks: {
                            font: { size: 11 },
                            color: '#64748b',
                            callback: function (value) {
                                return formatCompact(value);
                            }
                        }
                    }
                }
            }
        });
    }

    /**
     * Update summary metric cards and legend
     */
    function updateMetrics(data) {
        // FI Target card
        var fiValue = document.querySelector('.nw-metric-fi-value');
        var fiSub = document.querySelector('.nw-metric-fi-sub');
        if (fiValue) fiValue.textContent = formatCurrency(data.fiTarget);
        if (fiSub) fiSub.textContent = 'Based on $' + formatCompact(data.annualSpending) + '/yr spending';

        // Current Net Worth card
        var currentValue = document.querySelector('.nw-metric-current-value');
        var currentSub = document.querySelector('.nw-metric-current-sub');
        if (currentValue) currentValue.textContent = formatCurrency(data.base.startingBalance);
        if (currentSub) currentSub.textContent = 'Age ' + data.currentAge + ', Year ' + data.currentYear;

        // Base Final card
        var baseValue = document.querySelector('.nw-metric-base-value');
        var baseSub = document.querySelector('.nw-metric-base-sub');
        if (baseValue) baseValue.textContent = formatCurrency(data.base.finalNetWorth);
        if (baseSub) baseSub.textContent = data.base.roiPercent + '% ROI';

        // Aggressive Final card
        var aggValue = document.querySelector('.nw-metric-aggressive-value');
        var aggSub = document.querySelector('.nw-metric-aggressive-sub');
        if (aggValue) aggValue.textContent = formatCurrency(data.aggressive.finalNetWorth);
        if (aggSub) aggSub.textContent = data.aggressive.roiPercent + '% ROI · $' + formatCompact(data.aggressive.annualSavings) + '/yr savings';

        // Update legend
        var legendBase = document.getElementById('nwLegendBase');
        var legendCons = document.getElementById('nwLegendConservative');
        var legendAgg = document.getElementById('nwLegendAggressive');
        if (legendBase) legendBase.innerHTML = '<span class="nw-legend-dot nw-dot-base"></span> Base (' + data.base.roiPercent + '%)';
        if (legendCons) legendCons.innerHTML = '<span class="nw-legend-dot nw-dot-conservative"></span> Conservative (' + data.conservative.roiPercent + '%)';
        if (legendAgg) legendAgg.innerHTML = '<span class="nw-legend-dot nw-dot-aggressive"></span> Aggressive (' + data.aggressive.roiPercent + '%)';
    }

    /**
     * Update scenario comparison cards
     */
    function updateScenarioCards(data) {
        // Base scenario
        setScenarioValues('nw-sc-base-', data.base);
        // Conservative scenario
        setScenarioValues('nw-sc-cons-', data.conservative);
        // Aggressive scenario
        setScenarioValues('nw-sc-agg-', data.aggressive);
    }

    /**
     * Set scenario card values with a given prefix
     */
    function setScenarioValues(prefix, scenario) {
        var roiEl = document.querySelector('.' + prefix + 'roi');
        var savingsEl = document.querySelector('.' + prefix + 'savings');
        var finalEl = document.querySelector('.' + prefix + 'final');
        var contribEl = document.querySelector('.' + prefix + 'contrib');
        var interestEl = document.querySelector('.' + prefix + 'interest');

        if (roiEl) roiEl.textContent = scenario.roiPercent + '%';
        if (savingsEl) savingsEl.textContent = '$' + formatCompact(scenario.annualSavings);
        if (finalEl) finalEl.textContent = '$' + formatCompact(scenario.finalNetWorth);
        if (contribEl) contribEl.textContent = '$' + formatCompact(scenario.totalContributions);
        if (interestEl) interestEl.textContent = '$' + formatCompact(scenario.totalInterestEarned);
    }

    /**
     * Update the projection table for the given scenario
     */
    function updateTable(scenarioName) {
        const data = window.nwProjectionData;
        if (!data) return;

        let scenario;
        if (scenarioName === 'Base') {
            scenario = data.base;
        } else if (scenarioName === 'Conservative') {
            scenario = data.conservative;
        } else {
            scenario = data.aggressive;
        }

        const tbody = document.getElementById('nwTableBody');
        if (!tbody) return;

        let html = '';
        scenario.years.forEach(function (year) {
            html += '<tr>';
            html += '<td>' + year.year + '</td>';
            html += '<td>' + year.age + '</td>';
            html += '<td class="text-end nw-currency">' + formatCurrency(year.startingBalance) + '</td>';
            html += '<td class="text-end nw-currency">' + formatCurrency(year.savings) + '</td>';
            html += '<td class="text-end nw-currency text-success">' + formatCurrency(year.roi) + '</td>';
            html += '<td class="text-end nw-currency fw-semibold">' + formatCurrency(year.endingBalance) + '</td>';
            html += '</tr>';
        });

        tbody.innerHTML = html;
    }

    /**
     * Initialize scenario tab switching
     */
    function initScenarioTabs() {
        const buttons = document.querySelectorAll('.nw-tab-btn');
        buttons.forEach(function (btn) {
            btn.addEventListener('click', function () {
                // Update active state
                buttons.forEach(function (b) { b.classList.remove('active'); });
                btn.classList.add('active');

                // Update table
                const scenario = btn.getAttribute('data-scenario');
                updateTable(scenario);
            });
        });
    }

    /**
     * Initialize live recalculation on input change (debounced)
     */
    function initLiveRecalculation() {
        let timeout;
        const inputs = document.querySelectorAll('.nw-input');

        inputs.forEach(function (input) {
            input.addEventListener('input', function () {
                clearTimeout(timeout);
                timeout = setTimeout(function () {
                    recalculate();
                }, 500);
            });
        });
    }

    /**
     * Recalculate projections via AJAX and update charts/table
     */
    function recalculate() {
        const params = {
            startingBalance: parseFloat(document.getElementById('nwStartingBalance')?.value) || 0,
            annualSavings: parseFloat(document.getElementById('nwAnnualSavings')?.value) || 0,
            annualIncome: parseFloat(document.getElementById('nwAnnualIncome')?.value) || 0,
            annualSpending: parseFloat(document.getElementById('nwAnnualSpending')?.value) || 0,
            currentAge: parseInt(document.getElementById('nwCurrentAge')?.value) || 33,
            retirementAge: parseInt(document.getElementById('nwRetirementAge')?.value) || 65,
            baseRoi: parseFloat(document.getElementById('nwBaseRoi')?.value) || 7,
            conservativeRoi: parseFloat(document.getElementById('nwConservativeRoi')?.value) || 6,
            aggressiveRoi: parseFloat(document.getElementById('nwAggressiveRoi')?.value) || 10,
            aggressiveSavings: parseFloat(document.getElementById('nwAggressiveSavings')?.value) || 45000
        };

        // Build query string
        const qs = Object.keys(params).map(function (k) {
            return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]);
        }).join('&');

        fetch('/Networth/Calculate?' + qs)
            .then(function (response) { return response.json(); })
            .then(function (data) {
                window.nwProjectionData = data;
                renderGrowthChart(data);
                renderComparisonChart(data);
                updateMetrics(data);
                updateScenarioCards(data);
                // Re-render the active scenario table
                const activeBtn = document.querySelector('.nw-tab-btn.active');
                if (activeBtn) {
                    updateTable(activeBtn.getAttribute('data-scenario'));
                }
            })
            .catch(function (err) {
                console.error('Failed to recalculate projection:', err);
            });
    }

    /**
     * Initialize everything when DOM is ready
     */
    function init() {
        const data = window.nwProjectionData;
        if (!data) return;

        // Render charts
        renderGrowthChart(data);
        renderComparisonChart(data);

        // Initialize scenario tabs
        initScenarioTabs();

        // Initialize live recalculation
        initLiveRecalculation();
    }

    // Run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
