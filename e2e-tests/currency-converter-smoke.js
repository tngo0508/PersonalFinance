/**
 * Currency Converter smoke test (#446): a fresh Account opens Currency Converter from the sidebar, converts 1,000,000
 * Japanese yen to Vietnamese đồng at the latest rate, and sees an amount, a rate and a rate date. Run from ui-test.js as
 * Test 9g.
 *
 * Expected texts are read from Resources/i18n/en.json, so a rewording does not break the test. CI seeds yen and đồng
 * rates (E2E_SEED_RESEARCH=true), so the page converts; without them it says "Conversion unavailable", which is the
 * page working as specified, and the test reports that instead.
 */

const path = require('path');

const TIMEOUT = 15000;
const RESOURCES = path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web', 'Resources', 'i18n');
const en = require(path.join(RESOURCES, 'en.json'));

const PASSWORD = 'TestPass1!';

/**
 * Register a fresh Account and make sure it is signed in.
 * @param {import('playwright').Page} page - A page in a signed-out context.
 * @param {string} base - The Web app's base URL.
 * @returns {Promise<boolean>} True when the Account is signed in.
 */
async function registerFreshAccount(page, base) {
    const email = `e2econverter_${Date.now()}@example.com`;
    await page.goto(`${base}/Identity/Account/Register`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    await page.fill('input[type="email"]', email);
    const passwords = await page.locator('input[type="password"]').all();
    for (const input of passwords.slice(0, 2)) {
        await input.fill(PASSWORD);
    }
    await page.check('input[type="checkbox"][name="Input.AcceptTerms"]');
    await page.click('#registerSubmit');
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
    if (await page.locator('#sidebar').isVisible().catch(() => false)) {
        return true;
    }
    await page.goto(`${base}/Identity/Account/Login`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', PASSWORD);
    await page.click('#login-submit');
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
    return page.locator('#sidebar').isVisible().catch(() => false);
}

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
 * Check one condition and report it.
 * @param {boolean} ok - The condition.
 * @param {string} message - What was checked.
 * @param {string} detail - Shown when it fails.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
function check(ok, message, detail, report) {
    if (ok) {
        report.pass(message);
    } else {
        report.fail(`${message}: ${detail}`);
    }
}

/**
 * Run the smoke test.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function}} options - The suite.
 * @returns {Promise<void>}
 */
async function runCurrencyConverterSmoke({browser, base, pass, fail}) {
    const report = {pass, fail};
    const context = await browser.newContext({locale: 'en-US', extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}});
    const page = await context.newPage();
    const rateDatePrefix = en['CurrencyConverter.Result.RateDate'].split('{date}')[0].trim();
    try {
        if (!await registerFreshAccount(page, base)) {
            fail('Currency converter smoke: could not register and sign in');
            return;
        }

        // 1. The sidebar link opens the page, which shows no result before the first submit.
        await page.goto(`${base}/Dashboard`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await Promise.all([
            page.waitForNavigation({waitUntil: 'networkidle', timeout: TIMEOUT}),
            page.click('#sidebar a[href*="/CurrencyConverter"]')
        ]);
        check(await page.locator('#currency-converter-form').isVisible().catch(() => false),
            'The sidebar link opens the Currency Converter', page.url(), report);
        check(await page.locator('[data-converter-result]').count() === 0, 'No result before the first submit',
            'a result is shown', report);

        // 2. Convert 1,000,000 JPY to VND at the latest rate.
        await page.fill('#converter-amount', '1000000');
        await page.selectOption('#converter-from', 'JPY');
        await page.selectOption('#converter-to', 'VND');
        await Promise.all([
            page.waitForNavigation({waitUntil: 'networkidle', timeout: TIMEOUT}),
            page.click('#converter-submit')
        ]);
        check(page.url().includes('from=JPY') && page.url().includes('to=VND'), 'The URL holds the inputs', page.url(), report);

        if (await page.locator('[data-converter-unavailable]').count() > 0) {
            check(false, 'The ApiService has yen and đồng rates', 'it says "Conversion unavailable"', report);
            return;
        }
        const amount = await textOf(page, '[data-converter-amount]');
        check(amount.includes('₫') && /\d/.test(amount), 'The converted amount is shown in đồng', `"${amount}"`, report);
        const rate = await textOf(page, '[data-converter-rate]');
        check(rate.startsWith('1 JPY =') && rate.endsWith('VND'), 'The rate is shown as "1 JPY = … VND"', `"${rate}"`, report);
        const rateDate = await textOf(page, '[data-converter-rate-date]');
        check(rateDate.startsWith(rateDatePrefix) && /\d/.test(rateDate), 'The rate date is shown', `"${rateDate}"`, report);
    } catch (error) {
        fail(`Currency converter smoke failed: ${error.message}`);
    } finally {
        await context.close();
    }
}

module.exports = {runCurrencyConverterSmoke};
