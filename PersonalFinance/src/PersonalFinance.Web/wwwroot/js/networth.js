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

    // Color palette — Dark futuristic neon theme
    const colors = {
        base: '#818cf8',
        conservative: '#a78bfa',
        aggressive: '#34d399',
        fi: '#38bdf8',
        baseAlpha: 'rgba(129, 140, 248, 0.15)',
        conservativeAlpha: 'rgba(167, 139, 250, 0.1)',
        aggressiveAlpha: 'rgba(52, 211, 153, 0.15)',
        gridColor: 'rgba(148, 163, 184, 0.08)',
        gridColorStrong: 'rgba(148, 163, 184, 0.15)',
        tickColor: '#64748b',
        tooltipBg: 'rgba(15, 23, 42, 0.96)',
        tooltipBorder: 'rgba(99, 102, 241, 0.3)'
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
     * Create a gradient fill for a chart line
     * @param {CanvasRenderingContext2D} ctx - Canvas context
     * @param {string} colorHex - Hex color for the gradient
     * @returns {CanvasGradient} The gradient object
     */
    function createLineGradient(ctx, colorHex) {
        var gradient = ctx.createLinearGradient(0, 0, 0, 350);
        gradient.addColorStop(0, hexToRgba(colorHex, 0.35));
        gradient.addColorStop(0.5, hexToRgba(colorHex, 0.12));
        gradient.addColorStop(1, hexToRgba(colorHex, 0.0));
        return gradient;
    }

    /**
     * Convert hex color to rgba string
     * @param {string} hex - Hex color (e.g. '#818cf8')
     * @param {number} alpha - Alpha value 0-1
     * @returns {string} RGBA string
     */
    function hexToRgba(hex, alpha) {
        var r = parseInt(hex.slice(1, 3), 16);
        var g = parseInt(hex.slice(3, 5), 16);
        var b = parseInt(hex.slice(5, 7), 16);
        return 'rgba(' + r + ', ' + g + ', ' + b + ', ' + alpha + ')';
    }

    /**
     * Render the growth chart showing all three scenarios over time
     */
    function renderGrowthChart(data) {
        var ctxEl = document.getElementById('nwGrowthChart');
        if (!ctxEl) return;

        if (growthChart) {
            growthChart.destroy();
        }

        var ctx = ctxEl.getContext('2d');
        var labels = data.base.years.map(function (y) { return y.year; });
        var fiTarget = data.fiTarget;

        // Build gradient fills
        var baseGrad = createLineGradient(ctx, colors.base);
        var consGrad = createLineGradient(ctx, colors.conservative);
        var aggGrad = createLineGradient(ctx, colors.aggressive);

        var datasets = [
            {
                label: 'Base (' + data.base.roiPercent + '%)',
                data: data.base.years.map(function (y) { return y.endingBalance; }),
                borderColor: colors.base,
                backgroundColor: baseGrad,
                fill: true,
                tension: 0.4,
                pointRadius: 0,
                pointHoverRadius: 6,
                pointHoverBackgroundColor: colors.base,
                pointHoverBorderColor: '#fff',
                pointHoverBorderWidth: 2,
                borderWidth: 3,
                borderCapStyle: 'round'
            },
            {
                label: 'Conservative (' + data.conservative.roiPercent + '%)',
                data: data.conservative.years.map(function (y) { return y.endingBalance; }),
                borderColor: colors.conservative,
                backgroundColor: consGrad,
                fill: true,
                tension: 0.4,
                pointRadius: 0,
                pointHoverRadius: 6,
                pointHoverBackgroundColor: colors.conservative,
                pointHoverBorderColor: '#fff',
                pointHoverBorderWidth: 2,
                borderWidth: 2,
                borderCapStyle: 'round'
            },
            {
                label: 'Aggressive (' + data.aggressive.roiPercent + '%)',
                data: data.aggressive.years.map(function (y) { return y.endingBalance; }),
                borderColor: colors.aggressive,
                backgroundColor: aggGrad,
                fill: true,
                tension: 0.4,
                pointRadius: 0,
                pointHoverRadius: 6,
                pointHoverBackgroundColor: colors.aggressive,
                pointHoverBorderColor: '#fff',
                pointHoverBorderWidth: 2,
                borderWidth: 3,
                borderCapStyle: 'round'
            },
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
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: colors.tooltipBg,
                        borderColor: colors.tooltipBorder,
                        borderWidth: 1,
                        titleColor: '#e2e8f0',
                        bodyColor: '#cbd5e1',
                        titleFont: { size: 13, weight: '700', family: '"Inter", system-ui, sans-serif' },
                        bodyFont: { size: 12, family: '"Inter", system-ui, sans-serif' },
                        padding: 14,
                        cornerRadius: 10,
                        displayColors: true,
                        boxPadding: 6,
                        usePointStyle: true,
                        callbacks: {
                            title: function (items) {
                                return '📅 ' + items[0].label;
                            },
                            label: function (context) {
                                var val = formatCurrency(context.parsed.y);
                                return ' ' + context.dataset.label + ':  ' + val;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            color: colors.gridColor,
                            drawBorder: false
                        },
                        ticks: {
                            font: { size: 11, weight: '500' },
                            color: colors.tickColor,
                            maxTicksLimit: 15
                        },
                        border: {
                            display: false
                        }
                    },
                    y: {
                        grid: {
                            color: colors.gridColor,
                            drawBorder: false
                        },
                        ticks: {
                            font: { size: 11, weight: '500' },
                            color: colors.tickColor,
                            callback: function (value) {
                                return formatCompact(value);
                            }
                        },
                        border: {
                            display: false
                        }
                    }
                },
                animation: {
                    duration: 1200,
                    easing: 'easeOutQuart'
                }
            },
            plugins: [{
                // Glow effect plugin for lines
                id: 'lineGlow',
                afterDraw: function (chart) {
                    var ctx = chart.ctx;
                    chart.data.datasets.forEach(function (dataset, i) {
                        if (i === 3) return; // skip FI target
                        var meta = chart.getDatasetMeta(i);
                        if (meta.hidden || !meta.dataset) return;

                        ctx.save();
                        ctx.shadowColor = dataset.borderColor;
                        ctx.shadowBlur = 12;
                        ctx.shadowOffsetX = 0;
                        ctx.shadowOffsetY = 0;
                        ctx.strokeStyle = dataset.borderColor;
                        ctx.lineWidth = dataset.borderWidth;
                        ctx.lineCap = 'round';
                        ctx.lineJoin = 'round';

                        // Draw the glow line by iterating over points
                        if (meta.data.length > 1) {
                            ctx.beginPath();
                            ctx.moveTo(meta.data[0].x, meta.data[0].y);
                            for (var j = 1; j < meta.data.length; j++) {
                                ctx.lineTo(meta.data[j].x, meta.data[j].y);
                            }
                            ctx.stroke();
                        }
                        ctx.restore();
                    });
                }
            }]
        });
    }

    /**
     * Render the comparison bar chart showing final net worth across scenarios
     */
    function renderComparisonChart(data) {
        var ctxEl = document.getElementById('nwComparisonChart');
        if (!ctxEl) return;

        if (comparisonChart) {
            comparisonChart.destroy();
        }

        var ctx = ctxEl.getContext('2d');

        // Use ending balance from the last year of each scenario for comparison
        var baseLastYear = data.base.years[data.base.years.length - 1];
        var conservativeLastYear = data.conservative.years[data.conservative.years.length - 1];
        var aggressiveLastYear = data.aggressive.years[data.aggressive.years.length - 1];

        // Create gradient backgrounds for bars
        var baseBarGrad = ctx.createLinearGradient(0, 0, 0, 280);
        baseBarGrad.addColorStop(0, colors.base);
        baseBarGrad.addColorStop(1, hexToRgba(colors.base, 0.4));

        var consBarGrad = ctx.createLinearGradient(0, 0, 0, 280);
        consBarGrad.addColorStop(0, colors.conservative);
        consBarGrad.addColorStop(1, hexToRgba(colors.conservative, 0.4));

        var aggBarGrad = ctx.createLinearGradient(0, 0, 0, 280);
        aggBarGrad.addColorStop(0, colors.aggressive);
        aggBarGrad.addColorStop(1, hexToRgba(colors.aggressive, 0.4));

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
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: colors.tooltipBg,
                        borderColor: colors.tooltipBorder,
                        borderWidth: 1,
                        titleColor: '#e2e8f0',
                        bodyColor: '#cbd5e1',
                        titleFont: { size: 13, weight: '700', family: '"Inter", system-ui, sans-serif' },
                        bodyFont: { size: 12, family: '"Inter", system-ui, sans-serif' },
                        padding: 14,
                        cornerRadius: 10,
                        displayColors: true,
                        boxPadding: 6,
                        usePointStyle: true,
                        callbacks: {
                            title: function (items) {
                                return '📊 ' + items[0].label + ' Scenario';
                            },
                            label: function (context) {
                                return ' Final Net Worth:  ' + formatCurrency(context.parsed.y);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: {
                            font: { size: 12, weight: '600' },
                            color: colors.tickColor
                        },
                        border: {
                            display: false
                        }
                    },
                    y: {
                        grid: {
                            color: colors.gridColor,
                            drawBorder: false
                        },
                        ticks: {
                            font: { size: 11, weight: '500' },
                            color: colors.tickColor,
                            callback: function (value) {
                                return formatCompact(value);
                            }
                        },
                        border: {
                            display: false
                        }
                    }
                },
                animation: {
                    duration: 1000,
                    easing: 'easeOutQuart'
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
        const fiveMillion = 5000000;

        if (endingBalance >= fiveMillion) {
            return ' class="nw-row-5m"';
        }
        if (endingBalance >= fiTarget) {
            return ' class="nw-row-fi"';
        }

        return '';
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
            const roiClass = year.roi >= annualIncome ? ' nw-roi-highlight' : '';
            return `<tr${rowClass}>
                <td>${year.year}</td>
                <td>${year.age}</td>
                <td class="text-end nw-currency">${formatCurrency(year.startingBalance)}</td>
                <td class="text-end nw-currency">${formatCurrency(year.savings)}</td>
                <td class="text-end nw-currency text-success${roiClass}">${formatCurrency(year.roi)}</td>
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
     * Validate input values client-side before sending to the server.
     * @returns {boolean} True if all inputs are valid, false otherwise.
     */
    function validateInputs() {
        let isValid = true;
        const validators = [
            { id: 'nwStartingBalance', min: 0, max: 1000000000, label: 'Starting balance' },
            { id: 'nwAnnualSavings', min: 0, max: 10000000, label: 'Annual savings' },
            { id: 'nwAnnualIncome', min: 0, max: 100000000, label: 'Annual income' },
            { id: 'nwAnnualSpending', min: 0, max: 10000000, label: 'Annual spending' },
            { id: 'nwAggressiveSavings', min: 0, max: 10000000, label: 'Aggressive savings' },
            { id: 'nwCurrentAge', min: 18, max: 100, label: 'Current age' },
            { id: 'nwRetirementAge', min: 18, max: 120, label: 'Retirement age' },
            { id: 'nwBaseRoi', min: 0, max: 50, label: 'Base ROI' },
            { id: 'nwConservativeRoi', min: 0, max: 50, label: 'Conservative ROI' },
            { id: 'nwAggressiveRoi', min: 0, max: 50, label: 'Aggressive ROI' }
        ];

        // Clear previous validation states
        validators.forEach(function (v) {
            const el = document.getElementById(v.id);
            if (el) {
                el.classList.remove('nw-input-invalid');
            }
        });

        validators.forEach(function (v) {
            const el = document.getElementById(v.id);
            if (!el) return;
            const val = parseFloat(el.value);
            if (isNaN(val) || val < v.min || val > v.max) {
                el.classList.add('nw-input-invalid');
                isValid = false;
            }
        });

        // Check retirement age > current age
        const currentAge = parseInt(document.getElementById('nwCurrentAge')?.value, 10);
        const retirementAge = parseInt(document.getElementById('nwRetirementAge')?.value, 10);
        if (!isNaN(currentAge) && !isNaN(retirementAge) && retirementAge <= currentAge) {
            document.getElementById('nwRetirementAge')?.classList.add('nw-input-invalid');
            isValid = false;
        }

        return isValid;
    }

    /**
     * Generate a random integer between min and max (inclusive).
     * @param {number} min - Minimum value.
     * @param {number} max - Maximum value.
     * @returns {number} Random integer.
     */
    function randomInt(min, max) {
        return Math.floor(Math.random() * (max - min + 1)) + min;
    }

    /**
     * Generate a random float between min and max, rounded to the given decimal places.
     * @param {number} min - Minimum value.
     * @param {number} max - Maximum value.
     * @param {number} decimals - Number of decimal places.
     * @returns {number} Random float.
     */
    function randomFloat(min, max, decimals) {
        var value = Math.random() * (max - min) + min;
        var factor = Math.pow(10, decimals);
        return Math.round(value * factor) / factor;
    }

    /**
     * Randomize all input fields with realistic values and recalculate.
     */
    function randomizeParameters() {
        // Financial Overview - generate realistic but varied scenarios
        var startingBalance = randomInt(10000, 500000);
        var annualIncome = randomInt(40000, 250000);
        var savingsRate = randomFloat(0.10, 0.50, 2); // 10% to 50% savings rate
        var annualSavings = Math.round(annualIncome * savingsRate / 1000) * 1000;
        var spendingRate = randomFloat(0.35, 0.70, 2); // 35% to 70% of income after taxes
        var annualSpending = Math.round(annualIncome * spendingRate / 1000) * 1000;

        // Timeline - realistic ages
        var currentAge = randomInt(22, 55);
        var retirementAge = currentAge + randomInt(10, 35);
        if (retirementAge > 80) retirementAge = 80;

        // Return Assumptions - realistic ROI ranges
        var conservativeRoi = randomFloat(3.0, 7.0, 1);
        var baseRoi = conservativeRoi + randomFloat(0.5, 3.0, 1);
        var aggressiveRoi = baseRoi + randomFloat(1.0, 5.0, 1);
        var aggressiveSavings = annualSavings + randomInt(5000, 50000);

        document.getElementById('nwStartingBalance').value = startingBalance;
        document.getElementById('nwAnnualSavings').value = annualSavings;
        document.getElementById('nwAnnualIncome').value = annualIncome;
        document.getElementById('nwAnnualSpending').value = annualSpending;
        document.getElementById('nwCurrentAge').value = currentAge;
        document.getElementById('nwRetirementAge').value = retirementAge;
        document.getElementById('nwBaseRoi').value = baseRoi;
        document.getElementById('nwConservativeRoi').value = conservativeRoi;
        document.getElementById('nwAggressiveRoi').value = aggressiveRoi;
        document.getElementById('nwAggressiveSavings').value = aggressiveSavings;

        // Clear any validation errors
        clearValidationMessage();
        document.querySelectorAll('.nw-input-invalid').forEach(function (el) {
            el.classList.remove('nw-input-invalid');
        });

        // Recalculate with randomized values
        recalculate();
    }

    /**
     * Reset all input fields to their default values and recalculate.
     */
    function resetToDefaults() {
        document.getElementById('nwStartingBalance').value = 195033.14;
        document.getElementById('nwAnnualSavings').value = 32000;
        document.getElementById('nwAnnualIncome').value = 108409.60;
        document.getElementById('nwAnnualSpending').value = 50000;
        document.getElementById('nwCurrentAge').value = 33;
        document.getElementById('nwRetirementAge').value = 65;
        document.getElementById('nwBaseRoi').value = 7.0;
        document.getElementById('nwConservativeRoi').value = 6.0;
        document.getElementById('nwAggressiveRoi').value = 10.0;
        document.getElementById('nwAggressiveSavings').value = 45000;

        // Clear any validation errors
        clearValidationMessage();
        document.querySelectorAll('.nw-input-invalid').forEach(function (el) {
            el.classList.remove('nw-input-invalid');
        });

        // Recalculate with default values
        recalculate();
    }

    /**
     * Recalculate projections via AJAX and update all UI elements.
     */
    function recalculate() {
        // Validate inputs before sending
        if (!validateInputs()) {
            return;
        }

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
                    if (response.status === 400) {
                        return response.text().then(function (msg) {
                            showValidationMessage(msg);
                            throw new Error('Validation error');
                        });
                    }
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                return response.json();
            })
            .then(function (data) {
                clearValidationMessage();
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
                if (err.message !== 'Validation error') {
                    console.error('Failed to recalculate projection:', err);
                }
            });
    }

    /**
     * Display a validation error message to the user.
     * @param {string} message - The error message from the server.
     */
    function showValidationMessage(message) {
        clearValidationMessage();
        const container = document.querySelector('.nw-header');
        if (!container) return;

        const alert = document.createElement('div');
        alert.id = 'nwValidationAlert';
        alert.className = 'alert alert-danger alert-dismissible fade show nw-alert mt-3';
        alert.role = 'alert';
        alert.innerHTML = message + '<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>';
        container.appendChild(alert);
    }

    /**
     * Clear any displayed validation error message.
     */
    function clearValidationMessage() {
        const alert = document.getElementById('nwValidationAlert');
        if (alert) {
            alert.remove();
        }
    }

    /**
     * Initialize Bootstrap tooltips for parameter hints.
     */
    function initTooltips() {
        const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
        tooltipTriggerList.forEach(function (el) {
            new bootstrap.Tooltip(el);
        });
    }

    /**
     * Initialize everything when DOM is ready
     */
    function init() {
        const data = window.nwProjectionData;
        if (!data) return;

        // Initialize tooltips for parameter hints
        initTooltips();

        // Render charts
        renderGrowthChart(data);
        renderComparisonChart(data);

        // Initialize scenario tabs
        initScenarioTabs();

        // Initialize live recalculation
        initLiveRecalculation();

        // Initialize randomize button
        const randomBtn = document.getElementById('nwRandomBtn');
        if (randomBtn) {
            randomBtn.addEventListener('click', randomizeParameters);
        }

        // Initialize reset button
        const resetBtn = document.getElementById('nwResetBtn');
        if (resetBtn) {
            resetBtn.addEventListener('click', resetToDefaults);
        }
    }

    // Run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
