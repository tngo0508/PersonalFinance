/**
 * Financial Universe smoke test (spec #617): a fresh Account creates one Savings Goal, opens Financial Universe from
 * the sidebar, and reads the list view: the goal under Savings Goals, a Placeholder row with a link into the owning
 * feature for every kind the Account has nothing of, and the JSON island the graph renderer reads (slice 1). Then the
 * graph (slice 2, ADR 0020): a laptop window opens on the graph with Cytoscape's canvas drawn, the zoom buttons change
 * the zoom and fit restores it, the toggle switches to the list and back, and a phone opens on the list with the graph
 * a press away. Run from ui-test.js as Test 9n.
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
    } finally {
        await context.close();
    }
}

module.exports = {runFinancialUniverseSmoke};
