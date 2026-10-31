/**
 * Research first-session walkthrough (spec #217, Testing Decisions): the steps a new Account takes through the
 * Sectors page (Sectors 1 to 7, which were Research 2 to 6e on the Sectors tab before the Sectors page shared its panel)
 * the Funds page (Funds 1 to 4: table sort, risk roles, correlation, holdings list), the tabbed page's Funds and World
 * tabs, then the Companies tab's 8 steps (spec #242, Company 1 to 8). Run from ui-test.js as
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
 * The page's address as the page sees it. A history.replaceState change reaches page.url() only later, so the steps
 * that check it read location.href instead.
 * @param {import('playwright').Page} page - The page.
 * @returns {Promise<URL>} The address.
 */
async function addressOf(page) {
    return new URL(await page.evaluate(() => window.location.href));
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
            await page.waitForSelector('#research-treemap svg', {timeout: TIMEOUT});
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

        // The Sectors steps run on the Sectors page (/Research/Sectors), which shares its panel and scripts with the
        // tabbed page's Sectors tab; steps 7b, 9, 10 and 11 below still cover that tab.
        const crumbCurrent = page.locator('#research-sectors-breadcrumb [aria-current="location"]');
        const allSectors = page.locator('#research-sectors-breadcrumb .research-breadcrumb__link', {hasText: 'All sectors'});

        // Sectors 1: the page opens on the latest year, says "Public float", credits its source, and the Research nav
        // marks Sectors as the current page.
        await step('Sectors 1', fail, async () => {
            await page.goto(`${base}/Research/Sectors`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-treemap svg', {timeout: TIMEOUT});
            const label = await periodLabel(page, 'sectors');
            const title = await page.locator('#research-panel-sectors .research-card__title').textContent();
            const source = await page.locator('#research-panel-sectors .research-card__source').textContent();
            const current = ((await page.locator('.research-nav a[aria-current="page"]').textContent()) || '').trim();
            const tabs = await page.locator('[role="tablist"]').count();
            if (label === latest && /Public float/.test(title) && /Source: SEC EDGAR/.test(source) && current === 'Sectors' &&
                tabs === 0) {
                pass(`Sectors 1: the Sectors page shows ${latest} with "Public float" wording and a source credit, Sectors current in the nav`);
            } else {
                fail(`Sectors 1: label "${label}", title "${title}", source "${source}", nav current "${current}", tablists ${tabs}`);
            }
        });

        // Sectors 2: drag the slider to the earliest year; the label and the live region follow.
        await step('Sectors 2', fail, async () => {
            const box = await page.locator('#research-sectors-year').boundingBox();
            await page.mouse.move(box.x + box.width - 4, box.y + box.height / 2);
            await page.mouse.down();
            await page.mouse.move(box.x - 20, box.y + box.height / 2, {steps: 8});
            await page.mouse.up();
            const label = await periodLabel(page, 'sectors');
            const live = await page.locator('#research-live').textContent();
            if (label === earliest && live.startsWith(`${earliest}:`)) {
                pass(`Sectors 2: dragging to the start shows ${earliest} and announces it`);
            } else {
                fail(`Sectors 2: label "${label}", live region "${live}"`);
            }
        });

        // Sectors 3: play runs to the latest year and stops.
        await step('Sectors 3', fail, async () => {
            const play = page.locator('#research-panel-sectors [data-research-play]');
            await play.click();
            await page.waitForFunction((last) => {
                const panel = document.getElementById('research-panel-sectors');
                return panel.querySelector('[data-research-period]').textContent.trim() === last &&
                    panel.querySelector('[data-research-play]').dataset.state === 'paused';
            }, latest, {timeout: TIMEOUT});
            pass(`Sectors 3: play advances to ${latest} and stops`);
        });

        // Sectors 4: the Technology highlight.
        await step('Sectors 4', fail, async () => {
            const toggle = page.locator('#research-tech-toggle');
            await toggle.click();
            const pressed = await toggle.getAttribute('aria-pressed');
            const share = page.locator('#research-tech-share');
            if (pressed === 'true' && await share.isVisible() && (await share.textContent()).trim().length > 0) {
                pass('Sectors 4: "Technology (incl. chip makers)" lights up and states its share');
            } else {
                fail(`Sectors 4: aria-pressed "${pressed}", share visible ${await share.isVisible()}`);
            }
        });

        // Sectors 5: zoom into an office from the legend; the breadcrumb shows it and its "All sectors" link returns.
        await step('Sectors 5', fail, async () => {
            const office = page.locator('#research-sectors-legend button').first();
            const officeName = (await office.textContent()).trim();
            await office.click();
            const zoomedIn = await office.getAttribute('aria-pressed') === 'true' &&
                (await crumbCurrent.textContent()).startsWith(officeName);
            await allSectors.click();
            const zoomedOut = (await crumbCurrent.textContent()).startsWith('All sectors') &&
                await office.getAttribute('aria-pressed') === 'false' &&
                await page.locator('#research-sectors-breadcrumb .research-breadcrumb__link').count() === 0;
            if (zoomedIn && zoomedOut) {
                pass(`Sectors 5: a legend button zooms into ${officeName}; the breadcrumb's "All sectors" link returns`);
            } else {
                fail(`Sectors 5: zoomed in ${zoomedIn}, zoomed out ${zoomedOut}`);
            }
        });

        // Sectors 5a: the takeaway cards follow the zoom, and the tooltip gives the share of all filers and a rank.
        await step('Sectors 5a', fail, async () => {
            const cards = page.locator('#research-sectors-takeaways .research-takeaway');
            const labels = await cards.locator('.research-takeaway__label').allTextContents();
            const totalNote = page.locator('#research-sectors-takeaways .research-takeaway').first()
                .locator('.research-takeaway__note');
            const atRoot = labels[0] === 'Total' && labels.includes('Largest part') &&
                (await totalNote.textContent()).startsWith('All filers');
            await page.locator('#research-sectors-legend button').first().click();
            const zoomedNote = (await totalNote.textContent()).includes('of all filers');
            // The chart hides its tooltip on scroll, and the legend click may have scrolled: bring the chart into view
            // and let any scroll event fire first.
            await page.locator('#research-treemap').scrollIntoViewIfNeeded();
            await page.evaluate(() => new Promise((resolve) => {
                requestAnimationFrame(() => requestAnimationFrame(resolve));
            }));
            await page.locator('#research-treemap g.research-treemap__tile').first()
                .dispatchEvent('pointermove', {clientX: 200, clientY: 200, pointerType: 'mouse', bubbles: true});
            const tooltip = page.locator('#research-tooltip');
            await tooltip.waitFor({state: 'visible', timeout: TIMEOUT});
            const tip = await tooltip.textContent();
            await allSectors.click();
            if (atRoot && zoomedNote && /of all filers/.test(tip) && /#\d+ of \d+ in /.test(tip)) {
                pass(`Sectors 5a: cards (${labels.join(', ')}) follow the zoom; the tooltip shows the share of all and a rank`);
            } else {
                fail(`Sectors 5a: cards ${JSON.stringify(labels)}, zoomed note ${zoomedNote}, tooltip "${tip}"`);
            }
        });

        // Sectors 5b: both keyboard paths. Tiles: one tab stop, arrow keys move between them, Enter zooms in, Escape
        // zooms out and returns focus to the group left. Ranked list: a row fades the other tiles, and Enter zooms into it.
        await step('Sectors 5b', fail, async () => {
            const activeKey = () => page.evaluate(() => document.activeElement.getAttribute('data-key'));
            const stops = await page.locator('#research-treemap g.research-treemap__tile[tabindex="0"]').count();
            await page.locator('#research-treemap g.research-treemap__tile[tabindex="0"]').focus();
            const first = await activeKey();
            await page.keyboard.press('ArrowRight');
            const second = await activeKey();
            const moved = Boolean(first) && Boolean(second) && first !== second;
            const office = second;
            await page.keyboard.press('Enter');
            const zoomedName = (await crumbCurrent.textContent()).split(' · ')[0];
            const inside = await activeKey();
            await page.keyboard.press('Escape');
            const back = await activeKey();
            const tilesOk = stops === 1 && moved && zoomedName !== 'All sectors' && Boolean(inside) && inside !== office &&
                (await crumbCurrent.textContent()).startsWith('All sectors') && back === office;

            const row = page.locator('#research-sectors-ranked button').first();
            const rowName = (await row.locator('.research-ranked__name').textContent()).trim();
            await row.focus();
            const faded = await page.locator('#research-treemap .research-treemap__tile--faded').count();
            await page.keyboard.press('Enter');
            const listZoom = (await crumbCurrent.textContent()).startsWith(rowName);
            const listFocus = await page.evaluate(() => {
                const list = document.getElementById('research-sectors-ranked');
                const title = document.getElementById('research-sectors-ranked-title');
                return list.contains(document.activeElement) || document.activeElement === title;
            });
            await allSectors.click();
            if (tilesOk && faded > 0 && listZoom && listFocus) {
                pass(`Sectors 5b: arrows, Enter and Escape drive the tiles (back to ${office}); the ranked list zooms into ${rowName}`);
            } else {
                fail(`Sectors 5b: tiles ${tilesOk} (stops ${stops}, ${first}→${second}, in ${zoomedName}/${inside}, back ${back}), ` +
                    `faded ${faded}, list zoom ${listZoom}, list focus ${listFocus}`);
            }
        });

        // Sectors 5c: inside one office, neighbouring tiles differ in fill, and the zoom keeps the year; a zoomable tile
        // drills in with Enter and back with Escape. There is no sunburst and no chart-type toggle.
        await step('Sectors 5c', fail, async () => {
            const period = await periodLabel(page, 'sectors');
            await page.locator('#research-sectors-legend button').first().click();
            const office = (await crumbCurrent.textContent()).split(' · ')[0];
            const keptYear = await periodLabel(page, 'sectors') === period;
            const fills = await page.locator('#research-treemap .research-treemap__rect').evaluateAll(
                (rects) => rects.map((r) => r.getAttribute('fill')));
            const distinct = fills.length < 2 || new Set(fills).size === fills.length;
            const zoomable = page.locator('#research-treemap g.research-treemap__tile[role="button"]').first();
            let drilled = true;
            if (await zoomable.count() > 0) {
                await zoomable.focus();
                await page.keyboard.press('Enter');
                drilled = !(await crumbCurrent.textContent()).startsWith(office);
                await page.locator('#research-treemap g.research-treemap__tile').first().focus();
                await page.keyboard.press('Escape');
                drilled = drilled && (await crumbCurrent.textContent()).startsWith(office);
            }
            const noSunburst = await page.locator('#research-sunburst, [data-research-view]').count() === 0;
            await allSectors.click();
            if (keptYear && distinct && drilled && noSunburst && fills.length > 0) {
                pass(`Sectors 5c: the treemap shows ${office} for ${period} (${fills.length} tiles, all distinct), drills with Enter/Escape; no sunburst`);
            } else {
                fail(`Sectors 5c: kept year ${keptYear}, fills ${JSON.stringify(fills)}, drilled ${drilled}, no sunburst ${noSunburst}`);
            }
        });

        // Sectors 6: the basket view. Without a basket the switch opens on Market with Your basket disabled; with SPY 60
        // and QQQ 40 it still opens on Market unless the address asks for ?view=basket. In the basket view every figure
        // is a share of the basket, stocks SEC does not classify are hatched grey (never in an office colour), and
        // International Corp Fin is neutral. A drill-down reaches an industry's companies and "Other (n companies)", the
        // unclassified card names the largest countries, their tiles keep the hatch, and the table lists the basket's
        // largest companies. Back on Market, the address drops ?view.
        await step('Sectors 6', fail, async () => {
            // Without a basket the page neither carries nor fetches the basket's data.
            const fetched = [];
            const onRequest = (request) => {
                if (request.url().includes('/Research/FundIndustries')) {
                    fetched.push(request.url());
                }
            };
            page.on('request', onRequest);
            await page.goto(`${base}/Research/Sectors`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const notCarried = await page.locator('script#research-fund-industries-data').count() === 0 && fetched.length === 0;
            const marketFirst = await page.locator('[data-sectors-view="market"]').getAttribute('aria-pressed') === 'true';
            const basketOff = await page.locator('[data-sectors-view="basket"]').isDisabled();
            const noBar = await page.locator('#research-basket').count() === 0;
            await page.goto(`${base}/Research/Sectors?basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const marketWithBasket = await page.locator('[data-sectors-view="market"]').getAttribute('aria-pressed') === 'true' &&
                !(await page.locator('[data-sectors-view="basket"]').isDisabled()) && fetched.length === 0;
            await page.goto(`${base}/Research/Sectors?view=basket&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-treemap g.research-treemap__tile[data-key="bu"]', {timeout: TIMEOUT});
            const fetchedOnce = fetched.length === 1;
            page.off('request', onRequest);
            const asOf = (await page.locator('#research-sectors-holdings-as-of').textContent()).trim();
            const expected = await page.evaluate(async () => {
                const data = await (await fetch(document.getElementById('research-sectors-views').dataset.industriesUrl)).json();
                const shares = { SPY: 0.6, QQQ: 0.4 };
                const fmt = (v) => (v > 0 && v < 0.05 ? '<0.1%' : `${v.toFixed(1)}%`);
                const sum = (pick) => data.funds.reduce((s, f) => s + shares[f.ticker] * pick(f), 0);
                const classified = sum((f) => f.industries.reduce((a, i) => a + i.percent, 0));
                const unclassified = sum((f) => f.unclassified.reduce((a, c) => a + c.percent, 0));
                const country = (code) => sum((f) => (f.unclassified.find((c) => c.country === code) || { percent: 0 }).percent);
                const officeShare = (name) => sum((f) => f.industries
                    .filter((i) => data.offices[data.industries[i.industry].office] === name).reduce((a, i) => a + i.percent, 0));
                const company = (name) => sum((f) => f.companies
                    .filter((c) => data.companies[c.company].name === name).reduce((a, c) => a + c.percent, 0));
                const software = data.industries.findIndex((i) => i.sicCode === '7372');
                const softwareCount = Math.max(...data.funds.map((f) => (f.industries.find((i) => i.industry === software) || { companyCount: 0 }).companyCount));
                return {
                    classified: fmt(classified),
                    unclassified: fmt(unclassified),
                    nl: fmt(country('NL')),
                    technology: fmt(officeShare('Technology')),
                    nvidia: fmt(company('Nvidia Corp')),
                    others: softwareCount - 2
                };
            });
            const basketOn = await page.locator('[data-sectors-view="basket"]').getAttribute('aria-pressed') === 'true';
            const title = (await page.locator('#research-sectors-title').textContent()).trim();
            // The scrubber stays: the basket's lean section follows its year.
            const scrubberShown = await page.locator('#research-panel-sectors [data-research-scrubber]').isVisible();
            const subtitle = await page.locator('#research-sectors-subtitle').textContent();
            const top = (await crumbCurrent.textContent()).trim();
            const cards = await page.locator('#research-sectors-takeaways .research-takeaway').evaluateAll((items) => items.map((i) => ({
                label: i.querySelector('.research-takeaway__label').textContent,
                value: i.querySelector('.research-takeaway__value').textContent,
                note: i.querySelector('.research-takeaway__note').textContent
            })));
            const card = (label) => cards.find((c) => c.label === label) || { value: '', note: '' };
            const fills = await page.evaluate(async () => {
                const fillOf = (key) => document.querySelector(`#research-treemap g.research-treemap__tile[data-key="${key}"] rect`)?.getAttribute('fill');
                const neutral = getComputedStyle(document.querySelector('.research')).getPropertyValue('--research-office-neutral').trim();
                const data = await (await fetch(document.getElementById('research-sectors-views').dataset.industriesUrl)).json();
                const intl = data.offices.indexOf('International Corp Fin');
                return { unclassified: fillOf('bu'), intl: fillOf(`bo${intl}`), neutral };
            });
            const hatchedSwatch = await page.locator('#research-sectors-legend .research-legend__button', {hasText: 'Not classified by the SEC'})
                .locator('.research-legend__swatch--hatched').count();

            // Drill: the Technology office, then its software industry from the ranked list.
            await page.locator('#research-sectors-legend .research-legend__button', {hasText: 'Technology'}).click();
            const crumb = (await crumbCurrent.textContent()).trim();
            await page.locator('#research-sectors-ranked button', {hasText: 'Services-Prepackaged Software'}).click();
            const leaves = (await page.locator('#research-sectors-ranked .research-ranked__name').allTextContents()).map((t) => t.trim());
            // The unclassified stocks: their card names the largest countries; their tiles keep the hatch.
            await page.locator('#research-sectors-breadcrumb .research-breadcrumb__link', {hasText: 'Your basket'}).click();
            await page.locator('#research-sectors-legend .research-legend__button', {hasText: 'Not classified by the SEC'}).click();
            const unclassifiedNote = await page.locator('#research-sectors-takeaways .research-takeaway').evaluateAll((items) => {
                const item = items.find((i) => i.querySelector('.research-takeaway__label').textContent === 'Not classified by the SEC');
                return item ? item.querySelector('.research-takeaway__note').textContent : '';
            });
            const tileFills = await page.locator('#research-treemap .research-treemap__rect').evaluateAll((rects) => rects.map((r) => r.getAttribute('fill')));
            await page.locator('#research-sectors-breadcrumb .research-breadcrumb__link', {hasText: 'Your basket'}).click();
            await page.click('#research-panel-sectors [data-research-table-toggle]');
            const caption = await page.locator('#research-sectors-table caption').textContent();
            const firstRow = (await page.locator('#research-sectors-table tbody tr').first().locator('td').allTextContents()).map((t) => t.trim());
            await page.click('#research-panel-sectors [data-research-table-toggle]');
            const words = (await page.locator('#research-panel-sectors').innerText()).toLowerCase();
            const advice = ['best', 'efficient', ' buy'].filter((w) => words.includes(w));

            // Back to Market: the title, the scrubber and the year return, and the address drops ?view.
            await page.click('[data-sectors-view="market"]');
            const marketTitle = (await page.locator('#research-sectors-title').textContent()).trim();
            const scrubberBack = await page.locator('#research-panel-sectors [data-research-scrubber]').isVisible() &&
                !(await page.locator('#research-lean').isVisible());
            const viewDropped = !new URL(page.url()).searchParams.has('view') &&
                new URL(page.url()).searchParams.get('basket') === 'SPY:60,QQQ:40';

            const ok = notCarried && fetchedOnce && asOf === 'Fund holdings as of 30 June 2026' && marketFirst && basketOff && noBar &&
                marketWithBasket && basketOn && title === 'Your basket by SEC industry' && scrubberShown &&
                subtitle.includes('Every percent is of your basket') && top.startsWith('Your basket') && top.includes('100.0%') &&
                card('Classified by the SEC').value === expected.classified &&
                card('Not classified by the SEC').value === expected.unclassified &&
                card('Not classified by the SEC').note.includes(`Netherlands ${expected.nl}`) &&
                card('Largest SEC industry office').value === expected.technology &&
                card('Largest company').value === expected.nvidia && card('Largest company').note.includes('Nvidia Corp') &&
                fills.unclassified === 'url(#research-hatch-treemap)' && fills.intl === fills.neutral &&
                hatchedSwatch === 1 && crumb === `Technology · ${expected.technology}` &&
                leaves.includes('Microsoft Corp (MSFT)') && leaves.includes(`Other (${expected.others} companies)`) &&
                unclassifiedNote.includes(`Largest countries: Netherlands ${expected.nl}`) &&
                tileFills.length > 0 && tileFills.every((f) => f === 'url(#research-hatch-treemap)') &&
                caption.includes('largest companies in your basket') && firstRow[1] === 'Nvidia Corp (NVDA)' &&
                firstRow[4] === expected.nvidia && advice.length === 0 &&
                marketTitle === 'Largest SEC filers by Public float' && scrubberBack && viewDropped;
            if (ok) {
                pass(`Sectors 6: Market first (also with a basket unless ?view=basket); the basket view shows ${expected.classified} classified by the SEC and ${expected.unclassified} not (Netherlands ${expected.nl}), Technology ${expected.technology}, Nvidia ${expected.nvidia}; hatched unclassified, neutral International Corp Fin; drills to "Other (${expected.others} companies)"; the table and Market return`);
            } else {
                fail(`Sectors 6: fetch ${notCarried}/${fetched.length} "${asOf}", market ${marketFirst}/${basketOff}/no bar ${noBar}/with basket ${marketWithBasket}, basket ${basketOn} "${title}" scrubber shown ${scrubberShown}, ` +
                    `top "${top}", cards ${JSON.stringify(cards)} vs ${JSON.stringify(expected)}, fills ${JSON.stringify(fills)} swatch ${hatchedSwatch}, ` +
                    `crumb "${crumb}", leaves ${JSON.stringify(leaves)}, note "${unclassifiedNote}", tiles ${JSON.stringify(tileFills)}, ` +
                    `table "${caption}" ${JSON.stringify(firstRow)}, advice ${advice}, market "${marketTitle}" ${scrubberBack}, view dropped ${viewDropped} (${page.url()})`);
            }
        });

        // Sectors 6a: Lean vs the market, under the basket chart. Each office's share of the SEC-classified part of the
        // basket against its share of every filer's Public float in the scrubber's year, with a signed difference in
        // points (an arrow and a colour too, never colour alone); the year follows the scrubber, rows are reachable by
        // keyboard, and a row zooms the chart into its office.
        await step('Sectors 6a', fail, async () => {
            await page.goto(`${base}/Research/Sectors?view=basket&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-lean-rows .research-lean__row', {timeout: TIMEOUT});
            const expectedFor = (index) => page.evaluate(async (periodIndex) => {
                const data = await (await fetch(document.getElementById('research-sectors-views').dataset.industriesUrl)).json();
                const sectors = JSON.parse(document.getElementById('research-sectors-data').textContent);
                const shares = { SPY: 0.6, QQQ: 0.4 };
                const fmt = (v) => (v > 0 && v < 0.05 ? '<0.1%' : `${v.toFixed(1)}%`);
                const pts = (v) => (Math.abs(v) < 0.05 ? '0.0 pts' : `${v > 0 ? '+' : '−'}${Math.abs(v).toFixed(1)} pts`);
                const officeOf = (i) => data.offices[data.industries[i].office];
                const classified = data.funds.reduce((s, f) => s + shares[f.ticker] * f.industries.reduce((a, i) => a + i.percent, 0), 0);
                const tech = data.funds.reduce((s, f) => s + shares[f.ticker] * f.industries
                    .filter((i) => officeOf(i.industry) === 'Technology').reduce((a, i) => a + i.percent, 0), 0) / classified * 100;
                const period = sectors.values[periodIndex];
                const byOffice = new Map();
                const add = (industry, value) => {
                    const office = sectors.offices[sectors.industries[industry].office];
                    byOffice.set(office, (byOffice.get(office) || 0) + value);
                };
                period.ranked.forEach((r) => add(sectors.companies[r.company].industry, r.publicFloat));
                period.other.forEach((o) => add(o.industry, o.publicFloat));
                const total = Array.from(byOffice.values()).reduce((a, v) => a + v, 0);
                const market = (byOffice.get('Technology') || 0) / total * 100;
                return { year: period.period, classified: fmt(classified), basket: fmt(tech), market: fmt(market), difference: pts(tech - market), more: tech > market };
            }, index);
            const rowOf = async (office) => page.locator('#research-lean-rows .research-lean__row', {hasText: office}).first().evaluate((row) => ({
                values: Array.from(row.querySelectorAll('.research-lean__value')).map((v) => v.textContent),
                difference: row.querySelector('.research-lean__difference').textContent.trim(),
                more: row.querySelector('.research-lean__difference').classList.contains('research-lean__difference--more'),
                arrow: row.querySelector('.research-lean__difference [aria-hidden="true"]').textContent.trim(),
                fill: getComputedStyle(row.querySelector('.research-lean__fill')).backgroundColor,
                label: row.getAttribute('aria-label') || ''
            }));
            const last = await expectedFor((await periodsOf(page, 'sectors')).length - 1);
            const title = (await page.locator('#research-lean-title').textContent()).trim();
            const note = await page.locator('#research-lean-note').textContent();
            const tech = await rowOf('Technology');
            const lastOk = title === `Lean vs the market, ${last.year}` && note.includes(`SEC-classified part of your basket (${last.classified} of it)`) &&
                note.includes('it is not a target') && tech.values[0] === last.basket && tech.values[1] === last.market &&
                tech.difference.endsWith(last.difference) && tech.more === last.more && tech.arrow === (last.more ? '▲' : '▼') &&
                tech.label.includes(`${last.basket} of the SEC-classified part of your basket`);

            // The year follows the scrubber: at the start, the market's shares are the first year's.
            await page.locator('#research-sectors-year').fill('0');
            await page.waitForTimeout(300);
            const first = await expectedFor(0);
            const firstTitle = (await page.locator('#research-lean-title').textContent()).trim();
            const techFirst = await rowOf('Technology');
            const followsYear = firstTitle === `Lean vs the market, ${first.year}` && techFirst.values[1] === first.market &&
                techFirst.difference.endsWith(first.difference);

            // Keyboard: Tab moves from one row to the next; a row zooms the chart into its office.
            await page.locator('#research-lean-rows .research-lean__row').first().focus();
            await page.keyboard.press('Tab');
            const nextIsRow = await page.evaluate(() => document.activeElement.classList.contains('research-lean__row'));
            await page.locator('#research-lean-rows button.research-lean__row', {hasText: 'Technology'}).click();
            await page.waitForTimeout(900);
            const crumb = (await page.locator('#research-sectors-breadcrumb [aria-current="location"]').textContent()).trim();
            const words = (await page.locator('#research-lean').innerText()).toLowerCase();
            const advice = ['best', 'efficient', ' buy', 'should'].filter((w) => words.includes(w));
            await page.locator('#research-sectors-year').fill(String((await periodsOf(page, 'sectors')).length - 1));
            await page.click('[data-sectors-view="market"]');
            const hiddenInMarket = !(await page.locator('#research-lean').isVisible());

            if (lastOk && followsYear && nextIsRow && crumb.startsWith('Technology') && advice.length === 0 && hiddenInMarket) {
                pass(`Sectors 6a: Technology is ${last.basket} of the SEC-classified part (${last.classified}) against ${last.market} of the market in ${last.year} (${last.difference}, ${last.more ? '▲' : '▼'}); ${first.year} reads ${first.market} (${first.difference}); rows tab in order and zoom the chart; hidden in Market`);
            } else {
                fail(`Sectors 6a: "${title}" note "${note}", Technology ${JSON.stringify(tech)} vs ${JSON.stringify(last)}, ` +
                    `first "${firstTitle}" ${JSON.stringify(techFirst)} vs ${JSON.stringify(first)}, tab ${nextIsRow}, crumb "${crumb}", advice ${advice}, hidden ${hiddenInMarket}`);
            }
        });

        // Sectors 7: ?focus= opens the chart zoomed, matched by name whatever its case: Finance › National Commercial
        // Banks shows the breadcrumb All sectors › Finance › National Commercial Banks. A name the data does not have
        // (Finance/Banks) leaves the chart at the top, and a malformed focus (three parts) is dropped by the server.
        await step('Sectors 7', fail, async () => {
            const crumbsOf = async (focus) => {
                await page.goto(`${base}/Research/Sectors?focus=${encodeURIComponent(focus)}`, {waitUntil: 'networkidle', timeout: TIMEOUT});
                await page.waitForSelector('#research-treemap svg', {timeout: TIMEOUT});
                return (await page.locator('#research-sectors-breadcrumb .research-breadcrumb__item').allTextContents())
                    .map((t) => t.trim().split(' · ')[0]);
            };
            const banks = await crumbsOf('Finance/National Commercial Banks');
            const legendPressed = ((await page.locator('#research-sectors-legend button[aria-pressed="true"]').textContent()) || '').trim();
            const ranked = (await page.locator('#research-sectors-ranked-title').textContent()).trim();
            const lower = await crumbsOf('finance');
            const unknown = await crumbsOf('Finance/Banks');
            const malformed = await crumbsOf('Finance/National Commercial Banks/Extra');
            const dropped = await page.locator('#research-panel-sectors[data-research-focus]').count() === 0;
            const ok = JSON.stringify(banks) === JSON.stringify(['All sectors', 'Finance', 'National Commercial Banks']) &&
                legendPressed === 'Finance' && ranked.startsWith('Largest parts of National Commercial Banks') &&
                JSON.stringify(lower) === JSON.stringify(['All sectors', 'Finance']) &&
                JSON.stringify(unknown) === JSON.stringify(['All sectors']) &&
                JSON.stringify(malformed) === JSON.stringify(['All sectors']) && dropped;
            if (ok) {
                pass('Sectors 7: ?focus=Finance/National Commercial Banks opens zoomed (All sectors › Finance › National Commercial Banks); case is ignored; an unknown or malformed focus stays at the top');
            } else {
                fail(`Sectors 7: banks ${JSON.stringify(banks)} legend "${legendPressed}" ranked "${ranked}", lower ${JSON.stringify(lower)}, ` +
                    `unknown ${JSON.stringify(unknown)}, malformed ${JSON.stringify(malformed)} dropped ${dropped}`);
            }
        });

        // The Funds steps run on the Funds page (/Research/Funds): the comparison table, the risk view's role filter,
        // the correlation view on every tracked fund, and the holdings list.
        const tickersInTable = () => page.locator('#research-compare-table tbody tr').evaluateAll(
            (rows) => rows.map((r) => r.dataset.ticker));

        // Funds 1: the table opens on 3 years sorted by return; a column header sorts by it (the address keeps it), SPY's
        // fee reads "not filed", and unknown values in the address fall back to the defaults.
        await step('Funds 1', fail, async () => {
            await page.goto(`${base}/Research/Funds?view=nope&window=9&sort=bogus&quarter=1999 Q1`,
                {waitUntil: 'networkidle', timeout: TIMEOUT});
            const current = (await page.locator('.research-funds-page__views [aria-current="page"]').textContent()).trim();
            const window = await page.locator('[data-funds-window][aria-current]').getAttribute('data-funds-window');
            const sortedBy = await page.locator('#research-compare-table th[aria-sort]').getAttribute('data-sort-key');
            const byReturn = await tickersInTable();
            await page.locator('#research-compare-table th[data-sort-key="ticker"] a').click();
            await page.waitForLoadState('networkidle');
            const byTicker = await tickersInTable();
            const sortParam = new URL(page.url()).searchParams.get('sort');
            const ascending = await page.locator('#research-compare-table th[data-sort-key="ticker"]').getAttribute('aria-sort');
            const spyFee = (await page.locator('#research-compare-table tr[data-ticker="SPY"] [data-fee]').textContent()).trim();
            const qqqFee = (await page.locator('#research-compare-table tr[data-ticker="QQQ"] [data-fee]').textContent()).trim();
            const ok = current === 'Compare' && window === '3' && sortedBy === 'return' && byReturn.length === 2 &&
                byTicker.join(',') === 'QQQ,SPY' && sortParam === 'ticker' && ascending === 'ascending' &&
                spyFee === 'not filed' && qqqFee === '0.19%';
            if (ok) {
                pass(`Funds 1: unknown values fall back to Compare, 3 years, by return (${byReturn.join(', ')}); Fund sorts A to Z; SPY's fee is "not filed", QQQ's ${qqqFee}`);
            } else {
                fail(`Funds 1: view "${current}", window ${window}, sorted ${sortedBy} ${JSON.stringify(byReturn)}, ` +
                    `by ticker ${JSON.stringify(byTicker)} (${sortParam}, ${ascending}), fees "${spyFee}"/"${qqqFee}"`);
            }
        });

        // Funds 2: the risk view draws both funds; a role chip narrows them and pressing it again shows every fund.
        await step('Funds 2', fail, async () => {
            await page.goto(`${base}/Research/Funds?view=risk`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-risk-chart svg', {timeout: TIMEOUT});
            const chips = (await page.locator('#research-risk-roles button').allTextContents()).map((t) => t.trim());
            const role = page.locator('#research-risk-roles button', {hasText: 'US broad'});
            await role.click();
            const pressed = await role.getAttribute('aria-pressed');
            const shownNarrowed = (await page.locator('#research-risk-note').textContent()).trim();
            await role.click();
            const released = await role.getAttribute('aria-pressed');
            const ok = chips.includes('US broad') && pressed === 'true' && released === 'false';
            if (ok) {
                pass(`Funds 2: the risk view's role chips (${chips.join(', ')}) narrow the funds and release again`);
            } else {
                fail(`Funds 2: chips ${JSON.stringify(chips)}, pressed ${pressed}/${released}, note "${shownNarrowed}"`);
            }
        });

        // Funds 3: the correlation view opens on every tracked fund, without a basket.
        await step('Funds 3', fail, async () => {
            await page.goto(`${base}/Research/Funds?view=correlation`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const shown = await page.locator('#research-correlation-view').isVisible();
            const allPressed = await page.locator('#research-correlation-all').getAttribute('aria-pressed');
            const columns = await page.locator('#research-correlation-matrix thead th').count();
            const promptHidden = !(await page.locator('#research-correlation-prompt').isVisible());
            if (shown && allPressed === 'true' && columns === 2 && promptHidden) {
                pass('Funds 3: the correlation view opens on all tracked funds, with no basket');
            } else {
                fail(`Funds 3: shown ${shown}, all "${allPressed}", columns ${columns}, prompt hidden ${promptHidden}`);
            }
        });

        // Funds 4: the holdings list opens a fund, then a category; each holding links to the company search by name
        // (never an identifier) and keeps the basket; a quarter link shows that quarter.
        await step('Funds 4', fail, async () => {
            await page.goto(`${base}/Research/Funds?view=holdings&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const latest = await page.locator('#research-holdings-list').getAttribute('data-period');
            const fund = page.locator('#research-holdings-list [data-ticker="SPY"] > details');
            await fund.locator(':scope > summary').click();
            const fundOpen = await fund.evaluate((d) => d.open);
            const category = fund.locator('.research-holdings__category > details').first();
            await category.locator(':scope > summary').click();
            const categoryOpen = await category.evaluate((d) => d.open);
            const link = category.locator('.research-holdings__link').first();
            const linkVisible = await link.isVisible();
            const href = await link.getAttribute('href');
            const url = new URL(href, base);
            const name = (await link.textContent()).trim();
            const identifierFree = !/\b[0-9A-Z]{8}[0-9]\b/.test(href) && !/\b[A-Z]{2}[0-9A-Z]{9}[0-9]\b/.test(href);
            await page.locator('[data-funds-quarter="2025 Q4"]').click();
            await page.waitForLoadState('networkidle');
            const earlier = await page.locator('#research-holdings-list').getAttribute('data-period');
            const keptBasket = new URL(page.url()).searchParams.get('basket');
            const ok = latest === '2026 Q2' && fundOpen && categoryOpen && linkVisible &&
                url.pathname.endsWith('/Research/Company') && url.searchParams.get('q') === name &&
                url.searchParams.get('basket') === 'SPY:60,QQQ:40' && identifierFree && earlier === '2025 Q4' &&
                keptBasket === 'SPY:60,QQQ:40';
            if (ok) {
                pass(`Funds 4: SPY opens to its categories and holdings; "${name}" links to the company search by name with the basket; 2025 Q4 shows on its link`);
            } else {
                fail(`Funds 4: latest ${latest}, open ${fundOpen}/${categoryOpen}, link ${linkVisible} "${href}" ("${name}"), ` +
                    `identifier-free ${identifierFree}, earlier ${earlier}, basket ${keptBasket}`);
            }
        });

        // Step 7: back on the tabbed page, the Funds tab shows the same holdings list at the latest quarter.
        await step('Research 7', fail, async () => {
            await page.goto(`${base}/Research`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.click('#research-tab-funds');
            const list = page.locator('#research-panel-funds #research-holdings-list');
            const shown = await list.isVisible();
            const quarter = await list.getAttribute('data-period');
            const funds = await list.locator(':scope > li').evaluateAll((items) => items.map((i) => i.dataset.ticker));
            if (shown && /^\d{4} Q[1-4]$/.test(quarter) && funds.join(',') === 'SPY,QQQ') {
                pass(`Research 7: the Funds tab lists what SPY and QQQ hold in ${quarter}`);
            } else {
                fail(`Research 7: shown ${shown}, quarter "${quarter}", funds ${JSON.stringify(funds)}`);
            }
        });

        // Step 7b: the Fund Basket bar shows on the Funds tab only; funds are picked by keyboard, weights scale to 100%,
        // "Use equal weights" resets them, and the basket lives in the address so a reload keeps it.
        await step('Research 7b', fail, async () => {
            const bar = page.locator('#research-basket');
            const search = page.locator('#research-basket-search');
            const basketParam = () => new URL(page.url()).searchParams.get('basket');
            const shownOnFunds = await bar.isVisible();
            // The Sectors tab has a basket view, so the bar shows there too; the Companies tab has none.
            await page.click('#research-tab-sectors');
            const shownOnSectors = await bar.isVisible();
            await page.click('#research-tab-companies');
            const hiddenOnCompanies = !(await bar.isVisible());
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
            const ok = shownOnFunds && shownOnSectors && hiddenOnCompanies && (await option.count()) === 0 && Boolean(grouped) &&
                picked === 'QQQ,SPY' && weighted === 'QQQ:50,SPY:70' && scaled && equal === 'QQQ,SPY' &&
                restored.join(',') === 'QQQ,SPY' && narrowFits && removed === 'QQQ';
            if (ok) {
                pass(`Research 7b: the basket bar picks QQQ then SPY by keyboard (grouped under "${grouped.trim()}"), scales 50/70, resets to equal, survives a reload and removes SPY`);
            } else {
                fail(`Research 7b: shown ${shownOnFunds}/${shownOnSectors}/${!hiddenOnCompanies}, picked ${picked}, weighted ${weighted}, ` +
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

        // Step 7d: with SPY and QQQ in the basket (from 7c), the overlap matrix, callouts and duplicated holdings show;
        // focusing a cell gives the shared count and highlights the pair's rows; a fund's label filters the table;
        // with one fund left the view asks for another.
        await step('Research 7d', fail, async () => {
            const view = page.locator('#research-overlap-view');
            const cells = page.locator('#research-overlap-matrix td[tabindex="0"]');
            const shown = await view.isVisible();
            const cellCount = await cells.count();
            const cellText = (await cells.first().textContent()).trim();
            const values = (await page.locator('#research-overlap-callouts .research-overlap__callout-value').allTextContents()).map((t) => t.trim());
            const largest = (await page.locator('#research-overlap-callouts .research-overlap__callout-note').nth(2).textContent()).trim();
            const firstRow = (await page.locator('#research-overlap-table tbody tr td').first().textContent()).trim();
            await cells.first().focus();
            const tooltip = (await page.locator('#research-overlap-tooltip').textContent()).trim();
            const highlighted = await page.locator('#research-overlap-table tr.is-highlighted').count();
            await page.locator('#research-overlap-matrix button[data-filter-ticker="SPY"]').click();
            const pressed = await page.locator('#research-overlap-matrix button[data-filter-ticker="SPY"]').getAttribute('aria-pressed');
            const filterShown = await page.locator('#research-overlap-filter').isVisible();
            await page.click('#research-overlap-filter-clear');
            const filterCleared = !(await page.locator('#research-overlap-filter').isVisible());
            await page.locator('#research-basket-chips [data-ticker="QQQ"] .research-basket__remove').click();
            const promptShown = await page.locator('#research-overlap-prompt').isVisible();
            const viewHidden = !(await view.isVisible());
            const ok = shown && cellCount === 2 && cellText === '44%' &&
                values.join('|') === '44.1%|62.7%|8.6%' && largest.includes('NVIDIA Corp, held by SPY and QQQ') &&
                firstRow === 'NVIDIA Corp' && tooltip.includes('101 securities in common') && highlighted === 6 &&
                pressed === 'true' && filterShown && filterCleared && promptShown && viewHidden;
            if (ok) {
                pass('Research 7d: SPY and QQQ overlap 44.1% by weight, with callouts, a focusable cell that highlights their 6 shared holdings, a label filter and a prompt for one fund');
            } else {
                fail(`Research 7d: shown ${shown}, cells ${cellCount} "${cellText}", callouts ${values.join('|')}, largest "${largest}", ` +
                    `first row "${firstRow}", tooltip "${tooltip}", highlighted ${highlighted}, pressed ${pressed}, ` +
                    `filter ${filterShown}/${filterCleared}, prompt ${promptShown}/${viewHidden}`);
            }
        });

        // Step 7e: the correlation matrix shows the basket's funds with the precomputed 3-year value printed, switches to
        // 1 year, asks for another fund with one left, and shows every tracked fund with "All tracked funds".
        await step('Research 7e', fail, async () => {
            await page.goto(`${base}/Research?tab=funds&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const expected = await page.evaluate(() => {
                const data = JSON.parse(document.getElementById('research-fund-returns-data').textContent);
                const value = (years) => data.windows.find((w) => w.years === years).correlations[0].correlation.toFixed(2);
                return {three: value(3), one: value(1)};
            });
            const cells = page.locator('#research-correlation-matrix td[tabindex="0"]');
            const shown = await page.locator('#research-correlation-view').isVisible();
            const cellCount = await cells.count();
            const threeText = (await cells.first().textContent()).trim();
            const threePressed = await page.locator('[data-correlation-years="3"]').getAttribute('aria-pressed');
            const callouts = await page.locator('#research-correlation-callouts li').count();
            await cells.first().focus();
            const tooltip = (await page.locator('#research-correlation-tooltip').textContent()).trim();
            await page.click('[data-correlation-years="1"]');
            const oneText = (await cells.first().textContent()).trim();
            await page.locator('#research-basket-chips [data-ticker="QQQ"] .research-basket__remove').click();
            const promptShown = await page.locator('#research-correlation-prompt').isVisible();
            await page.click('#research-correlation-all');
            const allShown = await page.locator('#research-correlation-view').isVisible();
            const allColumns = await page.locator('#research-correlation-matrix thead th').count();
            const ok = shown && cellCount === 2 && threeText === expected.three && threePressed === 'true' && callouts === 1 &&
                tooltip.includes('36 months to June 2026') && oneText === expected.one && promptShown && allShown && allColumns === 2;
            if (ok) {
                pass(`Research 7e: SPY and QQQ correlate ${threeText} over 3 years and ${oneText} over 1 year, as precomputed; one fund prompts; "All tracked funds" shows the grid`);
            } else {
                fail(`Research 7e: shown ${shown}, cells ${cellCount} "${threeText}"/${expected.three}, pressed ${threePressed}, callouts ${callouts}, ` +
                    `tooltip "${tooltip}", 1Y "${oneText}"/${expected.one}, prompt ${promptShown}, all ${allShown}/${allColumns}`);
            }
        });

        // Step 7f: the Performance tab grows 100 in the basket and each fund; the basket line, the table and the switch to
        // inflation-adjusted values match figures computed here from the fixture's monthly returns and CPI.
        await step('Research 7f', fail, async () => {
            await page.goto(`${base}/Research?tab=performance&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const expected = await page.evaluate(() => {
                const returns = JSON.parse(document.getElementById('research-fund-returns-data').textContent);
                const macro = JSON.parse(document.getElementById('research-macro-data').textContent);
                const spy = returns.funds.find((f) => f.ticker === 'SPY');
                const qqq = returns.funds.find((f) => f.ticker === 'QQQ');
                // The fixture's funds share their months, so the basket's month i is 60% SPY plus 40% QQQ.
                const basket = spy.returns.map((r, i) => 0.6 * r + 0.4 * qqq.returns[i]);
                const grow = (rs) => rs.reduce((g, r) => g * (1 + r / 100), 1);
                const cpi = new Map(macro.months.map((m) => [m.month.slice(0, 7), m.consumerPrices]));
                const first = spy.firstMonth.slice(0, 7);
                const last = spy.lastMonth.slice(0, 7);
                const [y, m] = first.split('-').map(Number);
                const baseMonth = m === 1 ? `${y - 1}-12` : `${y}-${String(m - 1).padStart(2, '0')}`;
                const inflation = cpi.get(last) / cpi.get(baseMonth);
                const fmt = (v) => {
                    const r = Math.round(v * 10) / 10;
                    return r < 0 ? `−${Math.abs(r).toFixed(1)}%` : `+${r.toFixed(1)}%`;
                };
                const g = grow(basket);
                return {
                    basketEnd: Math.round(g * 100),
                    basketRealEnd: Math.round(g / inflation * 100),
                    basketTotal: fmt((g - 1) * 100),
                    basketAnnualized: fmt((Math.pow(g, 12 / basket.length) - 1) * 100),
                    basketAfterInflation: fmt((g / inflation - 1) * 100),
                    spyTotal: fmt((grow(spy.returns) - 1) * 100),
                    spyEnd: Math.round(grow(spy.returns) * 100)
                };
            });
            const cells = async (line) => (await page.locator(`#research-performance-table tr[data-line="${line}"] td`).allTextContents()).map((t) => t.trim());
            const shown = await page.locator('#research-performance-view').isVisible();
            const basketLabel = (await page.locator('.research-performance__end--basket').textContent()).trim();
            const basketRow = await cells('Your basket');
            const spyRow = await cells('SPY');
            const lines = await page.locator('#research-performance-chart path.research-performance__line').count();
            const credit = (await page.locator('#research-performance .research-card__source').textContent()).trim();
            await page.click('[data-performance-view="real"]');
            const realLabel = (await page.locator('.research-performance__end--basket').textContent()).trim();
            const realPressed = await page.locator('[data-performance-view="real"]').getAttribute('aria-pressed');
            const ok = shown && lines === 3 && basketLabel === `Basket ${expected.basketEnd}` &&
                basketRow[0] === expected.basketAnnualized && basketRow[1] === expected.basketTotal &&
                basketRow[3] === expected.basketAfterInflation && spyRow[1] === expected.spyTotal &&
                realLabel === `Basket ${expected.basketRealEnd}` && realPressed === 'true' &&
                credit.includes('Bureau of Labor Statistics') && credit.includes('SEC EDGAR');
            if (ok) {
                pass(`Research 7f: 100 in the basket grows to ${expected.basketEnd} (${expected.basketTotal}, ${expected.basketAnnualized} a year), ${expected.basketRealEnd} after inflation; SPY ${expected.spyTotal}; sources credited`);
            } else {
                fail(`Research 7f: shown ${shown}, lines ${lines}, label "${basketLabel}"/${expected.basketEnd}, basket row ${JSON.stringify(basketRow)} vs ` +
                    `${JSON.stringify(expected)}, SPY row ${JSON.stringify(spyRow)}, real "${realLabel}" pressed ${realPressed}, credit "${credit}"`);
            }
        });

        // Step 7g: with SPY in the basket, the chart compares it with QQQ; the crosshair moves by keyboard and stays on one
        // x across the three panels; the 1Y preset sets the brush, and the table follows it.
        await step('Research 7g', fail, async () => {
            await page.goto(`${base}/Research?tab=performance&basket=SPY`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const tip = async () => (await page.locator('#research-performance-tooltip span').allTextContents()).map((t) => t.trim());
            const aligned = () => page.evaluate(() => {
                const group = document.querySelector('.research-performance__crosshair');
                const x = group.querySelector('line').getAttribute('x1');
                return Array.from(group.querySelectorAll('circle')).every((c) => c.getAttribute('cx') === x);
            });
            const compare = await page.locator('#research-performance-compare').inputValue();
            const areas = await page.locator('path.research-performance__difference').count();
            await page.focus('#research-performance-chart');
            const atEnd = await tip();
            await page.keyboard.press('ArrowLeft');
            const left = await tip();
            const leftAligned = await aligned();
            await page.keyboard.press('Home');
            const home = await tip();
            const readout = (await page.locator('#research-performance-readout').textContent()).trim();
            await page.click('[data-performance-range="12"]');
            const pressed = await page.locator('[data-performance-range="12"]').getAttribute('aria-pressed');
            const caption = (await page.locator('#research-performance-table caption').textContent()).trim();
            const spyTotal = (await page.locator('#research-performance-table tr[data-line="SPY"] td').nth(1).textContent()).trim();
            const brushWidth = Number(await page.locator('#research-performance-brush .selection').getAttribute('width'));
            const stripWidth = Number((await page.locator('#research-performance-brush svg').getAttribute('viewBox')).split(' ')[2]);
            const expected = await page.evaluate(() => {
                const returns = JSON.parse(document.getElementById('research-fund-returns-data').textContent);
                const spy = returns.funds.find((f) => f.ticker === 'SPY').returns.slice(-12);
                const total = (spy.reduce((g, r) => g * (1 + r / 100), 1) - 1) * 100;
                const r = Math.round(total * 10) / 10;
                return r < 0 ? `−${Math.abs(r).toFixed(1)}%` : `+${r.toFixed(1)}%`;
            });
            await page.click('[data-performance-range="max"]');
            const maxCaption = (await page.locator('#research-performance-table caption').textContent()).trim();
            const ok = compare === 'QQQ' && areas === 2 && atEnd[0] === 'June 2026' && left[0] === 'May 2026' && leftAligned &&
                home[0] === 'June 2021' && left.some((r) => r.startsWith('Basket minus QQQ:')) &&
                left.some((r) => /^Federal funds rate: [0-9.]+%$/.test(r)) && readout.startsWith('June 2021') &&
                pressed === 'true' && caption === 'Returns from July 2025 to June 2026' && spyTotal === expected &&
                brushWidth > 0 && brushWidth < stripWidth / 3 && maxCaption === 'Returns from July 2021 to June 2026';
            if (ok) {
                pass(`Research 7g: SPY against QQQ; ← and Home move one crosshair across the three panels (${left.length} tooltip rows); 1Y sets the brush and the table (SPY ${spyTotal}); Max restores it`);
            } else {
                fail(`Research 7g: compare ${compare}, areas ${areas}, end ${JSON.stringify(atEnd)}, left ${JSON.stringify(left)} aligned ${leftAligned}, ` +
                    `home ${home[0]}, readout "${readout.slice(0, 40)}", pressed ${pressed}, caption "${caption}", SPY ${spyTotal}/${expected}, ` +
                    `brush ${brushWidth}/${stripWidth}, max "${maxCaption}"`);
            }
        });

        // Step 7h: the chart shades the derived hiking cycle with a label, marks the basket's worst fall with the figure
        // the callout gives, and receives the configured recessions; a 1-year period leaves the earlier band out.
        await step('Research 7h', fail, async () => {
            await page.goto(`${base}/Research?tab=performance&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const macro = await page.evaluate(() => JSON.parse(document.getElementById('research-macro-data').textContent));
            const monthName = (iso) => new Date(`${iso.slice(0, 7)}-15T00:00:00Z`).toLocaleString('en-US', {month: 'long', year: 'numeric', timeZone: 'UTC'});
            const cycle = macro.hikingCycles[0];
            const bands = await page.locator('rect.research-performance__band--hike').count();
            const bandLabels = (await page.locator('.research-performance__band-label').allTextContents()).map((t) => t.trim());
            const legendText = (await page.locator('#research-performance-legend').textContent()).replace(/\s+/g, ' ');
            const aria = await page.locator('#research-performance-chart').getAttribute('aria-label');
            const worstLabel = (await page.locator('.research-performance__worst-label').textContent()).trim();
            const worstCallout = (await page.locator('#research-performance-callouts li').nth(1).locator('.research-overlap__callout-value').textContent()).trim();
            await page.click('[data-performance-range="12"]');
            const yearBands = await page.locator('rect.research-performance__band').count();
            await page.click('[data-performance-range="max"]');
            const ok = macro.hikingCycles.length === 1 && bands === 1 && bandLabels.includes('Fed rate rises') &&
                legendText.includes('Fed rate rises (shaded)') && legendText.includes("Basket's worst fall from a high") &&
                aria.includes(`Fed rate rises from ${monthName(cycle.start)} to ${monthName(cycle.end)}`) &&
                worstLabel === `Worst fall ${worstCallout}` && yearBands === 0 &&
                macro.recessions.some((r) => r.label === '2020 recession' && r.start.startsWith('2020-02'));
            if (ok) {
                pass(`Research 7h: "Fed rate rises" shaded ${monthName(cycle.start)} to ${monthName(cycle.end)} with a label and legend; "${worstLabel}" marked; the configured 2020 recession arrives; 1Y leaves the band out`);
            } else {
                fail(`Research 7h: cycles ${JSON.stringify(macro.hikingCycles)}, bands ${bands}, labels ${JSON.stringify(bandLabels)}, legend "${legendText}", ` +
                    `aria "${aria}", worst "${worstLabel}"/${worstCallout}, 1Y bands ${yearBands}, recessions ${JSON.stringify(macro.recessions)}`);
            }
        });

        // Step 7i: the Risk vs return tab plots the funds' precomputed figures and the basket diamond computed here from the
        // fixture; focusing a bubble opens its card (expense ratio, or n/a for SPY); the table sorts by Sharpe ratio and a
        // negative Sharpe ratio reads with a minus sign.
        await step('Research 7i', fail, async () => {
            await page.goto(`${base}/Research?tab=risk&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const expected = await page.evaluate(() => {
                const returns = JSON.parse(document.getElementById('research-fund-returns-data').textContent);
                const macro = JSON.parse(document.getElementById('research-macro-data').textContent);
                const spy = returns.funds.find((f) => f.ticker === 'SPY').returns.slice(-36);
                const qqq = returns.funds.find((f) => f.ticker === 'QQQ').returns.slice(-36);
                const basket = spy.map((r, i) => 0.6 * r + 0.4 * qqq[i]);
                const growth = basket.reduce((g, r) => g * (1 + r / 100), 1);
                const annualized = (Math.pow(growth, 12 / 36) - 1) * 100;
                const mean = basket.reduce((a, r) => a + r, 0) / 36;
                const volatility = Math.sqrt(basket.reduce((a, r) => a + (r - mean) ** 2, 0) / 35) * Math.sqrt(12);
                const bills = macro.months.slice(-36).map((m) => m.treasuryBill);
                const bill = bills.reduce((a, b) => a + b, 0) / bills.length;
                const fmt = (v, d) => (v < 0 ? `−${Math.abs(v).toFixed(d)}` : v.toFixed(d));
                const oneYear = returns.windows.find((w) => w.years === 1).funds.find((f) => f.ticker === 'QQQ');
                return {
                    annualized: `Annualized return: ${fmt(annualized, 1)}% a year`,
                    volatility: `Volatility: ${volatility.toFixed(1)}%`,
                    sharpe: `Sharpe ratio: ${fmt((annualized - bill) / volatility, 2)} (Treasury bill ${bill.toFixed(2)}%)`,
                    qqqOneYearSharpe: fmt(oneYear.sharpe, 2)
                };
            });
            const card = async () => (await page.locator('#research-risk-card span').allTextContents()).map((t) => t.trim());
            const bubbles = await page.locator('#research-risk-chart .research-risk__point:not(.research-risk__point--basket)').count();
            await page.focus('#research-risk-chart [data-ticker="Your basket"]');
            const basketCard = await card();
            await page.focus('#research-risk-chart [data-ticker="SPY"]');
            const spyCard = await card();
            await page.focus('#research-risk-chart [data-ticker="QQQ"]');
            const qqqCard = await card();
            await page.click('[data-risk-years="1"]');
            await page.click('#research-risk-table-toggle');
            const rows = await page.locator('#research-risk-table tbody tr').evaluateAll((trs) => trs.map((tr) => ({
                ticker: tr.dataset.ticker, sharpe: tr.children[3].textContent.trim()
            })));
            const qqqRow = rows.find((r) => r.ticker === 'QQQ');
            const sorted = rows.every((r, i) => i === 0 || Number(r.sharpe.replace('−', '-')) <= Number(rows[i - 1].sharpe.replace('−', '-')));
            const ok = bubbles === 2 && basketCard.includes(expected.annualized) && basketCard.includes(expected.volatility) &&
                basketCard.includes(expected.sharpe) && spyCard.includes('Expense ratio: n/a') &&
                qqqCard.includes('Expense ratio: 0.19%') && qqqCard.some((r) => r.startsWith('Net assets: $')) &&
                qqqRow && qqqRow.sharpe === expected.qqqOneYearSharpe && qqqRow.sharpe.startsWith('−') && sorted && rows.length === 3;
            if (ok) {
                pass(`Research 7i: the basket diamond matches the fixture (${expected.annualized.split(': ')[1]}, ${expected.volatility.split(': ')[1]}); SPY's expense ratio reads n/a, QQQ's 0.19%; over 1 year QQQ's Sharpe ratio reads ${qqqRow.sharpe} in a table sorted by Sharpe`);
            } else {
                fail(`Research 7i: bubbles ${bubbles}, basket ${JSON.stringify(basketCard)} vs ${JSON.stringify(expected)}, SPY ${JSON.stringify(spyCard)}, ` +
                    `QQQ ${JSON.stringify(qqqCard)}, rows ${JSON.stringify(rows)}`);
            }
        });

        // Step 7j: with SPY alone in the basket the diamond sits on SPY's bubble, and the labels still don't overlap (one
        // moves aside on a leader line); role chips hide funds without repainting the rest; the corners are described
        // against the basket, and nothing on the tab gives advice.
        await step('Research 7j', fail, async () => {
            await page.goto(`${base}/Research?tab=risk&basket=SPY`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const labelBoxes = () => page.evaluate(() => Array.from(document.querySelectorAll('#research-risk-chart .research-risk__label'))
                .map((t) => { const b = t.getBBox(); return { label: t.textContent, x: b.x, y: b.y, w: b.width, h: b.height }; }));
            const boxes = await labelBoxes();
            const overlapping = boxes.some((a, i) => boxes.some((b, j) => j > i &&
                a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h));
            const leaders = await page.locator('#research-risk-chart .research-risk__leader').count();
            await page.goto(`${base}/Research?tab=risk&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const quadrants = (await page.locator('.research-risk__quadrant').allTextContents()).map((t) => t.trim());
            const fill = () => page.evaluate(() => getComputedStyle(document.querySelector('#research-risk-chart [data-ticker="SPY"] circle')).fill);
            const spyBefore = await fill();
            const chips = (await page.locator('#research-risk-roles button').allTextContents()).map((t) => t.trim());
            await page.locator('#research-risk-roles button', {hasText: 'US broad'}).click();
            const shownAfter = await page.locator('#research-risk-chart .research-risk__point').evaluateAll((g) => g.map((n) => n.getAttribute('data-ticker')));
            const spyAfter = await fill();
            const pressed = await page.locator('#research-risk-roles button', {hasText: 'US broad'}).getAttribute('aria-pressed');
            await page.locator('#research-risk-roles button', {hasText: 'US broad'}).click();
            const restored = await page.locator('#research-risk-chart .research-risk__point').count();
            const words = (await page.locator('#research-panel-risk').innerText()).toLowerCase();
            const advice = ['efficient', ' best', ' buy', 'should', 'recommend'].filter((w) => words.includes(w));
            const ok = boxes.length === 3 && !overlapping && leaders >= 1 &&
                quadrants.length === 4 && quadrants.includes('Higher return, smaller swings than your basket') &&
                chips.join('|') === 'US broad|Growth, sector and thematic' && pressed === 'true' &&
                shownAfter.join(',') === 'SPY,Your basket' && spyBefore === spyAfter && restored === 3 && advice.length === 0;
            if (ok) {
                pass(`Research 7j: SPY and the basket on one spot keep apart labels (${leaders} leader); "US broad" leaves SPY and the basket in the same colour; four corners described against the basket; no advice wording`);
            } else {
                fail(`Research 7j: boxes ${JSON.stringify(boxes)} overlapping ${overlapping}, leaders ${leaders}, quadrants ${JSON.stringify(quadrants)}, chips ${JSON.stringify(chips)}, ` +
                    `pressed ${pressed}, shown ${shownAfter}, fill ${spyBefore}/${spyAfter}, restored ${restored}, advice ${JSON.stringify(advice)}`);
            }
        });

        // The World steps run on the World page (/Research/World), which shares its panel and scripts with the tabbed
        // page's World tab; steps 8b, 8c, 9 and 11 below still cover that tab.

        // World 1 (was Research 8): the World page opens on the market map, the Research nav marks World as the current
        // page, and switching to % of GDP changes the legend.
        await step('World 1', fail, async () => {
            await page.goto(`${base}/Research/World`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-world-map svg', {timeout: TIMEOUT});
            const current = ((await page.locator('.research-nav a[aria-current="page"]').textContent()) || '').trim();
            const tabs = await page.locator('[role="tablist"]').count();
            const legend = page.locator('#research-world-legend');
            const before = await legend.getAttribute('aria-label');
            const button = page.locator('[data-research-world-metric="percentOfGdp"]');
            await button.click();
            const after = await legend.getAttribute('aria-label');
            const firstStep = await legend.locator('li').first().textContent();
            if (current === 'World' && tabs === 0 && before !== after && /% of GDP/.test(after) && firstStep.includes('%') &&
                await button.getAttribute('aria-pressed') === 'true') {
                pass(`World 1: the World page shows the map, World current in the nav; "% of GDP" changes the legend to "${after}"`);
            } else {
                fail(`World 1: nav current "${current}", tablists ${tabs}, legend "${before}" -> "${after}", first step "${firstStep}"`);
            }
        });

        // World 2: with a basket in the address the World page still opens on the market map; ?view=basket opens the
        // basket map with the basket kept, and switching back to the market drops view= from the address.
        await step('World 2', fail, async () => {
            await page.goto(`${base}/Research/World?basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-world-map svg', {timeout: TIMEOUT});
            const marketFirst = await page.locator('[data-world-view="market"]').getAttribute('aria-pressed');
            await page.goto(`${base}/Research/World?view=basket&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-world-basket-map svg path', {timeout: TIMEOUT});
            const basketOn = await page.locator('[data-world-view="basket"]').getAttribute('aria-pressed');
            const marketHidden = !(await page.locator('#research-world-map').isVisible());
            const callouts = await page.locator('#research-world-basket-callouts li').count();
            await page.click('[data-world-view="market"]');
            const address = await addressOf(page);
            const ok = marketFirst === 'true' && basketOn === 'true' && marketHidden && callouts > 0 &&
                !address.searchParams.has('view') && address.searchParams.get('basket') === 'SPY:60,QQQ:40';
            if (ok) {
                pass('World 2: the page opens on the market map, ?view=basket opens the basket map with the basket kept, and the market switch drops view= from the address');
            } else {
                fail(`World 2: market first ${marketFirst}, basket ${basketOn}/${marketHidden}, callouts ${callouts}, address ${address.search}`);
            }
        });

        // World 3: the side panel's country list selects a country and puts it in the address; choosing none clears
        // the detail and the address's country=.
        await step('World 3', fail, async () => {
            await page.goto(`${base}/Research/World`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-world-map svg', {timeout: TIMEOUT});
            const hintBefore = await page.locator('#research-world-detail-hint').isVisible();
            await page.selectOption('#research-world-country', 'India');
            const name = ((await page.locator('#research-world-detail .research-world__detail-name').textContent()) || '').trim();
            const inAddress = (await addressOf(page)).searchParams.get('country');
            const outlined = await page.locator('#research-world-map .research-world__country--selected').count();
            await page.selectOption('#research-world-country', '');
            const detailHidden = !(await page.locator('#research-world-detail').isVisible());
            const cleared = !(await addressOf(page)).searchParams.has('country');
            if (hintBefore && name === 'India' && inAddress === 'India' && outlined === 1 && detailHidden && cleared) {
                pass('World 3: choosing India in the list shows its detail, outlines it and puts country=India in the address; choosing none clears both');
            } else {
                fail(`World 3: hint ${hintBefore}, detail "${name}", address "${inAddress}", outlined ${outlined}, hidden ${detailHidden}, cleared ${cleared}`);
            }
        });

        // World 4: ?country=Japan opens with Japan selected and its detail shown, matched whatever its case; an unknown
        // name selects nothing and leaves the address, and a malformed one is dropped by the server, without a page error.
        await step('World 4', fail, async () => {
            const errorsBefore = pageErrors.length;
            const open = async (country) => {
                await page.goto(`${base}/Research/World?country=${encodeURIComponent(country)}`, {waitUntil: 'networkidle', timeout: TIMEOUT});
                await page.waitForSelector('#research-world-map svg', {timeout: TIMEOUT});
                const detail = page.locator('#research-world-detail');
                return {
                    name: await detail.isVisible() ? ((await detail.locator('.research-world__detail-name').textContent()) || '').trim() : '',
                    figures: await detail.locator('dd').count(),
                    selected: await page.locator('#research-world-country').inputValue(),
                    outlined: await page.locator('#research-world-map .research-world__country--selected').count()
                };
            };
            const japan = await open('Japan');
            const lower = await open('japan');
            const unknown = await open('Atlantis');
            unknown.inAddress = (await addressOf(page)).searchParams.has('country');
            const malformed = await open('<script>alert(1)</script>');
            const dropped = await page.locator('#research-panel-world[data-research-country]').count() === 0;
            const ok = japan.name === 'Japan' && japan.figures === 2 && japan.selected === 'Japan' && japan.outlined === 1 &&
                lower.name === 'Japan' && unknown.name === '' && unknown.selected === '' && unknown.outlined === 0 && !unknown.inAddress &&
                malformed.name === '' && dropped && pageErrors.length === errorsBefore;
            if (ok) {
                pass('World 4: ?country=Japan selects Japan and shows its detail; case is ignored; an unknown or malformed country selects nothing, without errors');
            } else {
                fail(`World 4: japan ${JSON.stringify(japan)}, lower ${JSON.stringify(lower)}, unknown ${JSON.stringify(unknown)}, ` +
                    `malformed ${JSON.stringify(malformed)} dropped ${dropped}, page errors ${pageErrors.slice(errorsBefore).join(' | ')}`);
            }
        });

        // Step 8b: without a basket the World tab opens on the market map with "Your basket" off; with SPY and QQQ it opens
        // on the basket map, whose callouts and Hong Kong and Singapore dots match figures computed here from the fixture
        // (Singapore under 0.05% reads "<0.1%"); the map zooms with dots kept their size; emptying the basket goes back.
        await step('Research 8b', fail, async () => {
            await page.goto(`${base}/Research?tab=world`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            const marketFirst = await page.locator('#research-world-map svg').isVisible();
            const basketOff = await page.locator('[data-world-view="basket"]').isDisabled();
            await page.goto(`${base}/Research?tab=world&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-world-basket-map svg path', {timeout: TIMEOUT});
            const expected = await page.evaluate(() => {
                const data = JSON.parse(document.getElementById('research-fund-exposure-data').textContent);
                const shares = { SPY: 0.6, QQQ: 0.4 };
                const country = (code) => data.funds.reduce((sum, f) => sum + shares[f.ticker] * (f.countries.find((c) => c.code === code)?.percent || 0), 0);
                const outside = data.funds.reduce((sum, f) => sum + shares[f.ticker] * f.countries.filter((c) => c.code !== 'US').reduce((a, c) => a + c.percent, 0), 0);
                const fmt = (v) => (v > 0 && v < 0.05 ? '<0.1%' : `${v.toFixed(1)}%`);
                return { hk: fmt(country('HK')), sg: fmt(country('SG')), outside: fmt(outside), ie: fmt(country('IE')) };
            });
            const basketOn = await page.locator('[data-world-view="basket"]').getAttribute('aria-pressed');
            const marketHidden = !(await page.locator('#research-world-map').isVisible());
            const values = (await page.locator('#research-world-basket-callouts .research-overlap__callout-value').allTextContents()).map((t) => t.trim());
            const notes = (await page.locator('#research-world-basket-callouts .research-overlap__callout-note').allTextContents()).map((t) => t.trim());
            const hk = await page.locator('.research-world-basket__dot[data-code="HK"]').getAttribute('aria-label');
            const sg = await page.locator('.research-world-basket__dot[data-code="SG"]').getAttribute('aria-label');
            const rBefore = Number(await page.locator('.research-world-basket__dot[data-code="HK"]').getAttribute('r'));
            await page.locator('#research-world-basket-map').scrollIntoViewIfNeeded();
            const box = await page.locator('#research-world-basket-map').boundingBox();
            await page.mouse.move(box.x + box.width * 0.75, box.y + box.height * 0.45);
            await page.mouse.wheel(0, -600);
            await page.waitForTimeout(400);
            const transform = await page.locator('#research-world-basket-map svg > g').getAttribute('transform');
            const rAfter = Number(await page.locator('.research-world-basket__dot[data-code="HK"]').getAttribute('r'));
            await page.click('#research-world-basket-reset');
            await page.locator('#research-basket-chips [data-ticker="QQQ"] .research-basket__remove').click();
            await page.locator('#research-basket-chips [data-ticker="SPY"] .research-basket__remove').click();
            const backToMarket = await page.locator('[data-world-view="market"]').getAttribute('aria-pressed');
            const ok = marketFirst && basketOff && basketOn === 'true' && marketHidden &&
                values[0] === expected.outside && notes[0].includes(`Ireland, ${expected.ie}`) && notes[1].includes('HKD') &&
                notes[2] === 'North America' && hk.endsWith(`${expected.hk} of your basket`) && sg.endsWith('<0.1% of your basket') &&
                expected.sg === '<0.1%' && /scale\(/.test(transform || '') && rAfter < rBefore && backToMarket === 'true';
            if (ok) {
                pass(`Research 8b: market map without a basket; with SPY and QQQ the basket map shows ${expected.outside} outside the US (Ireland ${expected.ie}), Hong Kong ${expected.hk} and Singapore <0.1% as dots, zooms with dots kept their size, and an empty basket returns to the market`);
            } else {
                fail(`Research 8b: market ${marketFirst}/${basketOff}, basket ${basketOn}/${marketHidden}, callouts ${JSON.stringify(values)} ${JSON.stringify(notes)} vs ${JSON.stringify(expected)}, ` +
                    `HK "${hk}", SG "${sg}", zoom ${transform} r ${rBefore}->${rAfter}, back ${backToMarket}`);
            }
        });

        // Step 8c: the basket view's meters give their values to screen readers, focusing a region bar lights exactly its
        // countries (choosing it keeps them lit), the currency bars list what holdings are priced in, and the required
        // wording is there.
        await step('Research 8c', fail, async () => {
            await page.goto(`${base}/Research?tab=world&basket=SPY:60,QQQ:40`, {waitUntil: 'networkidle', timeout: TIMEOUT});
            await page.waitForSelector('#research-world-basket-map svg path', {timeout: TIMEOUT});
            const expected = await page.evaluate(() => {
                const data = JSON.parse(document.getElementById('research-fund-exposure-data').textContent);
                const shares = { SPY: 0.6, QQQ: 0.4 };
                const us = data.funds.reduce((sum, f) => sum + shares[f.ticker] * (f.countries.find((c) => c.code === 'US')?.percent || 0), 0);
                const europe = [...new Set(data.funds.flatMap((f) => f.countries.filter((c) => c.region === 'Europe').map((c) => c.code)))].sort();
                return { us: (Math.round(us * 10) / 10).toString(), europe };
            });
            const meters = await page.locator('#research-world-basket-meters [role="meter"]').evaluateAll((ms) => ms.map((m) => ({
                now: m.getAttribute('aria-valuenow'), text: m.getAttribute('aria-valuetext'), label: document.getElementById(m.getAttribute('aria-labelledby')).textContent
            })));
            const regions = (await page.locator('#research-world-basket-regions button .research-world-basket__bar-label').allTextContents()).map((t) => t.trim());
            await page.focus('#research-world-basket-regions button[data-region="Europe"]');
            const lit = () => page.locator('#research-world-basket-map .is-lit').evaluateAll((els) => els.map((e) => e.getAttribute('data-code')).sort());
            const litOnFocus = await lit();
            await page.click('#research-world-basket-regions button[data-region="Europe"]');
            await page.focus('#research-world-basket-reset');
            const litPinned = await lit();
            const pressed = await page.locator('#research-world-basket-regions button[data-region="Europe"]').getAttribute('aria-pressed');
            const currencies = (await page.locator('#research-world-basket-currencies .research-world-basket__bar-label').allTextContents()).map((t) => t.trim());
            const subtitle = (await page.locator('#research-world-basket .research-card__subtitle').textContent()).replace(/\s+/g, ' ');
            const wording = ["where each holding's issuer is organized, as each fund reports it", 'count as US dollars', 'not where companies earn their revenue']
                .every((phrase) => subtitle.includes(phrase));
            const ok = meters.length === 2 && meters[0].now === expected.us && meters[0].text.includes('Rest of world') &&
                meters[1].label.startsWith('US dollar vs other currencies') &&
                regions.join('|') === 'North America|Europe|Asia Pacific|Latin America|Middle East & Africa' &&
                litOnFocus.join() === expected.europe.join() && litPinned.join() === expected.europe.join() && pressed === 'true' &&
                currencies.join('|') === 'USD|HKD' && wording;
            if (ok) {
                pass(`Research 8c: meters read US ${expected.us}% and US dollar; focusing or choosing Europe lights ${expected.europe.join(', ')}; currencies USD and HKD; the required wording is present`);
            } else {
                fail(`Research 8c: meters ${JSON.stringify(meters)} vs ${expected.us}, regions ${JSON.stringify(regions)}, lit ${litOnFocus}/${litPinned} vs ${expected.europe}, ` +
                    `pressed ${pressed}, currencies ${JSON.stringify(currencies)}, wording ${wording} "${subtitle}"`);
            }
        });

        // Step 9: "Show as table" lists rows on each chart tab (the Funds tab's holdings are a list already).
        await step('Research 9', fail, async () => {
            const counts = {};
            for (const tabId of ['sectors', 'world']) {
                await page.click(`#research-tab-${tabId}`);
                // With a basket set, World opens on the basket map: the table belongs to the market view.
                if (tabId === 'world' && await page.locator('[data-world-view="market"][aria-pressed="false"]').count()) {
                    await page.click('[data-world-view="market"]');
                }
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
                await reduced.waitForSelector('#research-treemap g.research-treemap__tile', {timeout: TIMEOUT});
                const shape = () => reduced.evaluate(() => [...document.querySelectorAll('#research-treemap g.research-treemap__tile')]
                    .map((tile) => {
                        const rect = tile.querySelector('rect');
                        return `${tile.dataset.key}=${tile.getAttribute('transform')}/${rect.getAttribute('width')}x${rect.getAttribute('height')}`;
                    }).join('|'));
                await reduced.locator('#research-sectors-year').fill('0');
                const atOnce = await shape();
                await reduced.waitForTimeout(1200);
                const settled = await shape();
                const label = await periodLabel(reduced, 'sectors');
                if (label === earliest && atOnce === settled) {
                    pass('Research 10: with reduced motion the slider change is drawn at once, with no transition');
                } else {
                    fail(`Research 10: label "${label}", tiles changed after the change: ${atOnce !== settled}`);
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
                await mobile.waitForSelector('#research-treemap svg', {timeout: TIMEOUT});
                const wide = [];
                for (const tabId of ['sectors', 'funds', 'performance', 'risk', 'world']) {
                    await mobile.tap(`#research-tab-${tabId}`);
                    await mobile.waitForTimeout(300);
                    if (await scrollsSideways(mobile)) {
                        wide.push(tabId);
                    }
                }
                await mobile.tap('#research-tab-sectors');
                // With a basket set, the tab opens on Your basket (the Sectors page only does so with ?view=basket),
                // which has no years: the slider is the market's.
                await mobile.waitForSelector('[data-sectors-view="basket"][aria-pressed="true"]', {timeout: TIMEOUT});
                pass('Research 11: with a basket set, the Sectors tab opens on Your basket');
                await mobile.tap('[data-sectors-view="market"]');
                // The cookie banner is fixed to the bottom of the screen and, depending on how its text wraps, can
                // cover the slider; answer it first, as someone on a phone would.
                const essentialOnly = mobile.locator('#cookieAcceptEssential');
                if (await essentialOnly.isVisible()) {
                    await essentialOnly.tap();
                    await mobile.locator('#cookieBanner').waitFor({state: 'hidden', timeout: TIMEOUT});
                }
                const box = await mobile.locator('#research-sectors-year').boundingBox();
                const tapAt = {x: box.x + 2, y: box.y + box.height / 2};
                // What the touch lands on, so a failure says why the slider did not move.
                const target = await mobile.evaluate(({x, y}) => {
                    const hit = document.elementFromPoint(x, y);
                    return hit ? `${hit.tagName.toLowerCase()}${hit.id ? `#${hit.id}` : ''}` : 'nothing';
                }, tapAt);
                await mobile.touchscreen.tap(tapAt.x, tapAt.y);
                const label = await periodLabel(mobile, 'sectors');
                if (wide.length === 0) {
                    pass('Research 11: no horizontal page scroll at 390 px on any tab');
                } else {
                    fail(`Research 11: horizontal page scroll at 390 px on ${wide.join(', ')}`);
                }
                if (label === earliest) {
                    pass(`Research 11: a touch on the slider moves Sectors to ${earliest}`);
                } else {
                    const viewportHeight = await mobile.evaluate(() => window.innerHeight);
                    fail(`Research 11: after a touch at the start of the slider the label is "${label}" ` +
                        `(touch at ${Math.round(tapAt.x)},${Math.round(tapAt.y)} of a ${viewportHeight} px viewport hit ${target})`);
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
 * The Companies tab (spec #242): search, the profile, filings, press releases (spec #271), Back, a fund, reduced
 * motion and a phone.
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

    if (process.env.E2E_PRESS_RELEASES_OFF === 'true') {
        // Releases off: with ApiService's Research__PressReleases__Enabled=false, every lookup says the profile's
        // press releases are unavailable, so the section never shows.
        await step('Releases off', fail, async () => {
            const exampleCorpHidden = await page.locator(`${panel} [data-research-company-releases]`).isHidden();
            const other = watch(await context.newPage());
            try {
                await other.goto(`${base}/Research?tab=companies&ticker=EXGL`, {waitUntil: 'networkidle', timeout: TIMEOUT});
                await other.waitForSelector(`${panel} [data-research-company-profile]:not([hidden])`, {timeout: TIMEOUT});
                const exampleGlobalHidden = await other.locator(`${panel} [data-research-company-releases]`).isHidden();
                if (exampleCorpHidden && exampleGlobalHidden) {
                    pass('Releases off: switched off in ApiService, the section shows for neither company');
                } else {
                    fail(`Releases off: section hidden for EXMP ${exampleCorpHidden}, for EXGL ${exampleGlobalHidden}`);
                }
            } finally {
                await other.close();
            }
        });
    } else {
        // Releases 1 (spec #271): Example Corp's press releases fill in after the profile, linking to sec.gov; the
        // investor-day slides have no readable headline, so that entry shows its 8-K's events instead.
        await step('Releases 1', fail, async () => {
            const items = page.locator(`${panel} [data-research-releases-list] > li`);
            await page.waitForSelector(`${panel} [data-research-releases-list]:not([hidden])`, {timeout: TIMEOUT});
            const count = await items.count();
            const headlines = (await page.locator(`${panel} .research-companies__release-link`).evaluateAll(
                (links) => links.map((link) => link.firstChild.textContent.trim())));
            const firstHref = await page.locator(`${panel} .research-companies__release-link`).first().getAttribute('href');
            const fallback = headlines.some((h) => h.includes('Results of operations'));
            const title = (await page.locator('#research-company-releases-title').textContent()).trim();
            if (count === 3 && firstHref.startsWith('https://www.sec.gov/Archives/edgar/data/') && fallback &&
                title === 'Press releases') {
                pass(`Releases 1: 3 press releases, the first links to sec.gov, one shows its events: ${JSON.stringify(headlines)}`);
            } else {
                fail(`Releases 1: ${count} releases ${JSON.stringify(headlines)}, first link "${firstHref}", title "${title}"`);
            }
        });

        // Releases 2: Example Global plc attached no press releases, which the section says plainly.
        await step('Releases 2', fail, async () => {
            const other = watch(await context.newPage());
            try {
                await other.goto(`${base}/Research?tab=companies&ticker=EXGL`, {waitUntil: 'networkidle', timeout: TIMEOUT});
                const empty = other.locator(`${panel} [data-research-releases-empty]`);
                await empty.waitFor({state: 'visible', timeout: TIMEOUT});
                const text = (await empty.textContent()).trim();
                const listHidden = await other.locator(`${panel} [data-research-releases-list]`).isHidden();
                if (/No press releases/.test(text) && listHidden) {
                    pass(`Releases 2: Example Global shows "${text}"`);
                } else {
                    fail(`Releases 2: empty line "${text}", list hidden ${listHidden}`);
                }
            } finally {
                await other.close();
            }
        });
    }

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
