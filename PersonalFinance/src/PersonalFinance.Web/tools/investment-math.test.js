'use strict';
/* eslint-env node */

/**
 * Tests for the Investment Calculator's projection math (wwwroot/js/investment-calculator/investment-math.js): future
 * value with monthly compounding, the yearly contribution increase, the fund fee's drag, the year-by-year rows and the
 * year growth overtakes what was put in. The module has no DOM, so it loads into a bare context.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(
    path.join(__dirname, '..', 'wwwroot', 'js', 'investment-calculator', 'investment-math.js'), 'utf8');

/**
 * Load the math module the way the page does, as a browser script that sets window.InvestmentMath.
 * @returns {object} The InvestmentMath namespace.
 */
function load () {
    const window = {};
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);
    return window.InvestmentMath;
}

const math = load();

/**
 * A plan with the page's defaults, changed by the given fields.
 * @param {object} [changes] - Fields to override.
 * @returns {object} The inputs.
 */
function plan (changes) {
    return Object.assign({ initial: 10000, monthly: 500, rate: 7, years: 20, increase: 0, fee: 0 }, changes);
}

/**
 * Assert two amounts agree to the cent.
 * @param {number} actual - The computed amount.
 * @param {number} expected - The known amount.
 */
function assertCents (actual, expected) {
    assert.ok(Math.abs(actual - expected) < 0.005, `${actual} is not ${expected}`);
}

test('10,000 plus 500 a month at 7% for 20 years grows to 300,850.72', () => {
    // Closed-form future value with monthly compounding, deposits at each month end:
    // 10000·(1+0.07/12)^240 + 500·((1+0.07/12)^240 − 1)/(0.07/12).
    const result = math.project(plan());

    assertCents(result.final, 300850.72);
    assert.strictEqual(result.contributed, 130000);
    assertCents(result.growth, 170850.72);
});

test('at a 0% assumed return the balance is exactly what was put in', () => {
    const result = math.project(plan({ rate: 0 }));

    assert.strictEqual(result.final, 130000);
    assert.strictEqual(result.growth, 0);
    assert.strictEqual(result.milestoneYear, null);
});

test('the yearly contribution increase raises each later year\'s deposits', () => {
    // 100 a month in year 1, 110 in year 2, 121 in year 3.
    const result = math.project(plan({ initial: 0, monthly: 100, rate: 0, years: 3, increase: 10 }));

    assert.deepStrictEqual(Array.from(result.rows.slice(1), (row) => Math.round(row.contributedYear * 100) / 100), [1200, 1320, 1452]);
    assertCents(result.final, 3972);
});

test('a 1% fee on a 7% assumed return grows 10,000 to 18,193.97 instead of 20,096.61', () => {
    // 10000·(1+0.06/12)^120 with the fee; 10000·(1+0.07/12)^120 without.
    const result = math.project(plan({ monthly: 0, years: 10, fee: 1 }));

    assertCents(result.final, 18193.97);
    assertCents(result.noFee, 20096.61);
    assertCents(result.feeCost, 1902.65);
    assertCents(result.rows[10].noFee, 20096.61);
});

test('the year-by-year rows add up to the totals', () => {
    const result = math.project(plan({ increase: 3, fee: 0.5 }));
    const sum = (field) => result.rows.reduce((total, row) => total + row[field], 0);

    assert.strictEqual(result.rows.length, 21);
    assert.deepStrictEqual(Object.assign({}, result.rows[0]),
        { year: 0, contributedYear: 10000, growthYear: 0, contributed: 10000, balance: 10000, noFee: 10000 });
    assertCents(sum('contributedYear'), result.contributed);
    assertCents(sum('growthYear'), result.growth);
    assertCents(sum('contributedYear') + sum('growthYear'), result.final);
});

test('growth overtakes what was put in the year 10,000 at 7.2% doubles: year 10', () => {
    // (1+0.006)^108 ≈ 1.91, (1+0.006)^120 ≈ 2.05.
    const result = math.project(plan({ monthly: 0, rate: 7.2, years: 30 }));

    assert.strictEqual(result.milestoneYear, 10);
});

test('no milestone when growth never overtakes the deposits within the plan', () => {
    assert.strictEqual(math.project(plan({ years: 5 })).milestoneYear, null);
    assert.strictEqual(math.project(plan({ initial: 0, monthly: 0 })).milestoneYear, null);
});

test('out-of-range and missing inputs are clamped before projecting', () => {
    const inputs = math.normalize({ initial: -5, monthly: 'abc', rate: 40, years: 80.4, increase: -1, fee: 9 });

    assert.deepStrictEqual(Object.assign({}, inputs), { initial: 0, monthly: 0, rate: 15, years: 50, increase: 0, fee: 3 });
    assert.strictEqual(math.normalize({ years: 0 }).years, 1);
});

test('the rate presets are the Money Priorities rates', () => {
    assert.deepStrictEqual(Array.from(math.PRESETS, (preset) => [preset.key, preset.rate]),
        [['conservative', 4], ['base', 7], ['aggressive', 10]]);
});
