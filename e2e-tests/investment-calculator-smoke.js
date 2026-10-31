/**
 * Investment Calculator smoke test (spec #587): a fresh Account opens the calculator; the summary bar and the D3
 * balance chart show a projection; moving the monthly contribution slider changes the headline balance; a rate preset
 * sets the assumed return; the fee tile reacts to a higher fund fee; and the Today's money switch shows the headline
 * and the year-by-year table in today's money; the goal tile returns a monthly contribution that "Use this amount"
 * puts in the rail; on Compare what-ifs a lever previews a dashed line and joins the comparison. Run from ui-test.js as
 * Test 9m. A later slice extends it: a saved scenario that survives a reload.
 *
 * Expected texts are read from Resources/i18n/en.json, so a rewording does not break the test.
 */

const path = require('path');
const {registerFreshAccount} = require('./reporting-currency-smoke');

const TIMEOUT = 15000;
const en = require(path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web', 'Resources', 'i18n', 'en.json'));

/**
 * The text of the first element matching a selector, trimmed; '' when there is none.
 * @param {import('playwright').Page} page - The page.
 * @param {string} selector - A CSS selector.
 * @returns {Promise<string>} The text.
 */
async function textOf(page, selector) {
    return ((await page.locator(selector).first().textContent().catch(() => '')) || '').trim();
}

/**
 * Wait until an element's text differs from the given text.
 * @param {import('playwright').Page} page - The page.
 * @param {string} selector - A CSS selector.
 * @param {string} before - The text it had.
 * @returns {Promise<string>} The new text, or the old one after the timeout.
 */
async function changedText(page, selector, before) {
    await page.waitForFunction(
        ([sel, old]) => (document.querySelector(sel)?.textContent || '').trim() !== old,
        [selector, before], {timeout: TIMEOUT}).catch(() => {});
    return textOf(page, selector);
}

/**
 * Run the smoke test.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function}} options - The suite.
 * @returns {Promise<void>}
 */
async function runInvestmentCalculatorSmoke({browser, base, pass, fail}) {
    const check = (ok, message, detail) => (ok ? pass(message) : fail(`${message}: ${detail}`));
    const context = await browser.newContext({
        locale: 'en-US',
        viewport: {width: 1366, height: 900},
        extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}
    });
    const page = await context.newPage();
    try {
        if (!await registerFreshAccount(page, base)) {
            fail('Investment Calculator smoke: could not register and sign in');
            return;
        }

        await page.goto(`${base}/InvestmentCalculator`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.locator('#icBalanceChart svg path.ic-series').first().waitFor({state: 'attached', timeout: TIMEOUT});

        // 1. The page opens with a projection: a balance in dollars and a drawn balance chart.
        const opening = await textOf(page, '#icFinal');
        check(/^\$\d{1,3}(,\d{3})+$/.test(opening), 'The summary bar shows the balance in dollars', `"${opening}"`);
        const series = await page.locator('#icBalanceChart path.ic-series').count();
        check(series === 3, "The balance chart draws the balance, what was put in and today's money", `${series} series`);
        check((await textOf(page, '#icMilestone')).length > 0, 'The balance tile says when growth overtakes deposits', 'empty');

        // 2. Moving the monthly contribution slider changes the results.
        const slider = page.locator('#icMonthlyRange');
        await slider.focus();
        for (let i = 0; i < 5; i++) {
            await page.keyboard.press('ArrowRight');
        }
        const moved = await changedText(page, '#icFinal', opening);
        check(moved !== opening, 'Moving the monthly contribution slider changes the balance', `"${opening}" → "${moved}"`);
        const field = await page.inputValue('#icMonthly');
        check(field === await slider.inputValue(), 'The number field follows the slider', `field ${field}`);

        // 3. A rate preset sets the assumed return.
        await page.click('[data-rate-preset="10"]');
        const rate = await page.inputValue('#icRate');
        const pressed = await page.getAttribute('[data-rate-preset="10"]', 'aria-pressed');
        check(rate === '10' && pressed === 'true', `The ${en['InvestmentCalculator.Preset.Aggressive']} preset sets a 10% assumed return`,
            `rate ${rate}, aria-pressed ${pressed}`);

        // 4. A higher fund fee costs more.
        const feeBefore = await textOf(page, '#icFeeCost');
        await page.click('.ic-more__summary');
        await page.fill('#icFee', '1');
        const feeAfter = await changedText(page, '#icFeeCost', feeBefore);
        const amount = (text) => Number(text.replace(/[^\d]/g, ''));
        check(amount(feeAfter) > amount(feeBefore), 'A 1% fund fee costs more than the 0.1% default',
            `"${feeBefore}" → "${feeAfter}"`);

        // 5. Today's money shows the headline at today's prices: less than in future money, and the label says so.
        const nominal = await textOf(page, '#icFinal');
        const rowsBefore = await page.locator('#icYearRows tr').count();
        await page.locator('label[for="icTodaysMoney"]').click();
        const real = await changedText(page, '#icFinal', nominal);
        check(amount(real) > 0 && amount(real) < amount(nominal), "Today's money shows a smaller headline balance",
            `"${nominal}" → "${real}"`);
        const heroLabel = await textOf(page, '#icFinalLabel');
        check(heroLabel.toLowerCase().includes('today'), "The headline label says it is in today's money", `"${heroLabel}"`);
        check(await textOf(page, '#icOther') === nominal, 'The bar keeps the future-money balance beside it',
            `"${await textOf(page, '#icOther')}"`);
        check(rowsBefore > 1 && await textOf(page, '#icYearOther') === en['InvestmentCalculator.Js.Money.Future'],
            "The year-by-year table lists every year and switches to today's money", `${rowsBefore} rows`);

        // 6. The goal tile works out the monthly contribution a goal needs, and "Use this amount" puts it in the rail.
        const answerBefore = await textOf(page, '#icGoalAnswer');
        await page.locator('label[for="icGoalModeMonthly"]').click();
        await page.fill('#icGoal', '2000000');
        const answer = await changedText(page, '#icGoalAnswer', answerBefore);
        const perMonth = new RegExp(`^${en['InvestmentCalculator.Js.Goal.Monthly'].replace('{amount}', '\\$[\\d,]+')}$`);
        check(perMonth.test(answer), 'The goal tile returns a monthly contribution', `"${answer}"`);
        await page.click('#icGoalUse');
        const used = await page.inputValue('#icMonthly');
        check(used === String(amount(answer)), '"Use this amount" sets the monthly contribution in the rail',
            `answer "${answer}", monthly field ${used}`);
        await page.waitForFunction(() => document.getElementById('icGoalProgress')?.getAttribute('aria-valuenow') === '100',
            null, {timeout: TIMEOUT}).catch(() => {});
        const progress = await page.getAttribute('#icGoalProgress', 'aria-valuenow');
        check(progress === '100', 'With that amount the plan reaches the goal', `aria-valuenow ${progress}`);
        await page.locator('#icBalanceChart .ic-goal-line').first().waitFor({state: 'attached', timeout: TIMEOUT}).catch(() => {});
        const goalLines = await page.locator('#icBalanceChart .ic-goal-line').count();
        check(goalLines === 1, 'The balance chart draws the goal line', `${goalLines} goal lines`);

        // 7. Compare what-ifs: a lever previews as a dashed line without changing the plan, then joins the comparison.
        await page.click('#icViewCompareTab');
        await page.waitForFunction(() => !document.getElementById('icCompare')?.hidden, null, {timeout: TIMEOUT}).catch(() => {});
        check(new URL(page.url()).searchParams.get('view') === 'compare', 'The Compare view is kept in the query string', page.url());
        const planBefore = await textOf(page, '#icFinal');
        const monthlyBefore = await page.inputValue('#icMonthly');
        await page.click('[data-lever="monthly"]');
        await page.locator('#icCompareChart path.ic-series[stroke-dasharray]').first()
            .waitFor({state: 'attached', timeout: TIMEOUT}).catch(() => {});
        const ghosts = await page.locator('#icCompareChart path.ic-series[stroke-dasharray]').count();
        check(ghosts === 1, 'A lever previews a dashed line on the comparison chart', `${ghosts} dashed lines`);
        check(await textOf(page, '#icFinal') === planBefore && await page.inputValue('#icMonthly') === monthlyBefore,
            'The preview leaves the plan unchanged', `monthly ${await page.inputValue('#icMonthly')}`);
        const more = en['InvestmentCalculator.Js.Diff.More'].replace('{amount}', '');
        check((await textOf(page, '#icPreviewText')).includes(more.trim()), 'The preview says how much more it ends with',
            `"${await textOf(page, '#icPreviewText')}"`);
        await page.click('#icPreviewAdd');
        await page.locator('#icCmpList .ic-cmp__item').nth(1).waitFor({state: 'attached', timeout: TIMEOUT}).catch(() => {});
        const items = await page.locator('#icCmpList .ic-cmp__item').count();
        const lines = await page.locator('#icCompareChart path.ic-series').count();
        check(items === 2 && lines === 2, 'The variant appears in the comparison beside the plan', `${items} rows, ${lines} lines`);
        check((await textOf(page, '#icCmpList .ic-cmp__item--1 .ic-cmp__diff')).includes(more.trim()),
            'The variant shows its difference from the plan', `"${await textOf(page, '#icCmpList .ic-cmp__item--1 .ic-cmp__diff')}"`);
        await page.click('#icViewOverviewTab');

        // 8. The educational-estimate note links to the Disclaimer.
        const disclaimer = await page.locator('.ic-disclaimer a').getAttribute('href').catch(() => null);
        check(disclaimer === '/disclaimer', 'The estimate note links to the Disclaimer', `${disclaimer}`);
    } catch (error) {
        fail(`Investment Calculator smoke failed: ${error.message}`);
    } finally {
        await context.close();
    }
}

module.exports = {runInvestmentCalculatorSmoke};
