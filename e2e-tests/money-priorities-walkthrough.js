/**
 * Money Priorities walkthrough (spec #315, slice #326): a fresh Account opens the page from the sidebar, answers the
 * questions three times (the debt, emergency-fund and IRA leaves), checks each headline and its "Your path" list, uses
 * Earlier results (View and Delete), and narrows the window to see the path view of the Priority Tree. Run from
 * ui-test.js as Test 9d.
 *
 * The ApiService computes every figure and the "Your path" sentences; these checks read them as the page shows them.
 * Runs on the InMemory database like the rest of the suite: a fresh Account starts with no Earlier results, and each
 * submit varies its answers, because an identical resubmit returns the existing Walk.
 */

const TIMEOUT = 15000;

const DEBT_LEAF = 'Pay down the high-interest debt first';
const FUND_LEAF = 'Build the emergency fund to 3 months';
const IRA_LEAF = 'Max the IRA, then the 401(k)';

/**
 * Register a fresh Account and make sure it is signed in.
 * @param {import('playwright').Page} page - A page in a new, signed-out context.
 * @param {string} base - The Web app's base URL.
 * @returns {Promise<boolean>} True when the Account is signed in.
 */
async function registerFreshAccount(page, base) {
    const email = `e2emoneypriorities_${Date.now()}@example.com`;
    const password = 'TestPass1!';
    await page.goto(`${base}/Identity/Account/Register`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    await page.fill('input[type="email"]', email);
    const passwords = await page.locator('input[type="password"]').all();
    for (const input of passwords.slice(0, 2)) {
        await input.fill(password);
    }
    await page.check('input[type="checkbox"][name="Input.AcceptTerms"]');
    await page.click('#registerSubmit');
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});

    if (!await page.locator('#sidebar').isVisible().catch(() => false)) {
        await page.goto(`${base}/Identity/Account/Login`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.fill('input[type="email"]', email);
        await page.fill('input[type="password"]', password);
        await page.click('#login-submit');
        await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
    }
    return page.locator('#sidebar').isVisible().catch(() => false);
}

/**
 * Run one step, failing it (not the whole walkthrough) when it throws.
 * @param {string} name - The step's name.
 * @param {Function} fail - The suite's fail().
 * @param {Function} body - The step.
 * @returns {Promise<void>}
 */
async function step(name, fail, body) {
    try {
        await body();
    } catch (error) {
        fail(`${name}: ${error.message.split('\n')[0]}`);
    }
}

/**
 * Fill the form's fields (by input id) and choose its radios, leaving every other field as it is.
 * @param {import('playwright').Page} page - A page showing /MoneyPriorities.
 * @param {{fields?: Object<string, string>, select?: Object<string, string>, check?: string[]}} answers - Values by
 *        input id, select values by id, and radio ids to choose.
 * @returns {Promise<void>}
 */
async function answer(page, {fields = {}, select = {}, check = []}) {
    for (const [id, value] of Object.entries(fields)) {
        await page.fill(`#${id}`, value);
    }
    for (const [id, value] of Object.entries(select)) {
        await page.selectOption(`#${id}`, value);
    }
    for (const id of check) {
        await page.check(`#${id}`);
    }
}

/**
 * Submit the form and wait until the result card shows the expected priority.
 * @param {import('playwright').Page} page - A page showing /MoneyPriorities.
 * @param {string} leaf - The leaf label the headline should end with.
 * @returns {Promise<void>}
 */
async function submitFor(page, leaf) {
    const response = page.waitForResponse((r) => r.url().includes('/MoneyPriorities/Submit'), {timeout: TIMEOUT});
    await page.click('#mpSubmit');
    const result = await response;
    if (result.status() !== 200) {
        throw new Error(`the submit returned ${result.status()}`);
    }
    await page.locator('#mpHeadline', {hasText: leaf}).waitFor({timeout: TIMEOUT});
    // Earlier results refresh after the result is shown.
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
}

/**
 * Check the headline and "Your path" against what the leaf should show.
 * @param {import('playwright').Page} page - A page showing a result.
 * @param {string} amount - The monthly amount as the headline shows it ("$500.00").
 * @param {string} leaf - The leaf label.
 * @param {string[]} expectedPath - The expected sentences, in order.
 * @returns {Promise<string|null>} Null when everything matches, otherwise what differs.
 */
async function resultProblem(page, amount, leaf, expectedPath) {
    const headline = ((await page.locator('#mpHeadline').textContent()) || '').trim();
    // The server ends the headline's leaf label with a full stop.
    const expectedHeadline = `Put your ${amount} a month toward: ${leaf}.`;
    if (headline !== expectedHeadline) {
        return `headline "${headline}", expected "${expectedHeadline}"`;
    }
    const sentences = (await page.locator('#mpPath li').allTextContents()).map((text) => text.trim());
    if (sentences.join(' | ') !== expectedPath.join(' | ')) {
        return `"Your path" was [${sentences.join(' | ')}], expected [${expectedPath.join(' | ')}]`;
    }
    return null;
}

/**
 * The Earlier results rows' priorities, top to bottom.
 * @param {import('playwright').Page} page - A page showing /MoneyPriorities.
 * @returns {Promise<string[]>} The leaf labels.
 */
async function earlierLeaves(page) {
    const leaves = await page.locator('#mpEarlier li.mp-earlier__item .mp-earlier__leaf').allTextContents();
    return leaves.map((text) => text.trim());
}

/**
 * The walkthrough. Every check reports through pass/fail.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function,
 *          consoleErrors: string[], pageErrors: string[]}} suite - The ui-test.js suite.
 * @returns {Promise<void>}
 */
async function runMoneyPrioritiesWalkthrough({browser, base, pass, fail, consoleErrors, pageErrors}) {
    const context = await browser.newContext({ignoreHTTPSErrors: true, viewport: {width: 1280, height: 800}});
    const page = await context.newPage();
    page.on('console', (msg) => {
        if (msg.type() === 'error') {
            consoleErrors.push(msg.text());
        }
    });
    page.on('pageerror', (error) => pageErrors.push(error.message));
    try {
        if (!await registerFreshAccount(page, base)) {
            fail('Money Priorities 1: could not register and sign in a fresh Account');
            return;
        }

        // Step 1: open the page from the sidebar; the empty state and the unhighlighted tree show.
        await step('Money Priorities 1', fail, async () => {
            await page.goto(`${base}/Dashboard`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.locator('#sidebar a.sidebar-link', {hasText: 'Money Priorities'}).click();
            await page.waitForURL(/\/MoneyPriorities/, {timeout: TIMEOUT});
            await page.locator('#mp-diagram svg').first().waitFor({state: 'visible', timeout: TIMEOUT});
            const active = await page.locator('#sidebar a.sidebar-link.active', {hasText: 'Money Priorities'}).count();
            const emptyShown = await page.locator('#mpEmpty').isVisible();
            const resultHidden = await page.locator('#mpResult').isHidden();
            const earlierEmpty = await page.locator('#mpEarlierEmpty').isVisible();
            const rows = await page.locator('#mpEarlier li').count();
            const pathView = await page.locator('#mp-diagram .mp-diagram__view--path').count();
            const toggleHidden = await page.locator('#mp-diagram .mp-diagram__toggle').isHidden();
            if (active === 1 && emptyShown && resultHidden && earlierEmpty && rows === 0 && pathView === 0
                && toggleHidden) {
                pass('Money Priorities 1: opened from the sidebar; empty state, no Earlier results, the whole tree drawn');
            } else {
                fail(`Money Priorities 1: sidebar active ${active}, empty state ${emptyShown}, result hidden ${
                    resultHidden}, Earlier empty ${earlierEmpty} with ${rows} rows, path view ${pathView}, ` +
                    `toggle hidden ${toggleHidden}`);
            }
        });

        // Step 2: the debt leaf.
        await step('Money Priorities 2', fail, async () => {
            await answer(page, {fields: {mpMonthlyAmount: '500', mpDebtApr: '22'}});
            await page.locator('#mpDebtBalanceField').waitFor({state: 'visible', timeout: TIMEOUT});
            await answer(page, {fields: {mpDebtBalance: '5000'}});
            await submitFor(page, DEBT_LEAF);
            const problem = await resultProblem(page, '$500.00', DEBT_LEAF, [
                'Debt with APR of 7% or more? Yes.',
                `Suggested priority: ${DEBT_LEAF}.`
            ]);
            const figures = ((await page.locator('#mpFigures').textContent()) || '').replace(/\s+/g, ' ').trim();
            const hasFigures = figures.includes('12 months') && figures.includes('$574');
            const diagramShown = await page.locator('#mp-diagram svg').first().isVisible();
            if (!problem && hasFigures && diagramShown) {
                pass('Money Priorities 2: debt leaf headline and "Your path"; paid off in 12 months, $574 interest');
            } else {
                fail(`Money Priorities 2: ${problem || 'headline and path ok'}; figures "${figures}"; ` +
                    `Priority Tree shown ${diagramShown}`);
            }
        });

        // Step 3: the emergency-fund leaf.
        await step('Money Priorities 3', fail, async () => {
            await answer(page, {fields: {mpMonthlyAmount: '400', mpDebtApr: '5', mpEmergencyFundMonths: '1'}});
            await page.locator('#mpMonthlyExpensesField').waitFor({state: 'visible', timeout: TIMEOUT});
            await answer(page, {fields: {mpMonthlyExpenses: '3000'}});
            await submitFor(page, FUND_LEAF);
            const problem = await resultProblem(page, '$400.00', FUND_LEAF, [
                'Debt with APR of 7% or more? No.',
                'Emergency fund covers 3 months of expenses? No.',
                `Suggested priority: ${FUND_LEAF}.`
            ]);
            if (!problem) {
                pass('Money Priorities 3: emergency-fund leaf headline and "Your path"');
            } else {
                fail(`Money Priorities 3: ${problem}`);
            }
        });

        // Step 4: the IRA leaf (horizon over 10 years, an age range, not at the limit).
        await step('Money Priorities 4', fail, async () => {
            await answer(page, {
                fields: {mpMonthlyAmount: '600', mpDebtApr: '0', mpEmergencyFundMonths: '6', mpHorizonYears: '20'},
                check: ['mpMatchYes']
            });
            await page.locator('#mpAgeBandField').waitFor({state: 'visible', timeout: TIMEOUT});
            await answer(page, {select: {mpAgeBand: '1'}, check: ['mpLimitNo']});
            await submitFor(page, IRA_LEAF);
            const problem = await resultProblem(page, '$600.00', IRA_LEAF, [
                'Debt with APR of 7% or more? No.',
                'Emergency fund covers 3 months of expenses? Yes.',
                'Getting the full employer match? Yes.',
                'When will you need this money? Over 10 years.',
                'Already at the IRS limit for your age? No.',
                `Suggested priority: ${IRA_LEAF}.`
            ]);
            if (!problem) {
                pass('Money Priorities 4: IRA leaf headline and "Your path"');
            } else {
                fail(`Money Priorities 4: ${problem}`);
            }
        });

        // Step 5: Earlier results list the three Walks, newest first; View re-shows the oldest (the debt leaf).
        await step('Money Priorities 5', fail, async () => {
            await page.waitForFunction(() => document.querySelectorAll('#mpEarlier li').length === 3, null,
                {timeout: TIMEOUT});
            const leaves = await earlierLeaves(page);
            const emptyHidden = await page.locator('#mpEarlierEmpty').isHidden();
            await page.locator('#mpEarlier li.mp-earlier__item', {hasText: DEBT_LEAF})
                .getByRole('button', {name: /^View/}).click();
            await page.locator('#mpHeadline', {hasText: DEBT_LEAF}).waitFor({timeout: TIMEOUT});
            const problem = await resultProblem(page, '$500.00', DEBT_LEAF, [
                'Debt with APR of 7% or more? Yes.',
                `Suggested priority: ${DEBT_LEAF}.`
            ]);
            const refilledApr = await page.inputValue('#mpDebtApr');
            const expectedOrder = [IRA_LEAF, FUND_LEAF, DEBT_LEAF].join(' | ');
            if (leaves.join(' | ') === expectedOrder && emptyHidden && !problem && Number(refilledApr) === 22) {
                pass('Money Priorities 5: three Earlier results, newest first; View re-shows the debt result and its answers');
            } else {
                fail(`Money Priorities 5: rows [${leaves.join(' | ')}], empty hidden ${emptyHidden}, ${
                    problem || 'result ok'}, APR refilled as "${refilledApr}"`);
            }
        });

        // Step 6: Delete asks first, then removes the row.
        await step('Money Priorities 6', fail, async () => {
            let asked = '';
            page.once('dialog', async (dialog) => {
                asked = dialog.message();
                await dialog.accept();
            });
            await page.locator('#mpEarlier li.mp-earlier__item', {hasText: FUND_LEAF})
                .getByRole('button', {name: /^Delete/}).click();
            await page.waitForFunction(() => document.querySelectorAll('#mpEarlier li').length === 2, null,
                {timeout: TIMEOUT});
            const leaves = await earlierLeaves(page);
            if (asked.includes('Delete') && leaves.join(' | ') === [IRA_LEAF, DEBT_LEAF].join(' | ')) {
                pass('Money Priorities 6: Delete confirms, then removes the emergency-fund row');
            } else {
                fail(`Money Priorities 6: confirm "${asked}", rows left [${leaves.join(' | ')}]`);
            }
        });

        // Step 7: a narrow window shows the path view and the "Show the whole tree" toggle.
        await step('Money Priorities 7', fail, async () => {
            await page.setViewportSize({width: 390, height: 844});
            // The cookie banner is fixed to the bottom of a narrow screen and can cover the controls; answer it first.
            const essentialOnly = page.locator('#cookieAcceptEssential');
            if (await essentialOnly.isVisible()) {
                await essentialOnly.click();
                await page.locator('#cookieBanner').waitFor({state: 'hidden', timeout: TIMEOUT});
            }
            const toggle = page.locator('#mp-diagram .mp-diagram__toggle');
            await page.locator('#mp-diagram .mp-diagram__view--path svg').waitFor({state: 'visible', timeout: TIMEOUT});
            await toggle.waitFor({state: 'visible', timeout: TIMEOUT});
            const label = ((await toggle.textContent()) || '').trim();
            const closed = await toggle.getAttribute('aria-expanded');
            await toggle.click();
            await page.locator('#mp-diagram .mp-diagram__scroll svg').waitFor({state: 'visible', timeout: TIMEOUT});
            const opened = await toggle.getAttribute('aria-expanded');
            await toggle.click();
            await page.locator('#mp-diagram .mp-diagram__view--path svg').waitFor({state: 'visible', timeout: TIMEOUT});
            const closedAgain = await toggle.getAttribute('aria-expanded');
            const pageScrolls = await page.evaluate(
                () => document.documentElement.scrollWidth > document.documentElement.clientWidth);
            if (label === 'Show the whole tree' && closed === 'false' && opened === 'true' && closedAgain === 'false'
                && !pageScrolls) {
                pass('Money Priorities 7: at 390 px the path view shows; "Show the whole tree" toggles aria-expanded');
            } else {
                fail(`Money Priorities 7: toggle "${label}", aria-expanded ${closed} -> ${opened} -> ${closedAgain}, ` +
                    `page scrolls sideways ${pageScrolls}`);
            }
        });
    } finally {
        await context.close();
    }
}

module.exports = {runMoneyPrioritiesWalkthrough};
