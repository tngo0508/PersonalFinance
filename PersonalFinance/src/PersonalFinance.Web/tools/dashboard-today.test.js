'use strict';
/* eslint-env node */

/**
 * Tests for the Dashboard Today section's calendar arithmetic (wwwroot/js/dashboard/dashboard-today.js): the month
 * grid starts on the culture's first weekday, recent activity keeps its calendar day whatever the time zone, and the
 * money lines (compounding today, days to FI, budget pace) are only worked out from figures that are there.
 * Run with: npm run test:i18n
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const SOURCE = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'dashboard', 'dashboard-today.js'), 'utf8');

/**
 * Load the Today helpers without a dashboard page (the section itself does not start).
 * @returns {object} The PFDashboardToday namespace.
 */
function load () {
    const window = { Intl: Intl, Date: Date };
    window.window = window;
    vm.createContext(window);
    vm.runInContext(SOURCE, window);
    return window.PFDashboardToday;
}

const today = load();

test('October 2026 starts on a Thursday in a Sunday-first week', () => {
    const weeks = today.monthWeeks(2026, 9, 0);
    assert.strictEqual(weeks.length, 5);
    assert.deepStrictEqual(Array.from(weeks[0]), [null, null, null, null, 1, 2, 3]);
    assert.deepStrictEqual(Array.from(weeks[4]), [25, 26, 27, 28, 29, 30, 31]);
});

test('a Monday-first week shifts the first row by one', () => {
    const weeks = today.monthWeeks(2026, 9, 1);
    assert.deepStrictEqual(Array.from(weeks[0]), [null, null, null, 1, 2, 3, 4]);
    assert.deepStrictEqual(Array.from(weeks[weeks.length - 1]), [26, 27, 28, 29, 30, 31, null]);
});

test('a month that starts on the first weekday has no leading blanks', () => {
    // February 2026 starts on a Sunday and has exactly four weeks.
    const weeks = today.monthWeeks(2026, 1, 0);
    assert.strictEqual(weeks.length, 4);
    assert.strictEqual(weeks[0][0], 1);
    assert.strictEqual(weeks[3][6], 28);
});

test('days in month handles leap years', () => {
    assert.strictEqual(today.daysInMonth(2028, 1), 29);
    assert.strictEqual(today.daysInMonth(2026, 1), 28);
    assert.strictEqual(today.daysInMonth(2026, 11), 31);
});

test('the United States starts the week on Sunday and Vietnam on Monday', () => {
    assert.strictEqual(today.firstDayOfWeek('en-US'), 0);
    assert.strictEqual(today.firstDayOfWeek('vi-VN'), 1);
});

test('activity days come from the date part, never shifted by a time zone', () => {
    const days = today.activityDays([
        { date: '2026-10-01T00:00:00' },
        { date: '2026-10-21T23:30:00Z' },
        { date: null },
        {}
    ]);
    assert.deepStrictEqual(Array.from(days).sort(), ['2026-10-01', '2026-10-21']);
    assert.strictEqual(today.activityDays(undefined).size, 0);
});

test('day keys are local and zero-padded', () => {
    assert.strictEqual(today.dayKey(new Date(2026, 0, 5)), '2026-01-05');
});

test('the day fraction runs from midnight to the next midnight', () => {
    assert.strictEqual(today.dayFraction(new Date(2026, 9, 26, 0, 0, 0)), 0);
    assert.strictEqual(today.dayFraction(new Date(2026, 9, 26, 12, 0, 0)), 0.5);
    assert.strictEqual(today.monthFraction(new Date(2026, 9, 1, 0, 0, 0)), 0);
    assert.strictEqual(today.monthFraction(new Date(2026, 9, 16, 12, 0, 0)), 0.5);
});

test('a year of daily growth compounds to the annual return', () => {
    const perDay = today.dailyGrowth(100000, 7);
    let worth = 100000;
    for (let day = 0; day < 365; day++) {
        worth += worth * (perDay / 100000);
    }
    assert.ok(Math.abs(worth - 107000) < 0.01);
});

test('there is no daily growth without a positive net worth and return', () => {
    assert.strictEqual(today.dailyGrowth(0, 7), null);
    assert.strictEqual(today.dailyGrowth(-5000, 7), null);
    assert.strictEqual(today.dailyGrowth(100000, 0), null);
    assert.strictEqual(today.dailyGrowth(undefined, undefined), null);
});

test('days to FI are left out once FI is reached or never comes', () => {
    assert.strictEqual(today.daysToFi(12, false), 4383);
    assert.strictEqual(today.daysToFi(12, true), null);
    assert.strictEqual(today.daysToFi(null, false), null);
    assert.strictEqual(today.daysToFi(0, false), null);
});

test('budget pace compares spending with the share of the month gone', () => {
    // Noon on 16 October 2026: half of the 31-day month is gone, and 16 days are left counting today.
    const noon = new Date(2026, 9, 16, 12, 0, 0);
    const budget = { year: 2026, month: 10, budgetedExpenses: 3100, actualExpenses: 1000 };
    const pace = today.budgetPace(budget, noon);
    assert.strictEqual(pace.status, 'onPace');
    assert.strictEqual(pace.daysLeft, 16);
    assert.strictEqual(pace.remaining, 2100);
    assert.strictEqual(pace.perDay, 2100 / 16);

    assert.strictEqual(today.budgetPace(Object.assign({}, budget, { actualExpenses: 2000 }), noon).status, 'ahead');
    const over = today.budgetPace(Object.assign({}, budget, { actualExpenses: 3500 }), noon);
    assert.strictEqual(over.status, 'over');
    assert.strictEqual(over.perDay, 0);
});

test('budget pace is only shown for the current month with planned expenses', () => {
    const noon = new Date(2026, 9, 16, 12, 0, 0);
    assert.strictEqual(today.budgetPace({ year: 2026, month: 9, budgetedExpenses: 3100, actualExpenses: 0 }, noon), null);
    assert.strictEqual(today.budgetPace({ year: 2026, month: 10, budgetedExpenses: 0, actualExpenses: 0 }, noon), null);
    assert.strictEqual(today.budgetPace(null, noon), null);
});
