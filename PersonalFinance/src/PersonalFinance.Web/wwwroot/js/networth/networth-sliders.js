/* global PFI18n */
/**
 * Net Worth slider and input synchronization module.
 * Handles slider-input pairing, formatting, and the savings rate indicator.
 * Depends on: networth-utils.js (window.NW)
 */
(function () {
    'use strict';

    const NW = window.NW;

    /**
     * Format a number for display in a slider field input.
     * @param {number} value - The numeric value.
     * @param {string} type - 'currency', 'percent', or 'number'.
     * @returns {string} Formatted string.
     */
    function formatSliderValue (value, type) {
        const num = NW.PF.safeParseFloat(value, NaN);
        if (isNaN(num)) { return ''; }
        if (type === 'currency') {
            return PFI18n.formatNumber(Math.round(num));
        }
        if (type === 'percent') {
            return PFI18n.formatNumber(num, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
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
        fields.forEach((field) => {
            const rangeEl = document.getElementById(field.rangeId);
            const saveEl = document.getElementById(field.saveId);
            if (rangeEl && saveEl) {
                // The form posts in the page culture (7,5 in vi-VN); a range's own value is always invariant.
                saveEl.value = PFI18n.formatNumber(Number(rangeEl.value), { useGrouping: false, maximumFractionDigits: 10 });
            }
        });
        const currencySelect = document.getElementById('nwCurrency');
        const currencySave = document.getElementById('nwSaveCurrencyCode');
        if (currencySelect && currencySave) {
            currencySave.value = currencySelect.value;
        }
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
        rangeEl.addEventListener('input', () => {
            inputEl.value = formatSliderValue(rangeEl.value, formatType);
            updateSavingsIndicator();
            syncSaveInputs();
            NW.triggerRecalculation();
        });

        // Input → Range (on blur and Enter key)
        inputEl.addEventListener('input', () => {
            const num = NW.parseTyped(inputEl.value);
            if (!isNaN(num)) {
                const min = NW.PF.safeParseFloat(rangeEl.min, 0);
                const max = NW.PF.safeParseFloat(rangeEl.max, Infinity);
                rangeEl.value = Math.max(min, Math.min(max, num));
            }
            updateSavingsIndicator();
            syncSaveInputs();
            NW.triggerRecalculation();
        });

        inputEl.addEventListener('blur', () => {
            const num = NW.parseTyped(inputEl.value);
            if (!isNaN(num)) {
                const min = NW.PF.safeParseFloat(rangeEl.min, 0);
                const max = NW.PF.safeParseFloat(rangeEl.max, Infinity);
                const clamped = Math.max(min, Math.min(max, num));
                rangeEl.value = clamped;
                inputEl.value = formatSliderValue(clamped, formatType);
            }
            syncSaveInputs();
            NW.triggerRecalculation();
        });

        inputEl.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                inputEl.blur();
            }
        });
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

        const savings = NW.parseTyped(savingsEl.value) || 0;
        const income = NW.parseTyped(incomeEl.value) || 0;

        if (income <= 0) {
            rateValueEl.textContent = '0%';
            rateFillEl.style.width = '0%';
            rateFillEl.className = 'nw-savings-indicator__fill';
            return;
        }

        const rate = (savings / income) * 100;
        const displayRate = Math.min(rate, 100);
        rateValueEl.textContent = `${PFI18n.formatNumber(rate, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) }%`;
        rateFillEl.style.width = `${displayRate }%`;

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

    // Expose slider functions
    window.NW = Object.assign(window.NW || {}, {
        formatSliderValue: formatSliderValue,
        syncSaveInputs: syncSaveInputs,
        updateSavingsIndicator: updateSavingsIndicator,
        initLiveRecalculation: initLiveRecalculation
    });

})();
