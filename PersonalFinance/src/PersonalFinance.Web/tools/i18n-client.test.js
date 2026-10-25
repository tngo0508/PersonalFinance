'use strict';
/* eslint-env node */

/**
 * Tests for the browser module wwwroot/js/core/i18n.js, run in a vm with a minimal document.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'core', 'i18n.js'), 'utf8');

/**
 * Load i18n.js against a page holding the given JSON islands.
 * @param {Array<Object<string, string>>} islands The islands' contents, in document order.
 * @param {string} [lang] The page's html lang.
 * @returns {object} The module's PFI18n global.
 */
function load (islands, lang) {
    const elements = islands.map((strings) => ({ textContent: JSON.stringify(strings) }));
    const window = {};
    const context = {
        window,
        Intl,
        console: { warn () {} },
        document: {
            documentElement: { lang: lang || 'en' },
            querySelectorAll (selector) {
                assert.strictEqual(selector, 'script[type="application/json"][data-i18n-strings]');
                return elements;
            }
        }
    };
    vm.runInNewContext(SOURCE, context);
    return window.PFI18n;
}

test('reads keys from every island on the page', () => {
    const i18n = load([
        { 'Shell.Js.Search.Unavailable': 'Tìm kiếm hiện không khả dụng.' },
        { 'SavingsGoals.Js.ConfirmDelete': 'Xóa "{name}"?' }
    ], 'vi');

    assert.strictEqual(i18n.t('Shell.Js.Search.Unavailable'), 'Tìm kiếm hiện không khả dụng.');
    assert.strictEqual(i18n.t('SavingsGoals.Js.ConfirmDelete', { name: 'Quỹ' }), 'Xóa "Quỹ"?');
});

test('a missing key returns the key', () => {
    assert.strictEqual(load([]).t('Shell.Js.Nope'), 'Shell.Js.Nope');
});

test('plural picks the English category and formats the count', () => {
    const i18n = load([{ 'Shell.Js.Results.one': '{count} result', 'Shell.Js.Results.other': '{count} results' }]);

    assert.strictEqual(i18n.plural('Shell.Js.Results', 1), '1 result');
    assert.strictEqual(i18n.plural('Shell.Js.Results', 1200), '1,200 results');
});
