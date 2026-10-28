/**
 * Theme smoke test (#479, slice #481): the one top-bar theme control. Signed out, then signed in, it drives the control
 * by mouse and keyboard, checks that the root's theme attributes follow each choice, that System follows an emulated
 * device colour scheme, that the choice survives a reload and sign-in, and that the avatar menu holds no theme items.
 * In dark mode it checks key text against its effective background (4.5:1) on the Landing Page, a sign-in page,
 * Account settings and the Net Worth help modal (slice #482). Run from ui-test.js as Test 9j.
 *
 * Expected labels are read from Resources/i18n/en.json, so a rewording does not break the test.
 */

const path = require('path');

const TIMEOUT = 15000;
const WEB = path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web');
const en = require(path.join(WEB, 'Resources', 'i18n', 'en.json'));
// Written in the repo, not axe-core (MPL-2.0 is not on the license policy's list)
const {AA_TEXT, contrastRatio, flattenBackground} = require(path.join(WEB, 'tools', 'contrast.js'));

/**
 * Key text on each page the dark contrast check visits (#482). Each selector's first visible element is measured.
 * Feature slices add their pages here.
 */
const CONTRAST_PAGES = {
    landing: ['.lp-hero__title', '.lp-hero__lead', '.lp-hero__note a', '.lp-section__title', '.lp-tool__text',
        '.lp-step__text', '.lp-privacy__list', '.lp-closing__lead', '.lp-btn--primary', '.app-footer__links a'],
    signIn: ['.auth-page__title', '.auth-page__subtitle', '.auth-page .form-label', '#forgot-password', '#login-submit'],
    accountSettings: ['.account-page__title', '.account-page__subtitle', '.account-nav__link[aria-current="page"]',
        '.account-nav__link:not([aria-current="page"])', '.account-page__content .form-label'],
    modal: ['#nwHelpModal .modal-title', '#nwHelpModal .nw-text', '#nwHelpModal .nw-help-note__title',
        '#nwHelpModal .modal-footer .btn']
};

const PASSWORD = 'TestPass1!';
const BUTTON = '[data-theme-button]';
const POPOVER = '[data-theme-popover]';

/**
 * The button's accessible name for a choice, e.g. "Theme: Dark".
 * @param {string} choice - 'light', 'dark' or 'system'.
 * @returns {string}
 */
function labelFor(choice) {
    const mode = en[`Common.Theme.${choice[0].toUpperCase()}${choice.slice(1)}`];
    return en['Common.Theme.Current'].replace('{mode}', mode);
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
 * What the page shows of the theme: the root attributes, the stored choice, and the control's state.
 * @param {import('playwright').Page} page - The page.
 * @returns {Promise<{theme: string, bsTheme: string, stored: string|null, label: string, expanded: string,
 *     open: boolean, focusOnButton: boolean, focusedChoice: string|null, checked: string[], icon: string[]}>}
 */
async function themeState(page) {
    return page.evaluate(({button, popover}) => {
        const root = document.documentElement;
        const control = document.querySelector(button);
        const panel = document.querySelector(popover);
        const active = document.activeElement;
        return {
            theme: root.getAttribute('data-theme'),
            bsTheme: root.getAttribute('data-bs-theme'),
            stored: localStorage.getItem('theme'),
            label: control.getAttribute('aria-label'),
            expanded: control.getAttribute('aria-expanded'),
            open: !panel.hidden,
            focusOnButton: active === control,
            focusedChoice: active ? active.getAttribute('data-theme-choice') : null,
            checked: Array.from(panel.querySelectorAll('[aria-checked="true"]'), (el) => el.getAttribute('data-theme-choice')),
            icon: Array.from(control.querySelectorAll('[data-theme-icon]'))
                .filter((el) => !el.hidden).map((el) => el.getAttribute('data-theme-icon'))
        };
    }, {button: BUTTON, popover: POPOVER});
}

/**
 * Wait until the root carries a theme (the device-setting change is handled asynchronously). A timeout is not an
 * error here: the check that follows reports what the page shows.
 * @param {import('playwright').Page} page - The page.
 * @param {string} theme - 'light' or 'dark'.
 * @returns {Promise<void>}
 */
async function waitForTheme(page, theme) {
    await page.waitForFunction((expected) => document.documentElement.getAttribute('data-theme') === expected, theme,
        {timeout: TIMEOUT}).catch(() => {});
}

/**
 * Drive the control by mouse and keyboard and check what each step applies.
 * @param {import('playwright').Page} page - A page showing the top bar, with the device in light mode.
 * @param {string} who - "Signed out" or "Signed in", for the messages.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function driveControl(page, who, report) {
    const button = page.locator(BUTTON);
    check(await button.isVisible().catch(() => false), `${who}: the top-bar theme control is visible`, 'not visible', report);

    // Mouse: open, choose Dark.
    await button.click();
    let state = await themeState(page);
    check(state.open && state.expanded === 'true', `${who}: clicking the control opens the popover`, JSON.stringify(state), report);
    await page.click(`${POPOVER} [data-theme-choice="dark"]`);
    state = await themeState(page);
    check(state.theme === 'dark' && state.bsTheme === 'dark' && state.stored === 'dark' && !state.open
        && state.label === labelFor('dark') && state.icon.join() === 'dark' && state.focusOnButton,
    `${who}: choosing Dark applies it, closes the popover and names the mode`, JSON.stringify(state), report);

    // Keyboard: Enter opens on the checked choice, the arrows move, Enter picks System.
    await button.focus();
    await page.keyboard.press('Enter');
    state = await themeState(page);
    check(state.open && state.focusedChoice === 'dark' && state.checked.join() === 'dark',
        `${who}: Enter opens the popover on the checked choice`, JSON.stringify(state), report);
    await page.keyboard.press('ArrowDown');
    state = await themeState(page);
    check(state.focusedChoice === 'system' && state.stored === 'dark', `${who}: ArrowDown moves to System without choosing`,
        JSON.stringify(state), report);
    await page.keyboard.press('Enter');
    state = await themeState(page);
    check(state.stored === null && state.theme === 'light' && state.label === labelFor('system')
        && state.icon.join() === 'system' && !state.open && state.focusOnButton,
    `${who}: Enter on System clears the choice, follows the light device and returns focus`, JSON.stringify(state), report);

    // Escape and an outside click close without a choice.
    await page.keyboard.press('Space');
    await page.keyboard.press('ArrowUp');
    await page.keyboard.press('Escape');
    state = await themeState(page);
    check(!state.open && state.focusOnButton && state.stored === null, `${who}: Escape closes without a choice and returns focus`,
        JSON.stringify(state), report);
    await button.click();
    // The top bar is fixed over the page's top edge, so the outside click lands just below it.
    const bar = await page.locator('header.topbar').boundingBox();
    await page.mouse.click(bar.x + 2, bar.y + bar.height + 2);
    state = await themeState(page);
    check(!state.open && state.stored === null && state.focusOnButton,
        `${who}: a click outside closes without a choice and returns focus`, JSON.stringify(state), report);

    // System follows the device live.
    await page.emulateMedia({colorScheme: 'dark'});
    await waitForTheme(page, 'dark');
    state = await themeState(page);
    check(state.theme === 'dark' && state.label === labelFor('system'), `${who}: System follows a dark device`, JSON.stringify(state), report);
    await page.emulateMedia({colorScheme: 'light'});
    await waitForTheme(page, 'light');
    state = await themeState(page);
    check(state.theme === 'light', `${who}: System follows the device back to light`, JSON.stringify(state), report);
}

/**
 * The colours of the first visible element per selector: its text colour, and the background colours of it and its
 * ancestors, outermost first. Gradients and pseudo-element overlays (the Landing Page skyline) are not read, so the
 * check measures text against the surface colour under them.
 * @param {import('playwright').Page} page - The page.
 * @param {string[]} selectors - The key text.
 * @returns {Promise<{selector: string, found: boolean, colour?: string, layers?: string[]}[]>}
 */
async function measureColours(page, selectors) {
    return page.evaluate((list) => list.map((selector) => {
        const element = Array.from(document.querySelectorAll(selector))
            .find((el) => el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden');
        if (!element) {
            return {selector, found: false};
        }
        const layers = [];
        for (let node = element; node; node = node.parentElement) {
            layers.unshift(getComputedStyle(node).backgroundColor);
        }
        return {selector, found: true, colour: getComputedStyle(element).color, layers};
    }), selectors);
}

/**
 * In dark mode, check that each key text on a page meets 4.5:1 against its effective background.
 * @param {import('playwright').Page} page - A page showing the content to check, in dark mode.
 * @param {string} name - The page, for the messages.
 * @param {string[]} selectors - The key text.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkDarkContrast(page, name, selectors, report) {
    const theme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
    check(theme === 'dark', `${name}: the page is in dark mode for the contrast check`, `data-theme=${theme}`, report);
    for (const measured of await measureColours(page, selectors)) {
        if (!measured.found) {
            report.fail(`${name}: no visible "${measured.selector}" to check contrast`);
            continue;
        }
        const background = flattenBackground(measured.layers);
        const ratio = contrastRatio(measured.colour, background);
        const shown = `rgb(${[background.r, background.g, background.b].map(Math.round).join(', ')})`;
        check(ratio >= AA_TEXT, `${name}: "${measured.selector}" meets ${AA_TEXT}:1 in dark mode`,
            `${ratio.toFixed(2)}:1 (${measured.colour} on ${shown})`, report);
    }
}

/**
 * Register a fresh Account and make sure it is signed in.
 * @param {import('playwright').Page} page - A page in a signed-out context.
 * @param {string} base - The Web app's base URL.
 * @returns {Promise<boolean>} True when the Account is signed in.
 */
async function registerFreshAccount(page, base) {
    const email = `e2etheme_${Date.now()}@example.com`;
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
 * Run the smoke test.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function}} options - The suite.
 * @returns {Promise<void>}
 */
async function runThemeSmoke({browser, base, pass, fail}) {
    const report = {pass, fail};
    const context = await browser.newContext({locale: 'en-US', colorScheme: 'light'});
    const page = await context.newPage();
    try {
        // 1. Signed out.
        await page.goto(`${base}/Identity/Account/Login`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        let state = await themeState(page);
        check(state.label === labelFor('system') && state.icon.join() === 'system' && state.theme === 'light',
            'Signed out: a new device starts on System', JSON.stringify(state), report);
        await driveControl(page, 'Signed out', report);

        // 2. The choice survives a reload.
        await page.click(BUTTON);
        await page.click(`${POPOVER} [data-theme-choice="dark"]`);
        await page.reload({waitUntil: 'networkidle', timeout: TIMEOUT});
        state = await themeState(page);
        check(state.theme === 'dark' && state.label === labelFor('dark') && state.checked.join() === 'dark',
            'Dark survives a reload', JSON.stringify(state), report);

        // Dark contrast, signed out (#482): a sign-in page and the Landing Page.
        await checkDarkContrast(page, 'Sign-in page', CONTRAST_PAGES.signIn, report);
        await page.goto(`${base}/`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Landing Page', CONTRAST_PAGES.landing, report);

        // 3. And signing in.
        if (!await registerFreshAccount(page, base)) {
            fail('Theme smoke: could not register and sign in');
            return;
        }
        state = await themeState(page);
        check(state.theme === 'dark' && state.label === labelFor('dark'), 'Dark survives signing in', JSON.stringify(state), report);

        // Dark contrast, signed in: Account settings, and a page with a modal open (the Net Worth help).
        await page.goto(`${base}/Identity/Account/Manage`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Account settings', CONTRAST_PAGES.accountSettings, report);
        await page.goto(`${base}/Networth`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.click('.nw-help-btn');
        await page.locator('#nwHelpModal.show .modal-title').waitFor({state: 'visible', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Net Worth help modal', CONTRAST_PAGES.modal, report);
        // Reload to close the modal before the avatar menu check
        await page.reload({waitUntil: 'networkidle', timeout: TIMEOUT});

        // 4. The avatar menu holds no theme items.
        await page.click('.app-user-toggle');
        await page.locator('.app-user-menu').waitFor({state: 'visible', timeout: TIMEOUT});
        const menuThemeItems = await page.locator('.app-user-menu [data-theme-choice], .app-user-menu [data-theme-button]').count();
        const menuText = await page.locator('.app-user-menu').innerText();
        check(menuThemeItems === 0 && !menuText.split('\n').map((line) => line.trim()).includes(en['Common.Theme']),
            'The avatar menu has no theme items', `${menuThemeItems} item(s); menu: ${menuText}`, report);
        await page.keyboard.press('Escape');

        // 5. Signed in: the same control.
        await driveControl(page, 'Signed in', report);
    } finally {
        await context.close();
    }
}

module.exports = {runThemeSmoke};
