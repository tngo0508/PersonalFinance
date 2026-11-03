/**
 * Financial Universe smoke test (spec #617, slice 1): a fresh Account creates one Savings Goal, opens Financial
 * Universe from the sidebar, and reads the list view: the goal under Savings Goals, a Placeholder row with a link into
 * the owning feature for every kind the Account has nothing of, and the JSON island the graph renderer (slice 2) will
 * read. Run from ui-test.js as Test 9n.
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
    } finally {
        await context.close();
    }
}

module.exports = {runFinancialUniverseSmoke};
