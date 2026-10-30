/**
 * Theme smoke test (#479, slice #481): the one top-bar theme control. Signed out, then signed in, it drives the control
 * by mouse and keyboard, checks that the root's theme attributes follow each choice, that System follows an emulated
 * device colour scheme, that the choice survives a reload and sign-in, and that the avatar menu holds no theme items.
 * In dark mode it checks key text against its effective background (4.5:1) on the Landing Page, a sign-in page,
 * Account settings and the Net Worth help modal (slice #482), the Net Worth page (slice #485), the Google Drive
 * sample-data notice and its Budget report and Transactions modals (slice #484), Budget, Budget Tracker and Savings
 * Goals (slice #487), the Dashboard and Money Priorities (slice #489), Community, News and the AI chat widget
 * (slice #488), and Research's Sectors and World tabs (slice #486), and that the Investment Calculator's, the Google
 * Drive Transactions timeline's, the Budget planner's Month Strip, the Net Worth runway, the Research sunburst and the
 * World map restyle on a theme switch without a reload (slices #483, #484, #487, #485 and #486).
 * Run from ui-test.js as Test 9j.
 *
 * Expected labels are read from Resources/i18n/en.json, so a rewording does not break the test.
 */

const path = require('path');

const TIMEOUT = 15000;
const WEB = path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web');
const en = require(path.join(WEB, 'Resources', 'i18n', 'en.json'));
// Written in the repo, not axe-core (MPL-2.0 is not on the license policy's list)
const {AA_TEXT, contrastRatio, flattenBackground, parseColour} = require(path.join(WEB, 'tools', 'contrast.js'));

/**
 * Key text on each page the dark contrast check visits (#482). Each selector's first visible element is measured.
 * Feature slices add their pages here.
 */
const CONTRAST_PAGES = {
    landing: ['.lp-hero__title', '.lp-hero__lead', '.lp-hero__note a', '.lp-section__title', '.lp-tool__text',
        '.lp-tool__figure', '.lp-ledger__total-figure', '.lp-more__text', '.lp-step__text', '.lp-privacy__list',
        '.lp-everyday__list', '.lp-closing__lead', '.lp-btn--primary', '.app-footer__links a'],
    signIn: ['.auth-page__title', '.auth-page__subtitle', '.auth-page .form-label', '#forgot-password', '#login-submit'],
    accountSettings: ['.account-page__title', '.account-page__subtitle', '.account-nav__link[aria-current="page"]',
        '.account-nav__link:not([aria-current="page"])', '.account-page__content .form-label'],
    // The Net Worth page (#485): the answer, the runway's legend and readout, the story's typed-in amounts, the math
    netWorth: ['.nw-title', '.nw-subtitle', '.nw-hero__lede', '.nw-view__btn[aria-pressed="true"]',
        '.nw-view__btn[aria-pressed="false"]', '.nw-runway__legend li', '.nw-math > summary', '.nw-story__sentence',
        '.nw-story__input', '.nw-years__head'],
    modal: ['#nwHelpModal .modal-title', '#nwHelpModal .nw-text', '#nwHelpModal .nw-help-note__title',
        '#nwHelpModal .modal-footer .btn'],
    // Budget, Budget Tracker and Savings Goals (#487); a fresh Account's Savings Goals shows its empty state
    budget: ['.budget-title', '.budget-subtitle', '.budget-ledger__title', '.budget-category__label',
        '.budget-remaining__label', '.budget-remaining__value', '.budget-total__label'],
    budgetTracker: ['.bt-page-header__title', '.bt-page-header__subtitle', '.bt-tabs .nav-link.active',
        '.bt-tabs .nav-link:not(.active)', '.bt-init-prompt__title', '.bt-ledger thead th',
        '.bt-section__title--expense', '.bt-section__title--income', '.bt-diff-neutral'],
    savingsGoals: ['.savings-title', '.savings-subtitle', '.savings-empty__title', '.savings-empty__text'],
    // The Dashboard and Money Priorities (#489), as a fresh Account sees them
    dashboard: ['.dash-header__eyebrow', '.dash-header__title', '.dash-header__lede'],
    moneyPriorities: ['.mp-title', '.mp-intro', '.mp-card__title'],
    // Community (the Guild Hall, seeded in E2E), News, and the AI chat widget open on the Dashboard (#488)
    community: ['.community-title', '.community-subtitle', '.community-section-title', '.community-rail-title',
        '.community-pin-guidance', '.community-btn', '.community-eyebrow', '.community-pseudonym-name'],
    news: ['.finance-hub-title', '.finance-hub-subtitle', '.finance-hub-tab-btn.active',
        '.finance-hub-tab-btn:not(.active)', '.finance-hub-search-btn'],
    chatWidget: ['.ai-chat-header h2', '.ai-chat-header p', '.ai-chat-eyebrow', '.ai-chat-welcome-text', '.ai-chat-chip',
        '.ai-chat-disclaimer'],
    // Research (#486): the Sectors sunburst (a chart view) and the World map (a map view), on the seeded snapshots
    researchSectors: ['.research-header__title', '.research-tabs__tab[aria-selected="true"]',
        '.research-tabs__tab[aria-selected="false"]', '#research-sectors-title', '.research-panel__title',
        '#research-sectors-legend .research-legend__button', '.research-view-toggle__button[aria-pressed="true"]'],
    researchWorld: ['.research-header__title', '.research-tabs__tab[aria-selected="true"]', '#research-world-subtitle',
        '#research-panel-world .research-panel__title', '#research-panel-world .research-legend__item'],
    // Google Drive on sample data (#484): the sample-data notice, then the Budget report and Transactions modals. The
    // sample spreadsheet holds one month, so the report shows its month view: one period chip, the active one.
    googleDriveNotice: ['.gd-notice strong', '.gd-notice .small', '.gd-notice .btn', '.gd-section-header__title'],
    googleDriveReport: ['#monthlyBudgetModal .bgt-modal-title', '#monthlyBudgetModal .bgt-period-btn.active',
        '#budgetPeriodTitle', '#budgetInsight', '#monthlyBudgetModal .bgt-tile-label',
        '#monthlyBudgetModal .bgt-tile-value', '#monthlyBudgetModal .bgt-card-title'],
    googleDriveTransactions: ['#transactionsModal .bgt-modal-title', '#transactionsModal .bgt-tile-label',
        '#transactionsModal .bgt-tile-value', '#transactionsModal .bgt-card-title', '#transactionsModal .bgt-card-sub']
};

// A folder link with no API key: the Google Drive page connects it on sample data
const SAMPLE_FOLDER_URL = 'https://drive.google.com/drive/folders/e2e-theme-sample';

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
 * What the Investment Calculator's canvas shows, next to the tokens it should show: the background in the top padding,
 * where only the background is drawn, and the first series at the centre of the total value's end dot.
 * @param {import('playwright').Page} page - The Investment Calculator.
 * @returns {Promise<{theme: string, marker: boolean, background: {token: string, pixel: number[]|null},
 *     series: {token: string, pixel: number[]|null}}>} marker: the page has not reloaded since the check set it.
 */
async function chartState(page) {
    return page.evaluate(() => {
        const canvas = document.getElementById('calcChart');
        const root = document.documentElement;
        const style = getComputedStyle(root);
        const dpr = window.devicePixelRatio || 1;
        const meta = canvas ? canvas._chartMeta : null;
        const pixelAt = (x, y) => (canvas && canvas.width > 0
            ? Array.from(canvas.getContext('2d').getImageData(Math.round(x * dpr), Math.round(y * dpr), 1, 1).data).slice(0, 3)
            : null);
        const last = meta && meta.points.length > 0 ? meta.points[meta.points.length - 1] : null;
        return {
            theme: root.getAttribute('data-theme'),
            marker: window.__chartThemeCheck === true,
            background: {
                token: style.getPropertyValue('--app-surface').trim(),
                pixel: canvas ? pixelAt(canvas.width / dpr / 2, 4) : null
            },
            series: {
                token: style.getPropertyValue('--app-chart-1').trim(),
                pixel: last ? pixelAt(meta.toCanvasX(last.x), meta.toCanvasY(last.y)) : null
            }
        };
    });
}

/**
 * Does a sampled canvas pixel show its token's colour?
 * @param {{token: string, pixel: number[]|null}} sample - From chartState.
 * @returns {boolean}
 */
function pixelMatchesToken(sample) {
    if (!sample.pixel || !sample.token) {
        return false;
    }
    const expected = parseColour(sample.token);
    return [expected.r, expected.g, expected.b].every((channel, i) => Math.abs(channel - sample.pixel[i]) <= 2);
}

/**
 * Does the canvas show the theme's chart background and first series?
 * @param {object} state - From chartState.
 * @returns {boolean}
 */
function chartMatchesTokens(state) {
    return pixelMatchesToken(state.background) && pixelMatchesToken(state.series);
}

/**
 * The sampled pixels, to tell two draws apart.
 * @param {object} state - From chartState.
 * @returns {string}
 */
function chartPixels(state) {
    return JSON.stringify([state.background.pixel, state.series.pixel]);
}

/**
 * Choose a theme through the top-bar control and wait for it to apply.
 * @param {import('playwright').Page} page - The page.
 * @param {string} theme - 'light' or 'dark'.
 * @returns {Promise<void>}
 */
async function chooseTheme(page, theme) {
    await page.click(BUTTON);
    await page.click(`${POPOVER} [data-theme-choice="${theme}"]`);
    await waitForTheme(page, theme);
}

/**
 * The Investment Calculator's chart takes its colours from the chart palette (#483): switching the theme restyles the
 * canvas without a reload. Leaves the page in dark mode, as it found it.
 * @param {import('playwright').Page} page - A signed-in page in dark mode.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkChartRestyles(page, base, report) {
    await page.goto(`${base}/InvestmentCalculator`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    await page.locator('#calcChart').waitFor({state: 'visible', timeout: TIMEOUT});
    await page.evaluate(() => { window.__chartThemeCheck = true; });

    const dark = await chartState(page);
    check(dark.theme === 'dark' && chartMatchesTokens(dark), 'Chart: the canvas shows the dark chart background and series',
        JSON.stringify(dark), report);

    await chooseTheme(page, 'light');
    const light = await chartState(page);
    check(light.theme === 'light' && light.marker && chartMatchesTokens(light) && chartPixels(light) !== chartPixels(dark),
        'Chart: switching to Light restyles the canvas without a reload', JSON.stringify(light), report);

    await chooseTheme(page, 'dark');
    const back = await chartState(page);
    check(back.theme === 'dark' && back.marker && chartMatchesTokens(back),
        'Chart: switching back to Dark restyles it again', JSON.stringify(back), report);
}

/**
 * The Budget planner's Month Strip and ledger, next to the token they should show: the Housing segment's and
 * swatch's colour as the browser paints them. A fresh Account's budget has no amounts, so the segment is sampled
 * by its computed style, not its width.
 * @param {import('playwright').Page} page - The Budget page.
 * @returns {Promise<{theme: string, marker: boolean, token: string, segment: string|null, swatch: string|null}>}
 *     marker: the page has not reloaded since the check set it. Colours are rgb(r, g, b) strings.
 */
async function budgetStripState(page) {
    return page.evaluate(() => {
        const root = document.documentElement;
        const hex = getComputedStyle(root).getPropertyValue('--app-budget-category-1').trim().replace('#', '');
        const full = hex.length === 3 ? hex.split('').map((c) => c + c).join('') : hex;
        const n = parseInt(full, 16);
        const segment = document.querySelector('.budget-strip__segment[data-category="Housing"]');
        const swatch = document.querySelector('.budget-row[data-category="Housing"] .budget-row__swatch');
        return {
            theme: root.getAttribute('data-theme'),
            marker: window.__chartThemeCheck === true,
            token: Number.isNaN(n) ? '' : `rgb(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255})`,
            segment: segment ? getComputedStyle(segment).backgroundColor : null,
            swatch: swatch ? getComputedStyle(swatch).backgroundColor : null
        };
    });
}

/**
 * Whether the strip and the ledger paint the token's colour.
 * @param {object} state - From budgetStripState.
 * @returns {boolean}
 */
function budgetStripMatchesToken(state) {
    return state.token !== '' && state.segment === state.token && state.swatch === state.token;
}

/**
 * The Budget planner's Month Strip and swatches take their colours from the category tokens (#487): switching the
 * theme recolours them without a reload. Leaves the page in dark mode, as it found it.
 * @param {import('playwright').Page} page - A signed-in page on the Budget page, in dark mode.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkBudgetChartRestyles(page, report) {
    await page.locator('#budgetStrip').waitFor({state: 'visible', timeout: TIMEOUT});
    await page.evaluate(() => { window.__chartThemeCheck = true; });

    const dark = await budgetStripState(page);
    check(dark.theme === 'dark' && budgetStripMatchesToken(dark), 'Budget strip: the segments hold the dark category colours',
        JSON.stringify(dark), report);

    await chooseTheme(page, 'light');
    const light = await budgetStripState(page);
    check(light.theme === 'light' && light.marker && budgetStripMatchesToken(light) && light.segment !== dark.segment,
        'Budget strip: switching to Light recolours the segments without a reload', JSON.stringify(light), report);

    await chooseTheme(page, 'dark');
    const back = await budgetStripState(page);
    check(back.theme === 'dark' && back.marker && budgetStripMatchesToken(back),
        'Budget strip: switching back to Dark recolours them again', JSON.stringify(back), report);
}

/**
 * The Net Worth runway, next to the token it should show: the Base path's stroke and the range band's fill as the
 * browser paints them, and the ledger green they come from.
 * @param {import('playwright').Page} page - The Net Worth page.
 * @returns {Promise<{theme: string, marker: boolean, token: string, base: string|null, band: string|null}>}
 *     marker: the page has not reloaded since the check set it. Colours are rgb(r, g, b) strings.
 */
async function runwayState(page) {
    return page.evaluate(() => {
        const root = document.documentElement;
        const hex = getComputedStyle(root).getPropertyValue('--lp-ledger').trim().replace('#', '');
        const full = hex.length === 3 ? hex.split('').map((c) => c + c).join('') : hex;
        const n = parseInt(full, 16);
        const base = document.querySelector('#nwRunway .nw-rw-base');
        const band = document.querySelector('#nwRunway .nw-rw-band');
        return {
            theme: root.getAttribute('data-theme'),
            marker: window.__chartThemeCheck === true,
            token: Number.isNaN(n) ? '' : `rgb(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255})`,
            base: base ? getComputedStyle(base).stroke : null,
            band: band ? getComputedStyle(band).fill : null
        };
    });
}

/**
 * Whether the runway paints the token's colour.
 * @param {object} state - From runwayState.
 * @returns {boolean}
 */
function runwayMatchesToken(state) {
    return state.token !== '' && state.base === state.token && state.band === state.token;
}

/**
 * The Net Worth runway takes its colours from the Ledger tokens through css/networth.css (#485): switching the theme
 * recolours it without a reload. Leaves the page in dark mode, as it found it.
 * @param {import('playwright').Page} page - A signed-in page on Net Worth, in dark mode.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkRunwayRestyles(page, report) {
    await page.locator('#nwRunway .nw-rw-base').waitFor({state: 'attached', timeout: TIMEOUT});
    await page.evaluate(() => { window.__chartThemeCheck = true; });

    const dark = await runwayState(page);
    check(dark.theme === 'dark' && runwayMatchesToken(dark), 'Net Worth runway: the path and band hold the dark ledger green',
        JSON.stringify(dark), report);

    await chooseTheme(page, 'light');
    const light = await runwayState(page);
    check(light.theme === 'light' && light.marker && runwayMatchesToken(light) && light.base !== dark.base,
        'Net Worth runway: switching to Light recolours it without a reload', JSON.stringify(light), report);

    await chooseTheme(page, 'dark');
    const back = await runwayState(page);
    check(back.theme === 'dark' && back.marker && runwayMatchesToken(back),
        'Net Worth runway: switching back to Dark recolours it again', JSON.stringify(back), report);
}

/**
 * The fills a Research chart or map paints, next to the theme's tokens for them: every distinct fill of the marks
 * (a hatch pattern's url() aside), and the token colours, both as the browser computes them.
 * @param {import('playwright').Page} page - The Research page on the chart's tab.
 * @param {string} marks - The marks' selector, e.g. the sunburst's arcs.
 * @param {string[]} tokens - The custom properties the fills come from.
 * @returns {Promise<{theme: string, marker: boolean, fills: string[], tokens: string[]}>} marker: the page has not
 *     reloaded since the check set it. Colours are rgb(r, g, b) strings.
 */
async function researchFillState(page, marks, tokens) {
    return page.evaluate(({marks, tokens}) => {
        const root = document.documentElement;
        const probe = document.createElement('span');
        document.body.append(probe);
        const computed = (colour) => {
            probe.style.color = '';
            probe.style.color = colour;
            return getComputedStyle(probe).color;
        };
        const style = getComputedStyle(root);
        const fills = [...new Set(Array.from(document.querySelectorAll(marks))
            .map((mark) => mark.getAttribute('fill'))
            .filter((fill) => fill && !fill.startsWith('url('))
            .map(computed))];
        const result = {
            theme: root.getAttribute('data-theme'),
            marker: window.__chartThemeCheck === true,
            fills,
            tokens: tokens.map((name) => computed(style.getPropertyValue(name).trim()))
        };
        probe.remove();
        return result;
    }, {marks, tokens});
}

/**
 * Whether a Research chart paints only its theme's token colours.
 * @param {object} state - From researchFillState.
 * @returns {boolean}
 */
function researchFillsMatchTokens(state) {
    return state.fills.length > 0 && state.fills.every((fill) => state.tokens.includes(fill));
}

/**
 * A Research chart or map takes its colours from the Research tokens (#486): switching the theme repaints it without a
 * reload. Leaves the page in dark mode, as it found it.
 * @param {import('playwright').Page} page - A signed-in page on the chart's Research tab, in dark mode.
 * @param {string} name - The chart, for the messages.
 * @param {string} marks - The marks' selector.
 * @param {string[]} tokens - The custom properties the fills come from.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkResearchRestyles(page, name, marks, tokens, report) {
    await page.locator(marks).first().waitFor({state: 'attached', timeout: TIMEOUT});
    await page.evaluate(() => { window.__chartThemeCheck = true; });

    const dark = await researchFillState(page, marks, tokens);
    check(dark.theme === 'dark' && researchFillsMatchTokens(dark), `${name}: the marks hold the dark Research colours`,
        JSON.stringify(dark), report);

    await chooseTheme(page, 'light');
    const light = await researchFillState(page, marks, tokens);
    check(light.theme === 'light' && light.marker && researchFillsMatchTokens(light)
        && JSON.stringify(light.fills) !== JSON.stringify(dark.fills),
    `${name}: switching to Light repaints it without a reload`, JSON.stringify(light), report);

    await chooseTheme(page, 'dark');
    const back = await researchFillState(page, marks, tokens);
    check(back.theme === 'dark' && back.marker && researchFillsMatchTokens(back),
        `${name}: switching back to Dark repaints it again`, JSON.stringify(back), report);
}

/**
 * The Google Drive Transactions timeline, next to the tokens it should show: its gridline colour and its spending
 * bars (the palette's second series), read from the Chart.js chart.
 * @param {import('playwright').Page} page - The Google Drive page with the Transactions modal open.
 * @returns {Promise<{theme: string, marker: boolean, grid: string|null, gridToken: string, spend: string|null,
 *     spendToken: string}>} marker: the page has not reloaded since the check set it.
 */
async function driveChartState(page) {
    return page.evaluate(() => {
        const root = document.documentElement;
        const style = getComputedStyle(root);
        const chart = window.Chart ? window.Chart.getChart('txTimelineChart') : null;
        const grid = chart ? chart.options.scales.y.grid.color : null;
        return {
            theme: root.getAttribute('data-theme'),
            marker: window.__chartThemeCheck === true,
            grid: typeof grid === 'function' ? grid({tick: {value: 1}}) : grid,
            gridToken: style.getPropertyValue('--app-chart-grid').trim(),
            spend: chart ? chart.data.datasets[0].backgroundColor : null,
            spendToken: style.getPropertyValue('--app-chart-2').trim()
        };
    });
}

/**
 * Does the Transactions timeline carry the theme's gridline and spending colours?
 * @param {object} state - From driveChartState.
 * @returns {boolean}
 */
function driveChartMatchesTokens(state) {
    const same = (a, b) => {
        const x = a && parseColour(a);
        const y = b && parseColour(b);
        return Boolean(x && y) && x.r === y.r && x.g === y.g && x.b === y.b;
    };
    return same(state.grid, state.gridToken) && same(state.spend, state.spendToken);
}

/**
 * Google Drive on sample data (#484): the sample-data notice, the Budget report modal and the Transactions modal are
 * readable in dark mode, and the Transactions timeline restyles on a theme switch without a reload. The sample
 * spreadsheet holds one month, so the report opens in its month view, which draws no chart; the chart check uses the
 * timeline, which the sample transactions do draw, reached through the report's Transactions button. Leaves the page
 * in dark mode.
 * @param {import('playwright').Page} page - A signed-in page in dark mode.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkGoogleDrive(page, base, report) {
    await page.goto(`${base}/GoogleDrive?folderUrl=${encodeURIComponent(SAMPLE_FOLDER_URL)}`,
        {waitUntil: 'networkidle', timeout: TIMEOUT});
    await checkDarkContrast(page, 'Google Drive sample data', CONTRAST_PAGES.googleDriveNotice, report);

    await page.locator('.btn-budget-report').first().click();
    await page.locator('#monthlyBudgetModal.show #budgetReportContent:not(.d-none) #budgetPeriodTitle')
        .waitFor({state: 'visible', timeout: TIMEOUT});
    await checkDarkContrast(page, 'Google Drive Budget report', CONTRAST_PAGES.googleDriveReport, report);

    await page.locator('#btnBudgetToTransactions').click();
    await page.locator('#transactionsModal.show #txContent:not(.d-none) #txTimelineChart')
        .waitFor({state: 'visible', timeout: TIMEOUT});
    await page.waitForFunction(() => Boolean(window.Chart && window.Chart.getChart('txTimelineChart')), null,
        {timeout: TIMEOUT}).catch(() => {});
    await checkDarkContrast(page, 'Google Drive Transactions', CONTRAST_PAGES.googleDriveTransactions, report);

    await page.evaluate(() => { window.__chartThemeCheck = true; });
    const dark = await driveChartState(page);
    check(dark.theme === 'dark' && driveChartMatchesTokens(dark), 'Transactions timeline: the chart shows the dark palette',
        JSON.stringify(dark), report);
    // The theme control sits in the top bar, behind the open modal, so the switch goes through the theme module
    await page.evaluate(() => window.ThemeMode.setChoice('light'));
    await waitForTheme(page, 'light');
    const light = await driveChartState(page);
    check(light.theme === 'light' && light.marker && driveChartMatchesTokens(light) && light.grid !== dark.grid,
        'Transactions timeline: switching to Light restyles the chart without a reload', JSON.stringify(light), report);
    await page.evaluate(() => window.ThemeMode.setChoice('dark'));
    await waitForTheme(page, 'dark');
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
        await chooseTheme(page, 'dark');
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
        // Net Worth (#485): dark contrast on the page, and the runway recolours on a switch
        await checkDarkContrast(page, 'Net Worth', CONTRAST_PAGES.netWorth, report);
        await checkRunwayRestyles(page, report);
        await page.click('.nw-help-btn');
        await page.locator('#nwHelpModal.show .modal-title').waitFor({state: 'visible', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Net Worth help modal', CONTRAST_PAGES.modal, report);
        // Google Drive on sample data, with the Budget report and then the Transactions modal open (#484)
        await checkGoogleDrive(page, base, report);
        // A chart restyles on a theme switch (#483); leaving the page also closes the modal before the avatar menu check
        await checkChartRestyles(page, base, report);

        // Budget, Budget Tracker and Savings Goals (#487): dark contrast, and the Month Strip recolours on a switch.
        await page.goto(`${base}/Budget`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Budget', CONTRAST_PAGES.budget, report);
        await checkBudgetChartRestyles(page, report);
        await page.goto(`${base}/BudgetTracker`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Budget Tracker', CONTRAST_PAGES.budgetTracker, report);
        await page.goto(`${base}/SavingsGoals`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Savings Goals', CONTRAST_PAGES.savingsGoals, report);

        // The Dashboard and Money Priorities (#489): dark contrast.
        await page.goto(`${base}/Dashboard`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Dashboard', CONTRAST_PAGES.dashboard, report);
        await page.goto(`${base}/MoneyPriorities`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Money Priorities', CONTRAST_PAGES.moneyPriorities, report);

        // Research (#486): dark contrast, and the Sectors sunburst and the World map repaint on a switch.
        const offices = Array.from({length: 8}, (_, i) => `--app-research-office-${i + 1}`).concat('--app-text-faint');
        const worldSteps = Array.from({length: 8}, (_, i) => `--app-research-world-step-${i + 1}`)
            .concat('--app-research-world-nodata');
        await page.goto(`${base}/Research?tab=sectors`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Research Sectors', CONTRAST_PAGES.researchSectors, report);
        await checkResearchRestyles(page, 'Research sunburst', '#research-sunburst path.research-sunburst__arc', offices, report);
        await page.goto(`${base}/Research?tab=world`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Research World', CONTRAST_PAGES.researchWorld, report);
        await checkResearchRestyles(page, 'Research World map', '#research-world-map path.research-world__country', worldSteps,
            report);

        // Community, News and the AI chat widget (#488): dark contrast, the widget open on the Dashboard.
        await page.goto(`${base}/Community`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'Community', CONTRAST_PAGES.community, report);
        await page.goto(`${base}/News`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'News', CONTRAST_PAGES.news, report);
        await page.goto(`${base}/Dashboard`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await checkDarkContrast(page, 'AI chat launcher', ['.ai-chat-launcher-label'], report);
        // The cookie banner sits over the launcher in this fresh context; take the privacy-preserving choice
        if (await page.locator('#cookieAcceptEssential').isVisible().catch(() => false)) {
            await page.click('#cookieAcceptEssential');
        }
        await page.click('#ai-chat-button');
        // The welcome and its chips render a frame after the panel opens
        await page.locator('#ai-chat-panel .ai-chat-chip').first().waitFor({state: 'visible', timeout: TIMEOUT});
        await checkDarkContrast(page, 'AI chat widget', CONTRAST_PAGES.chatWidget, report);
        await page.click('#ai-chat-close');

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
