/**
 * Net Worth Projection page JavaScript
 * Handles chart rendering, scenario tab switching, and AJAX recalculation.
 */
(function () {
    'use strict';

    // Debounce delay in milliseconds for live recalculation
    const DEBOUNCE_DELAY = 500;

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

    // Scenario name mapping for safe property access
    const scenarioMap = {
        Base: 'base',
        Conservative: 'conservative',
        Aggressive: 'aggressive'
    };

    /**
     * Format a numeric value as a currency string.
     * @param {number} value - The numeric value to format.
     * @returns {string} Formatted currency string (e.g., "$1,234,567").
     */
    function formatCurrency(value) {
        return '$' + Math.round(value).toLocaleString('en-US');
    }

    /**
     * Format a numeric value as a compact currency string.
     * @param {number} value - The numeric value to format.
     * @returns {string} Compact currency string (e.g., "$1.2M", "$50K").
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

        // Use ending balance from the last year of each scenario for comparison
        const baseLastYear = data.base.years[data.base.years.length - 1];
        const conservativeLastYear = data.conservative.years[data.conservative.years.length - 1];
        const aggressiveLastYear = data.aggressive.years[data.aggressive.years.length - 1];

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
     * Update summary metric cards and legend.
     * @param {Object} data - The projection data object.
     */
    function updateMetrics(data) {
        // FI Target card
        const fiValue = document.querySelector('.nw-metric-fi-value');
        const fiSub = document.querySelector('.nw-metric-fi-sub');
        if (fiValue) fiValue.textContent = formatCurrency(data.fiTarget);
        if (fiSub) fiSub.textContent = `Based on $${formatCompact(data.annualSpending)}/yr spending`;

        // Current Net Worth card
        const currentValue = document.querySelector('.nw-metric-current-value');
        const currentSub = document.querySelector('.nw-metric-current-sub');
        if (currentValue) currentValue.textContent = formatCurrency(data.base.startingBalance);
        if (currentSub) currentSub.textContent = `Age ${data.currentAge}, Year ${data.currentYear}`;

        // Base Final card
        const baseValue = document.querySelector('.nw-metric-base-value');
        const baseSub = document.querySelector('.nw-metric-base-sub');
        if (baseValue) baseValue.textContent = formatCurrency(data.base.finalNetWorth);
        if (baseSub) baseSub.textContent = `${data.base.roiPercent}% ROI`;

        // Aggressive Final card
        const aggValue = document.querySelector('.nw-metric-aggressive-value');
        const aggSub = document.querySelector('.nw-metric-aggressive-sub');
        if (aggValue) aggValue.textContent = formatCurrency(data.aggressive.finalNetWorth);
        if (aggSub) aggSub.textContent = `${data.aggressive.roiPercent}% ROI · $${formatCompact(data.aggressive.annualSavings)}/yr savings`;

        // Update legend
        const legendBase = document.getElementById('nwLegendBase');
        const legendCons = document.getElementById('nwLegendConservative');
        const legendAgg = document.getElementById('nwLegendAggressive');
        if (legendBase) legendBase.innerHTML = `<span class="nw-legend-dot nw-dot-base"></span> Base (${data.base.roiPercent}%)`;
        if (legendCons) legendCons.innerHTML = `<span class="nw-legend-dot nw-dot-conservative"></span> Conservative (${data.conservative.roiPercent}%)`;
        if (legendAgg) legendAgg.innerHTML = `<span class="nw-legend-dot nw-dot-aggressive"></span> Aggressive (${data.aggressive.roiPercent}%)`;
    }

    /**
     * Update scenario comparison cards.
     * @param {Object} data - The projection data object.
     */
    function updateScenarioCards(data) {
        setScenarioValues('nw-sc-base-', data.base);
        setScenarioValues('nw-sc-cons-', data.conservative);
        setScenarioValues('nw-sc-agg-', data.aggressive);
    }

    /**
     * Set scenario card values with a given prefix.
     * @param {string} prefix - CSS class prefix (e.g., 'nw-sc-base-').
     * @param {Object} scenario - The scenario data object.
     */
    function setScenarioValues(prefix, scenario) {
        const roiEl = document.querySelector(`.${prefix}roi`);
        const savingsEl = document.querySelector(`.${prefix}savings`);
        const finalEl = document.querySelector(`.${prefix}final`);
        const contribEl = document.querySelector(`.${prefix}contrib`);
        const interestEl = document.querySelector(`.${prefix}interest`);

        if (roiEl) roiEl.textContent = `${scenario.roiPercent}%`;
        if (savingsEl) savingsEl.textContent = `$${formatCompact(scenario.annualSavings)}`;
        if (finalEl) finalEl.textContent = `$${formatCompact(scenario.finalNetWorth)}`;
        if (contribEl) contribEl.textContent = `$${formatCompact(scenario.totalContributions)}`;
        if (interestEl) interestEl.textContent = `$${formatCompact(scenario.totalInterestEarned)}`;
    }

    /**
     * Determine CSS classes for a table row based on milestone thresholds.
     * @param {number} endingBalance - The ending balance for the year.
     * @param {number} fiTarget - The financial independence target amount.
     * @param {number} annualIncome - The annual income amount.
     * @returns {string} Space-separated CSS class names for the row.
     */
    function getRowHighlightClass(endingBalance, fiTarget, annualIncome) {
        const classes = [];
        const fiveMillion = 5000000;

        if (endingBalance >= fiTarget) {
            classes.push('nw-row-fi');
        }
        if (endingBalance >= annualIncome) {
            classes.push('nw-row-income');
        }
        if (endingBalance >= fiveMillion) {
            classes.push('nw-row-5m');
        }

        return classes.length > 0 ? ' class="' + classes.join(' ') + '"' : '';
    }

    /**
     * Update the projection table for the given scenario.
     * @param {string} scenarioName - The scenario key (e.g., 'Base').
     */
    function updateTable(scenarioName) {
        const data = window.nwProjectionData;
        if (!data) return;

        const scenarioKey = scenarioMap[scenarioName] || 'base';
        const scenario = data[scenarioKey];
        if (!scenario) return;

        const tbody = document.getElementById('nwTableBody');
        if (!tbody) return;

        const fiTarget = data.fiTarget;
        const annualIncome = data.annualIncome;

        const rows = scenario.years.map(function (year) {
            const rowClass = getRowHighlightClass(year.endingBalance, fiTarget, annualIncome);
            return `<tr${rowClass}>
                <td>${year.year}</td>
                <td>${year.age}</td>
                <td class="text-end nw-currency">${formatCurrency(year.startingBalance)}</td>
                <td class="text-end nw-currency">${formatCurrency(year.savings)}</td>
                <td class="text-end nw-currency text-success">${formatCurrency(year.roi)}</td>
                <td class="text-end nw-currency fw-semibold">${formatCurrency(year.endingBalance)}</td>
            </tr>`;
        });

        tbody.innerHTML = rows.join('');
    }

    /**
     * Initialize scenario tab switching.
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
     * Initialize live recalculation on input change (debounced).
     */
    function initLiveRecalculation() {
        let timeout;
        const inputs = document.querySelectorAll('.nw-input');

        inputs.forEach(function (input) {
            input.addEventListener('input', function () {
                clearTimeout(timeout);
                timeout = setTimeout(function () {
                    recalculate();
                }, DEBOUNCE_DELAY);
            });
        });
    }

    /**
     * Recalculate projections via AJAX and update all UI elements.
     */
    function recalculate() {
        const params = {
            startingBalance: parseFloat(document.getElementById('nwStartingBalance')?.value) || 0,
            annualSavings: parseFloat(document.getElementById('nwAnnualSavings')?.value) || 0,
            annualIncome: parseFloat(document.getElementById('nwAnnualIncome')?.value) || 0,
            annualSpending: parseFloat(document.getElementById('nwAnnualSpending')?.value) || 0,
            currentAge: parseInt(document.getElementById('nwCurrentAge')?.value, 10) || 33,
            retirementAge: parseInt(document.getElementById('nwRetirementAge')?.value, 10) || 65,
            baseRoi: parseFloat(document.getElementById('nwBaseRoi')?.value) || 7,
            conservativeRoi: parseFloat(document.getElementById('nwConservativeRoi')?.value) || 6,
            aggressiveRoi: parseFloat(document.getElementById('nwAggressiveRoi')?.value) || 10,
            aggressiveSavings: parseFloat(document.getElementById('nwAggressiveSavings')?.value) || 45000
        };

        // Build query string using URLSearchParams for safety
        const urlParams = new URLSearchParams(params);
        const url = `/Networth/Calculate?${urlParams.toString()}`;

        fetch(url)
            .then(function (response) {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                return response.json();
            })
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
