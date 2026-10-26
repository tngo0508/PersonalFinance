/* global PFI18n */
/**
 * Net Worth API module.
 * Handles AJAX recalculation via fetch and debouncing.
 * Depends on: networth-utils.js (window.NW)
 */
(function () {
    'use strict';

    const NW = window.NW;

    // Debounce timer reference
    let recalcTimeout;

    /**
     * Trigger a debounced recalculation of projections.
     */
    function triggerRecalculation () {
        clearTimeout(recalcTimeout);
        recalcTimeout = setTimeout(() => {
            recalculate();
        }, NW.DEBOUNCE_DELAY);
    }

    /**
     * Recalculate projections via AJAX and update all UI elements.
     * @returns {Promise<void>} Resolves once the page has been updated (or the error shown).
     */
    async function recalculate () {
        // Validate inputs before sending
        if (!NW.validateInputs()) {
            return;
        }

        const params = {
            startingBalance: NW.getSliderValue('nwStartingBalance'),
            annualSavings: NW.getSliderValue('nwAnnualSavings'),
            annualIncome: NW.getSliderValue('nwAnnualIncome'),
            annualSpending: NW.getSliderValue('nwAnnualSpending'),
            currentAge: Math.round(NW.getSliderValue('nwCurrentAge')) || 33,
            retirementAge: Math.round(NW.getSliderValue('nwRetirementAge')) || 65,
            baseRoi: NW.getSliderValue('nwBaseRoi') || 7,
            conservativeRoi: NW.getSliderValue('nwConservativeRoi') || 6,
            aggressiveRoi: NW.getSliderValue('nwAggressiveRoi') || 10,
            aggressiveSavings: NW.getSliderValue('nwAggressiveSavings') || 45000
        };

        // Build query string using URLSearchParams for safety
        const url = `/Networth/Calculate?${new URLSearchParams(params).toString()}`;

        try {
            const response = await fetch(url);
            if (!response.ok) {
                if (response.status === 400) {
                    NW.showValidationMessage(await response.text());
                    return;
                }
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            const data = await response.json();

            NW.clearValidationMessage();
            window.nwProjectionData = data;
            NW.renderGrowthChart(data);
            NW.renderComparisonChart(data);
            NW.updateMetrics(data);
            NW.updateBreakdown(data);
            NW.updateScenarioCards(data);
            NW.updateScenarioBadges(data);
            // Re-render the active scenario table
            const activeBtn = document.querySelector('.nw-tab-btn.active');
            if (activeBtn) {
                NW.updateTable(activeBtn.getAttribute('data-scenario'));
            }
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error('Failed to recalculate projection:', err);
            NW.showValidationMessage(PFI18n.t('NetWorth.Js.LoadFailed'));
        }
    }

    // Expose API functions
    window.NW = Object.assign(window.NW || {}, {
        triggerRecalculation: triggerRecalculation,
        recalculate: recalculate
    });

})();
