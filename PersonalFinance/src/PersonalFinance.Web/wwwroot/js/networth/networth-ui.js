/**
 * Net Worth UI update module.
 * Handles updating metric cards, scenario cards, badges, and the projection table.
 * Depends on: networth-utils.js (window.NW)
 */
(function () {
    'use strict';

    const NW = window.NW;
    const PF = window.PFUtils;

    /**
     * Safely set text content on an element if it exists.
     * @param {string} selector - CSS selector
     * @param {string} text - Text to set
     */
    function setText (selector, text) {
        const el = document.querySelector(selector);
        if (el) { el.textContent = text; }
    }

    /**
     * Safely set innerHTML on an element if it exists.
     * @param {string} selector - CSS selector
     * @param {string} html - HTML to set
     */
    function setHtml (selector, html) {
        const el = document.querySelector(selector);
        if (el) { el.innerHTML = html; }
    }

    /**
     * Safely set innerHTML on an element by ID if it exists.
     * @param {string} id - Element ID
     * @param {string} html - HTML to set
     */
    function setHtmlById (id, html) {
        const el = document.getElementById(id);
        if (el) { el.innerHTML = html; }
    }

    /**
     * Update summary metric cards and legend.
     * @param {Object} data - The projection data object.
     */
    function updateMetrics (data) {
        // FI Target card
        const fiValue = document.querySelector('.nw-metric-fi-value');
        if (fiValue) {
            const symbolEl = fiValue.querySelector('.nw-currency-symbol');
            const amountEl = fiValue.querySelector('.nw-currency-amount');
            if (symbolEl) { symbolEl.textContent = '$'; }
            if (amountEl) { amountEl.textContent = Math.round(data.fiTarget).toLocaleString('en-US'); }
        }
        setHtml('.nw-metric-fi-sub', 'Based on <span class="nw-currency-symbol">$</span>' + PF.escapeHtml(NW.formatCompact(data.annualSpending)) + '/yr spending');

        // Current Net Worth card
        setText('.nw-metric-current-value', NW.formatCurrency(data.base.startingBalance));
        setText('.nw-metric-current-sub', 'Age ' + data.currentAge + ', Year ' + data.currentYear);

        // Base Final card
        setText('.nw-metric-base-value', NW.formatCurrency(data.base.finalNetWorth));
        setText('.nw-metric-base-sub', data.base.roiPercent + '% ROI');

        // Aggressive Final card
        setText('.nw-metric-aggressive-value', NW.formatCurrency(data.aggressive.finalNetWorth));
        setHtml('.nw-metric-aggressive-sub', PF.escapeHtml(String(data.aggressive.roiPercent)) + '% ROI · <span class="nw-currency-symbol">$</span>' + PF.escapeHtml(NW.formatCompact(data.aggressive.annualSavings)) + '/yr savings');

        // Legend
        setHtmlById('nwLegendBase', '<span class="nw-legend-dot nw-dot-base"></span> Base (' + PF.escapeHtml(String(data.base.roiPercent)) + '%)');
        setHtmlById('nwLegendConservative', '<span class="nw-legend-dot nw-dot-conservative"></span> Conservative (' + PF.escapeHtml(String(data.conservative.roiPercent)) + '%)');
        setHtmlById('nwLegendAggressive', '<span class="nw-legend-dot nw-dot-aggressive"></span> Aggressive (' + PF.escapeHtml(String(data.aggressive.roiPercent)) + '%)');
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
        const savingsEl = document.querySelector('.' + prefix + 'savings');
        const finalEl = document.querySelector('.' + prefix + 'final');
        const contribEl = document.querySelector('.' + prefix + 'contrib');
        const interestEl = document.querySelector('.' + prefix + 'interest');

        if (savingsEl) { savingsEl.innerHTML = '<span class="nw-currency-symbol">$</span>' + PF.escapeHtml(NW.formatCompact(scenario.annualSavings)); }
        if (finalEl) { finalEl.innerHTML = '<span class="nw-currency-symbol">$</span>' + PF.escapeHtml(NW.formatCompact(scenario.finalNetWorth)); }
        if (contribEl) { contribEl.innerHTML = '<span class="nw-currency-symbol">$</span>' + PF.escapeHtml(NW.formatCompact(scenario.totalContributions)); }
        if (interestEl) { interestEl.innerHTML = '<span class="nw-currency-symbol">$</span>' + PF.escapeHtml(NW.formatCompact(scenario.totalInterestEarned)); }
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
                badge.textContent = roiValues[index] + '% ROI';
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

        const scenarioKey = NW.scenarioMap[scenarioName] || 'base';
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
                '<td class="text-end nw-currency">' + esc(NW.formatCurrency(year.startingBalance)) + '</td>' +
                '<td class="text-end nw-currency">' + esc(NW.formatCurrency(year.savings)) + '</td>' +
                '<td class="text-end nw-currency text-success' + roiClass + '">' + esc(NW.formatCurrency(year.roi)) + '</td>' +
                '<td class="text-end nw-currency fw-semibold">' + esc(NW.formatCurrency(year.endingBalance)) + '</td>' +
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
     * Initialize Bootstrap tooltips for parameter hints.
     * Creates tooltip instances for all elements with data-bs-toggle="tooltip".
     */
    function initTooltips () {
        const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
        tooltipTriggerList.forEach(function (el) {
            new bootstrap.Tooltip(el);
        });
    }

    // Expose UI functions
    window.NW = Object.assign(window.NW || {}, {
        updateMetrics: updateMetrics,
        updateScenarioCards: updateScenarioCards,
        updateScenarioBadges: updateScenarioBadges,
        updateTable: updateTable,
        initScenarioTabs: initScenarioTabs,
        initTooltips: initTooltips
    });

})();
