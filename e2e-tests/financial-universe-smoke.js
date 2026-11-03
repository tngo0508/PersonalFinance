/**
 * Financial Universe smoke test (spec #617): a fresh Account creates one Savings Goal, opens Financial Universe from
 * the sidebar, and reads the list view: the goal under Savings Goals, a Placeholder row with a link into the owning
 * feature for every kind the Account has nothing of, and the JSON island the graph renderer reads (slice 1). Then the
 * graph (slice 2, ADR 0020): a laptop window opens on the graph with Cytoscape's canvas drawn, the zoom buttons change
 * the zoom and fit restores it, the toggle switches to the list and back, and a phone opens on the list with the graph
 * a press away. Then the explorer (slice 3): two budget months recorded through the Budget Tracker endpoints, the
 * month picker switching to last month with that month's amounts in the URL-carried page, the detail panel opened by a
 * tap on a node and by a row's Details button (Escape and Close dismiss it, focus returns), hover fading the rest of the
 * graph, and a node dragged then restored by a reload. Run from ui-test.js as Test 9n.
 *
 * Expected texts are read from Resources/i18n/en.json, so a rewording does not break the test.
 */

const path = require('path');
const {registerFreshAccount, createGoal} = require('./reporting-currency-smoke');

const TIMEOUT = 15000;
const en = require(path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web', 'Resources', 'i18n', 'en.json'));

/**
 * The trimmed text of the first element matching a selector; '' when there is none.
 * @param {import('playwright').Page} page - The page.
 * @param {string} selector - A CSS selector.
 * @returns {Promise<string>} The text.
 */
async function textOf(page, selector) {
    return ((await page.locator(selector).first().textContent().catch(() => '')) || '').trim();
}

/**
 * What the graph shows: the view in effect, the toggle's pressed button, the zoom and label share, the panels' state.
 * @param {import('playwright').Page} page - The page.
 * @returns {Promise<{mode: string|null, zoom: number, share: number, minShare: number, canvases: number,
 *     graphHidden: boolean, listHidden: boolean, zoomToolsHidden: boolean, pressed: string|null}>}
 */
async function readGraphState(page) {
    return page.evaluate(() => {
        const api = window.FinancialUniverseGraph;
        const cy = api && api.instance();
        const pressed = document.querySelector('[data-fu-view][aria-pressed="true"]');
        return {
            mode: api ? api.mode() : null,
            zoom: cy ? cy.zoom() : 0,
            share: api && cy ? api.labelShare() : 0,
            minShare: api ? api.minLabelScale : 0,
            canvases: document.querySelectorAll('#fuGraph canvas').length,
            graphHidden: document.getElementById('fuGraphPanel').hidden,
            listHidden: document.getElementById('fuListPanel').hidden,
            zoomToolsHidden: document.querySelector('[data-fu-zoom-tools]').hidden,
            pressed: pressed ? pressed.getAttribute('data-fu-view') : null
        };
    });
}

/**
 * The JSON island as the page rendered it.
 * @param {import('playwright').Page} page - The page.
 * @returns {Promise<object|null>} The page data, or null when the island is missing.
 */
async function readIsland(page) {
    return page.evaluate(() => {
        const island = document.getElementById('fuPageData');
        return island ? JSON.parse(island.textContent || '{}') : null;
    });
}

/**
 * What the detail panel shows: whether it is open, the node it is for, its lines, and which element has focus.
 * @param {import('playwright').Page} page - The page.
 * @returns {Promise<{open: boolean, node: string|null, kind: string, label: string, amount: string, period: string,
 *     link: string|null, focused: string|null}>}
 */
async function readPanelState(page) {
    return page.evaluate(() => {
        const panel = document.getElementById('fuNodePanel');
        const text = (selector) => ((panel.querySelector(selector) || {}).textContent || '').trim();
        const link = panel.querySelector('[data-fu-field="link"]');
        const active = document.activeElement;
        return {
            open: panel.classList.contains('show'),
            node: panel.getAttribute('data-fu-node'),
            kind: text('[data-fu-field="kind"]'),
            label: text('[data-fu-field="label"]'),
            amount: text('[data-fu-field="amount"]'),
            period: text('[data-fu-field="period"]'),
            link: link && !link.hidden ? link.getAttribute('href') : null,
            focused: active ? active.getAttribute('data-test-id') : null
        };
    });
}

/**
 * Where a graph node is drawn, in page coordinates, with its model position and whether it can be grabbed.
 * @param {import('playwright').Page} page - The page.
 * @param {string} nodeId - The node's id in the island.
 * @returns {Promise<{x: number, y: number, modelX: number, modelY: number, grabbable: boolean, dimmed: number}|null>}
 *     dimmed: how many elements wear the dim class.
 */
async function readNodeOnScreen(page, nodeId) {
    return page.evaluate((id) => {
        const api = window.FinancialUniverseGraph;
        const cy = api && api.instance();
        const node = cy ? cy.getElementById(id) : null;
        if (!node || node.empty()) {
            return null;
        }
        const rect = document.getElementById('fuGraph').getBoundingClientRect();
        const rendered = node.renderedPosition();
        const model = node.position();
        return {
            x: rect.left + rendered.x,
            y: rect.top + rendered.y,
            modelX: model.x,
            modelY: model.y,
            grabbable: node.grabbable(),
            dimmed: cy.elements(`.${api.dimClass}`).length
        };
    }, nodeId);
}

/**
 * Records budget transactions through the Web's JSON endpoints (the Budget Tracker page's own calls), one income and
 * one expense per month given, so the Financial Universe has a month to switch to. The default categories are
 * seeded by the first categories read.
 * @param {import('playwright').Page} page - A signed-in page (the layout carries the antiforgery token).
 * @param {string} base - The Web app's origin.
 * @param {{date: string, income: number, expense: number}[]} months - ISO dates inside each month, with the amounts.
 * @returns {Promise<string>} '' when every call succeeded, else what failed.
 */
async function seedBudgetMonths(page, base, months) {
    return page.evaluate(async ({origin, entries}) => {
        const meta = document.querySelector('meta[name="__RequestVerificationToken"]');
        const headers = {
            'Content-Type': 'application/json',
            'RequestVerificationToken': meta ? meta.getAttribute('content') : ''
        };
        const categoriesResponse = await fetch(`${origin}/BudgetTracker/GetCategories`, {credentials: 'same-origin'});
        if (!categoriesResponse.ok) {
            return `GetCategories ${categoriesResponse.status}`;
        }
        const categories = await categoriesResponse.json();
        // BudgetCategoryKind travels as its number from the Web's JSON: Expense = 1, Income = 2.
        const incomeCategory = categories.find((c) => c.kind === 2 || c.kind === 'Income');
        const expenseCategory = categories.find((c) => c.kind === 1 || c.kind === 'Expense');
        if (!incomeCategory || !expenseCategory) {
            return `no default categories: ${JSON.stringify(categories).slice(0, 200)}`;
        }
        for (const entry of entries) {
            const lines = [
                {date: entry.date, amount: entry.income, description: 'Universe salary', categoryId: incomeCategory.id},
                {date: entry.date, amount: entry.expense, description: 'Universe rent', categoryId: expenseCategory.id}
            ];
            for (const line of lines) {
                const response = await fetch(`${origin}/BudgetTracker/CreateTransaction`, {
                    method: 'POST', headers, credentials: 'same-origin', body: JSON.stringify(line)
                });
                if (!response.ok) {
                    return `CreateTransaction ${response.status} for ${JSON.stringify(line)}`;
                }
            }
        }
        return '';
    }, {origin: base, entries: months});
}

/**
 * An ISO date (yyyy-MM-dd) and the picker's value (yyyy-MM) for a day in the month `offset` months from now.
 * @param {number} offset - 0 for this month, -1 for last month.
 * @returns {{iso: string, period: string}}
 */
function monthAt(offset) {
    const today = new Date();
    const first = new Date(today.getFullYear(), today.getMonth() + offset, 1);
    // The 15th of a past month; today for the current one, so the entries never post-date "now".
    const day = offset === 0 ? today.getDate() : 15;
    const pad = (n) => String(n).padStart(2, '0');
    const period = `${first.getFullYear()}-${pad(first.getMonth() + 1)}`;
    return {iso: `${period}-${pad(day)}`, period};
}

/**
 * Run the smoke test.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function}} options - The suite.
 * @returns {Promise<void>}
 */
async function runFinancialUniverseSmoke({browser, base, pass, fail}) {
    const check = (ok, message, detail) => (ok ? pass(message) : fail(`${message}: ${detail}`));
    const context = await browser.newContext({
        locale: 'en-US',
        viewport: {width: 1366, height: 900},
        extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}
    });
    const page = await context.newPage();
    try {
        if (!await registerFreshAccount(page, base)) {
            fail('Financial Universe smoke: could not register and sign in');
            return;
        }

        // One goal, so the page has a real node beside its Placeholders.
        await page.goto(`${base}/SavingsGoals`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await createGoal(page, {name: 'Universe Car', currency: 'USD', target: '1000', current: '150'});

        // 1. The sidebar's Overview group reaches the page.
        await page.goto(`${base}/Dashboard`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await Promise.all([
            page.waitForURL(/\/FinancialUniverse/i, {timeout: TIMEOUT}),
            page.click('#sidebar [data-test-id="nav-financial-universe"]')
        ]);
        await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
        check(await textOf(page, 'h1') === en['FinancialUniverse.Title'], 'The nav opens Financial Universe', await textOf(page, 'h1'));

        // 2. The goal is listed under Savings Goals with its saved amount and target, linking to Savings Goals.
        const goalRow = page.locator('[data-test-id="fu-section-savings-goals"] li[data-node-kind="SavingsGoal"]').first();
        const goalText = ((await goalRow.textContent().catch(() => '')) || '').replace(/\s+/g, ' ').trim();
        check(goalText.includes('Universe Car') && goalText.includes('$150') && goalText.includes('$1,000'),
            'The goal is listed with its saved amount and target', `"${goalText}"`);
        const goalHref = await goalRow.locator('a').first().getAttribute('href');
        check(/\/SavingsGoals/i.test(goalHref || ''), 'The goal row links into Savings Goals', `href ${goalHref}`);

        // 3. Every empty kind shows one Placeholder row with a call-to-action link into the feature that fills it.
        const placeholders = [
            ['income-sources', 'FinancialUniverse.Placeholder.IncomeSource', /BudgetTracker/i],
            ['expense-categories', 'FinancialUniverse.Placeholder.ExpenseCategory', /BudgetTracker/i],
            ['net-worth', 'FinancialUniverse.Placeholder.NetWorth', /Networth/i],
            ['investment-scenarios', 'FinancialUniverse.Placeholder.InvestmentScenario', /InvestmentCalculator/i]
        ];
        for (const [section, key, hrefPattern] of placeholders) {
            const row = page.locator(`[data-test-id="fu-section-${section}"] li[data-node-kind="Placeholder"]`).first();
            const text = ((await row.textContent().catch(() => '')) || '').replace(/\s+/g, ' ').trim();
            const href = await row.locator('a').first().getAttribute('href').catch(() => null);
            check(text.includes(en[key]) && hrefPattern.test(href || ''),
                `The ${section} section shows its Placeholder with a link`, `"${text}" -> ${href}`);
        }

        // 4. The JSON island carries the labelled nodes for the renderer: no composing in JavaScript.
        const pageData = await page.evaluate(() => {
            const island = document.getElementById('fuPageData');
            return island ? JSON.parse(island.textContent || '{}') : null;
        });
        const goalNode = pageData && pageData.nodes && pageData.nodes.find(n => n.kind === 'SavingsGoal');
        check(!!goalNode && goalNode.label === 'Universe Car' && goalNode.amountText === '$150' && /\/SavingsGoals/i.test(goalNode.href),
            'The JSON island carries the labelled goal node with its link', JSON.stringify(goalNode));
        check(!!pageData && Array.isArray(pageData.edges) && pageData.edges.length >= 1,
            'The JSON island carries the edges', JSON.stringify(pageData && pageData.edges));

        // 5. A laptop window opens on the graph: the fitted labels read, Cytoscape has drawn its canvas, and the
        //    toggle says so. The accessible list stays in the page, hidden.
        await page.waitForFunction(() => window.FinancialUniverseGraph && window.FinancialUniverseGraph.mode() !== null,
            null, {timeout: TIMEOUT});
        const graphState = await readGraphState(page);
        check(graphState.mode === 'graph' && graphState.canvases >= 1 && graphState.share >= graphState.minShare
            && !graphState.graphHidden && graphState.listHidden && graphState.pressed === 'graph',
            'A laptop window opens on the graph with its canvas drawn', JSON.stringify(graphState));

        // 6. The toolbar: zoom in and out change the zoom; fit brings it back.
        const zoomBefore = graphState.zoom;
        await page.click('[data-test-id="fu-zoom-in"]');
        const zoomedIn = (await readGraphState(page)).zoom;
        await page.click('[data-test-id="fu-zoom-out"]');
        await page.click('[data-test-id="fu-zoom-out"]');
        const zoomedOut = (await readGraphState(page)).zoom;
        await page.click('[data-test-id="fu-fit"]');
        const refit = (await readGraphState(page)).zoom;
        check(zoomedIn > zoomBefore && zoomedOut < zoomBefore && Math.abs(refit - zoomBefore) < 0.01,
            'Zoom in, zoom out and fit change the zoom as expected', JSON.stringify({zoomBefore, zoomedIn, zoomedOut, refit}));

        // 7. The toggle switches to the list and back; the zoom tools hide with the graph.
        await page.click('[data-test-id="fu-view-list"]');
        const listState = await readGraphState(page);
        check(listState.mode === 'list' && listState.graphHidden && !listState.listHidden && listState.pressed === 'list'
            && listState.zoomToolsHidden, 'The toggle shows the list and hides the graph', JSON.stringify(listState));
        await page.click('[data-test-id="fu-view-graph"]');
        const backState = await readGraphState(page);
        check(backState.mode === 'graph' && !backState.graphHidden && backState.listHidden && !backState.zoomToolsHidden,
            'The toggle shows the graph again', JSON.stringify(backState));

        // 8. A phone opens on the list: at 375px the fitted labels fall under the legibility share. "Graph" still works.
        const phone = await browser.newContext({
            locale: 'en-US',
            viewport: {width: 375, height: 812},
            isMobile: true,
            hasTouch: true,
            storageState: await context.storageState(),
            extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}
        });
        try {
            const phonePage = await phone.newPage();
            await phonePage.goto(`${base}/FinancialUniverse`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await phonePage.waitForFunction(() => window.FinancialUniverseGraph && window.FinancialUniverseGraph.mode() !== null,
                null, {timeout: TIMEOUT});
            const phoneState = await readGraphState(phonePage);
            check(phoneState.mode === 'list' && phoneState.graphHidden && !phoneState.listHidden
                && phoneState.share < phoneState.minShare,
                'A phone opens on the list because the fitted labels would not read', JSON.stringify(phoneState));
            await phonePage.click('[data-test-id="fu-view-graph"]');
            const phoneGraph = await readGraphState(phonePage);
            check(phoneGraph.mode === 'graph' && !phoneGraph.graphHidden && phoneGraph.canvases >= 1,
                'On a phone, Graph shows the drawing on request', JSON.stringify(phoneGraph));
        } finally {
            await phone.close();
        }

        // 9. The month picker (slice 3): with budget data in this month and the last, the picker lists both, and
        //    choosing last month reloads the page with the month in the URL and the amounts of that month: the income
        //    changes and the goal's contribution arrow, 150 this month from the goal's opening amount, is 0 last month.
        const thisMonth = monthAt(0);
        const lastMonth = monthAt(-1);
        const seeded = await seedBudgetMonths(page, base, [
            {date: thisMonth.iso, income: 3000, expense: 1000},
            {date: lastMonth.iso, income: 2000, expense: 500}
        ]);
        check(seeded === '', 'Two budget months are recorded through the Budget Tracker endpoints', seeded);
        await page.goto(`${base}/FinancialUniverse`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const monthOptions = await page.evaluate(() => Array.from(document.querySelectorAll('[data-test-id="fu-month"] option'))
            .map((option) => ({value: option.value, label: option.textContent.trim(), selected: option.selected})));
        const selectedOption = monthOptions.find((option) => option.selected);
        check(monthOptions.some((option) => option.value === lastMonth.period) && selectedOption && selectedOption.value === thisMonth.period,
            'The month picker lists last month and selects this one', JSON.stringify(monthOptions));
        const thisIsland = await readIsland(page);
        const incomeNow = thisIsland.nodes.find((n) => n.id === 'income');
        const goalId = (thisIsland.nodes.find((n) => n.kind === 'SavingsGoal') || {}).id;
        const goalEdgeNow = thisIsland.edges.find((e) => e.target === goalId);
        check(!!incomeNow && incomeNow.amountText === '$3,000' && !!goalEdgeNow && Number(goalEdgeNow.weight) === 150,
            "This month's Income is $3,000 and the goal's arrow carries its $150 opening contribution",
            JSON.stringify({incomeNow, goalEdgeNow}));
        const monthSubmitHidden = await page.evaluate(() => document.querySelector('[data-fu-month-submit]').hidden);
        check(monthSubmitHidden, 'With script, the picker needs no Show button', String(monthSubmitHidden));
        await Promise.all([
            page.waitForURL((url) => url.searchParams.get('period') === lastMonth.period, {timeout: TIMEOUT}),
            page.selectOption('[data-test-id="fu-month"]', lastMonth.period)
        ]);
        await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
        const lastIsland = await readIsland(page);
        const incomeThen = lastIsland.nodes.find((n) => n.id === 'income');
        const goalEdgeThen = lastIsland.edges.find((e) => e.target === goalId);
        const lastLabel = (monthOptions.find((option) => option.value === lastMonth.period) || {}).label;
        check(!!incomeThen && incomeThen.amountText === '$2,000' && !!goalEdgeThen && Number(goalEdgeThen.weight) === 0
            && lastIsland.monthLabel === lastLabel && (await textOf(page, 'header p.text-body-secondary.mb-0')).includes(lastLabel),
            "Choosing last month reloads the page with that month's amounts and label",
            JSON.stringify({url: page.url(), incomeThen, goalEdgeThen, monthLabel: lastIsland.monthLabel, lastLabel}));
        const stillSelected = await page.evaluate(() => document.querySelector('[data-test-id="fu-month"]').value);
        check(stillSelected === lastMonth.period, 'The picker shows the month the page is on', stillSelected);

        // 10. The detail panel from the graph: pointing at Income fades what it does not touch; tapping it opens the
        //     panel with the island's lines; Escape closes it and focus returns to the Graph toggle.
        await page.goto(`${base}/FinancialUniverse`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.waitForFunction(() => window.FinancialUniverseGraph && window.FinancialUniverseGraph.mode() !== null,
            null, {timeout: TIMEOUT});
        if ((await readGraphState(page)).mode !== 'graph') {
            await page.click('[data-test-id="fu-view-graph"]');
        }
        const incomeOnScreen = await readNodeOnScreen(page, 'income');
        check(!!incomeOnScreen && incomeOnScreen.dimmed === 0, 'Before any hover, nothing in the graph is dimmed', JSON.stringify(incomeOnScreen));
        await page.mouse.move(incomeOnScreen.x, incomeOnScreen.y);
        await page.waitForFunction((id) => {
            const api = window.FinancialUniverseGraph;
            return api.instance().elements(`.${api.dimClass}`).length > 0 && !api.instance().getElementById(id).hasClass(api.dimClass);
        }, 'income', {timeout: TIMEOUT}).catch(() => null);
        const hovered = await page.evaluate((id) => {
            const api = window.FinancialUniverseGraph;
            const cy = api.instance();
            const scenario = cy.nodes().filter((n) => n.data('kind') === 'Placeholder' && n.id().includes('investmentscenario')).first();
            return {
                dimmed: cy.elements(`.${api.dimClass}`).length,
                incomeDimmed: cy.getElementById(id).hasClass(api.dimClass),
                neighbourDimmed: cy.getElementById(id).closedNeighborhood().filter(`.${api.dimClass}`).length,
                scenarioDimmed: scenario.nonempty() ? scenario.hasClass(api.dimClass) : null
            };
        }, 'income');
        check(hovered.dimmed > 0 && !hovered.incomeDimmed && hovered.neighbourDimmed === 0 && hovered.scenarioDimmed === true,
            'Pointing at Income fades everything outside its neighbourhood', JSON.stringify(hovered));
        const canvasBox = await page.locator('#fuGraph').boundingBox();
        await page.mouse.move(canvasBox.x + 3, canvasBox.y + 3);
        await page.waitForFunction(() => {
            const api = window.FinancialUniverseGraph;
            return api.instance().elements(`.${api.dimClass}`).length === 0;
        }, null, {timeout: TIMEOUT}).catch(() => null);
        const unhovered = await readNodeOnScreen(page, 'income');
        check(unhovered.dimmed === 0, 'Moving off the node restores the graph', JSON.stringify(unhovered));

        await page.mouse.click(incomeOnScreen.x, incomeOnScreen.y);
        await page.waitForSelector('#fuNodePanel.show', {timeout: TIMEOUT}).catch(() => null);
        const graphPanelState = await readPanelState(page);
        const expectedPeriod = en['FinancialUniverse.Panel.Period.Month'].replace('{month}', thisIsland.monthLabel);
        check(graphPanelState.open && graphPanelState.node === 'income' && graphPanelState.label === en['FinancialUniverse.Node.Income']
            && graphPanelState.kind === en['FinancialUniverse.Kind.Income'] && graphPanelState.amount === '$3,000'
            && graphPanelState.period === expectedPeriod && /\/BudgetTracker/i.test(graphPanelState.link || '')
            && graphPanelState.focused === 'fu-node-panel-close',
            'Tapping Income opens the detail panel with its kind, amount, period and link, focused on Close',
            JSON.stringify({graphPanelState, expectedPeriod}));
        const panelHighlight = await readNodeOnScreen(page, 'income');
        check(panelHighlight.dimmed > 0, 'While the panel is open, the node stays highlighted', JSON.stringify(panelHighlight));
        await page.keyboard.press('Escape');
        await page.waitForFunction(() => !document.getElementById('fuNodePanel').classList.contains('show'), null, {timeout: TIMEOUT}).catch(() => null);
        const afterEscape = await readPanelState(page);
        const afterEscapeGraph = await readNodeOnScreen(page, 'income');
        check(!afterEscape.open && afterEscape.node === null && afterEscape.focused === 'fu-view-graph' && afterEscapeGraph.dimmed === 0,
            'Escape closes the panel, clears the highlight and returns focus to the Graph toggle', JSON.stringify({afterEscape, afterEscapeGraph}));

        // 11. The detail panel from the list: a row's Details button opens the same panel for the goal, and the close
        //     button returns focus to that button.
        await page.click('[data-test-id="fu-view-list"]');
        const detailsButton = page.locator(`[data-fu-details="${goalId}"]`);
        check(await detailsButton.isVisible(), 'With script, every list row shows a Details button', String(await detailsButton.count()));
        await detailsButton.click();
        await page.waitForSelector('#fuNodePanel.show', {timeout: TIMEOUT}).catch(() => null);
        const listPanelState = await readPanelState(page);
        check(listPanelState.open && listPanelState.node === goalId && listPanelState.label === 'Universe Car'
            && listPanelState.amount === '$150' && listPanelState.kind === en['FinancialUniverse.Kind.SavingsGoal']
            && listPanelState.period === en['FinancialUniverse.Panel.Period.Latest'] && /\/SavingsGoals/i.test(listPanelState.link || ''),
            "Details on the goal's row opens the panel with the goal's latest figure", JSON.stringify(listPanelState));
        await page.click('[data-test-id="fu-node-panel-close"]');
        await page.waitForFunction(() => !document.getElementById('fuNodePanel').classList.contains('show'), null, {timeout: TIMEOUT}).catch(() => null);
        const afterClose = await readPanelState(page);
        check(!afterClose.open && afterClose.focused === `fu-details-${goalId}`,
            'Close returns focus to the Details button that opened the panel', JSON.stringify(afterClose));

        // 12. Drag: a node moves with the pointer and stays grabbable; a reload runs the layout again, so it is back.
        await page.click('[data-test-id="fu-view-graph"]');
        const beforeDrag = await readNodeOnScreen(page, 'income');
        await page.mouse.move(beforeDrag.x, beforeDrag.y);
        await page.mouse.down();
        await page.mouse.move(beforeDrag.x + 30, beforeDrag.y + 20, {steps: 5});
        await page.mouse.move(beforeDrag.x + 60, beforeDrag.y + 40, {steps: 5});
        await page.mouse.up();
        const afterDrag = await readNodeOnScreen(page, 'income');
        const moved = Math.hypot(afterDrag.modelX - beforeDrag.modelX, afterDrag.modelY - beforeDrag.modelY);
        const panelAfterDrag = await readPanelState(page);
        check(beforeDrag.grabbable && afterDrag.grabbable && moved > 10 && !panelAfterDrag.open,
            'Dragging Income moves it, keeps it draggable and opens no panel', JSON.stringify({beforeDrag, afterDrag, moved}));
        await page.reload({waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.waitForFunction(() => window.FinancialUniverseGraph && window.FinancialUniverseGraph.mode() !== null,
            null, {timeout: TIMEOUT});
        if ((await readGraphState(page)).mode !== 'graph') {
            await page.click('[data-test-id="fu-view-graph"]');
        }
        const afterReload = await readNodeOnScreen(page, 'income');
        const restored = Math.hypot(afterReload.modelX - beforeDrag.modelX, afterReload.modelY - beforeDrag.modelY);
        check(restored < 1, 'A reload lays the graph out afresh: the dragged node is back where the layout put it',
            JSON.stringify({beforeDrag, afterReload, restored}));
    } finally {
        await context.close();
    }
}

module.exports = {runFinancialUniverseSmoke};
