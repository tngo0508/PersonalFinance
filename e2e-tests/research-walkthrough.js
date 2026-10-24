/**
 * Research first-session walkthrough (spec #217, Testing Decisions): the 11 steps a new Account takes through the
 * Sectors, Funds and World tabs, then the Companies tab's 8 steps (spec #242, Company 1 to 8). Run from ui-test.js as
 * Test 9c.
 *
 * Needs the ApiService started in Development with E2E_SEED_RESEARCH=true, which stores small fixture snapshots
 * (PersonalFinance.ApiService/Features/Research/ResearchFixtureSeeder.cs) and answers company lookups from a made-up
 * SEC (Features/Research/Adapters/FixtureSecCompanyClient.cs: EXMP Example Corp, EXFD a fund, EXGL an IFRS filer),
 * and the Web app with E2E_DISABLE_EMAIL_CONFIRMATION=true so the fresh Account can sign in. No request leaves the two
 * apps.
 */

const TIMEOUT = 15000;

/**
 * Register a fresh Account and make sure it is signed in.
 * @param {import('playwright').Page} page - A page in a new, signed-out context.
 * @param {string} base - The Web app's base URL.
 * @returns {Promise<boolean>} True when the Account is signed in.
 */
async function registerFreshAccount(page, base) {
    const email = `e2eresearch_${Date.now()}@example.com`;
    const password = 'TestPass1!';
    await page.goto(`${base}/Identity/Account/Register`, {waitUntil: 'networkidle', timeout: TIMEOUT});
    await page.fill('input[type="email"]', email);
    const passwords = await page.locator('input[type="password"]').all();
    for (const input of passwords.slice(0, 2)) {
        await input.fill(password);
    }
    await page.check('input[type="checkbox"][name="Input.AcceptTerms"]');
    await page.click('#registerSubmit');
    await page.waitForLoadState('networkidle', {timeout: TIMEOUT});

    if (!await page.locator('#sidebar').isVisible().catch(() => false)) {
        await page.goto(`${base}/Identity/Account/Login`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.fill('input[type="email"]', email);
        await page.fill('input[type="password"]', password);
        await page.click('#login-submit');
        await page.waitForLoadState('networkidle', {timeout: TIMEOUT});
    }
    return page.locator('#sidebar').isVisible().catch(() => false);
}

/**
 * The period labels a tab's data island holds, oldest first.
 * @param {import('playwright').Page} page - A page showing /Research.
 * @param {string} tabId - sectors, funds or world.
 * @returns {Promise<string[]>} The period labels, or [] when the island is missing.
 */
async function periodsOf(page, tabId) {
    return page.evaluate((id) => {
        const island = document.getElementById(`research-${id}-data`);
        return island ? (JSON.parse(island.textContent).periods || []) : [];
    }, tabId);
}

/**
 * The text of a tab's scrubber period label.
 * @param {import('playwright').Page} page - A page showing /Research.
 * @param {string} tabId - sectors, funds or world.
 * @returns {Promise<string>} The label, trimmed.
 */
async function periodLabel(page, tabId) {
    const text = await page.locator(`#research-panel-${tabId} [data-research-period]`).textContent();
    return (text || '').trim();
}

/**
 * Whether the page scrolls sideways (wider than the viewport).
 * @param {import('playwright').Page} page - The page.
 * @returns {Promise<boolean>} True when there is horizontal page scroll.
 */
async function scrollsSideways(page) {
    return page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
}

/**
 * Run one step, failing it (not the whole walkthrough) when it throws.
 * @param {string} name - The step's name.
 * @param {Function} fail - The suite's fail().
 * @param {Function} body - The step.
 * @returns {Promise<void>}
 */
async function step(name, fail, body) {
    try {
        await body();
    } catch (error) {
        fail(`${name}: ${error.message.split('\n')[0]}`);
    }
}

/**
 * The walkthrough. Every check reports through pass/fail.
 * @param {{browser: import('playwright').Browser, base: string, pass: Function, fail: Function,
 *          consoleErrors: string[], pageErrors: string[]}} suite - The ui-test.js suite.
 * @returns {Promise<void>}
 */
async function runResearchWalkthrough({browser, base, pass, fail, consoleErrors, pageErrors}) {
    const context = await browser.newContext({ignoreHTTPSErrors: true, viewport: {width: 1280, height: 800}});
    const watch = (p) => {
        p.on('console', (msg) => {
            if (msg.type() === 'error') {
                consoleErrors.push(msg.text());
            }
        });
        p.on('pageerror', (error) => pageErrors.push(error.message));
        return p;
    };
    const page = watch(await context.newPage());
    try {
        // Step 1: register a fresh Account and open Research from the sidebar.
        if (!await registerFreshAccount(page, base)) {
            fail('Research 1: could not register and sign in a fresh Account');
            return;
        }
        await step('Research 1', fail, async () => {
            await page.goto(`${base}/Dashboard`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.locator('#sidebar a.sidebar-link', {hasText: 'Research'}).click();
            await page.waitForURL(/\/Research/, {timeout: TIMEOUT});
            await page.waitForSelector('#research-sunburst svg', {timeout: TIMEOUT});
            const active = await page.locator('#sidebar a.sidebar-link.active', {hasText: 'Research'}).count();
            if (active === 1) {
                pass('Research 1: opened from the sidebar, which marks Research active');
            } else {
                fail('Research 1: the sidebar does not mark Research active');
            }
        });

        const sectorsPeriods = await periodsOf(page, 'sectors');
        if (sectorsPeriods.length < 2) {
            fail(`Research: the Sectors snapshot has ${sectorsPeriods.length} periods; is E2E_SEED_RESEARCH=true on the ApiService?`);
            return;
        }
        const latest = sectorsPeriods[sectorsPeriods.length - 1];
        const earliest = sectorsPeriods[0];

        // Step 2: Sectors opens on the latest year, says "Public float" and credits its source.
        await step('Research 2', fail, async () => {
            const label = await periodLabel(page, 'sectors');
            const title = await page.locator('#research-panel-sectors .research-card__title').textContent();
            const source = await page.locator('#research-panel-sectors .research-card__source').textContent();
            if (label === latest && /Public float/.test(title) && /Source: SEC EDGAR/.test(source)) {
                pass(`Research 2: Sectors shows ${latest} with "Public float" wording and a source credit`);
            } else {
                fail(`Research 2: label "${label}", title "${title}", source "${source}"`);
            }
        });

        // Step 3: drag the slider to the earliest year; the label and the live region follow.
        await step('Research 3', fail, async () => {
            const box = await page.locator('#research-sectors-year').boundingBox();
            await page.mouse.move(box.x + box.width - 4, box.y + box.height / 2);
            await page.mouse.down();
            await page.mouse.move(box.x - 20, box.y + box.height / 2, {steps: 8});
            await page.mouse.up();
            const label = await periodLabel(page, 'sectors');
            const live = await page.locator('#research-live').textContent();
            if (label === earliest && live.startsWith(`${earliest}:`)) {
                pass(`Research 3: dragging to the start shows ${earliest} and announces it`);
            } else {
                fail(`Research 3: label "${label}", live region "${live}"`);
            }
        });

        // Step 4: play runs to the latest year and stops.
        await step('Research 4', fail, async () => {
            const play = page.locator('#research-panel-sectors [data-research-play]');
            await play.click();
            await page.waitForFunction((last) => {
                const panel = document.getElementById('research-panel-sectors');
                return panel.querySelector('[data-research-period]').textContent.trim() === last &&
                    panel.querySelector('[data-research-play]').dataset.state === 'paused';
            }, latest, {timeout: TIMEOUT});
            pass(`Research 4: play advances to ${latest} and stops`);
        });

        // Step 5: the Technology highlight.
        await step('Research 5', fail, async () => {
            const toggle = page.locator('#research-tech-toggle');
            await toggle.click();
            const pressed = await toggle.getAttribute('aria-pressed');
            const share = page.locator('#research-tech-share');
            if (pressed === 'true' && await share.isVisible() && (await share.textContent()).trim().length > 0) {
                pass('Research 5: "Technology (incl. chip makers)" lights up and states its share');
            } else {
                fail(`Research 5: aria-pressed "${pressed}", share visible ${await share.isVisible()}`);
            }
        });

        // Step 6: zoom into an office from the legend; the breadcrumb shows it and its "All filers" link returns.
        await step('Research 6', fail, async () => {
            const office = page.locator('#research-sectors-legend button').first();
            const officeName = (await office.textContent()).trim();
            const current = page.locator('#research-sectors-breadcrumb [aria-current="location"]');
            await office.click();
            const zoomedIn = await office.getAttribute('aria-pressed') === 'true' &&
                (await current.textContent()).startsWith(officeName);
            await page.locator('#research-sectors-breadcrumb .research-breadcrumb__link', {hasText: 'All filers'}).click();
            const zoomedOut = (await current.textContent()).startsWith('All filers') &&
                await office.getAttribute('aria-pressed') === 'false' &&
                await page.locator('#research-sectors-breadcrumb .research-breadcrumb__link').count() === 0;
            if (zoomedIn && zoomedOut) {
                pass(`Research 6: a legend button zooms into ${officeName}; the breadcrumb's "All filers" link returns`);
            } else {
                fail(`Research 6: zoomed in ${zoomedIn}, zoomed out ${zoomedOut}`);
            }
        });

        // Step 6a: the takeaway cards follow the zoom, and the tooltip gives the share of all filers and a rank.
        await step('Research 6a', fail, async () => {
            const cards = page.locator('#research-sectors-takeaways .research-takeaway');
            const labels = await cards.locator('.research-takeaway__label').allTextContents();
            const totalNote = page.locator('#research-sectors-takeaways .research-takeaway').first()
                .locator('.research-takeaway__note');
            const atRoot = labels[0] === 'Total' && labels.includes('Largest part') &&
                (await totalNote.textContent()).startsWith('All filers');
            await page.locator('#research-sectors-legend button').first().click();
            const zoomedNote = (await totalNote.textContent()).includes('of all filers');
            // The chart hides its tooltip on scroll, and the legend click may have scrolled: bring the chart into view
            // and let any scroll event fire first. A ring segment's bounding-box centre often lies off the arc, so the
            // pointer event goes to the arc itself.
            await page.locator('#research-sunburst').scrollIntoViewIfNeeded();
            await page.evaluate(() => new Promise((resolve) => {
                requestAnimationFrame(() => requestAnimationFrame(resolve));
            }));
            await page.locator('#research-sunburst path.research-sunburst__arc:not([pointer-events="none"])').first()
                .dispatchEvent('pointermove', {clientX: 200, clientY: 200, pointerType: 'mouse', bubbles: true});
            const tooltip = page.locator('#research-tooltip');
            await tooltip.waitFor({state: 'visible', timeout: TIMEOUT});
            const tip = await tooltip.textContent();
            await page.locator('#research-sectors-breadcrumb .research-breadcrumb__link', {hasText: 'All filers'}).click();
            if (atRoot && zoomedNote && /of all filers/.test(tip) && /#\d+ of \d+ in /.test(tip)) {
                pass(`Research 6a: cards (${labels.join(', ')}) follow the zoom; the tooltip shows the share of all and a rank`);
            } else {
                fail(`Research 6a: cards ${JSON.stringify(labels)}, zoomed note ${zoomedNote}, tooltip "${tip}"`);
            }
        });

        // Step 6b: both keyboard paths. Arcs: arrow keys move between siblings, Enter zooms in, Escape zooms out and
        // returns focus to the group left. Ranked list: a row fades the other slices, and Enter zooms into it.
        await step('Research 6b', fail, async () => {
            const current = page.locator('#research-sectors-breadcrumb [aria-current="location"]');
            const activeKey = () => page.evaluate(() => document.activeElement.getAttribute('data-key'));
            await page.locator('#research-sunburst path[tabindex="0"]').focus();
            const first = await activeKey();
            await page.keyboard.press('ArrowRight');
            const second = await activeKey();
            const moved = Boolean(first) && Boolean(second) && first !== second;
            const office = second;
            await page.keyboard.press('Enter');
            const zoomedName = (await current.textContent()).split(' · ')[0];
            const inside = await activeKey();
            await page.keyboard.press('Escape');
            const back = await activeKey();
            const arcsOk = moved && zoomedName !== 'All filers' && Boolean(inside) && inside !== office &&
                (await current.textContent()).startsWith('All filers') && back === office;

            const row = page.locator('#research-sectors-ranked button').first();
            const rowName = (await row.locator('.research-ranked__name').textContent()).trim();
            await row.focus();
            const faded = await page.locator('#research-sunburst .research-sunburst__arc--faded').count();
            await page.keyboard.press('Enter');
            const listZoom = (await current.textContent()).startsWith(rowName);
            const listFocus = await page.evaluate(() => {
                const list = document.getElementById('research-sectors-ranked');
                const title = document.getElementById('research-sectors-ranked-title');
                return list.contains(document.activeElement) || document.activeElement === title;
            });
            await page.locator('#research-sectors-breadcrumb .research-breadcrumb__link', {hasText: 'All filers'}).click();
            if (arcsOk && faded > 0 && listZoom && listFocus) {
                pass(`Research 6b: arrows, Enter and Escape drive the arcs (back to ${office}); the ranked list zooms into ${rowName}`);
            } else {
                fail(`Research 6b: arcs ${arcsOk} (${first}→${second}, in ${zoomedName}/${inside}, back ${back}), ` +
                    `faded ${faded}, list zoom ${listZoom}, list focus ${listFocus}`);
            }
        });

        // Step 6c: the treemap toggle keeps the focus and the year; tiles drill in with Enter and back with Escape;
        // inside one office, neighbouring tiles differ in fill.
        await step('Research 6c', fail, async () => {
            const current = page.locator('#research-sectors-breadcrumb [aria-current="location"]');
            const period = await periodLabel(page, 'sectors');
            await page.locator('#research-sectors-legend button').first().click();
            const office = (await current.textContent()).split(' · ')[0];
            await page.click('[data-research-view="treemap"]');
            const shown = await page.locator('#research-treemap').isVisible() &&
                !(await page.locator('#research-sunburst').isVisible()) &&
                await page.getAttribute('[data-research-view="treemap"]', 'aria-pressed') === 'true';
            const keptFocus = (await current.textContent()).startsWith(office) &&
                await periodLabel(page, 'sectors') === period;
            const fills = await page.locator('#research-treemap .research-treemap__rect').evaluateAll(
                (rects) => rects.map((r) => r.getAttribute('fill')));
            const distinct = fills.length < 2 || new Set(fills).size === fills.length;
            const zoomable = page.locator('#research-treemap g.research-treemap__tile[role="button"]').first();
            let drilled = true;
            if (await zoomable.count() > 0) {
                await zoomable.focus();
                await page.keyboard.press('Enter');
                drilled = !(await current.textContent()).startsWith(office);
                await page.locator('#research-treemap g.research-treemap__tile').first().focus();
                await page.keyboard.press('Escape');
                drilled = drilled && (await current.textContent()).startsWith(office);
            }
            await page.click('[data-research-view="sunburst"]');
            const back = await page.locator('#research-sunburst').isVisible() &&
                (await current.textContent()).startsWith(office);
            await page.locator('#research-sectors-breadcrumb .research-breadcrumb__link', {hasText: 'All filers'}).click();
            if (shown && keptFocus && distinct && drilled && back) {
                pass(`Research 6c: the treemap shows ${office} for ${period} (${fills.length} tiles, all distinct), drills with Enter/Escape, and toggles back`);
            } else {
                fail(`Research 6c: shown ${shown}, kept focus ${keptFocus}, fills ${JSON.stringify(fills)}, drilled ${drilled}, back ${back}`);
            }
        });

        // Step 7: Funds opens with SPY expanded; QQQ expands with Enter; the scrubber shows quarters.
        await step('Research 7', fail, async () => {
            await page.click('#research-tab-funds');
            await page.waitForSelector('#research-funds-tree [role="tree"]', {timeout: TIMEOUT});
            const spy = page.locator('#research-funds-tree [role="treeitem"][aria-label^="SPY "]');
            const qqq = page.locator('#research-funds-tree [role="treeitem"][aria-label^="QQQ "]');
            const spyOpen = await spy.getAttribute('aria-expanded');
            const qqqBefore = await qqq.getAttribute('aria-expanded');
            await qqq.focus();
            await page.keyboard.press('Enter');
            await page.waitForFunction(() => {
                const node = document.querySelector('#research-funds-tree [role="treeitem"][aria-label^="QQQ "]');
                return node && node.getAttribute('aria-expanded') === 'true';
            }, null, {timeout: 5000});
            const quarter = await periodLabel(page, 'funds');
            if (spyOpen === 'true' && qqqBefore === 'false' && /^\d{4} Q[1-4]$/.test(quarter)) {
                pass(`Research 7: SPY starts open, QQQ opens with Enter, the scrubber shows ${quarter}`);
            } else {
                fail(`Research 7: SPY expanded "${spyOpen}", QQQ before "${qqqBefore}", quarter "${quarter}"`);
            }
        });

        // Step 7b: the Fund Basket bar shows on the Funds tab only; funds are picked by keyboard, weights scale to 100%,
        // "Use equal weights" resets them, and the basket lives in the address so a reload keeps it.
        await step('Research 7b', fail, async () => {
            const bar = page.locator('#research-basket');
            const search = page.locator('#research-basket-search');
            const basketParam = () => new URL(page.url()).searchParams.get('basket');
            const shownOnFunds = await bar.isVisible();
            await page.click('#research-tab-sectors');
            const hiddenOnSectors = !(await bar.isVisible());
            await page.click('#research-tab-funds');

            await search.fill('qq');
            const option = page.locator('#research-basket-options [role="option"]', {hasText: 'QQQ'});
            const grouped = await page.locator('#research-basket-options .research-basket__group-label').first().textContent();
            await search.press('Enter');
            await search.press('ArrowDown');
            await search.press('Enter');
            const picked = basketParam();
            await page.locator('#research-basket-chips [data-ticker="SPY"] input').fill('70');
            await page.locator('#research-basket-chips [data-ticker="SPY"] input').press('Tab');
            const weighted = basketParam();
            const scaled = await page.locator('#research-basket-note').isVisible();
            await page.click('#research-basket-equal');
            const equal = basketParam();
            await page.reload({waitUntil: 'networkidle'});
            const restored = await page.locator('#research-basket-chips [data-ticker]').evaluateAll(
                (chips) => chips.map((c) => c.dataset.ticker));
            // At 320 px, the narrowest common phone width, the chips wrap and their names shrink, so the page never
            // scrolls sideways (a chip that could not shrink pushed the page wider).
            await page.setViewportSize({width: 320, height: 800});
            // The charts redraw at the new width on their resize observers; let them settle before measuring.
            await page.waitForTimeout(400);
            const narrowFits = !(await scrollsSideways(page));
            await page.setViewportSize({width: 1280, height: 800});
            await page.locator('#research-basket-chips [data-ticker="SPY"] .research-basket__remove').click();
            const removed = basketParam();
            const ok = shownOnFunds && hiddenOnSectors && (await option.count()) === 0 && Boolean(grouped) &&
                picked === 'QQQ,SPY' && weighted === 'QQQ:50,SPY:70' && scaled && equal === 'QQQ,SPY' &&
                restored.join(',') === 'QQQ,SPY' && narrowFits && removed === 'QQQ';
            if (ok) {
                pass(`Research 7b: the basket bar picks QQQ then SPY by keyboard (grouped under "${grouped.trim()}"), scales 50/70, resets to equal, survives a reload and removes SPY`);
            } else {
                fail(`Research 7b: shown ${shownOnFunds}/${!hiddenOnSectors}, picked ${picked}, weighted ${weighted}, ` +
                    `scaled ${scaled}, equal ${equal}, restored ${restored}, fits at 320px ${narrowFits}, removed ${removed}`);
            }
        });

        // Step 7c: a link with an untracked fund keeps the rest and says so; the parser drops bad weights and repeats,
        // and keeps only the first five funds.
        await step('Research 7c', fail, async () => {
            await page.goto(`${base}/Research?tab=funds&basket=SPY:60,XYZ:20,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const notice = (await page.locator('#research-basket-notice').textContent()).trim();
            const kept = new URL(page.url()).searchParams.get('basket');
            const parsed = await page.evaluate(() => {
                const parse = window.ResearchBasket.parse;
                const six = parse('A,B,C,D,E,F', ['A', 'B', 'C', 'D', 'E', 'F']);
                const bad = parse('SPY:0,QQQ:abc,SPY,QQQ:101', ['SPY', 'QQQ']);
                const mixed = parse('spy:60,qqq', ['SPY', 'QQQ']);
                return {
                    six: [six.funds.length, six.tooMany, six.invalid],
                    bad: [bad.funds.map((f) => f.ticker).join(','), bad.invalid],
                    mixed: mixed.funds.map((f) => `${f.ticker}:${f.weight}`).join(',')
                };
            });
            const ok = notice === 'Some funds in this link aren\'t tracked and were left out.' && kept === 'SPY:60,QQQ:40' &&
                parsed.six.join() === '5,true,false' && parsed.bad.join() === 'SPY,true' && parsed.mixed === 'SPY:null,QQQ:null';
            if (ok) {
                pass('Research 7c: an untracked fund is left out with a notice; bad weights, repeats and a sixth fund are dropped');
            } else {
                fail(`Research 7c: notice "${notice}", kept ${kept}, parsed ${JSON.stringify(parsed)}`);
            }
        });

        // Step 8: World, switched to % of GDP, changes its legend.
        await step('Research 8', fail, async () => {
            await page.click('#research-tab-world');
            await page.waitForSelector('#research-world-map svg', {timeout: TIMEOUT});
            const legend = page.locator('#research-world-legend');
            const before = await legend.getAttribute('aria-label');
            const button = page.locator('[data-research-world-metric="percentOfGdp"]');
            await button.click();
            const after = await legend.getAttribute('aria-label');
            const firstStep = await legend.locator('li').first().textContent();
            if (before !== after && /% of GDP/.test(after) && firstStep.includes('%') &&
                await button.getAttribute('aria-pressed') === 'true') {
                pass(`Research 8: "% of GDP" changes the legend to "${after}"`);
            } else {
                fail(`Research 8: legend "${before}" -> "${after}", first step "${firstStep}"`);
            }
        });

        // Step 9: "Show as table" lists rows on each tab.
        await step('Research 9', fail, async () => {
            const counts = {};
            for (const tabId of ['sectors', 'funds', 'world']) {
                await page.click(`#research-tab-${tabId}`);
                await page.locator(`#research-panel-${tabId} [data-research-table-toggle]`).click();
                counts[tabId] = await page.locator(`#research-panel-${tabId} [data-research-table] tbody tr`).count();
            }
            if (Object.values(counts).every((n) => n > 0)) {
                pass(`Research 9: "Show as table" lists rows on each tab (${JSON.stringify(counts)})`);
            } else {
                fail(`Research 9: table rows per tab ${JSON.stringify(counts)}`);
            }
        });

        // Step 10: under reduced motion a slider change applies at once.
        await step('Research 10', fail, async () => {
            const reduced = watch(await context.newPage());
            try {
                await reduced.emulateMedia({reducedMotion: 'reduce'});
                await reduced.goto(`${base}/Research`, {waitUntil: 'networkidle', timeout: TIMEOUT});
                await reduced.waitForSelector('#research-sunburst path.research-sunburst__arc', {timeout: TIMEOUT});
                const shape = () => reduced.evaluate(() => [...document.querySelectorAll('#research-sunburst path.research-sunburst__arc')]
                    .map((arc) => `${arc.dataset.key}=${arc.getAttribute('d')}`).join('|'));
                await reduced.locator('#research-sectors-year').fill('0');
                const atOnce = await shape();
                await reduced.waitForTimeout(1200);
                const settled = await shape();
                const label = await periodLabel(reduced, 'sectors');
                if (label === earliest && atOnce === settled) {
                    pass('Research 10: with reduced motion the slider change is drawn at once, with no transition');
                } else {
                    fail(`Research 10: label "${label}", arcs changed after the change: ${atOnce !== settled}`);
                }
            } finally {
                await reduced.close();
            }
        });

        // Step 11: at 390 px there is no horizontal page scroll and the slider answers touch.
        await step('Research 11', fail, async () => {
            const phone = await browser.newContext({
                ignoreHTTPSErrors: true,
                storageState: await context.storageState(),
                viewport: {width: 390, height: 844},
                hasTouch: true,
                isMobile: true
            });
            try {
                const mobile = watch(await phone.newPage());
                // With a weighted basket, so the Funds tab's check covers the Fund Basket bar's chips too.
                await mobile.goto(`${base}/Research?basket=SPY:70,QQQ:50`, {waitUntil: 'networkidle', timeout: TIMEOUT});
                await mobile.waitForSelector('#research-sunburst svg', {timeout: TIMEOUT});
                const wide = [];
                for (const tabId of ['sectors', 'funds', 'world']) {
                    await mobile.tap(`#research-tab-${tabId}`);
                    await mobile.waitForTimeout(300);
                    if (await scrollsSideways(mobile)) {
                        wide.push(tabId);
                    }
                }
                await mobile.tap('#research-tab-sectors');
                const box = await mobile.locator('#research-sectors-year').boundingBox();
                await mobile.touchscreen.tap(box.x + 2, box.y + box.height / 2);
                const label = await periodLabel(mobile, 'sectors');
                if (wide.length === 0) {
                    pass('Research 11: no horizontal page scroll at 390 px on any tab');
                } else {
                    fail(`Research 11: horizontal page scroll at 390 px on ${wide.join(', ')}`);
                }
                if (label === earliest) {
                    pass(`Research 11: a touch on the slider moves Sectors to ${earliest}`);
                } else {
                    fail(`Research 11: after a touch at the start of the slider the label is "${label}"`);
                }
            } finally {
                await phone.close();
            }
        });

        await runCompaniesSteps({browser, context, page, base, pass, fail, watch});
    } finally {
        await context.close();
    }
}

/**
 * The bar heights of the Companies tab's charts, in chart order.
 * @param {import('playwright').Page} page - A page showing a Company Profile.
 * @returns {Promise<string>} The heights, joined.
 */
async function barHeights(page) {
    return page.evaluate(() => [...document.querySelectorAll('#research-panel-companies .research-companies__bar')]
        .map((bar) => bar.getAttribute('height')).join('|'));
}

/**
 * The Companies tab (spec #242): search, the profile, filings, Back, a fund, reduced motion and a phone.
 * @param {{browser: import('playwright').Browser, context: import('playwright').BrowserContext,
 *          page: import('playwright').Page, base: string, pass: Function, fail: Function, watch: Function}} suite -
 *     The walkthrough's browser, signed-in context and page.
 * @returns {Promise<void>}
 */
async function runCompaniesSteps({browser, context, page, base, pass, fail, watch}) {
    const panel = '#research-panel-companies';

    // Company 1: the tab puts itself in the address and opens on an empty search with no suggested companies.
    await step('Company 1', fail, async () => {
        await page.goto(`${base}/Research`, {waitUntil: 'networkidle', timeout: TIMEOUT});
        await page.click('#research-tab-companies');
        const url = new URL(page.url());
        const placeholder = await page.locator('#research-company-input').getAttribute('placeholder');
        const options = await page.locator('#research-company-suggestions [role="option"]').count();
        const profileHidden = await page.locator(`${panel} [data-research-company-profile]`).isHidden();
        if (url.searchParams.get('tab') === 'companies' && placeholder === 'e.g. MSFT or Microsoft' && options === 0 &&
            profileHidden) {
            pass('Company 1: the Companies tab sets tab=companies and opens on an empty search');
        } else {
            fail(`Company 1: url "${page.url()}", placeholder "${placeholder}", options ${options}, profile hidden ${profileHidden}`);
        }
    });

    // Company 2: type "exa", arrow down to Example Corp and press Enter.
    await step('Company 2', fail, async () => {
        const input = page.locator('#research-company-input');
        await input.click();
        await input.pressSequentially('exa', {delay: 40});
        await page.waitForSelector('#research-company-suggestions [role="option"]', {timeout: TIMEOUT});
        const tickers = await page.locator('#research-company-suggestions .research-companies__option-ticker').allTextContents();
        const index = tickers.indexOf('EXMP');
        if (index < 0) {
            fail(`Company 2: "exa" suggests ${JSON.stringify(tickers)}, not EXMP`);
            return;
        }
        for (let i = 0; i <= index; i++) {
            await page.keyboard.press('ArrowDown');
        }
        const active = await input.getAttribute('aria-activedescendant');
        await page.keyboard.press('Enter');
        await page.waitForSelector(`${panel} [data-research-company-profile]:not([hidden])`, {timeout: TIMEOUT});
        await page.waitForFunction(() => document.getElementById('research-live').textContent.includes('Example Corp'),
            null, {timeout: TIMEOUT});
        const name = (await page.locator(`${panel} [data-field="name"]`).textContent()).trim();
        const ticker = new URL(page.url()).searchParams.get('ticker');
        if (active && name === 'Example Corp' && ticker === 'EXMP') {
            pass('Company 2: arrow keys and Enter load Example Corp, set ticker=EXMP and announce it');
        } else {
            fail(`Company 2: active "${active}", name "${name}", ticker "${ticker}"`);
        }
    });

    // Company 3: four charts and the table; a "Not reported" cell; a derived header shows its formula on focus.
    await step('Company 3', fail, async () => {
        const charts = await page.locator(`${panel} .research-companies__chart svg`).count();
        const tableVisible = await page.locator(`${panel} [data-research-company-table] table`).isVisible();
        const notReported = await page.locator(`${panel} td`, {hasText: 'Not reported'}).count();
        const derived = page.locator(`${panel} th.research-companies__derived`).first();
        await derived.focus();
        const formula = derived.locator('.research-companies__formula');
        await page.waitForFunction((el) => el.getBoundingClientRect().height > 0, await formula.elementHandle(),
            {timeout: 2000});
        const formulaText = (await formula.textContent()).trim();
        if (charts === 4 && tableVisible && notReported > 0 && formulaText.includes('÷')) {
            pass(`Company 3: 4 charts, the table, ${notReported} "Not reported" cell(s), and "${formulaText}" on focus`);
        } else {
            fail(`Company 3: charts ${charts}, table ${tableVisible}, not reported ${notReported}, formula "${formulaText}"`);
        }
    });

    // Company 4: filings list 8-K events in words and link to EDGAR.
    await step('Company 4', fail, async () => {
        const events = await page.locator(`${panel} .research-companies__events`).allTextContents();
        const hrefs = await page.locator(`${panel} .research-companies__filing-link`).evaluateAll(
            (links) => links.map((link) => link.href));
        if (events.some((e) => e.includes('Results of operations')) && hrefs.length > 0 &&
            hrefs.every((h) => h.startsWith('https://www.sec.gov/Archives/edgar/data/'))) {
            pass(`Company 4: ${hrefs.length} filings link to EDGAR, 8-K events in words`);
        } else {
            fail(`Company 4: events ${JSON.stringify(events)}, links ${JSON.stringify(hrefs.slice(0, 2))}`);
        }
    });

    // Company 5: Back returns to the empty search.
    await step('Company 5', fail, async () => {
        await page.goBack({waitUntil: 'commit'});
        await page.waitForSelector(`${panel} [data-research-company-profile][hidden]`, {state: 'attached', timeout: TIMEOUT});
        const ticker = new URL(page.url()).searchParams.get('ticker');
        const value = await page.locator('#research-company-input').inputValue();
        if (!ticker && value === '' && await page.locator(panel).isVisible()) {
            pass('Company 5: Back returns to the empty search on the Companies tab');
        } else {
            fail(`Company 5: url "${page.url()}", input "${value}"`);
        }
    });

    // Company 6: a fund ticker says so and links to the Funds tab.
    await step('Company 6', fail, async () => {
        const input = page.locator('#research-company-input');
        await input.fill('EXFD');
        await input.press('Enter');
        const message = page.locator(`${panel} [data-research-company-message]`);
        await message.waitFor({state: 'visible', timeout: TIMEOUT});
        const text = await message.textContent();
        await message.locator('a', {hasText: 'Funds tab'}).click();
        const fundsSelected = await page.locator('#research-tab-funds').getAttribute('aria-selected');
        if (/is a fund/.test(text) && fundsSelected === 'true') {
            pass('Company 6: a fund ticker says it is a fund and its link opens the Funds tab');
        } else {
            fail(`Company 6: message "${text}", Funds selected "${fundsSelected}"`);
        }
    });

    // Company 7: under reduced motion the bars are drawn at once.
    await step('Company 7', fail, async () => {
        const reduced = watch(await context.newPage());
        try {
            await reduced.emulateMedia({reducedMotion: 'reduce'});
            await reduced.goto(`${base}/Research?tab=companies&ticker=EXMP`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await reduced.waitForSelector(`${panel} .research-companies__bar`, {timeout: TIMEOUT});
            const atOnce = await barHeights(reduced);
            await reduced.waitForTimeout(1200);
            const settled = await barHeights(reduced);
            if (atOnce === settled && !atOnce.split('|').includes('0')) {
                pass('Company 7: with reduced motion the bars are drawn at once, with no transition');
            } else {
                fail(`Company 7: bars at once "${atOnce}", settled "${settled}"`);
            }
        } finally {
            await reduced.close();
        }
    });

    // Company 8: at 390 px no horizontal page scroll, charts hidden, the table scrolls inside its card.
    await step('Company 8', fail, async () => {
        const phone = await browser.newContext({
            ignoreHTTPSErrors: true,
            storageState: await context.storageState(),
            viewport: {width: 390, height: 844},
            hasTouch: true,
            isMobile: true
        });
        try {
            const mobile = watch(await phone.newPage());
            await mobile.goto(`${base}/Research?tab=companies&ticker=EXMP`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await mobile.waitForSelector(`${panel} [data-research-company-table] table`, {timeout: TIMEOUT});
            const wide = await scrollsSideways(mobile);
            const chartsShown = await mobile.locator(`${panel} [data-research-company-charts]`).isVisible();
            const tableScrolls = await mobile.locator(`${panel} [data-research-company-table]`).evaluate(
                (host) => host.scrollWidth > host.clientWidth);
            if (!wide && !chartsShown && tableScrolls) {
                pass('Company 8: at 390 px no page scroll, charts hidden, the table scrolls in its card');
            } else {
                fail(`Company 8: page scrolls ${wide}, charts shown ${chartsShown}, table scrolls ${tableScrolls}`);
            }
        } finally {
            await phone.close();
        }
    });
}

module.exports = {runResearchWalkthrough};
