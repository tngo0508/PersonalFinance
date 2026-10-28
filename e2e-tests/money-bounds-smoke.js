/**
 * Money bounds smoke test (#444): a fresh Account switches the Budget Planner to Vietnamese đồng and saves a
 * 30,000,000 ₫ monthly income, which the dollar-sized bound used to reject as too large. The saved budget reads back
 * in đồng. Run from ui-test.js as Test 9i.
 *
 * Expected texts are read from Resources/i18n/en.json, so a rewording does not break the test.
 */

const path = require('path');
const {registerFreshAccount} = require('./reporting-currency-smoke');

const TIMEOUT = 15000;
const RESOURCES = path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web', 'Resources', 'i18n');
const en = require(path.join(RESOURCES, 'en.json'));

/**
 * Every validation message the Budget page shows, joined; '' when there is none.
 * @param {import('playwright').Page} page - The Budget page.
 * @returns {Promise<string>} The messages.
 */
async function errorsOf(page) {
    const texts = await page.locator('.alert-danger, .invalid-feedback, .field-validation-error').allTextContents().catch(() => []);
    return texts.map((text) => text.trim()).filter(Boolean).join(' | ');
}

/**
 * Run the smoke test.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function}} options - The suite.
 * @returns {Promise<void>}
 */
async function runMoneyBoundsSmoke({browser, base, pass, fail}) {
    const context = await browser.newContext({locale: 'en-US', extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}});
    const page = await context.newPage();
    const check = (ok, message, detail) => (ok ? pass(message) : fail(`${message}: ${detail}`));
    try {
        if (!await registerFreshAccount(page, base)) {
            fail('Money bounds smoke: could not register and sign in');
            return;
        }

        await page.goto(`${base}/Budget`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.selectOption('#CurrencyCode', 'VND');
        const incomeMax = await page.getAttribute('#MonthlyIncome', 'data-max').catch(() => '');
        check(incomeMax === '250000000000', 'Choosing đồng rescales the income bound', `data-max "${incomeMax}"`);

        await page.fill('#MonthlyIncome', '30000000');
        await page.fill('#Housing', '10000000');
        await Promise.all([
            page.waitForNavigation({waitUntil: 'networkidle', timeout: TIMEOUT}),
            page.click('#budgetSaveBtn')
        ]);

        const saved = ((await page.locator('.budget-alert.alert-success').first().textContent().catch(() => '')) || '').trim();
        check(saved.includes(en['BudgetPlanner.Message.Saved']), 'A 30,000,000 ₫ monthly income saves',
            `"${saved}" errors: ${await errorsOf(page)}`);
        const currency = await page.inputValue('#CurrencyCode').catch(() => '');
        check(currency === 'VND', 'The saved budget stays in đồng', `"${currency}"`);
        const income = await page.inputValue('#MonthlyIncome').catch(() => '');
        check(income.replace(/\D/g, '') === '30000000', 'The saved income reads back as 30,000,000', `"${income}"`);
    } catch (error) {
        fail(`Money bounds smoke failed: ${error.message}`);
    } finally {
        await context.close();
    }
}

module.exports = {runMoneyBoundsSmoke};
