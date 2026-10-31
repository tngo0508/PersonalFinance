/**
 * Investment Calculator smoke test (spec #587): a fresh Account opens the calculator; the summary bar and the D3
 * balance chart show a projection; moving the monthly contribution slider changes the headline balance; a rate preset
 * sets the assumed return; and the fee tile reacts to a higher fund fee. Run from ui-test.js as Test 9m.
 * Later slices extend it: Reach a goal, a lever previewed into Compare, a saved scenario that survives a reload.
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
        check(series === 2, 'The balance chart draws the balance and what was put in', `${series} series`);
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

        // 5. The educational-estimate note links to the Disclaimer.
        const disclaimer = await page.locator('.ic-disclaimer a').getAttribute('href').catch(() => null);
        check(disclaimer === '/disclaimer', 'The estimate note links to the Disclaimer', `${disclaimer}`);
    } catch (error) {
        fail(`Investment Calculator smoke failed: ${error.message}`);
    } finally {
        await context.close();
    }
}

module.exports = {runInvestmentCalculatorSmoke};
