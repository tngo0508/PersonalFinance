/* global PFI18n */
/**
 * Net Worth slider and input synchronization module.
 * Handles slider-input pairing, formatting, and the savings rate indicator. The number typed in a field is the number
 * kept, saved and projected; the slider beside it is a coarse control whose own position the browser snaps to its step
 * and range, so nothing reads a value back from a slider while its field holds one.
 * Depends on: networth-utils.js (window.NW)
 */
(function () {
    'use strict';

    const NW = window.NW;

    /**
     * Format a number for display in a slider field input: an amount with the minor units that were typed (278,123.45,
     * or 278,123 in a currency without them), a rate with its decimals (at least one, as the saved rate's four allow),
     * an age whole.
     * @param {number} value - The numeric value.
     * @param {string} type - 'currency', 'percent', or 'number'.
     * @returns {string} Formatted string.
     */
    function formatSliderValue (value, type) {
        const num = NW.PF.safeParseFloat(value, NaN);
        if (isNaN(num)) { return ''; }
        if (type === 'currency') {
            return PFI18n.formatInputAmount(num, NW.inputCurrency());
        }
        if (type === 'percent') {
            return PFI18n.formatNumber(num, { minimumFractionDigits: 1, maximumFractionDigits: 4 });
        }
        return Math.round(num).toString();
    }

    /**
     * Sync the hidden save inputs with the fields, so the form posts what the page shows: the number typed in each
     * field, exactly, or the slider's position only when the field holds no number.
     */
    function syncSaveInputs () {
        const fields = [
            { id: 'nwStartingBalance', saveId: 'nwSaveStartingBalance' },
            { id: 'nwAnnualSavings', saveId: 'nwSaveAnnualSavings' },
            { id: 'nwAnnualIncome', saveId: 'nwSaveAnnualIncome' },
            { id: 'nwAnnualSpending', saveId: 'nwSaveAnnualSpending' },
            { id: 'nwCurrentAge', saveId: 'nwSaveCurrentAge' },
            { id: 'nwRetirementAge', saveId: 'nwSaveRetirementAge' },
            { id: 'nwBaseRoi', saveId: 'nwSaveBaseRoi' },
            { id: 'nwConservativeRoi', saveId: 'nwSaveConservativeRoi' },
            { id: 'nwAggressiveRoi', saveId: 'nwSaveAggressiveRoi' },
            { id: 'nwAggressiveSavings', saveId: 'nwSaveAggressiveSavings' }
        ];
        fields.forEach((field) => {
            const inputEl = document.getElementById(field.id);
            const rangeEl = document.getElementById(`${field.id }Range`);
            const saveEl = document.getElementById(field.saveId);
            if (!rangeEl || !saveEl) { return; }
            const typed = inputEl ? NW.parseTyped(inputEl.value) : NaN;
            const value = Number.isNaN(typed) ? Number(rangeEl.value) : typed;
            // The form posts in the page culture (7,5 in vi-VN).
            saveEl.value = PFI18n.formatNumber(value, { useGrouping: false, maximumFractionDigits: 10 });
        });
        const currencySelect = document.getElementById('nwCurrency');
        const currencySave = document.getElementById('nwSaveCurrencyCode');
        if (currencySelect && currencySave) {
            currencySave.value = currencySelect.value;
        }
    }

    /**
     * Sync a range slider with its paired text input. The field is the value; the slider follows it (pegged at an end
     * when the value lies beyond the slider's range, which is narrower than the field's bound) and moves it when
     * dragged.
     * @param {string} rangeId - The range input ID.
     * @param {string} inputId - The text input ID.
     * @param {string} formatType - 'currency', 'percent', or 'number'.
     */
    function syncSlider (rangeId, inputId, formatType) {
        const rangeEl = document.getElementById(rangeId);
        const inputEl = document.getElementById(inputId);
        if (!rangeEl || !inputEl) { return; }

        /**
         * Put the slider where a number lies on its range; the browser snaps the slider to its step, the number stays.
         * @param {number} num - The field's number.
         */
        function position (num) {
            const min = NW.PF.safeParseFloat(rangeEl.min, 0);
            const max = NW.PF.safeParseFloat(rangeEl.max, Infinity);
            rangeEl.value = Math.max(min, Math.min(max, num));
        }

        // The field starts from its exact value (data-value, written by the view); the slider's own value is only
        // where that lies, already snapped to its step.
        const exact = inputEl.dataset ? NW.PF.safeParseFloat(inputEl.dataset.value, NaN) : NaN;
        const initial = isNaN(exact) ? NW.PF.safeParseFloat(rangeEl.value, NaN) : exact;
        if (!isNaN(initial)) { position(initial); }
        inputEl.value = formatSliderValue(initial, formatType);

        // Range → Input
        rangeEl.addEventListener('input', () => {
            inputEl.value = formatSliderValue(rangeEl.value, formatType);
            updateSavingsIndicator();
            syncSaveInputs();
            NW.triggerRecalculation();
        });

        // Input → Range (as typed, then tidied on blur and Enter)
        inputEl.addEventListener('input', () => {
            const num = NW.parseTyped(inputEl.value);
            if (!isNaN(num)) { position(num); }
            updateSavingsIndicator();
            syncSaveInputs();
            NW.triggerRecalculation();
        });

        inputEl.addEventListener('blur', () => {
            const num = NW.parseTyped(inputEl.value);
            if (!isNaN(num)) {
                position(num);
                inputEl.value = formatSliderValue(num, formatType);
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
     * The story sentence's numbers share one slider row: picking a number shows its slider and hides the others.
     */
    function initTuner () {
        const rows = document.querySelectorAll('[data-nw-tune]');
        const inputs = document.querySelectorAll('[data-nw-story]');

        /**
         * Show the slider of one story number and mark that number as the one being tuned.
         * @param {string} id - The number's input id.
         */
        function tune (id) {
            rows.forEach((row) => { row.hidden = row.getAttribute('data-nw-tune') !== id; });
            inputs.forEach((input) => {
                const field = input.closest('.nw-story__field');
                if (field) { field.classList.toggle('is-tuning', input.id === id); }
            });
        }

        inputs.forEach((input) => {
            input.addEventListener('focus', () => tune(input.id));
        });
        const visible = Array.from(rows).find((row) => !row.hidden);
        if (visible) { tune(visible.getAttribute('data-nw-tune')); }
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
        syncSlider('nwInflationRange', 'nwInflation', 'percent');
        initTuner();

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
