/**
 * Language switch smoke test (localization spec §5, slices L1a and L1c, #295): a signed-out visitor switches to
 * Vietnamese with the top-bar EN | VI switcher, registers, and finds the Savings Goals page in Vietnamese, including
 * the strings its JavaScript reads from the page's JSON island. The account-menu switcher then returns to English.
 * The choice is stored on the Account: Account settings › Language shows and changes it, and signing in from a fresh
 * browser brings it back. A browser that asks for Vietnamese (Accept-Language) gets it without choosing, on the
 * Landing Page and on a Legal Page, whose text stays English under the English-only notice (L3). Run from ui-test.js
 * as Test 9e.
 *
 * Expected texts are read from Resources/i18n/vi.json and en.json, so a re-translation does not break the test.
 */

const path = require('path');

const TIMEOUT = 15000;
const RESOURCES = path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web', 'Resources', 'i18n');
const vi = require(path.join(RESOURCES, 'vi.json'));
const en = require(path.join(RESOURCES, 'en.json'));

const PASSWORD = 'TestPass1!';

/**
 * Sign in with an email and password.
 * @param {import('playwright').Page} page - A page in a signed-out context.
 * @param {string} base - The Web app's base URL.
 * @param {string} email - The Account's email.
 * @returns {Promise<boolean>} True when the Account is signed in.
 */
async function signIn(page, base, email) {
    await page.goto(`${base}/Identity/Account/Login`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', PASSWORD);
    await page.click('#login-submit');
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
    return page.locator('#sidebar').isVisible().catch(() => false);
}

/**
 * Register a fresh Account and make sure it is signed in.
 * @param {import('playwright').Page} page - A page in a signed-out context.
 * @param {string} base - The Web app's base URL.
 * @returns {Promise<string|null>} The Account's email when it is signed in, else null.
 */
async function registerFreshAccount(page, base) {
    const email = `e2elanguage_${Date.now()}@example.com`;
    const password = PASSWORD;
    await page.goto(`${base}/Identity/Account/Register`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    await page.fill('input[type="email"]', email);
    const passwords = await page.locator('input[type="password"]').all();
    for (const input of passwords.slice(0, 2)) {
        await input.fill(password);
    }
    await page.check('input[type="checkbox"][name="Input.AcceptTerms"]');
    await page.click('#registerSubmit');
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});

    if (await page.locator('#sidebar').isVisible().catch(() => false)) {
        return email;
    }
    return await signIn(page, base, email) ? email : null;
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
async function runLanguageSwitchSmoke({browser, base, pass, fail}) {
    const report = {pass, fail};
    const context = await browser.newContext({locale: 'en-US', extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}});
    const page = await context.newPage();
    try {
        // 1. Signed out: the top-bar switcher changes the language and remembers it.
        await page.goto(`${base}/Identity/Account/Login`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        let lang = await page.getAttribute('html', 'lang');
        check(lang === 'en', 'Signed-out page starts in English', `html lang="${lang}"`, report);

        const viButton = page.locator('.topbar-auth .language-switcher button[data-language="vi"]');
        check(await viButton.isVisible().catch(() => false), 'Top-bar language switcher is visible', 'no VI button', report);
        await Promise.all([page.waitForLoadState('networkidle'), viButton.click()]);
        await page.waitForURL(/\/Identity\/Account\/Login/, {timeout: TIMEOUT});
        lang = await page.getAttribute('html', 'lang');
        const pressed = await page.getAttribute('.topbar-auth .language-switcher button[data-language="vi"]', 'aria-pressed');
        check(lang === 'vi' && pressed === 'true', 'Switching to VI reloads the same page in Vietnamese',
            `html lang="${lang}", aria-pressed="${pressed}"`, report);

        // 2. Signed in: Savings Goals renders in Vietnamese from server and JS strings.
        const email = await registerFreshAccount(page, base);
        if (!email) {
            fail('Language smoke: could not register and sign in');
            return;
        }
        await page.goto(`${base}/SavingsGoals`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const h1 = ((await page.locator('main h1').first().textContent().catch(() => '')) || '').trim();
        check(h1 === vi['SavingsGoals.Title'], 'Savings Goals heading is Vietnamese', `h1 "${h1}"`, report);
        const addGoal = page.locator('#addGoalBtn');
        const addText = ((await addGoal.textContent().catch(() => '')) || '').trim();
        check(addText === vi['SavingsGoals.AddGoal'], 'Add Goal button is Vietnamese', `"${addText}"`, report);
        const title = await page.title();
        check(title.startsWith(vi['SavingsGoals.Title']), 'Tab title is Vietnamese', `"${title}"`, report);

        const island = await page.evaluate(() => {
            const elements = document.querySelectorAll('script[type="application/json"][data-i18n-strings]');
            return elements.length === 0 ? null
                : Object.assign({}, ...Array.from(elements, (element) => JSON.parse(element.textContent)));
        });
        check(Boolean(island) && island['SavingsGoals.Js.ConfirmDelete'] === vi['SavingsGoals.Js.ConfirmDelete'],
            'JS string island carries the Vietnamese strings', JSON.stringify(island), report);
        check(Boolean(island) && island['Shell.Js.Search.Unavailable'] === vi['Shell.Js.Search.Unavailable'],
            'The shell island carries the Vietnamese shell strings too', JSON.stringify(island), report);
        const navDashboard = ((await page.locator('.sidebar-link[href^="/Dashboard"] .sidebar-link-text').first()
            .textContent().catch(() => '')) || '').trim();
        check(navDashboard === vi['Shell.Nav.Dashboard'], 'Sidebar navigation is Vietnamese', `"${navDashboard}"`, report);

        // Create a goal so the card text and the delete confirmation show.
        await addGoal.click();
        await page.locator('#addGoalModal').waitFor({state: 'visible', timeout: TIMEOUT});
        const goalName = `Quỹ ${Date.now()}`;
        await page.fill('#goalName', goalName);
        await page.fill('#goalTarget', '5000');
        await page.fill('#goalCurrent', '1000');
        await Promise.all([
            page.waitForNavigation({waitUntil: 'networkidle', timeout: TIMEOUT}),
            page.click('#addGoalModal button[type="submit"]')
        ]);
        const alert = ((await page.locator('.savings-alert.alert-success').textContent().catch(() => '')) || '').trim();
        check(alert.includes(vi['SavingsGoals.Message.Created']), 'Create confirmation is Vietnamese', `"${alert}"`, report);
        const remaining = ((await page.locator('.savings-goal-remaining').first().textContent().catch(() => '')) || '').trim();
        const [remainingBefore] = vi['SavingsGoals.Card.Remaining'].split('{amount}');
        check(remaining.startsWith(remainingBefore.trim()) && remaining.includes('4.000\u00A0$'),
            'Goal card uses the translated template with its amount', `"${remaining}"`, report);

        let dialogMessage = null;
        page.once('dialog', async (dialog) => {
            dialogMessage = dialog.message();
            await dialog.dismiss();
        });
        await page.locator('.savings-goal-delete-btn').first().click();
        await page.waitForTimeout(300);
        const expectedConfirm = vi['SavingsGoals.Js.ConfirmDelete'].replace('{name}', goalName);
        check(dialogMessage === expectedConfirm, 'Delete confirmation comes from PFI18n in Vietnamese',
            `"${dialogMessage}"`, report);

        // 3. The account-menu switcher goes back to English.
        await page.click('.app-user-toggle');
        const enItem = page.locator('.app-user-menu .language-switcher button[data-language="en"]');
        check(await enItem.isVisible().catch(() => false), 'Account menu has the language switcher', 'no English item', report);
        await Promise.all([page.waitForNavigation({waitUntil: 'networkidle', timeout: TIMEOUT}), enItem.click()]);
        const h1En = ((await page.locator('main h1').first().textContent().catch(() => '')) || '').trim();
        lang = await page.getAttribute('html', 'lang');
        check(lang === 'en' && h1En === en['SavingsGoals.Title'], 'Account-menu switch returns to English',
            `html lang="${lang}", h1 "${h1En}"`, report);

        // 4. Account settings › Language shows the stored choice (English, from the menu) and changes it.
        await page.goto(`${base}/Identity/Account/Manage/Language`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const storedEnglish = await page.isChecked('#language-en').catch(() => false);
        check(storedEnglish, 'Account settings › Language shows the stored English', 'English not selected', report);
        await page.check('#language-vi');
        await Promise.all([page.waitForNavigation({waitUntil: 'networkidle', timeout: TIMEOUT}), page.click('#save-language')]);
        lang = await page.getAttribute('html', 'lang');
        const saved = ((await page.locator('.account-page__content .alert-success').textContent().catch(() => '')) || '').trim();
        check(lang === 'vi' && saved.includes(vi['Account.Language.Saved']),
            'Saving Vietnamese in Account settings reloads in Vietnamese', `html lang="${lang}", "${saved}"`, report);

        // 5. The stored Language follows the Account: a fresh browser asking for English signs in to Vietnamese.
        const fresh = await browser.newContext({locale: 'en-US', extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}});
        try {
            const freshPage = await fresh.newPage();
            const signedIn = await signIn(freshPage, base, email);
            await freshPage.goto(`${base}/SavingsGoals`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const freshLang = await freshPage.getAttribute('html', 'lang');
            const freshH1 = ((await freshPage.locator('main h1').first().textContent().catch(() => '')) || '').trim();
            check(signedIn && freshLang === 'vi' && freshH1 === vi['SavingsGoals.Title'],
                'Signing in on a fresh browser restores the stored Vietnamese', `html lang="${freshLang}", h1 "${freshH1}"`, report);
        } finally {
            await fresh.close();
        }
    } catch (error) {
        fail(`Language smoke failed: ${error.message}`);
    } finally {
        await context.close();
    }

    // 6. A browser asking for Vietnamese gets it without choosing.
    const viContext = await browser.newContext({locale: 'vi-VN', extraHTTPHeaders: {'Accept-Language': 'vi-VN,vi;q=0.9'}});
    try {
        const viPage = await viContext.newPage();
        await viPage.goto(`${base}/`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const lang = await viPage.getAttribute('html', 'lang');
        check(lang === 'vi', 'Accept-Language vi picks Vietnamese for a new visitor', `html lang="${lang}"`, report);
        const hero = ((await viPage.locator('#lp-hero-title').textContent().catch(() => '')) || '').trim();
        check(hero === vi['Landing.Hero.Title'], 'Landing Page hero is Vietnamese', `"${hero}"`, report);

        // Legal text stays English (tagged lang="en") under a Vietnamese title and the English-only notice.
        await viPage.goto(`${base}/privacy`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const legalH1 = ((await viPage.locator('main h1').first().textContent().catch(() => '')) || '').trim();
        const notice = ((await viPage.locator('.legal-language-notice').textContent().catch(() => '')) || '').trim();
        const englishBody = await viPage.locator('.legal-page [lang="en"]').count().catch(() => 0);
        check(legalH1 === vi['Legal.Privacy.Title'] && notice === vi['Legal.EnglishOnlyNotice'] && englishBody === 1,
            'Privacy Policy shows a Vietnamese title, the English-only notice and English text',
            `h1 "${legalH1}", notice "${notice}", lang="en" blocks ${englishBody}`, report);
    } catch (error) {
        fail(`Language smoke (Accept-Language) failed: ${error.message}`);
    } finally {
        await viContext.close();
    }
}

module.exports = {runLanguageSwitchSmoke};
