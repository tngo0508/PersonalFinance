// Isolated browser check of the To-do List due badges; no app, database or account needed:
// node --test e2e-tests/todo-list-due-status.test.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const path = require('node:path');

const root = path.resolve(__dirname, '../PersonalFinance/src/PersonalFinance.Web/wwwroot');

test('open To-dos show Overdue and Due today as text from the local date, also at phone width', async () => {
    const browser = await chromium.launch();
    try {
        const page = await browser.newPage({ viewport: { width: 375, height: 812 } });
        // Late evening local time, so a UTC-based date would already be tomorrow in negative offsets.
        await page.clock.setFixedTime(new Date(2026, 9, 22, 23, 30));
        const todos = [
            { id: 1, title: 'Pay the electricity bill before the late fee kicks in', dueDate: '2026-10-21', completedAt: null },
            { id: 2, title: 'Renew car insurance', dueDate: '2026-10-22', completedAt: null },
            { id: 3, title: 'Book dentist', dueDate: '2026-10-23', completedAt: null },
            { id: 4, title: 'Read a book', dueDate: null, completedAt: null },
            { id: 5, title: 'Already done', dueDate: '2026-10-01', completedAt: '2026-10-02T09:00:00Z' }
        ];
        await page.setContent(`<!doctype html><html><body>
            <div id="todoPage" data-create-url="/Todos/Create">
                <form id="todoAddForm"><input id="todoTitle"><input id="todoDueDate" type="date">
                    <button id="todoAddBtn">Add</button><p id="todoAddError" hidden></p></form>
                <ul class="todo-list" id="todoList"></ul><div id="todoEmpty" hidden></div>
            </div>
            <script type="application/json" id="todo-data">${JSON.stringify(todos)}</script>
        </body></html>`);
        await page.addStyleTag({ path: path.join(root, 'css/todos.css') });
        await page.addScriptTag({ path: path.join(root, 'js/todos/todo-due-status.js') });
        await page.addScriptTag({ path: path.join(root, 'js/todos/todo-list.js') });

        const badgeText = id => page.locator(`[data-todo-id="${id}"] .todo-item__status`).allTextContents();
        assert.deepEqual(await badgeText(1), ['Overdue']);
        assert.deepEqual(await badgeText(2), ['Due today']);
        assert.deepEqual(await badgeText(3), []);
        assert.deepEqual(await badgeText(4), []);
        assert.deepEqual(await badgeText(5), []);

        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
        assert.ok(overflow <= 0, `page scrolls sideways by ${overflow}px at phone width`);
        const badge = await page.locator('[data-todo-id="1"] .todo-item__status').boundingBox();
        assert.ok(badge.x >= 0 && badge.x + badge.width <= 375);
    } finally {
        await browser.close();
    }
});
