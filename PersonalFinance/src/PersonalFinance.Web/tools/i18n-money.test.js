'use strict';
/* eslint-env node */

/**
 * Tests for the money and number half of wwwroot/js/core/i18n.js (localization spec §2.2, §5): the same outputs as the
 * C# MoneyFormatter and the strict parsing of the culture-aware model binder, in en-US and vi-VN.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'core', 'i18n.js'), 'utf8');
const NBSP = ' ';

/**
 * Load i18n.js into a fresh page whose <html> carries the given language and formatting culture.
 * @param {string} lang - The <html lang> value.
 * @param {string} [culture] - The <html data-culture> value; omitted to test the fallback.
 * @returns {object} The page's PFI18n.
 */
function load (lang, culture) {
    const attributes = culture ? { 'data-culture': culture } : {};
    const window = {};
    window.window = window;
    window.document = {
        readyState: 'complete',
        documentElement: { lang: lang, getAttribute: (name) => attributes[name] || null },
        getElementById: () => null,
        querySelectorAll: () => [],
        addEventListener: () => {}
    };
    window.navigator = { language: 'fr-FR' };
    vm.createContext(window);
    vm.runInContext(SOURCE, window);
    return window.PFI18n;
}

/**
 * Copy a result out of the page realm, so deepStrictEqual compares values, not prototypes.
 * @param {object} value - A plain object from the page.
 * @returns {object} The same data in this realm.
 */
function plain (value) {
    return JSON.parse(JSON.stringify(value));
}

const en = load('en', 'en-US');
const vi = load('vi', 'vi-VN');

test('formatMoney matches the C# formatter: symbol from the currency, separators from the culture', () => {
    assert.strictEqual(en.formatMoney(1234.56, 'USD'), '$1,234.56');
    assert.strictEqual(vi.formatMoney(1234567, 'VND'), `1.234.567${NBSP}₫`);
    assert.strictEqual(vi.formatMoney(1234.56, 'USD'), `1.234,56${NBSP}$`);
    assert.strictEqual(en.formatMoney(1234567, 'VND'), '₫1,234,567');
});

test('formatMoney: negatives lead with a minus, zero has no sign', () => {
    assert.strictEqual(en.formatMoney(-1234.56, 'USD'), '-$1,234.56');
    assert.strictEqual(vi.formatMoney(-1234567, 'VND'), `-1.234.567${NBSP}₫`);
    assert.strictEqual(en.formatMoney(0, 'USD'), '$0.00');
    assert.strictEqual(en.formatMoney(-0.004, 'USD'), '$0.00');
    assert.strictEqual(vi.formatMoney(-0.4, 'VND'), `0${NBSP}₫`);
});

test('formatMoney rounds half away from zero to the minor units, like the C# decimal path', () => {
    assert.strictEqual(en.formatMoney(2.345, 'USD'), '$2.35');
    assert.strictEqual(en.formatMoney(-2.345, 'USD'), '-$2.35');
    assert.strictEqual(en.formatMoney(1.005, 'USD'), '$1.01');
    assert.strictEqual(vi.formatMoney(1234.5, 'VND'), `1.235${NBSP}₫`);
    assert.strictEqual(vi.formatMoney(-1234.5, 'VND'), `-1.235${NBSP}₫`);
});

test('formatMoney whole drops the cents', () => {
    assert.strictEqual(en.formatMoney(1234.5, 'USD', { whole: true }), '$1,235');
    assert.strictEqual(vi.formatMoney(1234.5, 'USD', { whole: true }), `1.235${NBSP}$`);
});

test('formatMoney refuses an unsupported currency', () => {
    assert.throws(() => en.formatMoney(1, 'EUR'));
});

test('formatAmount is the number without the symbol, as a money input shows it', () => {
    assert.strictEqual(en.formatAmount(1234.5, 'USD'), '1,234.50');
    assert.strictEqual(vi.formatAmount(1234.5, 'USD'), '1.234,50');
    assert.strictEqual(vi.formatAmount(1234567.5, 'VND'), '1.234.568');
});

test('formatInputAmount shows the minor units only when the amount has any, as a field shows what was typed', () => {
    const en = load('en', 'en-US');
    assert.strictEqual(en.formatInputAmount(278000, 'USD'), '278,000');
    assert.strictEqual(en.formatInputAmount(278123.45, 'USD'), '278,123.45');
    assert.strictEqual(en.formatInputAmount(278123.5, 'USD'), '278,123.50');
    assert.strictEqual(en.formatInputAmount(2.345, 'USD'), '2.35');
    assert.strictEqual(en.formatInputAmount(278123.45, 'VND'), '278,123');

    const vi = load('vi', 'vi-VN');
    assert.strictEqual(vi.formatInputAmount(278123.45, 'USD'), '278.123,45');
});

test('the culture comes from <html data-culture>, never from navigator.language', () => {
    const fallback = load('en');
    assert.strictEqual(fallback.culture, 'en-US');
    assert.strictEqual(fallback.formatMoney(1234.5, 'USD'), '$1,234.50');
    assert.strictEqual(vi.culture, 'vi-VN');
});

test('formatNumber and formatDate follow the culture', () => {
    assert.strictEqual(en.formatNumber(1234567), '1,234,567');
    assert.strictEqual(vi.formatNumber(1234567), '1.234.567');
    assert.strictEqual(vi.formatNumber(12.5, { minimumFractionDigits: 1 }), '12,5');
    const date = new Date(Date.UTC(2026, 9, 25));
    assert.strictEqual(en.formatDate(date, { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' }),
        'Oct 25, 2026');
    assert.match(vi.formatDate(date, { year: 'numeric', month: 'numeric', day: 'numeric', timeZone: 'UTC' }),
        /^25\/10\/2026$/);
});

test('parseNumber reads the culture strictly, like the server binder', () => {
    const cases = [
        [en, '0', 0], [en, '1234.56', 1234.56], [en, '1,234.56', 1234.56], [en, '-1,234.56', -1234.56],
        [en, '1,234.', 1234], [en, ' 1,234,567 ', 1234567], [en, '.5', 0.5],
        [vi, '0', 0], [vi, '1234,56', 1234.56], [vi, '1.234,56', 1234.56], [vi, '-1.234,56', -1234.56],
        [vi, '1.234.567', 1234567], [vi, '1.234,', 1234], [vi, ',5', 0.5]
    ];
    for (const [i18n, text, expected] of cases) {
        assert.strictEqual(i18n.parseNumber(text), expected, `${i18n.culture} "${text}"`);
    }
});

test('parseNumber rejects a number written the other way, instead of misreading it 100 times larger', () => {
    const rejected = [
        [vi, '1234.56'], [vi, '1.5'], [vi, '1,234.56'], [vi, '12.34'],
        [en, '1234,56'], [en, '1,5'], [en, '1.234,56'],
        [en, ''], [en, 'abc'], [en, '1e3'], [en, '$1'], [en, '(1)'], [en, '1,,234'], [en, '.'], [en, '1-']
    ];
    for (const [i18n, text] of rejected) {
        assert.ok(Number.isNaN(i18n.parseNumber(text)), `${i18n.culture} "${text}"`);
    }
});

test('as-you-type: typing regroups the integer part and keeps the caret after the same digit', () => {
    // "1.234" with "5" typed at the end.
    assert.deepStrictEqual(plain(vi.reformatMoneyInput('1.2345', 6, 'insertText', 'USD')), { value: '12.345', caret: 6 });
    // "12.345" with "6" typed after "1".
    assert.deepStrictEqual(plain(vi.reformatMoneyInput('162.345', 2, 'insertText', 'USD')), { value: '162.345', caret: 2 });
    assert.deepStrictEqual(plain(en.reformatMoneyInput('12345', 5, 'insertText', 'USD')), { value: '12,345', caret: 6 });
    // A deletion that leaves a short group.
    assert.deepStrictEqual(plain(en.reformatMoneyInput('1,34,567', 3, 'deleteContentBackward', 'USD')),
        { value: '134,567', caret: 2 });
});

test('as-you-type: the decimal separator, a fraction and a leading minus survive', () => {
    assert.deepStrictEqual(plain(vi.reformatMoneyInput('1234,', 5, 'insertText', 'USD')), { value: '1.234,', caret: 6 });
    assert.deepStrictEqual(plain(vi.reformatMoneyInput('1.234,5', 7, 'insertText', 'USD')), { value: '1.234,5', caret: 7 });
    assert.deepStrictEqual(plain(en.reformatMoneyInput('-1234', 5, 'insertText', 'USD')), { value: '-1,234', caret: 6 });
    assert.deepStrictEqual(plain(en.reformatMoneyInput('0', 1, 'insertText', 'USD')), { value: '0', caret: 1 });
});

test('as-you-type: a typed group separator is dropped, and the fraction stops at the minor units', () => {
    // vi-VN user types "." meaning a decimal point: dropped, so "1.5" visibly becomes "15", never 1.5 read as 15 silently.
    assert.deepStrictEqual(plain(vi.reformatMoneyInput('1.5', 3, 'insertText', 'USD')), { value: '15', caret: 2 });
    assert.deepStrictEqual(plain(en.reformatMoneyInput('1.234', 5, 'insertText', 'USD')), { value: '1.23', caret: 4 });
    assert.deepStrictEqual(plain(vi.reformatMoneyInput('1234,5', 6, 'insertText', 'VND')), { value: '1.234', caret: 5 });
});

test('as-you-type: pasted text the culture cannot read is left exactly as pasted', () => {
    // A vi-VN user pastes an English number: leave it for the server to reject rather than regroup it into 123.456.
    assert.deepStrictEqual(plain(vi.reformatMoneyInput('1234.56', 7, 'insertFromPaste', 'USD')), { value: '1234.56', caret: 7 });
    assert.deepStrictEqual(plain(vi.reformatMoneyInput('1,234.56', 8, 'insertFromPaste', 'USD')), { value: '1,234.56', caret: 8 });
    assert.deepStrictEqual(plain(vi.reformatMoneyInput('1234,56', 7, 'insertFromPaste', 'USD')), { value: '1.234,56', caret: 8 });
});

test('as-you-type: typing other characters leaves the value as typed', () => {
    assert.deepStrictEqual(plain(en.reformatMoneyInput('12a', 3, 'insertText', 'USD')), { value: '12a', caret: 3 });
});
