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
        recalcTimeout = setTimeout(function () {
            recalculate();
        }, NW.DEBOUNCE_DELAY);
    }

    /**
     * Recalculate projections via AJAX and update all UI elements.
     */
    function recalculate () {
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
        const urlParams = new URLSearchParams(params);
        const url = '/Networth/Calculate?' + urlParams.toString();

        fetch(url)
            .then(function (response) {
                if (!response.ok) {
                    if (response.status === 400) {
                        return response.text().then(function (msg) {
                            NW.showValidationMessage(msg);
                            throw new Error('Validation error');
                        });
                    }
                    throw new Error('HTTP ' + response.status + ': ' + response.statusText);
                }
                return response.json();
            })
            .then(function (data) {
                NW.clearValidationMessage();
                window.nwProjectionData = data;
                NW.renderGrowthChart(data);
                NW.renderComparisonChart(data);
                NW.updateMetrics(data);
                NW.updateScenarioCards(data);
                NW.updateScenarioBadges(data);
                // Re-render the active scenario table
                const activeBtn = document.querySelector('.nw-tab-btn.active');
                if (activeBtn) {
                    NW.updateTable(activeBtn.getAttribute('data-scenario'));
                }
            })
            .catch(function (err) {
                if (err.message !== 'Validation error') {
                    console.error('Failed to recalculate projection:', err);
                    NW.showValidationMessage('Failed to load projection data. Please check your connection and try again.');
                }
            });
    }

    // Expose API functions
    window.NW = Object.assign(window.NW || {}, {
        triggerRecalculation: triggerRecalculation,
        recalculate: recalculate
    });

})();
