/**
 * Dashboard core module.
 * Reads the single JSON data island rendered by Views/Dashboard/Index.cshtml and runs each
 * registered section initialiser once the DOM is ready. Each chart or section lives in its own
 * file and registers itself with PFDashboard.registerSection.
 */
(function () {
    'use strict';

    const DATA_ELEMENT_ID = 'dashboard-data';
    const pending = [];
    let data = null;
    let started = false;

    /**
     * Parse the dashboard data island. Returns an empty object when it is missing or malformed,
     * so sections fall back to their server-rendered empty states.
     * @returns {Object} The camelCase dashboard summary.
     */
    function readData () {
        const element = document.getElementById(DATA_ELEMENT_ID);
        if (!element) {
            return {};
        }
        try {
            return JSON.parse(element.textContent || '{}') || {};
        } catch (error) {
            // eslint-disable-next-line no-console
            console.warn('Dashboard data could not be parsed:', error);
            return {};
        }
    }

    /**
     * Run one section initialiser, isolating failures so one section cannot break the others.
     * @param {{name: string, init: function(Object): void}} section - The section to initialise.
     * @returns {void}
     */
    function runSection (section) {
        try {
            section.init(data);
        } catch (error) {
            // eslint-disable-next-line no-console
            console.warn(`Dashboard section "${section.name}" failed to initialise:`, error);
        }
    }

    /**
     * Read the data island and initialise every section registered so far.
     * @returns {void}
     */
    function start () {
        data = readData();
        started = true;
        pending.splice(0).forEach(runSection);
        if (window.lucide && typeof window.lucide.createIcons === 'function') {
            window.lucide.createIcons();
        }
    }

    window.PFDashboard = {
        /**
         * Register a section initialiser. It receives the parsed dashboard data once the DOM is ready,
         * or immediately when registered after start-up.
         * @param {string} name - Section name used in diagnostics.
         * @param {function(Object): void} init - Initialiser receiving the dashboard data.
         * @returns {void}
         */
        registerSection: function (name, init) {
            const section = { name: name, init: init };
            if (started) {
                runSection(section);
            } else {
                pending.push(section);
            }
        },

        /**
         * Get the parsed dashboard data (empty object before start-up).
         * @returns {Object} The dashboard data.
         */
        getData: function () {
            return data || {};
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
