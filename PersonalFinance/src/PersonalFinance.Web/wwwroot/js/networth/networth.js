/* global PFI18n */
/**
 * Net Worth Projection page — main orchestrator.
 * Coordinates all networth modules: utils, charts, sliders, api, presets, ui.
 */
(function () {
    'use strict';

    const NW = window.NW ;

    /**
     * Cache of the initial saved/default parameter values loaded on page render.
     * Used by Reset to restore values without an API call.
     * @type {Object<string, number>|null}
     */
    let cachedSavedValues = null;

    /**
     * Capture the current slider values into the cache.
     * Called once on page load to store the saved/default values.
     */
    function cacheSavedValues () {
        const fields = [
            'nwStartingBalance',
            'nwAnnualSavings',
            'nwAnnualIncome',
            'nwAnnualSpending',
            'nwCurrentAge',
            'nwRetirementAge',
            'nwBaseRoi',
            'nwConservativeRoi',
            'nwAggressiveRoi',
            'nwAggressiveSavings'
        ];
        cachedSavedValues = {};
        fields.forEach((fieldId) => {
            const rangeEl = document.getElementById(`${fieldId }Range`);
            if (rangeEl) {
                cachedSavedValues[fieldId] = parseFloat(rangeEl.value);
            }
        });
    }

    /**
     * Show a brief toast-style message to the user.
     * @param {string} message - The message to display.
     */
    function showResetMessage (message) {
        // Remove existing message if present
        const existing = document.getElementById('nwResetMessage');
        if (existing) {
            existing.remove();
        }

        const msgEl = document.createElement('div');
        msgEl.id = 'nwResetMessage';
        msgEl.className = 'nw-reset-message';
        msgEl.setAttribute('role', 'status');
        msgEl.setAttribute('aria-live', 'polite');
        msgEl.textContent = message;

        // Insert before the actions bar
        const actionsEl = document.querySelector('.nw-form__actions');
        if (actionsEl) {
            actionsEl.parentNode.insertBefore(msgEl, actionsEl);
        }

        // Auto-dismiss after 4 seconds
        setTimeout(() => {
            if (msgEl.parentNode) {
                msgEl.remove();
            }
        }, 4000);
    }

    /**
     * Restore the cached saved values to all sliders and recalculate.
     * Does nothing if no cached values exist.
     */
    function resetToSaved () {
        if (cachedSavedValues) {
            NW.applyPreset(cachedSavedValues);
        }
    }

    /**
     * Initialize everything when DOM is ready.
     * Renders charts, sets up sliders, tabs, presets, and triggers initial recalculation.
     */
    function init () {
        const data = window.nwProjectionData;
        if (!data) { return; }
        NW.setResultCurrency(data.currencyCode);

        // Parameters are already loaded server-side via the Index action.
        // The sliders reflect saved values from the database on page render.

        // Initialize tooltips for parameter hints
        NW.initTooltips();

        // Render charts
        NW.renderGrowthChart(data);
        NW.renderComparisonChart(data);

        // Initialize scenario tabs and render the projection table for the selected scenario
        NW.initScenarioTabs();
        NW.updateTable(window.nwSelectedScenario || 'Base');

        // Initialize live recalculation
        NW.initLiveRecalculation();

        // The currency selector changes which currency the inputs are in; the server converts and recalculates.
        const currencySelect = document.getElementById('nwCurrency');
        if (currencySelect) {
            currencySelect.addEventListener('change', () => {
                NW.syncSaveInputs();
                NW.recalculate();
            });
        }

        // Initialize preset scenario buttons
        NW.initPresets();

        // Initialize randomize button
        const randomBtn = document.getElementById('nwRandomBtn');
        if (randomBtn) {
            randomBtn.addEventListener('click', NW.randomizeParameters);
        }

        // Cache the initial saved/default values before any interaction
        cacheSavedValues();

        // Initialize reset button — restores cached saved values (frontend-only)
        const resetBtn = document.getElementById('nwResetBtn');
        if (resetBtn) {
            resetBtn.addEventListener('click', resetToSaved);
        }

        // Initialize "Reset to saved" dropdown item
        const resetToSavedBtn = document.getElementById('nwResetToSaved');
        if (resetToSavedBtn) {
            resetToSavedBtn.addEventListener('click', resetToSaved);
        }

        // Initialize "Clear saved parameters" dropdown item
        const clearSavedBtn = document.getElementById('nwClearSaved');
        if (clearSavedBtn) {
            clearSavedBtn.addEventListener('click', () => {
                const confirmed = confirm(PFI18n.t('NetWorth.Js.ConfirmClear'));
                if (confirmed) {
                    const clearForm = document.getElementById('nwClearForm');
                    if (clearForm) {
                        clearForm.submit();
                    }
                }
            });
        }

        // Sync hidden save inputs with initial slider values
        NW.syncSaveInputs();

        // Trigger initial recalculation to sync charts with current slider values
        NW.recalculate();
    }

    // Run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Expose cache and message functions for testing
    window.NW = Object.assign(window.NW || {}, {
        getSavedValuesCache: function () { return cachedSavedValues; },
        showResetMessage: showResetMessage
    });

})();
