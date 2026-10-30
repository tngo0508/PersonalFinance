/**
 * Atelier Library smoke test (#517): a guest opens /Learning, finds an entry with the filter by abbreviation and by
 * Vietnamese name, reads a Term Entry with its title, meta description and canonical link, gets a 404 for an unknown
 * slug, finds the entries in /sitemap.xml and robots.txt, and reads the entry in dark mode (key text at 4.5:1). Ask the
 * AI (#521): a guest is asked to sign in; a fresh Account opens the AI Assistant from the entry and sees the "About"
 * chip, the two starter questions and, on the reply, "AI answer, not reviewed". The chat reply is faked in the browser
 * (no AI key in E2E); the request is checked to carry only the Term's slug. Run from ui-test.js as Test 9k.
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
    check(await page.locator('[data-test-id="atelier-ask-ai-sign-in"]').isVisible()
            && !(await page.locator('[data-test-id="atelier-ask-ai-button"]').count()),
        'Atelier: a guest is asked to sign in to Ask the AI', 'no sign-in prompt', report);

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
 * Ask the AI as a fresh Account: the widget opens about the entry, and a reply is labelled as not reviewed.
 * @param {import('playwright').Browser} browser - The browser.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkAskTheAi(browser, base, report) {
    const context = await browser.newContext({ignoreHTTPSErrors: true, locale: 'en-US',
        extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}});
    try {
        const page = await context.newPage();
        if (!(await registerFreshAccount(page, base))) {
            report.fail('Atelier: could not register an Account for the Ask the AI checks');
            return;
        }
        const sent = [];
        await page.route('**/api/AiAssistant/chat', async (route) => {
            sent.push(route.request().postDataJSON());
            await route.fulfill({status: 200, contentType: 'application/json',
                body: JSON.stringify({response: 'APR is the yearly cost of a loan.', aboutTerm: apr.term})});
        });
        await page.goto(`${base}/Learning/Term/apr`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.click('[data-test-id="atelier-ask-ai-button"]');

        const about = page.locator('[data-test-id="ai-chat-about"]');
        await about.waitFor({state: 'visible', timeout: TIMEOUT});
        const aboutText = (await about.textContent()).trim();
        check(aboutText === en['Shell.Js.Ai.Term.About'].replace('{term}', apr.term),
            'Atelier: Ask the AI opens the assistant with the "About" chip', aboutText, report);
        const starters = page.locator('#ai-chat-messages [data-starter]');
        check(await starters.count() === 2, 'Atelier: Ask the AI offers two starter questions',
            `${await starters.count()} starters`, report);

        await starters.first().click();
        await page.locator('.ai-chat-message-note').waitFor({state: 'visible', timeout: TIMEOUT});
        const note = (await page.locator('.ai-chat-message-note').textContent()).trim();
        check(note === en['Shell.Js.Ai.Term.Unreviewed'], 'Atelier: the reply is labelled "AI answer, not reviewed"',
            note, report);
        const request = sent[0] || {};
        check(request.termSlug === 'apr' && !('entry' in request),
            'Atelier: the chat request carries only the Term slug', JSON.stringify(request), report);
    } finally {
        await context.close();
    }
}

/**
 * Runs the Atelier Library smoke as a guest, then Ask the AI as a fresh Account.
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
    await checkAskTheAi(browser, base, report);
}

module.exports = {runLearningSmoke};
