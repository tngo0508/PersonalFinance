/* global PFI18n */
/**
 * Net Worth presets and randomization module.
 * Handles preset scenario buttons, randomization, and reset to defaults.
 * Depends on: networth-utils.js, networth-sliders.js (window.NW)
 */
(function () {
    'use strict';

    const NW = window.NW;

    /**
     * Generate a random integer between min and max (inclusive).
     * @param {number} min - Minimum value.
     * @param {number} max - Maximum value.
     * @returns {number} Random integer.
     */
    function randomInt (min, max) {
        return Math.floor(Math.random() * (max - min + 1)) + min;
    }

    /**
     * Generate a random float between min and max, rounded to the given decimal places.
     * @param {number} min - Minimum value.
     * @param {number} max - Maximum value.
     * @param {number} decimals - Number of decimal places.
     * @returns {number} Random float.
     */
    function randomFloat (min, max, decimals) {
        const value = Math.random() * (max - min) + min;
        const factor = Math.pow(10, decimals);
        return Math.round(value * factor) / factor;
    }

    /**
     * Apply a preset scenario by setting all slider values.
     * @param {Object} preset - Key-value pairs of field IDs to values.
     */
    function applyPreset (preset) {
        // Map of field IDs to their format types
        const fields = [
            { id: 'nwStartingBalance', type: 'currency' },
            { id: 'nwAnnualSavings', type: 'currency' },
            { id: 'nwAnnualIncome', type: 'currency' },
            { id: 'nwAnnualSpending', type: 'currency' },
            { id: 'nwCurrentAge', type: 'number' },
            { id: 'nwRetirementAge', type: 'number' },
            { id: 'nwBaseRoi', type: 'percent' },
            { id: 'nwConservativeRoi', type: 'percent' },
            { id: 'nwAggressiveRoi', type: 'percent' },
            { id: 'nwAggressiveSavings', type: 'currency' }
        ];

        fields.forEach((field) => {
            if (preset[field.id] !== undefined) {
                const rangeId = `${field.id }Range`;
                const rangeEl = document.getElementById(rangeId);
                const inputEl = document.getElementById(field.id);
                if (rangeEl) {
                    rangeEl.value = preset[field.id];
                }
                if (inputEl) {
                    inputEl.value = NW.formatSliderValue(preset[field.id], field.type);
                }
            }
        });

        // Clear validation
        NW.clearValidationMessage();
        NW.VALIDATORS.forEach((v) => {
            NW.setInvalid(v.id, false);
        });

        NW.updateSavingsIndicator();
        NW.syncSaveInputs();
        NW.recalculate();
    }

    /**
     * Initialize preset scenario buttons.
     * Wires up every [data-preset] button (FIRE, Coast, Traditional, Ultra, Conservative).
     */
    function initPresets () {
        const presets = {
            fire: {
                nwStartingBalance: 50000,
                nwAnnualSavings: 85000,
                nwAnnualIncome: 150000,
                nwAnnualSpending: 35000,
                nwCurrentAge: 28,
                nwRetirementAge: 45,
                nwBaseRoi: 8.0,
                nwConservativeRoi: 5.0,
                nwAggressiveRoi: 12.0,
                nwAggressiveSavings: 120000
            },
            coast: {
                nwStartingBalance: 350000,
                nwAnnualSavings: 20000,
                nwAnnualIncome: 120000,
                nwAnnualSpending: 55000,
                nwCurrentAge: 35,
                nwRetirementAge: 65,
                nwBaseRoi: 7.0,
                nwConservativeRoi: 5.0,
                nwAggressiveRoi: 10.0,
                nwAggressiveSavings: 35000
            },
            traditional: {
                nwStartingBalance: 100000,
                nwAnnualSavings: 15000,
                nwAnnualIncome: 95000,
                nwAnnualSpending: 60000,
                nwCurrentAge: 30,
                nwRetirementAge: 65,
                nwBaseRoi: 7.0,
                nwConservativeRoi: 5.0,
                nwAggressiveRoi: 9.0,
                nwAggressiveSavings: 25000
            },
            ultra: {
                nwStartingBalance: 200000,
                nwAnnualSavings: 150000,
                nwAnnualIncome: 350000,
                nwAnnualSpending: 80000,
                nwCurrentAge: 25,
                nwRetirementAge: 40,
                nwBaseRoi: 10.0,
                nwConservativeRoi: 6.0,
                nwAggressiveRoi: 15.0,
                nwAggressiveSavings: 200000
            },
            conservative: {
                nwStartingBalance: 150000,
                nwAnnualSavings: 12000,
                nwAnnualIncome: 80000,
                nwAnnualSpending: 45000,
                nwCurrentAge: 40,
                nwRetirementAge: 67,
                nwBaseRoi: 5.0,
                nwConservativeRoi: 3.0,
                nwAggressiveRoi: 7.0,
                nwAggressiveSavings: 18000
            }
        };

        // The inline preset cards (no saved parameters) or the More menu's preset items (saved parameters).
        document.querySelectorAll('[data-preset]').forEach((btn) => {
            const preset = presets[btn.dataset.preset];
            if (preset) {
                btn.addEventListener('click', () => {
                    applyPreset(preset);
                });
            }
        });
    }

    /**
     * Randomize all input fields with realistic values and recalculate.
     * Generates random but realistic financial parameters for exploration.
     */
    function randomizeParameters () {
        // Financial Overview - generate realistic but varied scenarios
        const startingBalance = randomInt(10000, 500000);
        const annualIncome = randomInt(40000, 250000);
        const savingsRate = randomFloat(0.10, 0.50, 2); // 10% to 50% savings rate
        const annualSavings = Math.round(annualIncome * savingsRate / 1000) * 1000;
        const spendingRate = randomFloat(0.35, 0.70, 2); // 35% to 70% of income after taxes
        const annualSpending = Math.round(annualIncome * spendingRate / 1000) * 1000;

        // Timeline - realistic ages
        const currentAge = randomInt(22, 55);
        let retirementAge = currentAge + randomInt(10, 35);
        if (retirementAge > 80) { retirementAge = 80; }

        // Return Assumptions - realistic ROI ranges
        const conservativeRoi = randomFloat(3.0, 7.0, 1);
        const baseRoi = conservativeRoi + randomFloat(0.5, 3.0, 1);
        const aggressiveRoi = baseRoi + randomFloat(1.0, 5.0, 1);
        const aggressiveSavings = annualSavings + randomInt(5000, 50000);

        applyPreset({
            nwStartingBalance: startingBalance,
            nwAnnualSavings: annualSavings,
            nwAnnualIncome: annualIncome,
            nwAnnualSpending: annualSpending,
            nwCurrentAge: currentAge,
            nwRetirementAge: retirementAge,
            nwBaseRoi: baseRoi,
            nwConservativeRoi: conservativeRoi,
            nwAggressiveRoi: aggressiveRoi,
            nwAggressiveSavings: aggressiveSavings
        });
    }

    /**
     * Reset all input fields to the cached saved values (from page load).
     * If no cached values exist, falls back to hardcoded defaults with a message.
     * This is a frontend-only operation — it does not affect saved database records.
     */
    function resetToSaved () {
        const cached = window.NW.getSavedValuesCache && window.NW.getSavedValuesCache();
        if (cached && Object.keys(cached).length > 0) {
            applyPreset(cached);
            return;
        }
        // Fallback to the server's NetWorthDefaults (rendered into the page) if no cached values
        const defaults = window.nwDefaultParameters;
        if (!defaults) {
            return;
        }
        if (window.NW.showResetMessage) {
            window.NW.showResetMessage(PFI18n.t('NetWorth.Js.UsingDefaults'));
        }
        applyPreset({
            nwStartingBalance: defaults.startingBalance,
            nwAnnualSavings: defaults.annualSavings,
            nwAnnualIncome: defaults.annualIncome,
            nwAnnualSpending: defaults.annualSpending,
            nwCurrentAge: defaults.currentAge,
            nwRetirementAge: defaults.retirementAge,
            nwBaseRoi: defaults.baseRoi,
            nwConservativeRoi: defaults.conservativeRoi,
            nwAggressiveRoi: defaults.aggressiveRoi,
            nwAggressiveSavings: defaults.aggressiveSavings
        });
    }

    // Expose preset functions
    window.NW = Object.assign(window.NW || {}, {
        applyPreset: applyPreset,
        initPresets: initPresets,
        randomizeParameters: randomizeParameters,
        resetToSaved: resetToSaved
    });

})();
