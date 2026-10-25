/**
 * Net Worth shared utilities, constants, and validation.
 * Exposes NW namespace on window for other networth modules.
 */
(function () {
    'use strict';

    // Reference to shared utilities
    const PF = window.PFUtils;

    // Debounce delay in milliseconds for live recalculation
    const DEBOUNCE_DELAY = 500;

    // Canvas height for growth line chart gradient
    const GROWTH_CHART_HEIGHT = 350;

    // Canvas height for comparison bar chart gradient
    const COMPARISON_CHART_HEIGHT = 280;

    // Color palette — Ledger theme (css/networth.css --nw-* scenario tokens)
    const colors = {
        base: '#1D6A50',
        conservative: '#5B6B78',
        aggressive: '#2F6690',
        fi: '#8A6A1E',
        baseAlpha: 'rgba(29, 106, 80, 0.12)',
        conservativeAlpha: 'rgba(91, 107, 120, 0.08)',
        aggressiveAlpha: 'rgba(47, 102, 144, 0.12)',
        gridColor: 'rgba(20, 40, 59, 0.06)',
        gridColorStrong: 'rgba(20, 40, 59, 0.12)',
        tickColor: '#5B6B78',
        tooltipBg: 'rgba(255, 255, 255, 0.96)',
        tooltipBorder: 'rgba(0, 0, 0, 0.1)'
    };

    // Scenario name mapping for safe property access
    const scenarioMap = {
        Base: 'base',
        Conservative: 'conservative',
        Aggressive: 'aggressive'
    };

    // Validation rules for input fields
    const VALIDATORS = [
        { id: 'nwStartingBalance', min: 0, max: 1000000000 },
        { id: 'nwAnnualSavings', min: 0, max: 10000000 },
        { id: 'nwAnnualIncome', min: 0, max: 100000000 },
        { id: 'nwAnnualSpending', min: 0, max: 10000000 },
        { id: 'nwAggressiveSavings', min: 0, max: 10000000 },
        { id: 'nwCurrentAge', min: 18, max: 100 },
        { id: 'nwRetirementAge', min: 18, max: 120 },
        { id: 'nwBaseRoi', min: 0, max: 50 },
        { id: 'nwConservativeRoi', min: 0, max: 50 },
        { id: 'nwAggressiveRoi', min: 0, max: 50 }
    ];

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

    /**
     * Convert hex color to rgba string.
     * @param {string} hex - Hex color (e.g. '#818cf8')
     * @param {number} alpha - Alpha value 0-1
     * @returns {string} RGBA string
     */
    function hexToRgba (hex, alpha) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return `rgba(${ r }, ${ g }, ${ b }, ${ alpha })`;
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
     * Toggle the invalid class on an input and its paired range slider.
     * @param {string} id - The input element ID (range ID is derived by appending 'Range').
     * @param {boolean} invalid - Whether to add (true) or remove (false) the invalid class.
     */
    function setInvalid (id, invalid) {
        const el = document.getElementById(id);
        const rangeEl = document.getElementById(`${id }Range`);
        if (el) { el.classList.toggle('nw-input-invalid', invalid); }
        if (rangeEl) { rangeEl.classList.toggle('nw-input-invalid', invalid); }
    }

    /**
     * Validate input values client-side before sending to the server.
     * Checks ranges and ensures retirement age is greater than current age.
     * @returns {boolean} True if all inputs are valid, false otherwise.
     */
    function validateInputs () {
        let isValid = true;

        // Clear and validate in a single pass
        VALIDATORS.forEach((v) => {
            const num = getSliderValue(v.id);
            const outOfRange = num < v.min || num > v.max;
            setInvalid(v.id, outOfRange);
            if (outOfRange) { isValid = false; }
        });

        // Retirement age must be greater than current age
        const currentAge = Math.round(getSliderValue('nwCurrentAge'));
        const retirementAge = Math.round(getSliderValue('nwRetirementAge'));
        const agesInvalid = currentAge && retirementAge && retirementAge <= currentAge;
        if (agesInvalid) {
            setInvalid('nwRetirementAge', true);
            isValid = false;
        }

        return isValid;
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
        alert.textContent = message;

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'btn-close';
        closeButton.setAttribute('data-bs-dismiss', 'alert');
        closeButton.setAttribute('aria-label', 'Close');
        alert.appendChild(closeButton);
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

    // Expose the NW namespace
    window.NW = Object.assign(window.NW || {}, {
        PF: PF,
        DEBOUNCE_DELAY: DEBOUNCE_DELAY,
        GROWTH_CHART_HEIGHT: GROWTH_CHART_HEIGHT,
        COMPARISON_CHART_HEIGHT: COMPARISON_CHART_HEIGHT,
        colors: colors,
        scenarioMap: scenarioMap,
        VALIDATORS: VALIDATORS,
        formatCurrency: formatCurrency,
        formatCompact: formatCompact,
        hexToRgba: hexToRgba,
        getSliderValue: getSliderValue,
        setInvalid: setInvalid,
        validateInputs: validateInputs,
        showValidationMessage: showValidationMessage,
        clearValidationMessage: clearValidationMessage
    });

})();
