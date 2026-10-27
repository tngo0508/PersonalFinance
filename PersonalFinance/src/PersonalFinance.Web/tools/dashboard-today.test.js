'use strict';
/* eslint-env node */

/**
 * Tests for the Dashboard Today section's calendar arithmetic (wwwroot/js/dashboard/dashboard-today.js): the month
 * grid starts on the culture's first weekday, and recent activity keeps its calendar day whatever the time zone.
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
