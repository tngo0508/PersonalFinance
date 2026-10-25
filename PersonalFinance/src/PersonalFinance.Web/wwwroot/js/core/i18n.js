/**
 * PROTOTYPE (#340, throwaway). Client-side string lookup shared by every page module.
 * The language comes from <html lang>. Strings come from one of two sources, whichever the page rendered:
 * - inline: <script type="application/json" id="i18n-strings"> (only this page's keys, already resolved);
 * - bundle: <meta name="i18n-bundle" content="/i18n/vi.<hash>.json"> fetched once and cached by the browser.
 * Modules call PFI18n.ready.then(...) and then PFI18n.t / PFI18n.plural, so both sources look the same to them.
 */
(function (global) {
    'use strict';

    const lang = document.documentElement.lang || 'en';
    const pluralRules = new Intl.PluralRules(lang);
    let strings = {};
    let source = 'none';

    /**
     * Read the inline island or fetch the bundle.
     * @returns {Promise<void>} Resolves when strings are available (never rejects; missing strings show their key).
     */
    async function load () {
        const inline = document.getElementById('i18n-strings');
        if (inline) {
            try {
                strings = JSON.parse(inline.textContent || '{}');
                source = 'inline';
            } catch (error) {
                // eslint-disable-next-line no-console
                console.warn('i18n strings could not be parsed:', error);
            }
            return;
        }
        const meta = document.querySelector('meta[name="i18n-bundle"]');
        if (!meta) {
            return;
        }
        try {
            const response = await fetch(meta.content, { credentials: 'same-origin' });
            strings = response.ok ? await response.json() : {};
            source = `bundle ${meta.content}`;
        } catch (error) {
            // eslint-disable-next-line no-console
            console.warn('i18n bundle could not be loaded:', error);
        }
    }

    /**
     * Replace {name} placeholders by name.
     * @param {string} template - The translated template.
     * @param {Object<string, *>} [args] - Placeholder values.
     * @returns {string} The formatted string.
     */
    function format (template, args) {
        return template.replace(/\{([A-Za-z][A-Za-z0-9]*)\}/g, (match, name) => {
            return args && Object.prototype.hasOwnProperty.call(args, name) ? String(args[name]) : match;
        });
    }

    /**
     * Look up a key and format its named placeholders. A missing key returns the key itself.
     * @param {string} key - Dotted key, e.g. "SavingsGoals.Js.ConfirmDelete".
     * @param {Object<string, *>} [args] - Placeholder values.
     * @returns {string} The localized string.
     */
    function t (key, args) {
        if (!Object.prototype.hasOwnProperty.call(strings, key)) {
            // eslint-disable-next-line no-console
            console.warn('i18n missing key:', key);
            return key;
        }
        return format(strings[key], args);
    }

    /**
     * Pick key.one / key.other (any CLDR category) for a count via Intl.PluralRules; {count} is always set.
     * @param {string} key - Key without the category suffix.
     * @param {number} count - The number deciding the form.
     * @param {Object<string, *>} [args] - Other placeholder values.
     * @returns {string} The localized string.
     */
    function plural (key, count, args) {
        const category = pluralRules.select(count);
        const full = Object.prototype.hasOwnProperty.call(strings, `${key }.${ category}`) ? `${key }.${ category}` : `${key }.other`;
        return t(full, Object.assign({}, args, { count: count.toLocaleString() }));
    }

    global.PFI18n = {
        lang: lang,
        ready: load(),
        t: t,
        plural: plural,
        /** @returns {string} Where the strings came from (for the prototype panel). */
        source: function () { return source; },
        /** @returns {number} How many keys this page received. */
        size: function () { return Object.keys(strings).length; }
    };
})(window);
