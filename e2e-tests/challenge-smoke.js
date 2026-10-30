/**
 * Guild Hall Challenge smoke test (#420, slice 6): a fresh Account joins the running Challenge on the Guild Hall rail,
 * checks in with one tap, sees its private streak, and still sees the Check-in after a reload; a second Account sees
 * the same Challenge with a Join button, the shared count and no streak, so another Account's Check-ins stay private.
 * Run from ui-test.js as Test 9l.
 *
 * Only an Admin writes Challenges, and an E2E run has none, so the ApiService must store the fixture Challenge
 * (E2E_SEED_COMMUNITY=true, set by e2e-inmemory.sh and CI's E2E job). COMMUNITY_FIXTURE=true on the test process says
 * it does; without it a missing Challenge skips the checks. Expected texts are read from Resources/i18n/en.json.
 */

const path = require('path');
const {registerFreshAccount} = require('./reporting-currency-smoke');

const TIMEOUT = 15000;
const RESOURCES = path.join(__dirname, '..', 'PersonalFinance', 'src', 'PersonalFinance.Web', 'Resources', 'i18n');
const en = require(path.join(RESOURCES, 'en.json'));
// CommunityFixtureSeeder.Title in the ApiService
const FIXTURE_TITLE = 'Pack a lunch';

/**
 * The fixture Challenge's card on the Guild Hall rail.
 * @param {import('playwright').Page} page - The Guild Hall.
 * @returns {import('playwright').Locator} The card.
 */
function fixtureCard(page) {
    return page.locator('.community-challenge').filter({hasText: FIXTURE_TITLE}).first();
}

/**
 * Click a Challenge button and wait for the card community.js swaps in.
 * @param {import('playwright').Page} page - The Guild Hall.
 * @param {string} action - The button's data-challenge-action.
 * @returns {Promise<number>} The action's HTTP status.
 */
async function act(page, action) {
    const [response] = await Promise.all([
        page.waitForResponse((r) => r.url().endsWith(`/Community/${action}`), {timeout: TIMEOUT}),
        fixtureCard(page).locator(`[data-challenge-action="${action}"]`).click()
    ]);
    // The swap happens after the body is read; wait for the card to show the next state.
    await page.waitForFunction((args) => {
        const card = [...document.querySelectorAll('.community-challenge')].find((c) => c.textContent.includes(args.title));
        return card && !card.querySelector(`[data-challenge-action="${args.action}"]:not(.is-kept)`);
    }, {title: FIXTURE_TITLE, action}, {timeout: TIMEOUT}).catch(() => {});
    return response.status();
}

/**
 * Open the Guild Hall as a fresh Account.
 * @param {import('playwright').Browser} browser - The browser.
 * @param {string} base - The Web app's base URL.
 * @returns {Promise<{context: import('playwright').BrowserContext, page: import('playwright').Page, ok: boolean}>}
 *   The signed-in page, with ok false when registering failed.
 */
async function openAsNewAccount(browser, base) {
    const context = await browser.newContext({locale: 'en-US', extraHTTPHeaders: {'Accept-Language': 'en-US,en;q=0.9'}});
    const page = await context.newPage();
    const ok = await registerFreshAccount(page, base);
    if (ok) {
        await page.goto(`${base}/Community`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    }
    return {context, page, ok};
}

/**
 * Run the smoke test.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function}} options - The suite.
 * @returns {Promise<void>}
 */
async function runChallengeSmoke({browser, base, pass, fail}) {
    const check = (ok, message, detail) => (ok ? pass(message) : fail(`${message}: ${detail}`));
    const member = await openAsNewAccount(browser, base);
    let other = null;
    try {
        if (!member.ok) {
            fail('Challenge smoke: could not register and sign in');
            return;
        }
        const card = fixtureCard(member.page);
        if (!await card.isVisible().catch(() => false)) {
            if (process.env.COMMUNITY_FIXTURE) {
                fail(`Challenge smoke: no "${FIXTURE_TITLE}" Challenge on the Guild Hall; is E2E_SEED_COMMUNITY=true on the ApiService?`);
            } else {
                console.log('  (Challenge checks skipped: the ApiService did not store the fixture Challenge)');
            }
            return;
        }

        const join = card.locator('[data-challenge-action="JoinChallenge"]');
        check((await join.textContent().catch(() => '')).trim() === en['Community.Challenge.Join'],
            'Challenge: a new Account sees the running Challenge with a Join button', 'no Join button');

        const joined = await act(member.page, 'JoinChallenge');
        const checkIn = card.locator('[data-challenge-action="CheckIn"]');
        check(joined === 200 && (await checkIn.getAttribute('aria-pressed').catch(() => null)) === 'false',
            'Challenge: joining swaps in a Check-in button', `HTTP ${joined}`);
        check(await card.locator('.community-private').isVisible().catch(() => false),
            'Challenge: a member sees the private streak line', 'not visible');

        const checkedIn = await act(member.page, 'CheckIn');
        const pressed = await checkIn.getAttribute('aria-pressed').catch(() => null);
        check(checkedIn === 200 && pressed === 'true',
            `Challenge: one tap checks in ("${en['Community.Challenge.KeptDaily']}")`, `HTTP ${checkedIn}, aria-pressed ${pressed}`);
        const streak = ((await card.locator('.community-private').textContent().catch(() => '')) || '').replace(/\s+/g, ' ').trim();
        const oneDay = en['Community.Challenge.StreakDays.one'].replace('{count}', '1');
        check(streak.includes(oneDay), `Challenge: the streak reads "${oneDay}"`, `"${streak}"`);

        await member.page.reload({waitUntil: 'networkidle', timeout: TIMEOUT});
        check((await fixtureCard(member.page).locator('[data-challenge-action="CheckIn"]').getAttribute('aria-pressed')
            .catch(() => null)) === 'true', 'Challenge: the Check-in is still there after a reload', 'not kept');
        check(await fixtureCard(member.page).locator('.community-lantern.is-lit').count() >= 1,
            'Challenge: at least one lantern is lit', 'none lit');

        other = await openAsNewAccount(browser, base);
        if (!other.ok) {
            fail('Challenge smoke: could not register the second Account');
            return;
        }
        const seen = fixtureCard(other.page);
        const count = ((await seen.locator('.community-challenge-count').textContent().catch(() => '')) || '').trim();
        check(/[1-9]/.test(count), 'Challenge: another Account sees how many kept it today', `"${count}"`);
        check(await seen.locator('[data-challenge-action="JoinChallenge"]').isVisible().catch(() => false)
            && !(await seen.locator('.community-private').isVisible().catch(() => false)),
        'Challenge: another Account sees a Join button and no one else\'s streak', 'streak or Check-in state shown');
    } catch (error) {
        fail(`Challenge smoke failed: ${error.message}`);
    } finally {
        await member.context.close();
        if (other) {
            await other.context.close();
        }
    }
}

module.exports = {runChallengeSmoke};
