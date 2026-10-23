// Unit checks for the To-do due status helper (wwwroot/js/todos/todo-due-status.js). No browser or app needed:
// node --test e2e-tests/todo-due-status.test.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname,
    '../PersonalFinance/src/PersonalFinance.Web/wwwroot/js/todos/todo-due-status.js'), 'utf8');
const sandbox = { window: {} };
vm.runInNewContext(source, sandbox);
const { classify } = sandbox.window.PFTodoDueStatus;

// 22 Oct 2026, late evening local time: the local calendar date is what counts, not UTC.
const today = new Date(2026, 9, 22, 23, 30);

test('an open To-do due before today is overdue', () => {
    assert.equal(classify({ dueDate: '2026-10-21', completedAt: null }, today), 'overdue');
});

test('an open To-do due on the local today is due today', () => {
    assert.equal(classify({ dueDate: '2026-10-22', completedAt: null }, today), 'due-today');
});

test('an open To-do due after today, or with no due date, has no status', () => {
    assert.equal(classify({ dueDate: '2026-10-23', completedAt: null }, today), null);
    assert.equal(classify({ dueDate: null, completedAt: null }, today), null);
});

test('a completed To-do is never marked, even when its due date has passed', () => {
    assert.equal(classify({ dueDate: '2026-10-01', completedAt: '2026-10-02T09:00:00Z' }, today), null);
    assert.equal(classify({ dueDate: '2026-10-22', completedAt: '2026-10-22T09:00:00Z' }, today), null);
});

test('the date rolls over at local midnight', () => {
    const justAfterMidnight = new Date(2026, 9, 23, 0, 5);
    assert.equal(classify({ dueDate: '2026-10-22', completedAt: null }, justAfterMidnight), 'overdue');
    assert.equal(classify({ dueDate: '2026-10-23', completedAt: null }, justAfterMidnight), 'due-today');
});
