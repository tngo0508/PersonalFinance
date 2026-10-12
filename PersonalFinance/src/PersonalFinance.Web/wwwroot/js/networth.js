/**
 * Net Worth Projection page JavaScript
 * Handles chart rendering, scenario tab switching, AJAX recalculation, and data persistence.
 */
(function () {
    'use strict';

    // Debounce delay in milliseconds for live recalculation
    const DEBOUNCE_DELAY = 500;

    // API endpoint base path for net worth controller actions
    const _apiBase = '/Networth';

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

    // Reference to shared utilities
    const PF = window.PFUtils;

    /**
     * Format a numeric value as a currency string (delegates to shared utils).
     * @param {number} value - The numeric value to format.
     * @returns {string} Formatted currency string (e.g., "$1,234,567").
     */
    function formatCurrency (value) {
        return PF.formatCurrency(value);
    }

    /**
     * Format a numeric value as a compact currency string (delegates to shared utils).
     * @param {number} value - The numeric value to format.
     * @returns {string} Compact currency string (e.g., "$1.2M", "$50K").
     */
    function formatCompact (value) {
        return PF.formatCompactCurrency(value);
    }

    // Canvas height for growth line chart gradient
    const GROWTH_CHART_HEIGHT = 350;

    // Canvas height for comparison bar chart gradient
    const COMPARISON_CHART_HEIGHT = 280;

    /**
     * Create a gradient fill for a chart line
     * @param {CanvasRenderingContext2D} ctx - Canvas context
     * @param {string} colorHex - Hex color for the gradient
     * @returns {CanvasGradient} The gradient object
     */
    function createLineGradient (ctx, colorHex) {
        const gradient = ctx.createLinearGradient(0, 0, 0, GROWTH_CHART_HEIGHT);
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
    function hexToRgba (hex, alpha) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return 'rgba(' + r + ', ' + g + ', ' + b + ', ' + alpha + ')';
    }

    /**
     * Render the growth chart showing all three scenarios over time
     */
    function renderGrowthChart (data) {
        const ctxEl = document.getElementById('nwGrowthChart');
        if (!ctxEl) { return; }

        if (growthChart) {
            growthChart.destroy();
        }

        const ctx = ctxEl.getContext('2d');
        const labels = data.base.years.map(function (y) { return y.year; });
        const fiTarget = data.fiTarget;

        // Build gradient fills
        const baseGrad = createLineGradient(ctx, colors.base);
        const consGrad = createLineGradient(ctx, colors.conservative);
        const aggGrad = createLineGradient(ctx, colors.aggressive);

        const datasets = [
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
                                const val = formatCurrency(context.parsed.y);
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
                    const ctx = chart.ctx;
                    chart.data.datasets.forEach(function (dataset, i) {
                        if (i === 3) { return; } // skip FI target
                        const meta = chart.getDatasetMeta(i);
                        if (meta.hidden || !meta.dataset) { return; }

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
                            for (let j = 1; j < meta.data.length; j++) {
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
        const baseBarGrad = ctx.createLinearGradient(0, 0, 0, COMPARISON_CHART_HEIGHT);
        baseBarGrad.addColorStop(0, colors.base);
        baseBarGrad.addColorStop(1, hexToRgba(colors.base, 0.4));

        const consBarGrad = ctx.createLinearGradient(0, 0, 0, COMPARISON_CHART_HEIGHT);
        consBarGrad.addColorStop(0, colors.conservative);
        consBarGrad.addColorStop(1, hexToRgba(colors.conservative, 0.4));

        const aggBarGrad = ctx.createLinearGradient(0, 0, 0, COMPARISON_CHART_HEIGHT);
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
    function updateMetrics (data) {
        // FI Target card
        const fiValue = document.querySelector('.nw-metric-fi-value');
        const fiSub = document.querySelector('.nw-metric-fi-sub');
        if (fiValue) {
            const symbolEl = fiValue.querySelector('.nw-currency-symbol');
            const amountEl = fiValue.querySelector('.nw-currency-amount');
            if (symbolEl) { symbolEl.textContent = '$'; }
            if (amountEl) { amountEl.textContent = Math.round(data.fiTarget).toLocaleString('en-US'); }
        }
        if (fiSub) {
            fiSub.innerHTML = 'Based on <span class="nw-currency-symbol">$</span>' + PF.escapeHtml(formatCompact(data.annualSpending)) + '/yr spending';
        }

        // Current Net Worth card
        const currentValue = document.querySelector('.nw-metric-current-value');
        const currentSub = document.querySelector('.nw-metric-current-sub');
        if (currentValue) { currentValue.textContent = formatCurrency(data.base.startingBalance); }
        if (currentSub) { currentSub.textContent = `Age ${data.currentAge}, Year ${data.currentYear}`; }

        // Base Final card
        const baseValue = document.querySelector('.nw-metric-base-value');
        const baseSub = document.querySelector('.nw-metric-base-sub');
        if (baseValue) { baseValue.textContent = formatCurrency(data.base.finalNetWorth); }
        if (baseSub) { baseSub.textContent = `${data.base.roiPercent}% ROI`; }

        // Aggressive Final card
        const aggValue = document.querySelector('.nw-metric-aggressive-value');
        const aggSub = document.querySelector('.nw-metric-aggressive-sub');
        if (aggValue) { aggValue.textContent = formatCurrency(data.aggressive.finalNetWorth); }
        if (aggSub) {
            aggSub.innerHTML = PF.escapeHtml(String(data.aggressive.roiPercent)) + '% ROI · <span class="nw-currency-symbol">$</span>' + PF.escapeHtml(formatCompact(data.aggressive.annualSavings)) + '/yr savings';
        }

        // Update legend
        const legendBase = document.getElementById('nwLegendBase');
        const legendCons = document.getElementById('nwLegendConservative');
        const legendAgg = document.getElementById('nwLegendAggressive');
        if (legendBase) { legendBase.innerHTML = '<span class="nw-legend-dot nw-dot-base"></span> Base (' + PF.escapeHtml(String(data.base.roiPercent)) + '%)'; }
        if (legendCons) { legendCons.innerHTML = '<span class="nw-legend-dot nw-dot-conservative"></span> Conservative (' + PF.escapeHtml(String(data.conservative.roiPercent)) + '%)'; }
        if (legendAgg) { legendAgg.innerHTML = '<span class="nw-legend-dot nw-dot-aggressive"></span> Aggressive (' + PF.escapeHtml(String(data.aggressive.roiPercent)) + '%)'; }
    }

    /**
     * Update scenario comparison cards.
     * @param {Object} data - The projection data object.
     */
    function updateScenarioCards (data) {
        setScenarioValues('nw-sc-base-', data.base);
        setScenarioValues('nw-sc-cons-', data.conservative);
        setScenarioValues('nw-sc-agg-', data.aggressive);
    }

    /**
     * Set scenario card values with a given prefix.
     * @param {string} prefix - CSS class prefix (e.g., 'nw-sc-base-').
     * @param {Object} scenario - The scenario data object.
     */
    function setScenarioValues (prefix, scenario) {
        const savingsEl = document.querySelector(`.${prefix}savings`);
        const finalEl = document.querySelector(`.${prefix}final`);
        const contribEl = document.querySelector(`.${prefix}contrib`);
        const interestEl = document.querySelector(`.${prefix}interest`);

        if (savingsEl) { savingsEl.innerHTML = '<span class="nw-currency-symbol">$</span>' + PF.escapeHtml(formatCompact(scenario.annualSavings)); }
        if (finalEl) { finalEl.innerHTML = '<span class="nw-currency-symbol">$</span>' + PF.escapeHtml(formatCompact(scenario.finalNetWorth)); }
        if (contribEl) { contribEl.innerHTML = '<span class="nw-currency-symbol">$</span>' + PF.escapeHtml(formatCompact(scenario.totalContributions)); }
        if (interestEl) { interestEl.innerHTML = '<span class="nw-currency-symbol">$</span>' + PF.escapeHtml(formatCompact(scenario.totalInterestEarned)); }
    }

    /**
     * Update scenario card ROI badges.
     * @param {Object} data - The projection data object.
     */
    function updateScenarioBadges (data) {
        const badges = document.querySelectorAll('.nw-scenario-badge');
        const roiValues = [data.base.roiPercent, data.conservative.roiPercent, data.aggressive.roiPercent];
        badges.forEach(function (badge, index) {
            if (index < roiValues.length) {
                badge.textContent = `${roiValues[index]}% ROI`;
            }
        });
    }

    /**
     * Determine CSS classes for a table row based on milestone thresholds.
     * @param {number} endingBalance - The ending balance for the year.
     * @param {number} fiTarget - The financial independence target amount.
     * @param {number} annualIncome - The annual income amount.
     * @returns {string} Space-separated CSS class names for the row.
     */
    function getRowHighlightClass (endingBalance, fiTarget, _annualIncome) {
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
    function updateTable (scenarioName) {
        const data = window.nwProjectionData;
        if (!data) { return; }

        const scenarioKey = scenarioMap[scenarioName] || 'base';
        const scenario = data[scenarioKey];
        if (!scenario) { return; }

        const tbody = document.getElementById('nwTableBody');
        if (!tbody) { return; }

        const fiTarget = data.fiTarget;
        const annualIncome = data.annualIncome;

        const rows = scenario.years.map(function (year) {
            const rowClass = getRowHighlightClass(year.endingBalance, fiTarget, annualIncome);
            const roiClass = year.roi >= annualIncome ? ' nw-roi-highlight' : '';
            const esc = PF.escapeHtml;
            return '<tr' + rowClass + '>' +
                '<td>' + esc(year.year) + '</td>' +
                '<td>' + esc(year.age) + '</td>' +
                '<td class="text-end nw-currency">' + esc(formatCurrency(year.startingBalance)) + '</td>' +
                '<td class="text-end nw-currency">' + esc(formatCurrency(year.savings)) + '</td>' +
                '<td class="text-end nw-currency text-success' + roiClass + '">' + esc(formatCurrency(year.roi)) + '</td>' +
                '<td class="text-end nw-currency fw-semibold">' + esc(formatCurrency(year.endingBalance)) + '</td>' +
            '</tr>';
        });

        tbody.innerHTML = rows.join('');
    }

    /**
     * Initialize scenario tab switching.
     * Switches the projection table between Base, Conservative, and Aggressive scenarios.
     */
    function initScenarioTabs () {
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
     * Format a number for display in a slider field input.
     * @param {number} value - The numeric value.
     * @param {string} type - 'currency', 'percent', or 'number'.
     * @returns {string} Formatted string.
     */
    function formatSliderValue (value, type) {
        const num = PF.safeParseFloat(value, NaN);
        if (isNaN(num)) { return ''; }
        if (type === 'currency') {
            return Math.round(num).toLocaleString('en-US');
        }
        if (type === 'percent') {
            return num.toFixed(1);
        }
        return Math.round(num).toString();
    }

    /**
     * Sync hidden save inputs with current slider values for form model binding.
     * Ensures that when the form is submitted, the server receives the current slider values.
     */
    function syncSaveInputs () {
        const fields = [
            { rangeId: 'nwStartingBalanceRange', saveId: 'nwSaveStartingBalance' },
            { rangeId: 'nwAnnualSavingsRange', saveId: 'nwSaveAnnualSavings' },
            { rangeId: 'nwAnnualIncomeRange', saveId: 'nwSaveAnnualIncome' },
            { rangeId: 'nwAnnualSpendingRange', saveId: 'nwSaveAnnualSpending' },
            { rangeId: 'nwCurrentAgeRange', saveId: 'nwSaveCurrentAge' },
            { rangeId: 'nwRetirementAgeRange', saveId: 'nwSaveRetirementAge' },
            { rangeId: 'nwBaseRoiRange', saveId: 'nwSaveBaseRoi' },
            { rangeId: 'nwConservativeRoiRange', saveId: 'nwSaveConservativeRoi' },
            { rangeId: 'nwAggressiveRoiRange', saveId: 'nwSaveAggressiveRoi' },
            { rangeId: 'nwAggressiveSavingsRange', saveId: 'nwSaveAggressiveSavings' }
        ];
        fields.forEach(function (field) {
            const rangeEl = document.getElementById(field.rangeId);
            const saveEl = document.getElementById(field.saveId);
            if (rangeEl && saveEl) {
                saveEl.value = rangeEl.value;
            }
        });
    }

    /**
     * Sync a range slider with its paired text input.
     * @param {string} rangeId - The range input ID.
     * @param {string} inputId - The text input ID.
     * @param {string} formatType - 'currency', 'percent', or 'number'.
     */
    function syncSlider (rangeId, inputId, formatType) {
        const rangeEl = document.getElementById(rangeId);
        const inputEl = document.getElementById(inputId);
        if (!rangeEl || !inputEl) { return; }

        // Initialize display from range value
        inputEl.value = formatSliderValue(rangeEl.value, formatType);

        // Range → Input
        rangeEl.addEventListener('input', function () {
            inputEl.value = formatSliderValue(rangeEl.value, formatType);
            updateSavingsIndicator();
            syncSaveInputs();
            triggerRecalculation();
        });

        // Input → Range (on blur and Enter key)
        inputEl.addEventListener('input', function () {
            const raw = inputEl.value.replace(/[^0-9.]/g, '');
            const num = PF.safeParseFloat(raw, NaN);
            if (!isNaN(num)) {
                const min = PF.safeParseFloat(rangeEl.min, 0);
                const max = PF.safeParseFloat(rangeEl.max, Infinity);
                rangeEl.value = Math.max(min, Math.min(max, num));
            }
            updateSavingsIndicator();
            syncSaveInputs();
            triggerRecalculation();
        });

        inputEl.addEventListener('blur', function () {
            const raw = inputEl.value.replace(/[^0-9.]/g, '');
            const num = PF.safeParseFloat(raw, NaN);
            if (!isNaN(num)) {
                const min = PF.safeParseFloat(rangeEl.min, 0);
                const max = PF.safeParseFloat(rangeEl.max, Infinity);
                const clamped = Math.max(min, Math.min(max, num));
                rangeEl.value = clamped;
                inputEl.value = formatSliderValue(clamped, formatType);
            }
            syncSaveInputs();
            triggerRecalculation();
        });

        inputEl.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                inputEl.blur();
            }
        });
    }

    /**
     * Debounced recalculation trigger shared by sliders and inputs.
     * Waits for the user to stop interacting before sending an AJAX request.
     */
    let recalcTimeout;
    /**
     * Trigger a debounced recalculation of projections.
     */
    function triggerRecalculation () {
        clearTimeout(recalcTimeout);
        recalcTimeout = setTimeout(function () {
            recalculate();
        }, DEBOUNCE_DELAY);
    }

    /**
     * Update the savings rate indicator bar.
     * Calculates and displays the ratio of annual savings to annual income.
     */
    function updateSavingsIndicator () {
        const savingsEl = document.getElementById('nwAnnualSavings');
        const incomeEl = document.getElementById('nwAnnualIncome');
        const rateValueEl = document.getElementById('nwSavingsRateValue');
        const rateFillEl = document.getElementById('nwSavingsRateFill');

        if (!savingsEl || !incomeEl || !rateValueEl || !rateFillEl) { return; }

        const savings = PF.safeParseFloat(savingsEl.value.replace(/[^0-9.]/g, ''), 0);
        const income = PF.safeParseFloat(incomeEl.value.replace(/[^0-9.]/g, ''), 0);

        if (income <= 0) {
            rateValueEl.textContent = '0%';
            rateFillEl.style.width = '0%';
            rateFillEl.className = 'nw-savings-indicator__fill';
            return;
        }

        const rate = (savings / income) * 100;
        const displayRate = Math.min(rate, 100);
        rateValueEl.textContent = rate.toFixed(1) + '%';
        rateFillEl.style.width = displayRate + '%';

        // Color coding
        rateFillEl.className = 'nw-savings-indicator__fill';
        if (rate >= 30) {
            rateFillEl.classList.add('nw-savings-great');
        } else if (rate >= 20) {
            rateFillEl.classList.add('nw-savings-good');
        }
    }

    /**
     * Initialize live recalculation on input change (debounced).
     * Sets up all slider-input pairs and the initial savings indicator.
     */
    function initLiveRecalculation () {
        // Sync all slider pairs
        syncSlider('nwStartingBalanceRange', 'nwStartingBalance', 'currency');
        syncSlider('nwAnnualSavingsRange', 'nwAnnualSavings', 'currency');
        syncSlider('nwAnnualIncomeRange', 'nwAnnualIncome', 'currency');
        syncSlider('nwAnnualSpendingRange', 'nwAnnualSpending', 'currency');
        syncSlider('nwCurrentAgeRange', 'nwCurrentAge', 'number');
        syncSlider('nwRetirementAgeRange', 'nwRetirementAge', 'number');
        syncSlider('nwBaseRoiRange', 'nwBaseRoi', 'percent');
        syncSlider('nwConservativeRoiRange', 'nwConservativeRoi', 'percent');
        syncSlider('nwAggressiveRoiRange', 'nwAggressiveRoi', 'percent');
        syncSlider('nwAggressiveSavingsRange', 'nwAggressiveSavings', 'currency');

        // Initial savings indicator
        updateSavingsIndicator();
    }

    /**
     * Apply a preset scenario by setting all slider values.
     * @param {Object} preset - Key-value pairs of field IDs to values.
     */
    function applyPreset (preset) {
        // Map of field IDs to their format types
        const fields = [
            { id: 'nwStartingBalance', type: 'currency' },
            { id: 'nwAnnualSavings', type: 'currency' },
            { id: 'nwAnnualIncome', type: 'currency' },
            { id: 'nwAnnualSpending', type: 'currency' },
            { id: 'nwCurrentAge', type: 'number' },
            { id: 'nwRetirementAge', type: 'number' },
            { id: 'nwBaseRoi', type: 'percent' },
            { id: 'nwConservativeRoi', type: 'percent' },
            { id: 'nwAggressiveRoi', type: 'percent' },
            { id: 'nwAggressiveSavings', type: 'currency' }
        ];

        fields.forEach(function (field) {
            if (preset[field.id] !== undefined) {
                const rangeId = field.id + 'Range';
                const rangeEl = document.getElementById(rangeId);
                const inputEl = document.getElementById(field.id);
                if (rangeEl) {
                    rangeEl.value = preset[field.id];
                }
                if (inputEl) {
                    inputEl.value = formatSliderValue(preset[field.id], field.type);
                }
            }
        });

        // Clear validation
        clearValidationMessage();
        document.querySelectorAll('.nw-input-invalid').forEach(function (el) {
            el.classList.remove('nw-input-invalid');
        });

        updateSavingsIndicator();
        syncSaveInputs();
        recalculate();
    }

    /**
     * Initialize preset scenario buttons.
     * Wires up FIRE, Coast, Traditional, Ultra, and Conservative preset buttons.
     */
    function initPresets () {
        const presets = {
            fire: {
                nwStartingBalance: 50000,
                nwAnnualSavings: 85000,
                nwAnnualIncome: 150000,
                nwAnnualSpending: 35000,
                nwCurrentAge: 28,
                nwRetirementAge: 45,
                nwBaseRoi: 8.0,
                nwConservativeRoi: 5.0,
                nwAggressiveRoi: 12.0,
                nwAggressiveSavings: 120000
            },
            coast: {
                nwStartingBalance: 350000,
                nwAnnualSavings: 20000,
                nwAnnualIncome: 120000,
                nwAnnualSpending: 55000,
                nwCurrentAge: 35,
                nwRetirementAge: 65,
                nwBaseRoi: 7.0,
                nwConservativeRoi: 5.0,
                nwAggressiveRoi: 10.0,
                nwAggressiveSavings: 35000
            },
            traditional: {
                nwStartingBalance: 100000,
                nwAnnualSavings: 15000,
                nwAnnualIncome: 95000,
                nwAnnualSpending: 60000,
                nwCurrentAge: 30,
                nwRetirementAge: 65,
                nwBaseRoi: 7.0,
                nwConservativeRoi: 5.0,
                nwAggressiveRoi: 9.0,
                nwAggressiveSavings: 25000
            },
            ultra: {
                nwStartingBalance: 200000,
                nwAnnualSavings: 150000,
                nwAnnualIncome: 350000,
                nwAnnualSpending: 80000,
                nwCurrentAge: 25,
                nwRetirementAge: 40,
                nwBaseRoi: 10.0,
                nwConservativeRoi: 6.0,
                nwAggressiveRoi: 15.0,
                nwAggressiveSavings: 200000
            },
            conservative: {
                nwStartingBalance: 150000,
                nwAnnualSavings: 12000,
                nwAnnualIncome: 80000,
                nwAnnualSpending: 45000,
                nwCurrentAge: 40,
                nwRetirementAge: 67,
                nwBaseRoi: 5.0,
                nwConservativeRoi: 3.0,
                nwAggressiveRoi: 7.0,
                nwAggressiveSavings: 18000
            }
        };

        Object.keys(presets).forEach(function (key) {
            const btn = document.getElementById('nwPreset' + key.charAt(0).toUpperCase() + key.slice(1));
            if (btn) {
                btn.addEventListener('click', function () {
                    applyPreset(presets[key]);
                });
            }
        });
    }

    /**
     * Validate input values client-side before sending to the server.
     * Checks ranges and ensures retirement age is greater than current age.
     * @returns {boolean} True if all inputs are valid, false otherwise.
     */
    function validateInputs () {
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
            const rangeEl = document.getElementById(v.id + 'Range');
            if (el) { el.classList.remove('nw-input-invalid'); }
            if (rangeEl) { rangeEl.classList.remove('nw-input-invalid'); }
        });

        validators.forEach(function (v) {
            const val = getSliderValue(v.id);
            const el = document.getElementById(v.id);
            const rangeEl = document.getElementById(v.id + 'Range');
            const num = PF.safeParseFloat(val, NaN);
            if (isNaN(num) || num < v.min || num > v.max) {
                if (el) { el.classList.add('nw-input-invalid'); }
                if (rangeEl) { rangeEl.classList.add('nw-input-invalid'); }
                isValid = false;
            }
        });

        // Check retirement age > current age
        const currentAge = Math.round(getSliderValue('nwCurrentAge'));
        const retirementAge = Math.round(getSliderValue('nwRetirementAge'));
        if (currentAge && retirementAge && retirementAge <= currentAge) {
            const retEl = document.getElementById('nwRetirementAge');
            const retRange = document.getElementById('nwRetirementAgeRange');
            if (retEl) { retEl.classList.add('nw-input-invalid'); }
            if (retRange) { retRange.classList.add('nw-input-invalid'); }
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
    function randomInt (min, max) {
        return Math.floor(Math.random() * (max - min + 1)) + min;
    }

    /**
     * Generate a random float between min and max, rounded to the given decimal places.
     * @param {number} min - Minimum value.
     * @param {number} max - Maximum value.
     * @param {number} decimals - Number of decimal places.
     * @returns {number} Random float.
     */
    function randomFloat (min, max, decimals) {
        const value = Math.random() * (max - min) + min;
        const factor = Math.pow(10, decimals);
        return Math.round(value * factor) / factor;
    }

    /**
     * Randomize all input fields with realistic values and recalculate.
     * Generates random but realistic financial parameters for exploration.
     */
    function randomizeParameters () {
        // Financial Overview - generate realistic but varied scenarios
        const startingBalance = randomInt(10000, 500000);
        const annualIncome = randomInt(40000, 250000);
        const savingsRate = randomFloat(0.10, 0.50, 2); // 10% to 50% savings rate
        const annualSavings = Math.round(annualIncome * savingsRate / 1000) * 1000;
        const spendingRate = randomFloat(0.35, 0.70, 2); // 35% to 70% of income after taxes
        const annualSpending = Math.round(annualIncome * spendingRate / 1000) * 1000;

        // Timeline - realistic ages
        const currentAge = randomInt(22, 55);
        let retirementAge = currentAge + randomInt(10, 35);
        if (retirementAge > 80) { retirementAge = 80; }

        // Return Assumptions - realistic ROI ranges
        const conservativeRoi = randomFloat(3.0, 7.0, 1);
        const baseRoi = conservativeRoi + randomFloat(0.5, 3.0, 1);
        const aggressiveRoi = baseRoi + randomFloat(1.0, 5.0, 1);
        const aggressiveSavings = annualSavings + randomInt(5000, 50000);

        applyPreset({
            nwStartingBalance: startingBalance,
            nwAnnualSavings: annualSavings,
            nwAnnualIncome: annualIncome,
            nwAnnualSpending: annualSpending,
            nwCurrentAge: currentAge,
            nwRetirementAge: retirementAge,
            nwBaseRoi: baseRoi,
            nwConservativeRoi: conservativeRoi,
            nwAggressiveRoi: aggressiveRoi,
            nwAggressiveSavings: aggressiveSavings
        });
    }

    /**
     * Reset all input fields to their default values and recalculate.
     * Also submits the reset form to clear any saved custom values.
     */
    function resetToDefaults () {
        applyPreset({
            nwStartingBalance: 195033,
            nwAnnualSavings: 32000,
            nwAnnualIncome: 108410,
            nwAnnualSpending: 50000,
            nwCurrentAge: 33,
            nwRetirementAge: 65,
            nwBaseRoi: 7.0,
            nwConservativeRoi: 6.0,
            nwAggressiveRoi: 10.0,
            nwAggressiveSavings: 45000
        });
    }

    /**
     * Get the raw numeric value from a slider field (strips formatting).
     * @param {string} inputId - The text input ID.
     * @returns {number} The parsed numeric value.
     */
    function getSliderValue (inputId) {
        const el = document.getElementById(inputId);
        if (!el) { return 0; }
        const raw = el.value.replace(/[^0-9.]/g, '');
        return PF.safeParseFloat(raw, 0);
    }

    /**
     * Recalculate projections via AJAX and update all UI elements.
     */
    function recalculate () {
        // Validate inputs before sending
        if (!validateInputs()) {
            return;
        }

        const params = {
            startingBalance: getSliderValue('nwStartingBalance'),
            annualSavings: getSliderValue('nwAnnualSavings'),
            annualIncome: getSliderValue('nwAnnualIncome'),
            annualSpending: getSliderValue('nwAnnualSpending'),
            currentAge: Math.round(getSliderValue('nwCurrentAge')) || 33,
            retirementAge: Math.round(getSliderValue('nwRetirementAge')) || 65,
            baseRoi: getSliderValue('nwBaseRoi') || 7,
            conservativeRoi: getSliderValue('nwConservativeRoi') || 6,
            aggressiveRoi: getSliderValue('nwAggressiveRoi') || 10,
            aggressiveSavings: getSliderValue('nwAggressiveSavings') || 45000
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
                updateScenarioBadges(data);
                // Re-render the active scenario table
                const activeBtn = document.querySelector('.nw-tab-btn.active');
                if (activeBtn) {
                    updateTable(activeBtn.getAttribute('data-scenario'));
                }
            })
            .catch(function (err) {
                if (err.message !== 'Validation error') {
                    console.error('Failed to recalculate projection:', err);
                    showValidationMessage('Failed to load projection data. Please check your connection and try again.');
                }
            });
    }

    /**
     * Display a validation error message to the user.
     * @param {string} message - The error message from the server.
     */
    function showValidationMessage (message) {
        clearValidationMessage();
        const container = document.querySelector('.nw-header');
        if (!container) { return; }

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
    function clearValidationMessage () {
        const alert = document.getElementById('nwValidationAlert');
        if (alert) {
            alert.remove();
        }
    }

    /**
     * Initialize Bootstrap tooltips for parameter hints.
     * Creates tooltip instances for all elements with data-bs-toggle="tooltip".
     */
    function initTooltips () {
        const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
        tooltipTriggerList.forEach(function (el) {
            new bootstrap.Tooltip(el);
        });
    }

    /**
     * Initialize everything when DOM is ready.
     * Renders charts, sets up sliders, tabs, presets, and triggers initial recalculation.
     */
    function init () {
        const data = window.nwProjectionData;
        if (!data) { return; }

        // Parameters are already loaded server-side via the Index action.
        // The sliders reflect saved values from the database on page render.

        // Initialize tooltips for parameter hints
        initTooltips();

        // Render charts
        renderGrowthChart(data);
        renderComparisonChart(data);

        // Initialize scenario tabs
        initScenarioTabs();

        // Initialize live recalculation
        initLiveRecalculation();

        // Initialize preset scenario buttons
        initPresets();

        // Initialize randomize button
        const randomBtn = document.getElementById('nwRandomBtn');
        if (randomBtn) {
            randomBtn.addEventListener('click', randomizeParameters);
        }

        // Initialize reset button — reset sliders to defaults and clear saved data
        const resetBtn = document.querySelector('#nwResetForm button[type="submit"]');
        if (resetBtn) {
            resetBtn.addEventListener('click', function (e) {
                e.preventDefault();
                resetToDefaults();
                document.getElementById('nwResetForm').submit();
            });
        }

        // Sync hidden save inputs with initial slider values
        syncSaveInputs();

        // Trigger initial recalculation to sync charts with current slider values
        recalculate();
    }

    // Run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
