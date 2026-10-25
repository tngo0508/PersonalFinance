/**
 * Client-side strings, shared by every page module (ADR 0015, spec §2.1).
 * The language is <html lang>. The strings are the page's JSON island,
 * <script type="application/json" id="i18n-strings">, rendered by the <js-strings prefix="…"> tag helper with only that
 * page's keys, already resolved through the fallback chain on the server. Nothing is fetched.
 * Modules call PFI18n.t(key, args) and PFI18n.plural(key, count, args); the island is read on first use, so it may
 * appear anywhere on the page before that.
 */
(function (global) {
    'use strict';

    const PLACEHOLDER = /\{([A-Za-z][A-Za-z0-9]*)\}/g;
    const lang = document.documentElement.lang || 'en';
    const pluralRules = new Intl.PluralRules(lang);
    let strings = null;

    /**
     * The page's strings, parsed once from the island.
     * @returns {Object<string, string>} Key to translated template; empty when the page rendered no island.
     */
    function load () {
        if (strings) {
            return strings;
        }
        strings = {};
        const island = document.getElementById('i18n-strings');
        if (island) {
            try {
                strings = JSON.parse(island.textContent || '{}');
            } catch (error) {
                // eslint-disable-next-line no-console
                console.warn('i18n strings could not be parsed:', error);
            }
        }
        return strings;
    }

    /**
     * Replace {name} placeholders by name; a placeholder without a value stays as written.
     * @param {string} template - The translated template.
     * @param {Object<string, *>} [args] - Placeholder values.
     * @returns {string} The formatted string.
     */
    function format (template, args) {
        return template.replace(PLACEHOLDER, (match, name) => {
            return args && Object.prototype.hasOwnProperty.call(args, name) ? String(args[name]) : match;
        });
    }

    /**
     * Look a key up and format its named placeholders. A missing key returns the key itself.
     * @param {string} key - Dotted key, e.g. "SavingsGoals.Js.ConfirmDelete".
     * @param {Object<string, *>} [args] - Placeholder values.
     * @returns {string} The localized string.
     */
    function t (key, args) {
        const all = load();
        if (!Object.prototype.hasOwnProperty.call(all, key)) {
            // eslint-disable-next-line no-console
            console.warn('i18n missing key:', key);
            return key;
        }
        return format(all[key], args);
    }

    /**
     * Pick key.one, key.other or another CLDR category for a count with Intl.PluralRules, falling back to key.other.
     * {count} is always set, formatted in the page culture.
     * @param {string} key - Key without the category suffix.
     * @param {number} count - The number deciding the form.
     * @param {Object<string, *>} [args] - Other placeholder values.
     * @returns {string} The localized string.
     */
    function plural (key, count, args) {
        const specific = `${key}.${pluralRules.select(count)}`;
        const chosen = Object.prototype.hasOwnProperty.call(load(), specific) ? specific : `${key}.other`;
        return t(chosen, Object.assign({}, args, { count: formatNumber(count) }));
    }

    // ---- Numbers, dates and money (spec §2.2) ------------------------------------------------------------------
    // The formatting culture is <html data-culture>, the request culture the server rendered with (en-US, vi-VN).
    // navigator.language is never used: the page must format exactly as the server does.

    const culture = document.documentElement.getAttribute('data-culture') || 'en-US';

    /**
     * Mirror of PersonalFinance.Shared.Constants.SupportedCurrencies: minor units and the symbol shown in every culture.
     * @type {Object<string, {minorUnits: number, symbol: string}>}
     */
    const CURRENCIES = {
        USD: { minorUnits: 2, symbol: '$' },
        VND: { minorUnits: 0, symbol: '₫' }
    };

    /** The currency every amount is in until amounts carry their own (slice C1). */
    const DEFAULT_CURRENCY = 'USD';

    const separators = (function () {
        const parts = new Intl.NumberFormat(culture).formatToParts(12345.6);
        const find = (type, fallback) => (parts.find((part) => part.type === type) || { value: fallback }).value;
        return { group: find('group', ','), decimal: find('decimal', '.') };
    })();

    /**
     * @param {string} code - ISO 4217 code.
     * @returns {{minorUnits: number, symbol: string}} The supported currency.
     */
    function currencyOf (code) {
        const currency = CURRENCIES[code];
        if (!currency) {
            throw new Error(`'${code}' is not a supported currency.`);
        }
        return currency;
    }

    /**
     * Round half away from zero to a number of decimals on the number's shortest decimal form (2.345 → 2.35), as the
     * server does with decimals; a plain Intl rounding would see 2.345 as 2.34499… and give 2.34.
     * @param {number} value - The value.
     * @param {number} decimals - Decimals to keep.
     * @returns {number} The rounded value, never -0.
     */
    function roundHalfAway (value, decimals) {
        const magnitude = Math.round(Number(`${Math.abs(value)}e${decimals}`));
        const rounded = Number(`${magnitude}e-${decimals}`) * Math.sign(value);
        return rounded === 0 ? 0 : rounded;
    }

    /**
     * Format an amount of money like the server's MoneyFormatter: symbol and minor units from the currency, separators
     * and the symbol's position from the culture ($1,234.56; 1.234.567 ₫).
     * @param {number} amount - The amount.
     * @param {string} [currencyCode='USD'] - ISO 4217 code of the amount's currency.
     * @param {{whole?: boolean}} [options] - whole: round to whole units and drop the minor units.
     * @returns {string} The formatted amount.
     */
    function formatMoney (amount, currencyCode, options) {
        const code = currencyCode || DEFAULT_CURRENCY;
        const currency = currencyOf(code);
        const decimals = options && options.whole ? 0 : currency.minorUnits;
        const formatted = new Intl.NumberFormat(culture, {
            style: 'currency',
            currency: code,
            currencyDisplay: 'narrowSymbol',
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals
        }).format(roundHalfAway(Number(amount) || 0, decimals));
        // narrowSymbol is the plain symbol; replace in case a browser's data differs from the server's.
        return formatted.replace(/US\$|₫|\$/, currency.symbol);
    }

    /**
     * A compact amount for chart axes and small tiles ($1.2M, 1,2 Tr $), symbol from the currency.
     * @param {number} amount - The amount.
     * @param {string} [currencyCode='USD'] - ISO 4217 code.
     * @param {{fractionDigits?: number}} [options] - fractionDigits: exact decimals ($18.0M); default up to one.
     * @returns {string} The compact amount.
     */
    function formatCompactMoney (amount, currencyCode, options) {
        const code = currencyCode || DEFAULT_CURRENCY;
        currencyOf(code);
        const exact = options && typeof options.fractionDigits === 'number' ? options.fractionDigits : null;
        return new Intl.NumberFormat(culture, {
            style: 'currency',
            currency: code,
            currencyDisplay: 'narrowSymbol',
            notation: 'compact',
            minimumFractionDigits: exact === null ? 0 : exact,
            maximumFractionDigits: exact === null ? 1 : exact
        }).format(Number(amount) || 0);
    }

    /**
     * The amount without the symbol, grouped, with the currency's minor units (1.234,50): a money input's value.
     * @param {number} amount - The amount.
     * @param {string} [currencyCode='USD'] - ISO 4217 code.
     * @returns {string} The amount as a money input shows it.
     */
    function formatAmount (amount, currencyCode) {
        const decimals = currencyOf(currencyCode || DEFAULT_CURRENCY).minorUnits;
        return new Intl.NumberFormat(culture, { minimumFractionDigits: decimals, maximumFractionDigits: decimals })
            .format(roundHalfAway(Number(amount) || 0, decimals));
    }

    /**
     * Format a plain number (a count, a percentage's number) in the culture.
     * @param {number} value - The number.
     * @param {Intl.NumberFormatOptions} [options] - Intl options, e.g. maximumFractionDigits.
     * @returns {string} The formatted number.
     */
    function formatNumber (value, options) {
        return new Intl.NumberFormat(culture, options).format(value);
    }

    /**
     * Format a date in the culture.
     * @param {Date} date - The date.
     * @param {Intl.DateTimeFormatOptions} [options] - Intl options, e.g. { month: 'short', day: 'numeric' }.
     * @returns {string} The formatted date.
     */
    function formatDate (date, options) {
        return new Intl.DateTimeFormat(culture, options).format(date);
    }

    /**
     * The digits of an integer part: plain digits, or groups of three after a first group of one to three.
     * @param {string} part - The integer part.
     * @returns {string|null} The digits, '' for an empty part, null when malformed.
     */
    function integerDigits (part) {
        if (part === '') {
            return '';
        }
        const groups = part.split(separators.group);
        if (!groups.every((group) => /^\d+$/.test(group))) {
            return null;
        }
        const wellGrouped = groups.length === 1
            || (groups[0].length <= 3 && groups.slice(1).every((group) => group.length === 3));
        return wellGrouped ? groups.join('') : null;
    }

    /**
     * Read a typed number in the culture, strictly, with the server binder's rules: an optional leading minus, group
     * separators only between groups of three, at most one decimal separator (1.234,56 in vi-VN). A number written
     * the other culture's way (1234.56 or 1.5 in vi-VN) is rejected, never misread as 123456 or 15.
     * @param {string} text - The text.
     * @returns {number} The number, or NaN when the text is not one.
     */
    function parseNumber (text) {
        let input = String(text === null || text === undefined ? '' : text).trim();
        const negative = input.startsWith('-');
        if (negative) {
            input = input.slice(1);
        }
        const parts = input.split(separators.decimal);
        if (parts.length > 2) {
            return NaN;
        }
        const digits = integerDigits(parts[0]);
        const fraction = parts.length === 2 ? parts[1] : '';
        if (digits === null || !/^\d*$/.test(fraction) || (digits === '' && fraction === '')) {
            return NaN;
        }
        const value = Number(`${negative ? '-' : ''}${digits || '0'}${fraction ? `.${fraction}` : ''}`);
        return value === 0 ? 0 : value;
    }

    /**
     * Regroup a money input's text after an edit, keeping the caret after the same digit (as-you-type formatting).
     * Typing or deleting: group separators anywhere in the integer part are dropped and the digits regrouped, and the
     * fraction stops at the currency's minor units. Pasting or dropping: the text is regrouped only when parseNumber
     * reads it, else left exactly as pasted for validation to reject. Text with other characters is left as typed.
     * @param {string} value - The input's text after the edit.
     * @param {number} caret - The caret position in value.
     * @param {string} inputType - The InputEvent's inputType (insertText, deleteContentBackward, insertFromPaste, …).
     * @param {string} [currencyCode='USD'] - ISO 4217 code of the amount's currency.
     * @returns {{value: string, caret: number}} The new text and caret.
     */
    function reformatMoneyInput (value, caret, inputType, currencyCode) {
        const unchanged = { value: value, caret: caret };
        const minorUnits = currencyOf(currencyCode || DEFAULT_CURRENCY).minorUnits;
        const typing = /^(insertText|insertCompositionText|delete)/.test(inputType || '');
        if (!typing && Number.isNaN(parseNumber(value))) {
            return unchanged;
        }

        const allowed = (char) => /\d/.test(char) || char === separators.decimal || char === separators.group;
        const body = value.startsWith('-') ? value.slice(1) : value;
        if (![...body].every(allowed)) {
            return unchanged;
        }

        const sign = value.startsWith('-') ? '-' : '';
        const decimalAt = body.indexOf(separators.decimal);
        const integer = (decimalAt < 0 ? body : body.slice(0, decimalAt)).split(separators.group).join('');
        let fraction = decimalAt < 0 ? null : body.slice(decimalAt + 1).split(separators.group).join('');
        if (fraction !== null && !/^\d*$/.test(fraction.split(separators.decimal).join(''))) {
            return unchanged;
        }
        if (fraction !== null) {
            fraction = fraction.split(separators.decimal).join('').slice(0, minorUnits);
        }

        const grouped = integer.replace(/^0+(?=\d)/, '').replace(/\B(?=(\d{3})+(?!\d))/g, separators.group);
        const decimalPart = fraction === null || minorUnits === 0 ? '' : separators.decimal + fraction;
        const formatted = sign + grouped + decimalPart;

        // Keep the caret after the same number of significant characters (sign, digits, decimal separator).
        const significant = (char) => char !== separators.group;
        let wanted = [...value.slice(0, caret)].filter(significant).length;
        let position = 0;
        while (position < formatted.length && wanted > 0) {
            if (significant(formatted[position])) {
                wanted--;
            }
            position++;
        }
        return { value: formatted, caret: position };
    }

    /**
     * Give a text input as-you-type money formatting in the culture, and the currency's minor units on blur.
     * @param {HTMLInputElement} input - The input; data-currency names its currency (USD by default).
     */
    function bindMoneyInput (input) {
        if (!input || input.dataset.moneyInputBound) {
            return;
        }
        input.dataset.moneyInputBound = 'true';
        const currencyCode = () => input.dataset.currency || DEFAULT_CURRENCY;
        input.addEventListener('input', (event) => {
            const result = reformatMoneyInput(input.value, input.selectionStart ?? input.value.length,
                event.inputType || 'insertText', currencyCode());
            if (result.value !== input.value) {
                input.value = result.value;
                if (document.activeElement === input) {
                    input.setSelectionRange(result.caret, result.caret);
                }
            }
        });
        input.addEventListener('blur', () => {
            const value = parseNumber(input.value);
            if (!Number.isNaN(value)) {
                input.value = formatAmount(value, currencyCode());
            }
        });
    }

    /** Bind every input[data-money-input] on the page. */
    function bindMoneyInputs () {
        document.querySelectorAll('input[data-money-input]').forEach(bindMoneyInput);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindMoneyInputs);
    } else {
        bindMoneyInputs();
    }

    global.PFI18n = {
        lang: lang,
        culture: culture,
        t: t,
        plural: plural,
        format: format,
        formatMoney: formatMoney,
        formatCompactMoney: formatCompactMoney,
        formatAmount: formatAmount,
        formatNumber: formatNumber,
        formatDate: formatDate,
        parseNumber: parseNumber,
        reformatMoneyInput: reformatMoneyInput,
        bindMoneyInput: bindMoneyInput
    };
})(window);
