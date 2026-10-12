/**
 * Net Worth Projection page — main orchestrator.
 * Coordinates all networth modules: utils, charts, sliders, api, presets, ui.
 */
(function () {
    'use strict';

    const NW = window.NW;

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
        NW.initTooltips();

        // Render charts
        NW.renderGrowthChart(data);
        NW.renderComparisonChart(data);

        // Initialize scenario tabs
        NW.initScenarioTabs();

        // Initialize live recalculation
        NW.initLiveRecalculation();

        // Initialize preset scenario buttons
        NW.initPresets();

        // Initialize randomize button
        const randomBtn = document.getElementById('nwRandomBtn');
        if (randomBtn) {
            randomBtn.addEventListener('click', NW.randomizeParameters);
        }

        // Initialize reset button — reset sliders to defaults and clear saved data
        const resetBtn = document.querySelector('#nwResetForm button[type="submit"]');
        if (resetBtn) {
            resetBtn.addEventListener('click', function (e) {
                e.preventDefault();
                NW.resetToDefaults();
                document.getElementById('nwResetForm').submit();
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

})();
