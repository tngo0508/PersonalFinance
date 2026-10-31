/**
 * Atelier smoke test (#517, #519): a guest opens /Learning, sees the Journey's outline with a Start the Journey link,
 * finds an entry with the filter by abbreviation and by Vietnamese name, reads a Term Entry with its title, meta
 * description and canonical link (in Vietnamese, with the English Term under the Term), gets a 404 for an unknown slug, finds the entries in /sitemap.xml and robots.txt,
 * is asked to sign in on a Lesson, and reads the entry in dark mode (key text at 4.5:1). A fresh Account then answers
 * Lesson 1's Check, sees its keepsake, and finds "Opens tomorrow" on Lesson 2. Unlock timing stays in the service
 * tests. Ask the AI (#521): a guest is asked to sign in; a fresh Account opens the AI Assistant from the entry and
 * sees the "About" chip, the two starter questions and, on the reply, "AI answer, not reviewed". The chat reply is
 * faked in the browser (no AI key in E2E); the request is checked to carry only the Term's slug. Last, the fixture
 * learner (E2E_SEED_LEARNING=true on both apps), past Lesson 1 since two days ago, opens Lesson 2 in dark mode: the
 * Review (#520) shows Lesson 1's cards, a card turns with Enter and takes a mark, its text is readable, and Skip goes
 * to the Lesson. The same learner, mid-Journey with Lesson 2 open, finds the quiet Atelier card on the Dashboard
 * (#523), hides it, and brings it back with the switch on the Atelier page. Run from ui-test.js as Test 9k.
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
const aprVi = require(path.join(FIXTURE, 'terms', 'apr.vi.json'));
// Written in the repo, not axe-core (MPL-2.0 is not on the license policy's list)
const {AA_TEXT, contrastRatio, flattenBackground} = require(path.join(WEB, 'tools', 'contrast.js'));

// The fixture learner, created by the Web's LearningFixtureAccountSeeder; test values only.
const LEARNER = {email: 'e2e-atelier-learner@example.test', password: 'Atelier-E2E-2026!'};
const LESSON_ONE_TERMS = ['emergency-fund', 'compound-interest'];
const REVIEW_TEXT = ['.atelier-card-term', '.atelier-card-hint', '.atelier-card-one-line',
    '.atelier-review .atelier-lede'];

const DARK_TEXT = ['[data-test-id="atelier-term"]', '[data-test-id="atelier-one-line"]', '.atelier-reading p',
    '.atelier-note-heading', '.atelier-sources a', '[data-test-id="atelier-reviewed"]'];

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
    check(!(await page.locator('[data-test-id="atelier-en-name"]').count()),
        'Atelier: an English entry shows no second name under the Term', 'a second name is shown', report);
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

        await checkContrast(page, DARK_TEXT, report);
    } finally {
        await context.close();
    }
}

/**
 * Checks that each selector's first visible element meets 4.5:1 against its effective background.
 * @param {import('playwright').Page} page - The page.
 * @param {string[]} selectors - CSS selectors of the text to measure.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkContrast(page, selectors, report) {
    const theme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
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
        }), selectors);
    for (const item of measured) {
        if (!item.found) {
            report.fail(`Atelier: no visible "${item.selector}" to check contrast`);
            continue;
        }
        const ratio = contrastRatio(item.colour, flattenBackground(item.layers));
        check(ratio >= AA_TEXT, `Atelier: "${item.selector}" meets ${AA_TEXT}:1 in ${theme} mode`,
            `${ratio.toFixed(2)}:1`, report);
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
 * The fixture learner opens Lesson 2 in dark mode: the Review shows Lesson 1's cards, a card turns with Enter and
 * takes a "Not yet" mark (which keeps it in later runs against a kept database), its text is readable, and Skip hides
 * the Review and moves focus to the Lesson.
 * @param {import('playwright').Browser} browser - The browser.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkReview(browser, base, report) {
    const context = await browser.newContext({ignoreHTTPSErrors: true, colorScheme: 'dark', locale: 'en-US',
        extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}});
    try {
        const page = await context.newPage();
        await page.goto(`${base}/Identity/Account/Login`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.fill('input[type="email"]', LEARNER.email);
        await page.fill('input[type="password"]', LEARNER.password);
        await page.click('#login-submit');
        await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
        if (!(await page.locator('#sidebar').isVisible().catch(() => false))) {
            // Without LEARNING_FIXTURE (a run against your own apps) the fixture learner may not exist: skip.
            if (process.env.LEARNING_FIXTURE) {
                report.fail('Atelier: could not sign in as the fixture learner; is E2E_SEED_LEARNING=true on both apps?');
            }
            return;
        }

        await page.goto(`${base}/Learning/Lesson/borrow-wisely`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const access = await page.locator('[data-test-id="atelier-lesson"]').getAttribute('data-lesson-access');
        check(access === 'open', 'Atelier: Lesson 2 is open for the fixture learner', `access=${access}`, report);
        const review = page.locator('[data-test-id="atelier-review"]');
        if (!(await review.isVisible())) {
            report.fail('Atelier: Lesson 2 opens with the Review');
            return;
        }
        report.pass('Atelier: Lesson 2 opens with the Review');
        const slugs = await page.locator('[data-card]').evaluateAll((cards) =>
            cards.map((card) => card.getAttribute('data-test-id').replace('review-card-', '')));
        check(slugs.length > 0 && slugs.every((slug) => LESSON_ONE_TERMS.includes(slug)),
            'Atelier: the Review shows Lesson 1\'s cards', slugs.join(','), report);

        const card = page.locator('[data-card]').first();
        const face = card.locator('[data-card-turn]');
        await face.focus();
        await page.keyboard.press('Enter');
        check(await face.getAttribute('aria-expanded') === 'true' && await card.locator('[data-card-back]').isVisible(),
            'Atelier: Enter turns a card to its one-liner', 'not turned', report);
        await checkContrast(page, REVIEW_TEXT, report);

        const mark = page.waitForResponse((r) => r.url().includes('/Learning/Review/'), {timeout: TIMEOUT});
        await card.locator('[data-mark="false"]').click();
        const response = await mark;
        check(response.status() === 204, 'Atelier: a turned card takes a "Not yet" mark', `HTTP ${response.status()}`,
            report);
        const status = (await card.locator('[data-card-status]').textContent()).trim();
        check(status === en['Learning.Js.Review.MarkedNotYet'], 'Atelier: the mark is announced', status, report);

        await page.locator('[data-test-id="atelier-review-skip"]').click();
        const focused = await page.evaluate(() => document.activeElement && document.activeElement.id);
        check(!(await review.isVisible()) && focused === 'lessonStart',
            'Atelier: Skip hides the Review and moves to today\'s Lesson', `focused=${focused}`, report);

        await checkDashboardCard(page, base, report);
    } finally {
        await context.close();
    }
}

/**
 * The signed-in fixture learner, mid-Journey with Lesson 2 open: the Dashboard shows the quiet Atelier card with an
 * Open link and Lesson 1's keepsake, readable in dark mode; Hide removes it; the Atelier page's switch, now off, turns
 * it back on and the card returns. It ends with the card on, so a kept database starts the next run the same way.
 * @param {import('playwright').Page} page - A page signed in as the fixture learner.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkDashboardCard(page, base, report) {
    const card = page.locator('[data-test-id="dash-atelier"]');
    await page.goto(`${base}/Dashboard`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    if (!(await card.isVisible())) {
        report.fail('Atelier: the Dashboard shows the card while Lesson 2 is open');
        return;
    }
    const lesson = await card.getAttribute('data-lesson');
    const keepsakes = await card.locator('.dash-atelier__keepsake').count();
    check(lesson === 'borrow-wisely' && keepsakes === 1,
        'Atelier: the Dashboard shows the card while Lesson 2 is open, with Lesson 1\'s keepsake',
        `lesson=${lesson}, keepsakes=${keepsakes}`, report);
    const href = await card.locator('[data-test-id="dash-atelier-open"]').getAttribute('href');
    check(href === '/Learning/Lesson/borrow-wisely', 'Atelier: the card\'s Open link goes to Lesson 2', href, report);
    await checkContrast(page, ['.dash-atelier__title', '.dash-atelier__keepsakes-label', '.dash-atelier__hide'], report);

    await card.locator('[data-test-id="dash-atelier-hide"]').click();
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
    check(new URL(page.url()).pathname === '/Dashboard' && !(await card.isVisible()),
        'Atelier: Hide removes the card and stays on the Dashboard', page.url(), report);

    await page.goto(`${base}/Learning`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    const toggle = page.locator('[data-test-id="atelier-dashboard-switch"]');
    const off = await toggle.getAttribute('aria-checked');
    check(off === 'false', 'Atelier: the switch on the Atelier page shows the card is off', `aria-checked=${off}`, report);
    await toggle.click();
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
    const on = await toggle.getAttribute('aria-checked');
    check(on === 'true', 'Atelier: the switch turns the card back on', `aria-checked=${on}`, report);

    await page.goto(`${base}/Dashboard`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    check(await card.isVisible(), 'Atelier: the card is back on the Dashboard', 'not visible', report);
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
 * A Vietnamese reader sees the entry's Term in Vietnamese with the English Term under it.
 * @param {import('playwright').Browser} browser - The suite's browser.
 * @param {string} base - The Web app's base URL.
 * @param {{pass: Function, fail: Function}} report - The suite's pass and fail.
 */
async function checkEntryInVietnamese(browser, base, report) {
    const context = await browser.newContext({ignoreHTTPSErrors: true, locale: 'vi-VN'});
    try {
        const page = await context.newPage();
        await page.goto(`${base}/Learning/Term/apr`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        const term = (await page.locator('[data-test-id="atelier-term"]').textContent()).trim();
        check(term === aprVi.term, 'Atelier: a Vietnamese entry shows its Term in Vietnamese', term, report);
        const englishName = page.locator('[data-test-id="atelier-en-name"]');
        const shown = await englishName.count() ? (await englishName.textContent()).trim() : 'nothing';
        check(shown === apr.term, 'Atelier: a Vietnamese entry shows the English Term under the Term', shown, report);
    } finally {
        await context.close();
    }
}

/**
 * Runs the Atelier smoke as a guest, then as fresh Accounts.
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
    await checkEntryInVietnamese(browser, base, report);
    await checkDarkMode(browser, base, report);
    await checkJourney(browser, base, report);
    await checkAskTheAi(browser, base, report);
    await checkReview(browser, base, report);
}

module.exports = {runLearningSmoke};
