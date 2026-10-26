/* global PFI18n */
/**
 * Net Worth UI update module.
 * Handles updating metric cards, scenario cards, badges, and the projection table.
 * Depends on: networth-utils.js (window.NW)
 */
(function () {
    'use strict';

    const NW = window.NW;

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
     * Create an element with an optional class and text.
     * @param {string} tag - The tag name.
     * @param {string} [className] - The class attribute.
     * @param {string} [text] - Text content.
     * @returns {HTMLElement} The new element.
     */
    function el (tag, className, text) {
        const node = document.createElement(tag);
        if (className) { node.className = className; }
        if (text !== undefined) { node.textContent = text; }
        return node;
    }

    /**
     * Update summary metric cards and legend.
     * @param {Object} data - The projection data object.
     */
    function updateMetrics (data) {

        // FI Target card
        setText('.nw-metric-fi-value', NW.formatCurrency(data.fiTarget));
        setText('.nw-metric-fi-sub', PFI18n.t('NetWorth.Js.Metric.FiSub', { amount: NW.formatCompact(data.annualSpending) }));

        // Current Net Worth card
        setText('.nw-metric-current-value', NW.formatCurrency(data.base.startingBalance));
        setText('.nw-metric-current-sub', PFI18n.t('NetWorth.Js.Metric.CurrentSub', { age: data.currentAge, year: data.currentYear }));

        // Base Final card
        setText('.nw-metric-base-value', NW.formatCurrency(data.base.finalNetWorth));
        setText('.nw-metric-base-sub', PFI18n.t('NetWorth.Js.Metric.BaseSub', {
            rate: NW.formatRate(data.base.roiPercent),
            age: data.retirementAge
        }));

        // Aggressive Final card
        setText('.nw-metric-aggressive-value', NW.formatCurrency(data.aggressive.finalNetWorth));
        setText('.nw-metric-aggressive-sub', PFI18n.t('NetWorth.Js.Metric.AggressiveSub', {
            rate: NW.formatRate(data.aggressive.roiPercent),
            amount: NW.formatCompact(data.aggressive.annualSavings)
        }));

        // Legend
        setLegend('nwLegendBase', 'nw-dot-base', 'Base', data.base.roiPercent);
        setLegend('nwLegendConservative', 'nw-dot-conservative', 'Conservative', data.conservative.roiPercent);
        setLegend('nwLegendAggressive', 'nw-dot-aggressive', 'Aggressive', data.aggressive.roiPercent);
    }

    /**
     * Set a growth chart legend item: its colour dot, then the scenario name and rate.
     * @param {string} id - The legend item's ID.
     * @param {string} dotClass - The dot's colour class.
     * @param {string} scenario - Base, Conservative or Aggressive.
     * @param {number} roiPercent - The scenario's return rate.
     */
    function setLegend (id, dotClass, scenario, roiPercent) {
        const label = PFI18n.t('NetWorth.Js.Legend.Scenario', {
            name: NW.scenarioName(scenario),
            rate: NW.formatRate(roiPercent)
        });
        const item = document.getElementById(id);
        if (item) {
            item.replaceChildren(el('span', `nw-legend-dot ${dotClass}`), ` ${label}`);
        }
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
        const savingsEl = document.querySelector(`.${ prefix }savings`);
        const finalEl = document.querySelector(`.${ prefix }final`);
        const contribEl = document.querySelector(`.${ prefix }contrib`);
        const interestEl = document.querySelector(`.${ prefix }interest`);

        if (savingsEl) { savingsEl.textContent = NW.formatCompact(scenario.annualSavings); }
        if (finalEl) { finalEl.textContent = NW.formatCompact(scenario.finalNetWorth); }
        if (contribEl) { contribEl.textContent = NW.formatCompact(scenario.totalContributions); }
        if (interestEl) { interestEl.textContent = NW.formatCompact(scenario.totalInterestEarned); }
    }

    /**
     * Update scenario card ROI badges.
     * @param {Object} data - The projection data object.
     */
    function updateScenarioBadges (data) {
        const badges = document.querySelectorAll('.nw-scenario-badge');
        const roiValues = [data.base.roiPercent, data.conservative.roiPercent, data.aggressive.roiPercent];
        badges.forEach((badge, index) => {
            if (index < roiValues.length) {
                badge.textContent = PFI18n.t('NetWorth.Js.RoiBadge', { rate: NW.formatRate(roiValues[index]) });
            }
        });
    }

    /**
     * Determine CSS classes for a table row based on milestone thresholds.
     * @param {number} endingBalance - The ending balance for the year.
     * @param {number} fiTarget - The financial independence target amount.
     * @param {number} annualIncome - The annual income amount.
     * @returns {string} The row's CSS class, or '' for none.
     */
    function getRowHighlightClass (endingBalance, fiTarget, _annualIncome) {
        const fiveMillion = 5000000;

        if (endingBalance >= fiveMillion) {
            return 'nw-row-5m';
        }
        if (endingBalance >= fiTarget) {
            return 'nw-row-fi';
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

        const rows = scenario.years.map((year) => {
            const row = el('tr', getRowHighlightClass(year.endingBalance, fiTarget, annualIncome));
            const roiClass = year.roi >= annualIncome ? ' nw-roi-highlight' : '';
            row.append(
                el('td', '', String(year.year)),
                el('td', '', String(year.age)),
                el('td', 'text-end nw-currency', NW.formatCurrency(year.startingBalance)),
                el('td', 'text-end nw-currency', NW.formatCurrency(year.savings)),
                el('td', `text-end nw-currency text-success${roiClass}`, NW.formatCurrency(year.roi)),
                el('td', 'text-end nw-currency fw-semibold', NW.formatCurrency(year.endingBalance))
            );
            return row;
        });

        tbody.replaceChildren(...rows);
    }

    /**
     * Initialize scenario tab switching.
     * Switches the projection table between Base, Conservative, and Aggressive scenarios.
     */
    function initScenarioTabs () {
        const buttons = document.querySelectorAll('.nw-tab-btn');
        buttons.forEach((btn) => {
            btn.addEventListener('click', () => {
                // Update active state
                buttons.forEach((b) => { b.classList.remove('active'); });
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
        tooltipTriggerList.forEach((el) => {
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
