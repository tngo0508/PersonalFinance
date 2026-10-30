/**
 * Atelier smoke test (#517, #519): a guest opens /Learning, sees the Journey's outline with a Start the Journey link,
 * finds an entry with the filter by abbreviation and by Vietnamese name, reads a Term Entry with its title, meta
 * description and canonical link, gets a 404 for an unknown slug, finds the entries in /sitemap.xml and robots.txt,
 * is asked to sign in on a Lesson, and reads the entry in dark mode (key text at 4.5:1). A fresh Account then answers
 * Lesson 1's Check, sees its keepsake, and finds "Opens tomorrow" on Lesson 2. Unlock timing stays in the service
 * tests. Run from ui-test.js as Test 9k.
 *
 * ApiService must read the fixture content folder (Learning__ContentPath, set by e2e-inmemory.sh and CI's E2E job):
 * the real one holds no Term Entries until the content slices. Expected labels are read from Resources/i18n/en.json.
 */

const path = require('path');
const {registerFreshAccount} = require('./reporting-currency-smoke');

const TIMEOUT = 15000;
const WEB = path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web');
const en = require(path.join(WEB, 'Resources', 'i18n', 'en.json'));
const FIXTURE = path.join(__dirname, '..', 'PersonalFinance', 'tests', 'PersonalFinance.Tests', 'Learning', 'Fixtures',
    'Content');
const apr = require(path.join(FIXTURE, 'terms', 'apr.json'));
// Written in the repo, not axe-core (MPL-2.0 is not on the license policy's list)
const {AA_TEXT, contrastRatio, flattenBackground} = require(path.join(WEB, 'tools', 'contrast.js'));

const DARK_TEXT = ['[data-test-id="atelier-term"]', '[data-test-id="atelier-vi-name"]',
    '[data-test-id="atelier-one-line"]', '.atelier-reading p', '.atelier-note-heading', '.atelier-sources a',
    '[data-test-id="atelier-reviewed"]'];

/**
 * Report a check as passed or failed.
 * @param {boolean} ok - Whether the check held.
 * @param {string} message - What was checked.
 * @param {string} detail - What was seen, for a failure.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
function check(ok, message, detail, report) {
    if (ok) {
        report.pass(message);
    } else {
        report.fail(`${message} (${detail})`);
    }
}

/**
 * Whether the spine of a slug is shown.
 * @param {import('playwright').Page} page - The Library.
 * @param {string} slug - The Term Entry's slug.
 * @returns {Promise<boolean>} True when visible.
 */
function spineVisible(page, slug) {
    return page.locator(`[data-test-id="spine-${slug}"]`).isVisible();
}

/**
 * The Library and its filter, as a guest.
 * @param {import('playwright').Page} page - A signed-out page.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkLibrary(page, base, report) {
    const response = await page.goto(`${base}/Learning`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    check(response.status() === 200, 'Atelier: a guest opens /Learning', `HTTP ${response.status()}`, report);
    if (!process.env.LEARNING_FIXTURE && await page.locator('[data-test-id="atelier-empty"]').isVisible()) {
        // ApiService reads the real (empty) content folder: the empty state is the page working as specified.
        check(!(await page.locator('[data-test-id="nav-learning"]').isVisible()),
            'Atelier: an empty Library shows the "being written" note and hides the nav entry', 'nav shown', report);
        console.log('  (Atelier entry checks skipped: ApiService is not on the fixture content folder)');
        return false;
    }
    check(await page.locator('[data-test-id="stop-build-a-cushion"]').isVisible()
            && await page.locator('[data-test-id="stop-borrow-wisely"]').isVisible(),
        'Atelier: a guest sees the Journey outline', 'stops missing', report);
    check(await page.locator('[data-test-id="atelier-start"]').isVisible(),
        `Atelier: a guest sees "${en['Learning.Road.Start']}"`, 'not visible', report);
    check(await spineVisible(page, 'apr'), 'Atelier: the Library shows the fixture entries as book spines',
        'no apr spine; is Learning__ContentPath the fixture folder?', report);
    check(await page.locator('[data-test-id="nav-learning"]').isVisible(),
        `Atelier: the top bar shows "${en['Shell.Nav.Learning']}" once the Library has entries`, 'not visible', report);

    await page.fill('[data-test-id="atelier-filter"]', 'APR');
    check(await spineVisible(page, 'apr') && !(await spineVisible(page, 'emergency-fund')),
        'Atelier: the filter finds an entry by its abbreviation', 'apr hidden or emergency-fund still shown', report);

    // Typed without diacritics, as on a keyboard with no Vietnamese layout
    await page.fill('[data-test-id="atelier-filter"]', 'lai suat phan tram');
    check(await spineVisible(page, 'apr') && !(await spineVisible(page, 'credit-score')),
        'Atelier: the filter finds an entry by its Vietnamese name', 'apr hidden or credit-score still shown', report);

    await page.fill('[data-test-id="atelier-filter"]', '');
    await page.click('[data-test-id="spine-apr"]');
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
    check(page.url().endsWith('/Learning/Term/apr'), 'Atelier: a spine opens its Term Entry', page.url(), report);
    return true;
}

/**
 * The Term Entry page and its SEO tags.
 * @param {import('playwright').Page} page - The apr entry.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkEntry(page, report) {
    const term = (await page.locator('[data-test-id="atelier-term"]').textContent()).trim();
    check(term === apr.term, 'Atelier: the entry shows its Term', term, report);
    const viName = (await page.locator('[data-test-id="atelier-vi-name"]').textContent()).trim();
    check(viName === apr.vietnameseName, 'Atelier: the entry shows the Vietnamese name under the Term', viName, report);
    check(await page.locator('[data-test-id="note-us"]').isVisible(), `Atelier: the entry shows "${en['Learning.Entry.InTheUs']}"`,
        'not visible', report);
    check(await page.locator('[data-test-id="atelier-disclaimer"]').isVisible(), 'Atelier: the entry links the Disclaimer',
        'not visible', report);

    const title = await page.title();
    check(title.startsWith(apr.term), 'Atelier: the entry has its title', title, report);
    const description = await page.locator('meta[name="description"]').getAttribute('content');
    check(description === apr.oneLine, 'Atelier: the meta description is the one-liner', description, report);
    const canonical = await page.locator('link[rel="canonical"]').getAttribute('href');
    check(/^https?:\/\/[^/]+\/Learning\/Term\/apr$/.test(canonical || ''), 'Atelier: the entry has a canonical URL',
        canonical, report);
}

/**
 * The 404, the sitemap and robots.txt.
 * @param {import('playwright').Page} page - A signed-out page.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkDiscoverability(page, base, report) {
    const missing = await page.request.get(`${base}/Learning/Term/no-such-term`);
    check(missing.status() === 404, 'Atelier: an unknown slug is a 404', `HTTP ${missing.status()}`, report);

    const sitemap = await page.request.get(`${base}/sitemap.xml`);
    const sitemapText = await sitemap.text();
    check(sitemap.status() === 200 && sitemapText.includes('/Learning/Term/apr</loc>')
            && sitemapText.includes('/Learning</loc>'),
        'Atelier: /sitemap.xml lists /Learning and the entries', `HTTP ${sitemap.status()}`, report);

    await page.goto(`${base}/Learning/Lesson/build-a-cushion`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    check(await page.locator('[data-test-id="atelier-sign-in"]').isVisible()
            && !(await page.locator('[data-test-id="atelier-check"]').count()),
        'Atelier: a guest on a Lesson sees its title and a sign-in prompt, not the Check', 'gate missing', report);

    const robots = await page.request.get(`${base}/robots.txt`);
    const robotsText = await robots.text();
    check(robots.status() === 200 && /Sitemap: \S+\/sitemap\.xml/.test(robotsText),
        'Atelier: robots.txt is served and points at the sitemap', `HTTP ${robots.status()}`, report);
}

/**
 * The entry in dark mode: key text meets 4.5:1 against its effective background.
 * @param {import('playwright').Browser} browser - The browser.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkDarkMode(browser, base, report) {
    const context = await browser.newContext({ignoreHTTPSErrors: true, colorScheme: 'dark'});
    try {
        const page = await context.newPage();
        await page.goto(`${base}/Learning/Term/apr`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const theme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
        check(theme === 'dark', 'Atelier: the entry follows the dark theme', `data-theme=${theme}`, report);

        const measured = await page.evaluate((list) => list.map((selector) => {
            const element = Array.from(document.querySelectorAll(selector))
                .find((el) => el.getClientRects().length > 0);
            if (!element) {
                return {selector, found: false};
            }
            const layers = [];
            for (let node = element; node; node = node.parentElement) {
                layers.unshift(getComputedStyle(node).backgroundColor);
            }
            return {selector, found: true, colour: getComputedStyle(element).color, layers};
        }), DARK_TEXT);
        for (const item of measured) {
            if (!item.found) {
                report.fail(`Atelier: no visible "${item.selector}" to check contrast`);
                continue;
            }
            const ratio = contrastRatio(item.colour, flattenBackground(item.layers));
            check(ratio >= AA_TEXT, `Atelier: "${item.selector}" meets ${AA_TEXT}:1 in dark mode`,
                `${ratio.toFixed(2)}:1`, report);
        }
    } finally {
        await context.close();
    }
}

/**
 * A fresh Account answers Lesson 1's Check, sees its keepsake, and finds Lesson 2 opening tomorrow.
 * @param {import('playwright').Browser} browser - The browser.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkJourney(browser, base, report) {
    const context = await browser.newContext({ignoreHTTPSErrors: true, colorScheme: 'light', locale: 'en-US',
        extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}});
    try {
        const page = await context.newPage();
        if (!(await registerFreshAccount(page, base))) {
            report.fail('Atelier: could not register an Account for the Journey checks');
            return;
        }
        await page.goto(`${base}/Learning`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const first = await page.locator('[data-test-id="stop-build-a-cushion"]').getAttribute('data-state');
        check(first === 'open', 'Atelier: Lesson 1 is open for a new learner', `state=${first}`, report);

        await page.goto(`${base}/Learning/Lesson/build-a-cushion`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const questions = page.locator('[data-question]');
        const count = await questions.count();
        const finish = page.waitForResponse((r) => r.url().endsWith('/Learning/Lesson/build-a-cushion/Finish'),
            {timeout: TIMEOUT});
        for (let i = 0; i < count; i++) {
            await questions.nth(i).locator('[data-option]').first().click();
            check(await questions.nth(i).locator('[data-explanation]').isVisible(),
                `Atelier: question ${i + 1} shows its explanation once answered`, 'hidden', report);
        }
        const response = await finish;
        check(response.status() === 204, 'Atelier: answering every question finishes the Lesson',
            `HTTP ${response.status()}`, report);
        await page.locator('[data-test-id="atelier-keepsake"]').waitFor({state: 'visible', timeout: TIMEOUT});
        report.pass('Atelier: the keepsake appears once the Lesson is finished');

        await page.goto(`${base}/Learning`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const status = (await page.locator('[data-test-id="stop-status-borrow-wisely"]').textContent()).trim();
        check(status === en['Learning.Road.Status.OpensTomorrow'], 'Atelier: Lesson 2 opens tomorrow', status, report);
        check(await page.locator('[data-test-id="atelier-desk"]').isVisible(),
            'Atelier: the finished Lesson\'s keepsake is on the desk', 'no desk', report);
    } finally {
        await context.close();
    }
}

/**
 * Runs the Atelier smoke as a guest, then as a fresh Account.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function}} options - The
 *     browser, the Web app's base URL and the suite's pass and fail.
 */
async function runLearningSmoke({browser, base, pass, fail}) {
    const report = {pass, fail};
    const context = await browser.newContext({ignoreHTTPSErrors: true, colorScheme: 'light'});
    try {
        const page = await context.newPage();
        if (!(await checkLibrary(page, base, report))) {
            return;
        }
        await checkEntry(page, report);
        await checkDiscoverability(page, base, report);
    } finally {
        await context.close();
    }
    await checkDarkMode(browser, base, report);
    await checkJourney(browser, base, report);
}

module.exports = {runLearningSmoke};
