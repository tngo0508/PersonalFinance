'use strict';
/* eslint-env node */

/**
 * Tests for how the Net Worth page reads its slider fields (wwwroot/js/networth/networth-utils.js): the fields show
 * numbers in the page culture (180.000 and 7,5 in vi-VN), and the projection must read them back as 180000 and 7.5.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const JS = path.join(__dirname, '..', 'wwwroot', 'js');
const SOURCES = ['core/i18n.js', 'core/utils.js', 'networth/networth-utils.js']
    .map((file) => fs.readFileSync(path.join(JS, file), 'utf8'));

/**
 * Load the Net Worth utilities into a page in the given culture whose fields hold the given text.
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
    return window.NW;
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
