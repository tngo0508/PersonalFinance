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
     * {count} is always set, formatted for the page language.
     * @param {string} key - Key without the category suffix.
     * @param {number} count - The number deciding the form.
     * @param {Object<string, *>} [args] - Other placeholder values.
     * @returns {string} The localized string.
     */
    function plural (key, count, args) {
        const specific = `${key}.${pluralRules.select(count)}`;
        const chosen = Object.prototype.hasOwnProperty.call(load(), specific) ? specific : `${key}.other`;
        return t(chosen, Object.assign({}, args, { count: count.toLocaleString(lang) }));
    }

    global.PFI18n = {
        lang: lang,
        t: t,
        plural: plural,
        format: format
    };
})(window);
