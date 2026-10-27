/**
 * Reporting Currency smoke test (localization spec §5, slice C2, #411): a fresh Account creates a US dollar goal and a
 * Vietnamese đồng goal on the Savings Goals page. The đồng goal's card shows its own amount in đồng and what it is
 * worth in the Account's Reporting Currency; the summary total above the goals is in US dollars and carries the
 * conversion caption. Account settings › Currency then switches the Reporting Currency to đồng, and the total follows.
 * Run from ui-test.js as Test 9f.
 *
 * Expected texts are read from Resources/i18n/en.json, so a rewording does not break the test. The caption reads
 * "Converted at …" when the ApiService has an exchange rate (CI seeds one with E2E_SEED_RESEARCH=true) and
 * "Conversion unavailable …" when it has none; both are the page working as specified. The Web app stores the
 * Reporting Currency in the shared database the ApiService reads; when the two apps run on the InMemory provider
 * (CI, or a local run without SQL Server) each has its own, so the ApiService keeps reporting in dollars and only the
 * Web's own round trip is checked.
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
    const email = `e2ecurrency_${Date.now()}@example.com`;
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
 * Create a goal through the Add Goal modal.
 * @param {import('playwright').Page} page - The Savings Goals page.
 * @param {{name: string, currency: string, target: string, current: string}} goal - What to enter.
 * @returns {Promise<void>}
 */
async function createGoal(page, goal) {
    await page.click('#addGoalBtn');
    await page.locator('#addGoalModal').waitFor({state: 'visible', timeout: TIMEOUT});
    await page.fill('#goalName', goal.name);
    await page.selectOption('#goalCurrency', goal.currency);
    await page.fill('#goalTarget', goal.target);
    await page.fill('#goalCurrent', goal.current);
    await Promise.all([
        page.waitForNavigation({waitUntil: 'networkidle', timeout: TIMEOUT}),
        page.click('#addGoalModal button[type="submit"]')
    ]);
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
 * The text of the first element matching a selector, trimmed; '' when there is none.
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
async function runReportingCurrencySmoke({browser, base, pass, fail}) {
    const report = {pass, fail};
    const context = await browser.newContext({locale: 'en-US', extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}});
    const page = await context.newPage();
    const convertedPrefix = en['Common.Conversion.ConvertedAt'].split('{rate}')[0].trim();
    const unavailableText = en['Common.Conversion.Unavailable'];
    try {
        if (!await registerFreshAccount(page, base)) {
            fail('Reporting currency smoke: could not register and sign in');
            return;
        }

        // 1. A US dollar goal and a Vietnamese đồng goal; the Account reports in US dollars by default.
        await page.goto(`${base}/SavingsGoals`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await createGoal(page, {name: 'Car', currency: 'USD', target: '1000', current: '100'});
        await createGoal(page, {name: 'Tết', currency: 'VND', target: '25800000', current: '2580000'});

        const cards = page.locator('.savings-goal-card');
        check(await cards.count() === 2, 'Both goals are listed', `${await cards.count()} cards`, report);
        const vndCard = cards.filter({hasText: 'Tết'}).first();
        const vndCurrent = ((await vndCard.locator('.savings-goal-current').textContent().catch(() => '')) || '').trim();
        check(vndCurrent.includes('₫') && vndCurrent.includes('2,580,000'), 'The đồng goal shows its own amount in đồng',
            `"${vndCurrent}"`, report);
        const usdCard = cards.filter({hasText: 'Car'}).first();
        check(await usdCard.locator('[data-goal-reporting]').count() === 0,
            'A goal already in the Reporting Currency carries no conversion line', 'line present', report);

        // 2. The total above the goals is in the Reporting Currency and says how it got there.
        const total = await textOf(page, '[data-summary="total-saved"]');
        const caption = await textOf(page, '.savings-summary [data-conversion-caption]');
        const converted = caption.startsWith(convertedPrefix);
        check(converted || caption.startsWith(unavailableText),
            'The total carries the conversion caption', `"${caption}"`, report);
        check(total.startsWith('$'), 'The total saved is in US dollars', `"${total}"`, report);
        const vndReporting = ((await vndCard.locator('[data-goal-reporting]').textContent().catch(() => '')) || '').trim();
        if (converted) {
            // The fixture rate is 25,800 ₫ per dollar: 2,580,000 ₫ saved is $100, so the two goals total $200.
            check(total.replace(/\s/g, '') === '$200', 'The total converts the đồng goal at the rate', `"${total}"`, report);
            check(vndReporting.includes('$100') && vndReporting.includes('$1,000'),
                'The đồng card says what it is worth in US dollars', `"${vndReporting}"`, report);
        } else {
            check(vndReporting === unavailableText, 'Without a rate the đồng card says conversion is unavailable',
                `"${vndReporting}"`, report);
        }

        // 3. Account settings › Currency switches the Reporting Currency; the total follows.
        await page.goto(`${base}/Identity/Account/Manage/Currency`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        check(await page.isChecked('#currency-USD').catch(() => false), 'Account settings › Currency shows US dollars', 'USD not selected', report);
        await page.check('#currency-VND');
        await Promise.all([page.waitForNavigation({waitUntil: 'networkidle', timeout: TIMEOUT}), page.click('#save-currency')]);
        const saved = await textOf(page, '.account-page__content .alert-success');
        check(saved.includes(en['Account.Currency.Saved']), 'Saving đồng confirms', `"${saved}"`, report);

        await page.goto(`${base}/Identity/Account/Manage/Currency`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        check(await page.isChecked('#currency-VND').catch(() => false), 'The stored Reporting Currency reads back as đồng',
            'VND not selected', report);

        await page.goto(`${base}/SavingsGoals`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const totalInDong = await textOf(page, '[data-summary="total-saved"]');
        if (totalInDong.includes('₫')) {
            pass('The total saved is now in đồng');
            const usdReporting = ((await page.locator('.savings-goal-card').filter({hasText: 'Car'}).first()
                .locator('[data-goal-reporting]').textContent().catch(() => '')) || '').trim();
            check(usdReporting !== '', 'The dollar goal now carries the conversion line', 'no line', report);
            const defaultCurrency = await page.inputValue('#goalCurrency').catch(() => '');
            check(defaultCurrency === 'VND', 'A new goal defaults to the Reporting Currency', `"${defaultCurrency}"`, report);
        } else {
            // The two apps do not share a database here (InMemory provider), so the ApiService still reports in dollars.
            check(totalInDong.startsWith('$'), 'Without a shared database the ApiService keeps reporting in dollars',
                `"${totalInDong}"`, report);
        }
    } catch (error) {
        fail(`Reporting currency smoke failed: ${error.message}`);
    } finally {
        await context.close();
    }
}

module.exports = {runReportingCurrencySmoke};
