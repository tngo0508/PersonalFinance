/* global PFI18n */
/**
 * Per-amount currency selectors (ADR 0016, localization spec §2.5). A <select data-currency-selector> names the money
 * inputs it governs in data-currency-targets (a CSS selector) and the symbol spans beside them in
 * data-currency-symbols. Choosing a currency re-formats each input's amount in that currency's minor units and swaps
 * the symbols; the input's data-currency then tells PFI18n's as-you-type formatting which currency the amount is in,
 * and the select's own value travels with the form post. Nothing here converts: conversion is the server's job.
 */
(function () {
    'use strict';

    /**
     * Apply a selector's current currency to its inputs and symbols.
     * @param {HTMLSelectElement} select - The selector.
     * @returns {void}
     */
    function apply (select) {
        const code = select.value;
        if (!code) {
            return;
        }
        if (select.dataset.currencyTargets) {
            document.querySelectorAll(select.dataset.currencyTargets).forEach((input) => {
                const value = PFI18n.parseNumber(input.value);
                input.dataset.currency = code;
                if (input.value !== '' && !Number.isNaN(value)) {
                    input.value = PFI18n.formatAmount(value, code);
                }
            });
        }
        if (select.dataset.currencySymbols) {
            document.querySelectorAll(select.dataset.currencySymbols).forEach((element) => {
                element.textContent = PFI18n.currencySymbol(code);
            });
        }
    }

    /**
     * Bind every selector on the page once; safe to call again after new markup is added.
     * @returns {void}
     */
    function bind () {
        document.querySelectorAll('select[data-currency-selector]').forEach((select) => {
            if (select.dataset.currencySelectorBound) {
                return;
            }
            select.dataset.currencySelectorBound = 'true';
            select.addEventListener('change', () => apply(select));
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bind);
    } else {
        bind();
    }

    /**
     * An ISO date (yyyy-MM-dd) in the page culture, without shifting it across time zones.
     * @param {string} value - The date as the server sends it.
     * @returns {string} The date in the culture, or the input when it is not an ISO date.
     */
    function isoDate (value) {
        const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value || ''));
        if (!match) {
            return String(value || '');
        }
        return PFI18n.formatDate(new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
    }

    /**
     * The caption for a converted total (ADR 0016): which rate converted the amounts and on what day, "rate as of" when
     * that rate is more than three days older than the day it stood in for, or that the conversion is unavailable.
     * Empty when every amount was already in the Reporting Currency. The texts come from the layout's Shell.Js island.
     * @param {Object|null|undefined} conversion - A ConversionDto as the ApiService sends it (camelCase).
     * @returns {string} The caption, or ''.
     */
    function captionText (conversion) {
        if (!conversion || (!conversion.converted && !conversion.unavailable)) {
            return '';
        }
        const parts = [];
        if (conversion.unavailable) {
            parts.push(PFI18n.t('Shell.Js.Conversion.Unavailable'));
        }
        (conversion.rates || []).forEach((rate) => {
            const rateText = PFI18n.t('Shell.Js.Conversion.RateText', {
                base: rate.baseCurrency,
                quote: PFI18n.formatMoney(rate.rate, rate.quoteCurrency)
            });
            const date = isoDate(rate.date);
            parts.push(PFI18n.t(rate.isDated ? 'Shell.Js.Conversion.ConvertedAtDated' : 'Shell.Js.Conversion.ConvertedAt',
                { rate: rateText, date: date }));
            if (rate.isStale) {
                parts.push(PFI18n.t('Shell.Js.Conversion.RateAsOf', { date: date }));
            }
        });
        return parts.join(' ');
    }

    window.PFCurrencySelector = { bind: bind, apply: apply };
    window.PFCurrency = { captionText: captionText };
})();
