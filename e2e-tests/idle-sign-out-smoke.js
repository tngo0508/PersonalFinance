/**
 * Idle Sign-out smoke test (#332): with two tabs of one signed-in browser, the warning opens in both, "Stay signed in"
 * in one keeps the session and closes it in both, and running out sends both tabs to Login with the inactivity message
 * and their return addresses; signing back in returns to the page. Run from ui-test.js as Test 9g.
 *
 * The browser's clock is Playwright's fake clock, fast-forwarded past the limits the layout gives the script, so the
 * Web app keeps its real IdleSignOut settings and no other test runs under a short timeout. The server's cookie is not
 * fast-forwarded: the sign-out itself goes through the real sign-out.
 */

const path = require('path');

const TIMEOUT = 15000;
const RESOURCES = path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web', 'Resources', 'i18n');
const en = require(path.join(RESOURCES, 'en.json'));

const PASSWORD = 'TestPass1!';
const DIALOG = '#idleSignOutDialog';

/**
 * Register a fresh Account and make sure it is signed in.
 * @param {import('playwright').Page} page - A page in a signed-out context.
 * @param {string} base - The Web app's base URL.
 * @returns {Promise<string|null>} The Account's email when it is signed in, else null.
 */
async function registerFreshAccount(page, base) {
    const email = `e2eidle_${Date.now()}@example.com`;
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
        return email;
    }
    await page.goto(`${base}/Identity/Account/Login`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', PASSWORD);
    await page.click('#login-submit');
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
    return await page.locator('#sidebar').isVisible().catch(() => false) ? email : null;
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
 * Whether the warning dialog is open in a tab.
 * @param {import('playwright').Page} page - The tab.
 * @returns {Promise<boolean>} True when it is open.
 */
async function warningOpen(page) {
    return page.locator(DIALOG).evaluate((dialog) => dialog.open).catch(() => false);
}

/**
 * Run the Idle Sign-out smoke test.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function}} options
 */
async function runIdleSignOutSmoke({browser, base, pass, fail}) {
    const report = {pass, fail};
    const context = await browser.newContext();
    try {
        await context.clock.install();
        const first = await context.newPage();
        const email = await registerFreshAccount(first, base);
        if (!email) {
            fail('Idle Sign-out smoke: could not register and sign in a fresh Account');
            return;
        }
        const second = await context.newPage();
        await second.goto(`${base}/SavingsGoals`, {waitUntil: 'networkidle', timeout: TIMEOUT});

        const timeoutSeconds = Number(await first.getAttribute(DIALOG, 'data-timeout-seconds'));
        const warningSeconds = Number(await first.getAttribute(DIALOG, 'data-warning-seconds'));
        check(timeoutSeconds === 30 * 60 && warningSeconds === 120, 'Idle Sign-out limits reach the script from config',
            `timeout ${timeoutSeconds}s, warning ${warningSeconds}s`, report);

        // Into the warning period in both tabs.
        await context.clock.fastForward((timeoutSeconds - warningSeconds + 5) * 1000);
        await first.waitForFunction((sel) => document.querySelector(sel).open, DIALOG, {timeout: TIMEOUT}).catch(() => null);
        await second.waitForFunction((sel) => document.querySelector(sel).open, DIALOG, {timeout: TIMEOUT}).catch(() => null);
        check(await warningOpen(first) && await warningOpen(second), 'The warning opens in every tab',
            `first ${await warningOpen(first)}, second ${await warningOpen(second)}`, report);
        const countdown = ((await first.locator('#idleSignOutCountdown').textContent().catch(() => '')) || '').trim();
        check(/\d+:\d\d/.test(countdown), 'The warning shows a live countdown', `"${countdown}"`, report);

        // "Stay signed in" in one tab checks in and closes the warning in both.
        const checkIn = first.waitForResponse((r) => r.url().includes('/idle-sign-out/check-in'), {timeout: TIMEOUT});
        await first.click('#idleSignOutStay');
        const checkInStatus = (await checkIn.catch(() => null))?.status();
        await second.waitForFunction((sel) => !document.querySelector(sel).open, DIALOG, {timeout: TIMEOUT}).catch(() => null);
        check(checkInStatus === 204, '"Stay signed in" checks in with the server', `status ${checkInStatus}`, report);
        check(!await warningOpen(first) && !await warningOpen(second), '"Stay signed in" closes the warning in every tab',
            `first ${await warningOpen(first)}, second ${await warningOpen(second)}`, report);
        await first.goto(`${base}/Dashboard`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        check(await first.locator('#sidebar').isVisible().catch(() => false), '"Stay signed in" keeps the session',
            first.url(), report);

        // Running out sends every tab to Login with the message and its own return address.
        await context.clock.fastForward((timeoutSeconds + 5) * 1000);
        await Promise.all([first, second].map((page) =>
            page.waitForURL(/\/Identity\/Account\/Login\?.*idle=true/i, {timeout: TIMEOUT}).catch(() => null)));
        const expected = en['Account.Login.IdleSignedOut'].replace('{minutes}', String(timeoutSeconds / 60));
        for (const [page, returnPath] of [[first, '/Dashboard'], [second, '/SavingsGoals']]) {
            const url = new URL(page.url());
            const message = ((await page.locator('[data-test-id="idle-sign-out-message"]').textContent()
                .catch(() => '')) || '').trim();
            check(url.pathname === '/Identity/Account/Login' && message === expected,
                'Running out lands on Login with the inactivity message', `${page.url()} "${message}"`, report);
            check(url.searchParams.get('ReturnUrl') === returnPath, 'The return address is kept',
                `ReturnUrl ${url.searchParams.get('ReturnUrl')}, expected ${returnPath}`, report);
        }

        // Signing back in returns to the page.
        await second.fill('input[type="email"]', email);
        await second.fill('input[type="password"]', PASSWORD);
        await second.click('#login-submit');
        await second.waitForLoadState('networkidle', {timeout: TIMEOUT});
        check(new URL(second.url()).pathname === '/SavingsGoals', 'Signing back in returns to the page',
            second.url(), report);
    } catch (error) {
        fail(`Idle Sign-out smoke failed: ${error.message}`);
    } finally {
        await context.close();
    }
}

module.exports = {runIdleSignOutSmoke};
