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
    return Object.assign({ initial: 10000, monthly: 500, rate: 7, years: 20, increase: 0, fee: 0, inflation: 0 }, changes);
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

test("in today's money, 10,000 kept for 10 years at 2.5% inflation is worth 7,811.98", () => {
    // 10000 / 1.025^10.
    const result = math.project(plan({ monthly: 0, rate: 0, years: 10, inflation: 2.5 }));

    assert.strictEqual(result.final, 10000);
    assertCents(result.real.final, 7811.98);
    assertCents(result.real.rows[10].balance, 7811.98);
    assert.strictEqual(result.real.rows[0].balance, 10000);
});

test("each year's deposits count at that year's prices in today's money", () => {
    // 1200 / 1.025 in year 1, 1200 / 1.025^2 in year 2; the 2,400 balance is 2400 / 1.025^2 today.
    const result = math.project(plan({ initial: 0, monthly: 100, rate: 0, years: 2, inflation: 2.5 }));

    assertCents(result.real.contributed, 2312.91);
    assertCents(result.real.final, 2284.35);
    assertCents(result.real.growth, -28.55);
});

test("the fee's cost in today's money is what the fee took, at the last year's prices", () => {
    const result = math.project(plan({ monthly: 0, years: 10, fee: 1, inflation: 2.5 }));

    assertCents(result.real.feeCost, 1902.65 / Math.pow(1.025, 10));
    assertCents(result.real.noFee, 20096.61 / Math.pow(1.025, 10));
});

test("at 0% inflation today's money is the same as future money", () => {
    const result = math.project(plan({ increase: 2, fee: 0.3 }));

    assert.strictEqual(result.real.final, result.final);
    assert.strictEqual(result.real.contributed, result.contributed);
    assert.deepStrictEqual(Array.from(result.real.rows, (row) => row.balance), Array.from(result.rows, (row) => row.balance));
});

test("the year-by-year rows add up to the totals in today's money too", () => {
    const result = math.project(plan({ increase: 3, fee: 0.5, inflation: 3 }));
    const real = result.real;
    const sum = (field) => real.rows.reduce((total, row) => total + row[field], 0);

    assert.strictEqual(real.rows.length, 21);
    assert.deepStrictEqual(Object.assign({}, real.rows[0]),
        { year: 0, contributedYear: 10000, growthYear: 0, contributed: 10000, balance: 10000, noFee: 10000 });
    assertCents(sum('contributedYear'), real.contributed);
    assertCents(real.rows[20].contributed, real.contributed);
    assertCents(sum('growthYear'), real.growth);
    assertCents(sum('contributedYear') + sum('growthYear'), real.final);
    assert.ok(real.final < result.final);
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
    const inputs = math.normalize({ initial: -5, monthly: 'abc', rate: 40, years: 80.4, increase: -1, fee: 9, inflation: 12 });

    assert.deepStrictEqual(Object.assign({}, inputs),
        { initial: 0, monthly: 0, rate: 15, years: 50, increase: 0, fee: 3, inflation: 10, delay: 0, stopAt: null });
    assert.strictEqual(math.normalize({ inflation: -1 }).inflation, 0);
    assert.strictEqual(math.normalize({ years: 0 }).years, 1);
});

test('the rate presets are the Money Priorities rates', () => {
    assert.deepStrictEqual(Array.from(math.PRESETS, (preset) => [preset.key, preset.rate]),
        [['conservative', 4], ['base', 7], ['aggressive', 10]]);
});

test('the monthly contribution needed: 120,000 in 10 years at 0% is 1,000 a month', () => {
    const answer = math.solveGoal(plan({ initial: 0, rate: 0, years: 10 }), { target: 120000, mode: 'monthly' });

    assert.strictEqual(answer.kind, 'monthly');
    assert.strictEqual(answer.monthly, 1000);
    assert.strictEqual(answer.year, 10);
});

test('the monthly contribution needed is rounded up to a whole amount that reaches the goal', () => {
    // 500 a month grows 10,000 to 300,850.72 in 20 years at 7%; 499 falls 520.93 short.
    const answer = math.solveGoal(plan(), { target: 300850, mode: 'monthly' });

    assert.strictEqual(answer.monthly, 500);
});

test('when the starting amount alone reaches the goal, it is already there, with the year', () => {
    // 10,000 at 7.2% doubles in year 10 (see the milestone test).
    const answer = math.solveGoal(plan({ rate: 7.2, years: 30 }), { target: 20000, mode: 'monthly' });

    assert.strictEqual(answer.kind, 'already');
    assert.strictEqual(answer.year, 10);
});

test('the years needed at the current monthly contribution', () => {
    const inputs = plan({ initial: 0, monthly: 1000, rate: 0, years: 5 });
    const answer = math.solveGoal(inputs, { target: 120000, mode: 'years' });

    assert.strictEqual(answer.kind, 'years');
    assert.strictEqual(answer.year, 10);
    assert.strictEqual(math.solveGoal(inputs, { target: 120001, mode: 'years' }).year, 11);
});

test('a goal not reached within 50 years is unreachable, with the shortfall', () => {
    // 100 a month at 0% puts in 60,000 in 50 years.
    const answer = math.solveGoal(plan({ initial: 0, monthly: 100, rate: 0 }), { target: 100000, mode: 'years' });

    assert.strictEqual(answer.kind, 'unreachable');
    assertCents(answer.shortfall, 40000);
});

test("a goal in today's money is inflated over time", () => {
    // 120,000 of today's money in 10 years at 2.5% inflation is 120,000·1.025^10 = 153,610.14: 1,280.08 a month at 0%.
    const inputs = plan({ initial: 0, rate: 0, years: 10, inflation: 2.5 });

    assert.strictEqual(math.solveGoal(inputs, { target: 120000, mode: 'monthly', todays: true }).monthly, 1281);
    assert.strictEqual(math.solveGoal(inputs, { target: 120000, mode: 'monthly' }).monthly, 1000);
});

test("the shortfall of a goal in today's money is in today's money", () => {
    // 60,000 put in over 50 years is worth 60,000 / 1.025^50 = 17,456.53 today.
    const answer = math.solveGoal(plan({ initial: 0, monthly: 100, rate: 0, inflation: 2.5 }),
        { target: 100000, mode: 'years', todays: true });

    assert.strictEqual(answer.kind, 'unreachable');
    assert.ok(Math.abs(answer.shortfall - 82543.47) < 0.01, `${answer.shortfall}`);
});

test('progress compares the current plan with the goal, and the plan reaches it in the reached year', () => {
    const answer = math.solveGoal(plan({ initial: 0, monthly: 1000, rate: 0, years: 20 }), { target: 120000, mode: 'monthly' });

    assert.strictEqual(answer.progress, 2);
    assert.strictEqual(answer.reachedYear, 10);
    assert.strictEqual(math.solveGoal(plan({ years: 5 }), { target: 1e9, mode: 'monthly' }).reachedYear, null);
});

test('a goal of nothing is already reached today', () => {
    const answer = math.solveGoal(plan(), { target: 0, mode: 'years' });

    assert.strictEqual(answer.kind, 'already');
    assert.strictEqual(answer.year, 0);
});

test('starting 5 years later leaves the first 5 years empty, then grows like a 15-year plan', () => {
    const later = math.project(plan({ delay: 5 }));

    assert.deepStrictEqual(Array.from(later.rows.slice(0, 5), (row) => row.balance), [0, 0, 0, 0, 0]);
    assert.strictEqual(later.rows[5].balance, 10000);
    assert.strictEqual(later.rows[5].contributedYear, 10000);
    // 10000·(1+0.07/12)^180 + 500·((1+0.07/12)^180 − 1)/(0.07/12).
    assertCents(later.final, 186970.62);
    assert.strictEqual(later.contributed, 100000);
});

test('the yearly increase counts from the year the plan starts', () => {
    const later = math.project(plan({ rate: 0, years: 4, increase: 10, delay: 2 }));

    assert.deepStrictEqual(Array.from(later.rows, (row) => Math.round(row.contributedYear)), [0, 0, 10000, 6000, 6600]);
});

test('stopping contributions at year 10 keeps the balance on what was put in', () => {
    const stopped = math.project(plan({ rate: 0, stopAt: 10 }));

    assert.strictEqual(stopped.contributed, 70000);
    assert.strictEqual(stopped.final, 70000);
    assert.strictEqual(stopped.rows[11].contributedYear, 0);
});

test('a delay or a stop year outside the plan is clamped to it', () => {
    assert.strictEqual(math.normalize({ years: 10, delay: 30 }).delay, 9);
    assert.strictEqual(math.normalize({ years: 10, stopAt: 30 }).stopAt, 10);
    assert.strictEqual(math.normalize({ years: 10, stopAt: -2 }).stopAt, 0);
    assert.strictEqual(math.normalize({ years: 10 }).stopAt, null);
    assert.strictEqual(math.normalize({ years: 10, stopAt: '' }).stopAt, null);
});

test('the levers are, in order: more a month, a higher return, start later, stop adding, a higher fee', () => {
    assert.deepStrictEqual(Array.from(math.LEVERS, (lever) => [lever.key, lever.field]),
        [['monthly', 'monthly'], ['rate', 'rate'], ['delay', 'delay'], ['stop', 'stopAt'], ['fee', 'fee']]);
});

test('each lever is the plan with one change', () => {
    const base = plan({ fee: 0.1 });
    const changed = (key, options) => {
        const variant = math.lever(base, key, options);
        const inputs = Object.assign({}, variant.inputs);
        const moved = Object.keys(inputs).filter((field) => inputs[field] !== math.normalize(base)[field]);
        return { field: variant.field, moved, value: inputs[variant.field] };
    };

    assert.deepStrictEqual(changed('monthly'), { field: 'monthly', moved: ['monthly'], value: 600 });
    assert.deepStrictEqual(changed('monthly', { monthlyStep: 2500000 }), { field: 'monthly', moved: ['monthly'], value: 2500500 });
    assert.deepStrictEqual(changed('rate'), { field: 'rate', moved: ['rate'], value: 8 });
    assert.deepStrictEqual(changed('delay'), { field: 'delay', moved: ['delay'], value: 5 });
    assert.deepStrictEqual(changed('stop'), { field: 'stopAt', moved: ['stopAt'], value: 10 });
    assert.deepStrictEqual(changed('stop', { stopAt: 4 }), { field: 'stopAt', moved: ['stopAt'], value: 4 });
    assert.deepStrictEqual(changed('fee'), { field: 'fee', moved: ['fee'], value: 0.6 });
});

test('a lever leaves the plan it was given untouched', () => {
    const base = plan();
    math.lever(base, 'monthly');

    assert.strictEqual(base.monthly, 500);
});

test('the levers move the final balance the way they say', () => {
    const final = math.project(plan()).final;
    const of = (key) => math.project(math.lever(plan(), key).inputs).final;

    assert.ok(of('monthly') > final, 'more a month ends higher');
    assert.ok(of('rate') > final, 'a higher return ends higher');
    assert.ok(of('delay') < final, 'starting later ends lower');
    assert.ok(of('stop') < final, 'stopping early ends lower');
    assert.ok(of('fee') < final, 'a higher fee ends lower');
    // Adding 100 a month at 7% for 20 years adds 100·((1+0.07/12)^240 − 1)/(0.07/12).
    assertCents(of('monthly') - final, 52092.67);
});

test('a lever stays within the bounds: a 15% return cannot go higher', () => {
    assert.strictEqual(math.lever(plan({ rate: 15 }), 'rate').inputs.rate, 15);
    assert.strictEqual(math.lever(plan({ years: 3 }), 'delay').inputs.delay, 2);
    assert.strictEqual(math.lever(plan(), 'nope'), null);
});

test('a starting amount that covers the goal only arrives when a delayed plan starts', () => {
    const inputs = plan({ initial: 50000, delay: 5 });

    for (const mode of ['monthly', 'years']) {
        const answer = math.solveGoal(inputs, { target: 20000, mode });
        assert.strictEqual(answer.kind, 'already', mode);
        assert.strictEqual(answer.year, 5, mode);
    }
});

test('the stop lever stops after a delayed start, never before it', () => {
    assert.strictEqual(math.lever(plan({ delay: 15 }), 'stop').inputs.stopAt, 16);
    assert.strictEqual(math.lever(plan({ delay: 3 }), 'stop').inputs.stopAt, 10);
});

test('a goal is unreachable when the deposits stop before they start, with the shortfall at the end of the plan', () => {
    // 1,000 at 0% for 10 years never moves: no monthly contribution changes that.
    const stopped = plan({ initial: 1000, monthly: 500, rate: 0, years: 10, stopAt: 0 });
    const answer = math.solveGoal(stopped, { target: 5000, mode: 'monthly' });

    assert.strictEqual(answer.kind, 'unreachable');
    assert.strictEqual(answer.year, 10);
    assertCents(answer.shortfall, 4000);
    assert.strictEqual(math.solveGoal(plan({ delay: 5, stopAt: 5 }), { target: 1e7, mode: 'monthly' }).kind, 'unreachable');
});
