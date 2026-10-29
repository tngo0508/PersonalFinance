'use strict';
/* eslint-env node */

/**
 * Tests for how the Net Worth page reads and writes its slider fields (wwwroot/js/networth/networth-utils.js and
 * networth-sliders.js): the fields show numbers in the page culture (180.000 and 7,5 in vi-VN), the projection must
 * read them back as 180000 and 7.5, and the number typed is the number kept: the slider beside a field only snaps its
 * own position to its step and range, never the amount.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const JS = path.join(__dirname, '..', 'wwwroot', 'js');
const SOURCES = ['core/i18n.js', 'core/utils.js', 'networth/networth-utils.js', 'networth/networth-sliders.js']
    .map((file) => fs.readFileSync(path.join(JS, file), 'utf8'));

/**
 * A stub element: a value, a dataset, and listeners that fire(type) calls.
 * @param {object} [props] - Properties to set on the element.
 * @returns {object} The element.
 */
function element (props) {
    const handlers = {};
    return Object.assign({
        value: '',
        dataset: {},
        min: '',
        max: '',
        step: '',
        classList: { toggle: () => {} },
        addEventListener: (type, handler) => { (handlers[type] = handlers[type] || []).push(handler); },
        fire: (type) => (handlers[type] || []).forEach((handler) => handler({ key: type }))
    }, props);
}

/**
 * Load the Net Worth utilities and sliders into a page in the given culture whose fields hold the given text.
 * @param {string} culture - The <html data-culture> value.
 * @param {Object<string, string|object>} fields - Field id → the text it shows, or a whole stub element.
 * @returns {object} The page's NW namespace.
 */
function load (culture, fields) {
    const window = {};
    window.window = window;
    window.document = {
        readyState: 'complete',
        documentElement: { lang: culture.slice(0, 2), getAttribute: (name) => (name === 'data-culture' ? culture : null) },
        getElementById: (id) => {
            if (!(id in fields)) { return null; }
            return typeof fields[id] === 'object' ? fields[id] : { value: fields[id] };
        },
        querySelectorAll: () => [],
        addEventListener: () => {}
    };
    vm.createContext(window);
    SOURCES.forEach((source) => vm.runInContext(source, window));
    // networth-api.js is not loaded: the sliders' recalculation is a no-op here.
    window.NW.triggerRecalculation = () => {};
    return window.NW;
}

/**
 * The starting balance field, its slider (stepping 1,000 up to 1,000,000) and its hidden save input.
 * @param {string} typed - The field's text.
 * @param {object} [inputProps] - More properties for the field.
 * @returns {Object<string, object>} The fields, keyed by id.
 */
function startingBalance (typed, inputProps) {
    return {
        nwCurrency: { value: 'USD' },
        nwStartingBalance: element(Object.assign({ value: typed }, inputProps)),
        nwStartingBalanceRange: element({ value: '278000', min: '0', max: '1000000', step: '1000' }),
        nwSaveStartingBalance: element({ value: '278000' })
    };
}

test('reads grouped amounts and decimal commas in vi-VN', () => {
    const NW = load('vi-VN', { nwStartingBalance: '180.000', nwBaseRoi: '7,5' });

    assert.strictEqual(NW.getSliderValue('nwStartingBalance'), 180000);
    assert.strictEqual(NW.getSliderValue('nwBaseRoi'), 7.5);
});

test('reads grouped amounts and decimal points in en-US', () => {
    const NW = load('en-US', { nwStartingBalance: '$180,000', nwBaseRoi: '7.5' });

    assert.strictEqual(NW.getSliderValue('nwStartingBalance'), 180000);
    assert.strictEqual(NW.getSliderValue('nwBaseRoi'), 7.5);
});

test('a money field reads its bound from data-max; without one it is unbounded', () => {
    const NW = load('vi-VN', {
        nwStartingBalance: { value: '1', dataset: { max: '25000000000000' } },
        nwAnnualIncome: { value: '1' }
    });

    assert.strictEqual(NW.boundOf('nwStartingBalance'), 25000000000000);
    assert.strictEqual(NW.boundOf('nwAnnualIncome'), Infinity);
    assert.strictEqual(NW.boundOf('nwNope'), Infinity);
});

test('a missing field or unreadable text reads as 0', () => {
    const NW = load('vi-VN', { nwAnnualIncome: 'abc' });

    assert.strictEqual(NW.getSliderValue('nwAnnualIncome'), 0);
    assert.strictEqual(NW.getSliderValue('nwNope'), 0);
});

test('a money field shows the cents that were typed, in the currency chosen on the page', () => {
    const usd = load('en-US', { nwCurrency: { value: 'USD' } });
    assert.strictEqual(usd.formatSliderValue(278123.45, 'currency'), '278,123.45');
    assert.strictEqual(usd.formatSliderValue(278000, 'currency'), '278,000');

    const vnd = load('vi-VN', { nwCurrency: { value: 'VND' } });
    assert.strictEqual(vnd.formatSliderValue(6953086250, 'currency'), '6.953.086.250');
});

test('a rate field shows the decimals that were typed, at least one', () => {
    const NW = load('en-US', {});

    assert.strictEqual(NW.formatSliderValue(7.25, 'percent'), '7.25');
    assert.strictEqual(NW.formatSliderValue(7, 'percent'), '7.0');
});

test('the save form carries the typed amount, not the slider position it was snapped to', () => {
    const en = startingBalance('278,123.45');
    load('en-US', en).syncSaveInputs();
    assert.strictEqual(en.nwSaveStartingBalance.value, '278123.45');

    const vi = startingBalance('278.123,45');
    load('vi-VN', vi).syncSaveInputs();
    assert.strictEqual(vi.nwSaveStartingBalance.value, '278123,45');
});

test('the save form falls back to the slider position when the field is unreadable', () => {
    const fields = startingBalance('abc');

    load('en-US', fields).syncSaveInputs();

    assert.strictEqual(fields.nwSaveStartingBalance.value, '278000');
});

test('a field starts from its exact data-value, not from the slider it was snapped to', () => {
    const fields = startingBalance('', { dataset: { value: '278123.45' } });

    load('en-US', fields).initLiveRecalculation();

    assert.strictEqual(fields.nwStartingBalance.value, '278,123.45');
    assert.strictEqual(Number(fields.nwStartingBalanceRange.value), 278123.45);
});

test('an amount beyond the slider pegs the slider and keeps the amount', () => {
    const fields = startingBalance('', { dataset: { value: '278000' } });
    const NW = load('en-US', fields);
    NW.initLiveRecalculation();

    fields.nwStartingBalance.value = '1,500,000';
    fields.nwStartingBalance.fire('input');
    fields.nwStartingBalance.fire('blur');

    assert.strictEqual(fields.nwStartingBalance.value, '1,500,000');
    assert.strictEqual(Number(fields.nwStartingBalanceRange.value), 1000000);
    assert.strictEqual(fields.nwSaveStartingBalance.value, '1500000');
});
