/* global PFI18n */
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

    // Scenario name mapping for safe property access
    const scenarioMap = {
        Base: 'base',
        Conservative: 'conservative',
        Aggressive: 'aggressive'
    };

    // Validation rules for input fields. A money field's max is null: it is read from the field's data-max, which the
    // view writes in the page's currency and the currency selector rescales (#444, NetWorthLimits in Shared).
    const VALIDATORS = [
        { id: 'nwStartingBalance', min: 0, max: null },
        { id: 'nwAnnualSavings', min: 0, max: null },
        { id: 'nwAnnualIncome', min: 0, max: null },
        { id: 'nwAnnualSpending', min: 0, max: null },
        { id: 'nwAggressiveSavings', min: 0, max: null },
        { id: 'nwCurrentAge', min: 18, max: 100 },
        { id: 'nwRetirementAge', min: 18, max: 120 },
        { id: 'nwBaseRoi', min: 0, max: 50 },
        { id: 'nwConservativeRoi', min: 0, max: 50 },
        { id: 'nwAggressiveRoi', min: 0, max: 50 },
        { id: 'nwInflation', min: 0, max: 20 }
    ];

    // The results' currency: the projection's (the Reporting Currency, or the inputs' own when no rate exists).
    let resultCurrency = 'USD';

    /**
     * Remember which currency the server's projection is in, so every result formats in it.
     * @param {string} currencyCode - ISO 4217 code from the projection.
     */
    function setResultCurrency (currencyCode) {
        if (currencyCode) {
            resultCurrency = currencyCode;
        }
    }

    /**
     * The currency the inputs are typed in: the page's selector.
     * @returns {string} ISO 4217 code.
     */
    function inputCurrency () {
        const select = document.getElementById('nwCurrency');
        return (select && select.value) || 'USD';
    }

    /**
     * Format a numeric value as a whole-unit amount in the results' currency.
     * @param {number} value - The numeric value to format.
     * @returns {string} Formatted currency string (e.g., "$1,234,567").
     */
    function formatCurrency (value) {
        return PFI18n.formatMoney(value, resultCurrency, { whole: true });
    }

    /**
     * Format a numeric value as a compact amount in the results' currency.
     * @param {number} value - The numeric value to format.
     * @returns {string} Compact currency string (e.g., "$1.2M", "$50K").
     */
    function formatCompact (value) {
        return PFI18n.formatCompactMoney(value, resultCurrency);
    }

    /**
     * Format a return rate's number in the page culture, without the % sign (7.5; 7,5 in vi-VN).
     * @param {number} ratePercent - The rate in percent.
     * @returns {string} The formatted number.
     */
    function formatRate (ratePercent) {
        return PFI18n.formatNumber(Number(ratePercent) || 0, { maximumFractionDigits: 2 });
    }

    /**
     * The translated name of a scenario the ApiService names Base, Conservative or Aggressive.
     * @param {string} name - The scenario's name.
     * @returns {string} The name in the page language.
     */
    function scenarioName (name) {
        return Object.prototype.hasOwnProperty.call(scenarioMap, name) ? PFI18n.t(`NetWorth.Js.Scenario.${name}`) : name;
    }

    /**
     * Read what was typed in a slider field, in the page culture: the currency symbol and group separators are
     * dropped, the culture's decimal separator kept (7,5 in vi-VN).
     * @param {string} text - The field's text.
     * @returns {number} The number, or NaN.
     */
    function parseTyped (text) {
        const decimal = PFI18n.formatNumber(1.5).replace(/\d/g, '');
        const kept = String(text).split('').filter((char) => /[\d-]/.test(char) || char === decimal).join('');
        return PFI18n.parseNumber(kept);
    }

    /**
     * Get the numeric value of a slider field, read in the page culture (180.000 is 180000 in vi-VN).
     * @param {string} inputId - The text input ID.
     * @returns {number} The parsed numeric value, 0 when the field is missing or unreadable.
     */
    function getSliderValue (inputId) {
        const el = document.getElementById(inputId);
        if (!el) { return 0; }
        const value = parseTyped(el.value);
        return Number.isNaN(value) ? 0 : value;
    }

    /**
     * The upper bound of a money field in the currency chosen on the page: its data-max. Infinity when the field
     * carries none; the server checks the same bound.
     * @param {string} id - The text input ID.
     * @returns {number} The bound in whole units.
     */
    function boundOf (id) {
        const el = document.getElementById(id);
        const max = el && el.dataset ? Number(el.dataset.max) : NaN;
        return Number.isFinite(max) && max > 0 ? max : Infinity;
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
            const max = v.max === null ? boundOf(v.id) : v.max;
            const outOfRange = num < v.min || num > max;
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
        const container = document.querySelector('.nw-hero');
        if (!container) { return; }

        const alert = document.createElement('div');
        alert.id = 'nwValidationAlert';
        alert.className = 'alert alert-danger alert-dismissible fade show nw-alert';
        alert.role = 'alert';
        alert.textContent = message;

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'btn-close';
        closeButton.setAttribute('data-bs-dismiss', 'alert');
        closeButton.setAttribute('aria-label', PFI18n.t('NetWorth.Js.Close'));
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
        scenarioMap: scenarioMap,
        VALIDATORS: VALIDATORS,
        formatCurrency: formatCurrency,
        formatCompact: formatCompact,
        setResultCurrency: setResultCurrency,
        inputCurrency: inputCurrency,
        formatRate: formatRate,
        scenarioName: scenarioName,
        parseTyped: parseTyped,
        getSliderValue: getSliderValue,
        boundOf: boundOf,
        setInvalid: setInvalid,
        validateInputs: validateInputs,
        showValidationMessage: showValidationMessage,
        clearValidationMessage: clearValidationMessage
    });

})();
