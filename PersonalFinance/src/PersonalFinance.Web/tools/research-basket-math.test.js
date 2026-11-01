'use strict';
/* eslint-env node */

/**
 * Tests for the Fund Basket's figures (wwwroot/js/research/research-basket-math.js), which the risk chart's basket
 * diamond and the Basket page's overview share: the monthly rebalanced, weight-scaled basket return, the average fee
 * of the funds that filed one, and the US share. The module is pure, so it loads into a bare context.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(
    path.join(__dirname, '..', 'wwwroot', 'js', 'research', 'research-basket-math.js'), 'utf8');

/**
 * Load the module the way the page does, as a browser script that sets window.ResearchBasketMath.
 * @returns {object} The ResearchBasketMath namespace.
 */
function load () {
    const window = {};
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);
    return window.ResearchBasketMath;
}

const math = load();

// Three months of two funds, and a 2% Treasury bill throughout. The basket 50/50 returns 2%, 1% and 0%.
const RETURNS = {
    funds: [
        { ticker: 'AAA', firstMonth: '2026-04-01', lastMonth: '2026-06-01', returns: [1, 2, -1] },
        { ticker: 'BBB', firstMonth: '2026-04-01', lastMonth: '2026-06-01', returns: [3, 0, 1] }
    ],
    windows: []
};
const MACRO = {
    months: [
        { month: '2026-04-01', treasuryBill: 2 },
        { month: '2026-05-01', treasuryBill: 2 },
        { month: '2026-06-01', treasuryBill: 2 }
    ]
};
const FACTS = { funds: [{ ticker: 'AAA', expenseRatio: 0.03 }, { ticker: 'BBB', expenseRatio: null }, { ticker: 'CCC', expenseRatio: 0.2 }] };
const EXPOSURE = {
    funds: [
        { ticker: 'AAA', countries: [{ code: 'US', percent: 100 }] },
        { ticker: 'BBB', countries: [{ code: 'JP', percent: 60 }, { code: 'US', percent: 40 }] }
    ]
};

/**
 * Assert two numbers are equal to 1e-9.
 * @param {number} actual - The computed value.
 * @param {number} expected - The hand-computed value.
 * @returns {void}
 */
function near (actual, expected) {
    assert.ok(Math.abs(actual - expected) < 1e-9, `${actual} is not ${expected}`);
}

test('one fund at 100% gets that fund\'s own figures', () => {
    const figures = math.pointForMonths(RETURNS, MACRO, [{ ticker: 'AAA', weight: 100 }], 3);
    // Growth 1.01 × 1.02 × 0.99, annualized over 3 months; sample deviation of 1, 2, −1 times √12.
    near(figures.annualizedReturn, 8.199925207759984);
    near(figures.volatility, 5.2915026221291805);
    near(figures.sharpe, 1.1716757319238131);
    assert.strictEqual(figures.start, '2026-04');
    assert.strictEqual(figures.end, '2026-06');
});

test('a 50/50 basket rebalances monthly over 3 months', () => {
    const figures = math.pointForMonths(RETURNS, MACRO, [{ ticker: 'AAA', share: 50 }, { ticker: 'BBB', share: 50 }], 3);
    // Monthly returns 2, 1, 0: growth 1.0302 over 3 months, deviation 1 times √12, (return − 2) / volatility.
    near(figures.annualizedReturn, 12.638324624896157);
    near(figures.volatility, 3.4641016151377544);
    near(figures.sharpe, 3.0710197929552105);
    near(figures.treasuryBill, 2);
});

test('weights that do not add up to 100 are scaled', () => {
    const scaled = math.pointForMonths(RETURNS, MACRO, [{ ticker: 'AAA', weight: 30 }, { ticker: 'BBB', weight: 30 }], 3);
    near(scaled.annualizedReturn, 12.638324624896157);
    const summary = math.summary(RETURNS, MACRO, FACTS, EXPOSURE, [{ ticker: 'AAA', weight: 1 }, { ticker: 'BBB', weight: 3 }], 1);
    // A quarter AAA (100% US) and three quarters BBB (40% US).
    near(summary.usShare, 55);
});

test('a fund without a fee is left out of the average fee and listed', () => {
    const summary = math.summary(RETURNS, MACRO, FACTS, EXPOSURE,
        [{ ticker: 'AAA', weight: 25 }, { ticker: 'BBB', weight: 50 }, { ticker: 'CCC', weight: 25 }], 1);
    // AAA and CCC filed one, at equal weight between them.
    near(summary.expenseRatio, 0.115);
    assert.deepStrictEqual(summary.missingFees, ['BBB']);
});

test('the summary has no return figures without the returns or a full window', () => {
    const noReturns = math.summary(null, MACRO, FACTS, EXPOSURE, [{ ticker: 'AAA', weight: 100 }], 3);
    const tooShort = math.summary(RETURNS, MACRO, FACTS, EXPOSURE, [{ ticker: 'AAA', weight: 100 }], 1);
    assert.strictEqual(noReturns.annualReturn, null);
    assert.strictEqual(tooShort.volatility, null);
    near(tooShort.expenseRatio, 0.03);
});

test('without the macro data the Sharpe ratio is missing', () => {
    const figures = math.pointForMonths(RETURNS, null, [{ ticker: 'AAA', weight: 100 }], 3);
    assert.strictEqual(figures.sharpe, null);
    assert.strictEqual(figures.treasuryBill, null);
});
