const {chromium} = require('playwright');
const {runResearchWalkthrough} = require('./research-walkthrough');
const {runMoneyPrioritiesWalkthrough} = require('./money-priorities-walkthrough');

(async () => {
    const results = {passed: 0, failed: 0, errors: []};
    const base = process.env.WEB_URL || 'http://localhost:5000';
    const apiBase = process.env.API_URL || 'https://localhost:7100';

    function pass(message) {
        results.passed++;
        console.log(`  ✓ ${message}`);
    }

    function fail(message) {
        results.failed++;
        results.errors.push(message);
        console.log(`  ✗ ${message}`);
    }

    /**
     * Verify the signed-in Dashboard at one viewport: greeting h1, heading order, summary card,
     * key figure tiles, quick actions, and the mobile bottom navigation clearing the last section.
     * @param {import('playwright').Page} dashPage - A page already showing /Dashboard.
     * @param {{name: string, width: number, mobile: boolean}} vp - The viewport under test.
     */
    async function checkDashboard(dashPage, vp) {
        const label = `Dashboard (${vp.name} ${vp.width}px)`;

        const h1 = dashPage.locator('main h1');
        const h1Count = await h1.count().catch(() => 0);
        const h1Text = h1Count > 0 ? (await h1.first().textContent().catch(() => '')) : '';
        if (h1Count === 1 && h1Text.includes('Welcome back')) {
            pass(`${label}: single h1 greeting "${h1Text.trim()}"`);
        } else {
            fail(`${label}: expected one "Welcome back" h1, found ${h1Count} ("${(h1Text || '').trim()}")`);
        }

        // Heading levels must not skip (e.g. h2 straight to h4) inside the main landmark.
        const skippedHeading = await dashPage.evaluate(() => {
            const levels = [...document.querySelectorAll('main h1, main h2, main h3, main h4, main h5, main h6')]
                .map(h => Number(h.tagName.substring(1)));
            for (let i = 1; i < levels.length; i++) {
                if (levels[i] > levels[i - 1] + 1) {
                    return `h${levels[i - 1]} -> h${levels[i]}`;
                }
            }
            return null;
        });
        if (skippedHeading === null) {
            pass(`${label}: heading levels do not skip`);
        } else {
            fail(`${label}: skipped heading level ${skippedHeading}`);
        }

        const summary = dashPage.locator('section.dash-summary');
        const summaryTitle = await summary.locator('h2').first().textContent({timeout: 3000}).catch(() => '');
        if (await summary.isVisible({timeout: 3000}).catch(() => false) && summaryTitle.includes('Net Worth')) {
            pass(`${label}: summary card is visible`);
        } else {
            fail(`${label}: summary card not found`);
        }

        const tiles = dashPage.locator('section.dash-figures[aria-label="Key figures"] a.dash-tile');
        const tileCount = await tiles.count().catch(() => 0);
        const tilesVisible = tileCount > 0 && await tiles.last().isVisible({timeout: 3000}).catch(() => false);
        if (tileCount === 4 && tilesVisible) {
            pass(`${label}: 4 key figure tiles are visible`);
        } else {
            fail(`${label}: expected 4 visible key figure tiles, found ${tileCount}`);
        }

        const actions = dashPage.locator('section.dash-actions a.dash-action');
        const actionCount = await actions.count().catch(() => 0);
        if (actionCount === 5 && await actions.first().isVisible({timeout: 3000}).catch(() => false)) {
            pass(`${label}: 5 quick actions are visible`);
        } else {
            fail(`${label}: expected 5 visible quick actions, found ${actionCount}`);
        }

        // Every progress bar needs an accessible name and a value range.
        const unlabelledBars = await dashPage.evaluate(() =>
            [...document.querySelectorAll('.dash [role="progressbar"]')].filter(bar =>
                !bar.getAttribute('aria-label') ||
                bar.getAttribute('aria-valuenow') === null ||
                bar.getAttribute('aria-valuemin') === null ||
                bar.getAttribute('aria-valuemax') === null
            ).length
        );
        if (unlabelledBars === 0) {
            pass(`${label}: progress bars expose a label and aria values`);
        } else {
            fail(`${label}: ${unlabelledBars} progress bar(s) missing a label or aria values`);
        }

        const bottomNav = dashPage.locator('nav.bottom-nav');
        const bottomNavVisible = await bottomNav.isVisible().catch(() => false);
        if (!vp.mobile) {
            if (!bottomNavVisible) {
                pass(`${label}: bottom navigation is hidden on desktop`);
            } else {
                fail(`${label}: bottom navigation should be hidden on desktop`);
            }
            return;
        }

        if (bottomNavVisible) {
            pass(`${label}: bottom navigation is visible on mobile`);
        } else {
            fail(`${label}: bottom navigation not visible on mobile`);
        }

        const animationName = await dashPage.evaluate(() => {
            const first = document.querySelector('.dash > *');
            return first ? getComputedStyle(first).animationName : null;
        });
        if (animationName === 'none') {
            pass(`${label}: entrance animation is disabled under prefers-reduced-motion`);
        } else {
            fail(`${label}: entrance animation still runs under reduced motion (${animationName})`);
        }

        // Scroll to the very bottom; the last Dashboard section must end above the fixed bottom nav.
        const overlap = await dashPage.evaluate(() => {
            window.scrollTo(0, document.documentElement.scrollHeight);
            const last = document.querySelector('.dash') && document.querySelector('.dash').lastElementChild;
            const nav = document.querySelector('nav.bottom-nav');
            if (!last || !nav) {
                return null;
            }
            return {
                lastBottom: Math.round(last.getBoundingClientRect().bottom),
                navTop: Math.round(nav.getBoundingClientRect().top)
            };
        });
        if (overlap && overlap.lastBottom <= overlap.navTop) {
            pass(`${label}: last section (bottom ${overlap.lastBottom}px) clears the bottom nav (top ${overlap.navTop}px)`);
        } else {
            fail(`${label}: last section overlaps the bottom nav ${JSON.stringify(overlap)}`);
        }
    }

    // Pages that require authentication — unauthenticated access should redirect to login
    const authPages = [
        {path: '/Networth', name: 'Net Worth', expectedTitle: 'Net Worth Projection'},
        {path: '/GoogleDrive', name: 'Google Drive', expectedTitle: null},
        {path: '/News', name: 'Finance News', expectedTitle: 'Finance News'},
        {path: '/Community', name: 'Community', expectedTitle: 'Community'},
    ];

    // Public pages accessible without authentication
    const publicPages = [
        {path: '/Identity/Account/Login', name: 'Login'},
        {path: '/Identity/Account/Register', name: 'Register'},
    ];

    let browser;
    try {
        console.log('Launching browser...');
        console.log(`  Web URL: ${base}`);
        console.log(`  API URL: ${apiBase}`);
        browser = await chromium.launch({headless: true});
        const context = await browser.newContext({
            ignoreHTTPSErrors: true,
            viewport: {width: 1280, height: 720}
        });
        const page = await context.newPage();

        // Collect console errors globally
        const consoleErrors = [];
        page.on('console', msg => {
            if (msg.type() === 'error') {
                consoleErrors.push(msg.text());
            }
        });

        const pageErrors = [];
        page.on('pageerror', error => {
            pageErrors.push(error.message);
        });

        // =========================================================
        // Test 1: Default route shows the Landing Page when signed out
        // =========================================================
        console.log('\n--- Test 1: Default Route (Landing Page) ---');
        try {
            await page.goto(`${base}/`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });
            const url = new URL(page.url());
            console.log(`  Resolved URL: ${url.href}`);
            const title = await page.title();
            console.log(`  Page title: "${title}"`);

            if (url.pathname === '/') {
                pass('Default route stays on / for signed-out visitors');
            } else {
                fail(`Default route redirected unexpectedly to: ${url.href}`);
            }

            if (title === 'PersonalFinance — Plan for enough') {
                pass('Landing page tab shows the brand and tagline');
            } else {
                fail(`Landing page title unexpected: "${title}"`);
            }

            const headingCount = await page.locator('h1#lp-hero-title').count().catch(() => 0);
            if (headingCount === 1) {
                pass('Landing Page hero heading is present');
            } else {
                fail('Landing Page hero heading not found');
            }

            const registerCta = page.locator('main a[href*="/Identity/Account/Register"]', {hasText: 'Create free account'});
            if (await registerCta.first().isVisible({timeout: 3000}).catch(() => false)) {
                pass('"Create free account" call to action is visible');
            } else {
                fail('"Create free account" call to action not found');
            }

            const signInCta = page.locator('main a[href*="/Identity/Account/Login"]', {hasText: 'Sign in'});
            if (await signInCta.first().isVisible({timeout: 3000}).catch(() => false)) {
                pass('"Sign in" call to action is visible');
            } else {
                fail('"Sign in" call to action not found');
            }

            const plan = page.locator('figure.lp-plan');
            const planCaption = await plan.first().locator('figcaption').textContent({timeout: 3000}).catch(() => '');
            if (planCaption && planCaption.includes('Sample plan') && planCaption.includes('FI target')) {
                pass('Sample plan chart has a visible text description of its figures');
            } else {
                fail('Sample plan chart caption not found');
            }

            const coreCount = await page.locator('.lp-tool').count().catch(() => 0);
            const moreCount = await page.locator('.lp-more__item').count().catch(() => 0);
            if (coreCount === 3 && moreCount === 6) {
                pass('Landing Page lists 3 core tools and 6 additional features');
            } else {
                fail(`Expected 3 core tools and 6 additional features, found ${coreCount} and ${moreCount}`);
            }
        } catch (error) {
            fail(`Default route failed: ${error.message}`);
        }

        // =========================================================
        // Test 1b: The Skyline never slows the Landing Page (ADR 0013)
        // =========================================================
        console.log('\n--- Test 1b: Landing Page Skyline ---');
        const skylineFile = /maplibre-gl|skyline-poster|stanton-skyline/;

        /**
         * Open the Landing Page in a fresh context and report the Skyline's requests and final state.
         * @param {object} contextOptions - Playwright context options (viewport, reducedMotion).
         * @returns {Promise<{files: string[], state: object}>} The Skyline files requested and the container state.
         */
        async function visitSkyline(contextOptions) {
            const skylineContext = await browser.newContext({ignoreHTTPSErrors: true, ...contextOptions});
            try {
                const skylinePage = await skylineContext.newPage();
                const files = [];
                skylinePage.on('request', request => {
                    if (skylineFile.test(request.url())) {
                        files.push(request.url());
                    }
                });
                await skylinePage.goto(`${base}/`, {waitUntil: 'networkidle', timeout: 15000});
                // The live scene starts once the browser is idle; give it time to settle either way.
                await skylinePage.waitForFunction(() => {
                    const el = document.querySelector('[data-skyline]');
                    return !el || !el.dataset.skylineState || ['live', 'skipped'].includes(el.dataset.skylineState);
                }, null, {timeout: 20000}).catch(() => {});
                const state = await skylinePage.evaluate(() => {
                    const el = document.querySelector('[data-skyline]');
                    const loadStart = performance.getEntriesByType('navigation')[0].loadEventStart;
                    const mapLibre = performance.getEntriesByType('resource').find(r => r.name.includes('maplibre-gl.js'));
                    return {
                        state: el ? el.dataset.skylineState : null,
                        reason: el ? el.dataset.skylineReason : null,
                        canvas: Boolean(el && el.querySelector('canvas')),
                        mapLibreAfterLoad: mapLibre ? mapLibre.startTime >= loadStart : null,
                        horizontalScroll: document.documentElement.scrollWidth > window.innerWidth
                    };
                });
                return {files, state};
            } finally {
                await skylineContext.close();
            }
        }

        try {
            for (const vp of [{name: 'phone', width: 375, height: 667}, {name: 'tablet', width: 768, height: 1024}]) {
                const {files, state} = await visitSkyline({viewport: {width: vp.width, height: vp.height}});
                if (files.length === 0 && !state.state) {
                    pass(`Skyline (${vp.name} ${vp.width}px): no MapLibre, data or poster requested`);
                } else {
                    fail(`Skyline (${vp.name} ${vp.width}px): expected no Skyline requests, got ${files.join(', ') || state.state}`);
                }
            }

            const desktop = await visitSkyline({viewport: {width: 1280, height: 720}});
            if (desktop.files.some(f => f.includes('skyline-poster'))) {
                pass('Skyline (desktop): poster requested');
            } else {
                fail('Skyline (desktop): poster was not requested');
            }
            if (desktop.state.state === 'live' && desktop.state.mapLibreAfterLoad === true && desktop.state.canvas) {
                pass('Skyline (desktop): MapLibre loaded after the load event and the scene is live');
            } else if (desktop.state.state === 'skipped' && desktop.state.reason === 'no-webgl' && desktop.state.mapLibreAfterLoad === null) {
                pass('Skyline (desktop): no WebGL in this browser, poster kept and MapLibre never requested');
            } else {
                fail(`Skyline (desktop): unexpected state ${JSON.stringify(desktop.state)}`);
            }
            if (!desktop.state.horizontalScroll) {
                pass('Skyline (desktop): no horizontal scroll');
            } else {
                fail('Skyline (desktop): the Skyline adds a horizontal scrollbar');
            }

            const reduced = await visitSkyline({viewport: {width: 1280, height: 720}, reducedMotion: 'reduce'});
            const reducedFiles = reduced.files.filter(f => !f.includes('skyline-poster'));
            if (reduced.state.state === 'skipped' && reduced.state.reason === 'reduced-motion' && !reduced.state.canvas && reducedFiles.length === 0) {
                pass('Skyline (reduced motion): poster kept, no WebGL canvas, MapLibre never requested');
            } else {
                fail(`Skyline (reduced motion): unexpected state ${JSON.stringify(reduced.state)}, requests ${reducedFiles.join(', ')}`);
            }
        } catch (error) {
            fail(`Skyline test failed: ${error.message}`);
        }

        // Signed-out visitors opening /Dashboard are sent to login with a return URL
        try {
            await page.goto(`${base}/Dashboard`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });
            const url = new URL(page.url());
            const returnUrl = url.searchParams.get('ReturnUrl') || '';
            if (url.pathname.includes('/Identity/Account/Login') && returnUrl.toLowerCase().includes('/dashboard')) {
                pass('Dashboard deep link redirects to login with return URL');
            } else {
                fail(`Dashboard deep link redirected unexpectedly to: ${url.href}`);
            }
        } catch (error) {
            fail(`Dashboard deep link check failed: ${error.message}`);
        }

        // =========================================================
        // Test 2: Authenticated pages redirect to login
        // =========================================================
        console.log('\n--- Test 2: Auth Pages Redirect to Login ---');
        for (const ap of authPages) {
            try {
                const errorsBefore = consoleErrors.length;
                await page.goto(`${base}${ap.path}`, {
                    waitUntil: 'networkidle',
                    timeout: 15000
                });
                const url = page.url();
                console.log(`  ${ap.name} (${ap.path}) → ${url}`);

                if (url.includes('/Identity/Account/Login')) {
                    pass(`${ap.name} correctly redirects to login`);
                } else if (url.includes(ap.path)) {
                    // Already logged in scenario
                    pass(`${ap.name} page loaded (authenticated session)`);
                } else {
                    fail(`${ap.name} redirected unexpectedly to: ${url}`);
                }

                // Report any new console errors from this page load
                if (consoleErrors.length > errorsBefore) {
                    for (let i = errorsBefore; i < consoleErrors.length; i++) {
                        console.log(`  [Console ERROR] ${consoleErrors[i]}`);
                    }
                }
            } catch (error) {
                fail(`${ap.name} navigation failed: ${error.message}`);
            }
        }

        // =========================================================
        // Test 3: Login page detailed verification
        // =========================================================
        console.log('\n--- Test 3: Login Page ---');
        try {
            await page.goto(`${base}/Identity/Account/Login`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });

            const title = await page.title();
            console.log(`  Title: "${title}"`);

            const lowerTitle = title.toLowerCase();
            if (lowerTitle.includes('sign in') || lowerTitle.includes('log in') || lowerTitle.includes('login')) {
                pass('Login page has correct title');
            } else {
                fail(`Login page title unexpected: "${title}"`);
            }

            // Check for login form
            const hasForm = await page.locator('form').first().isVisible({timeout: 5000}).catch(() => false);
            if (hasForm) {
                pass('Login form is visible');
            } else {
                fail('Login form not found');
            }

            // Check for email input
            const hasEmail = await page.locator('input[type="email"], input[name="Email"]').first().isVisible({timeout: 3000}).catch(() => false);
            if (hasEmail) {
                pass('Email input field is present');
            } else {
                fail('Email input field not found');
            }

            // Check for password input
            const hasPassword = await page.locator('input[type="password"]').first().isVisible({timeout: 3000}).catch(() => false);
            if (hasPassword) {
                pass('Password input field is present');
            } else {
                fail('Password input field not found');
            }

            // Check for login submit button
            const hasSubmit = await page.locator('button[type="submit"], input[type="submit"]').first().isVisible({timeout: 3000}).catch(() => false);
            if (hasSubmit) {
                pass('Login submit button is present');
            } else {
                fail('Login submit button not found');
            }

            // Check for "Remember me" checkbox
            const hasRememberMe = await page.locator('input[type="checkbox"]').first().isVisible({timeout: 2000}).catch(() => false);
            if (hasRememberMe) {
                pass('Remember me checkbox is present');
            } else {
                console.log('  [Info] Remember me checkbox not found (optional)');
            }

            // Check for external login (Google) link if configured
            const googleLink = page.locator('a:has-text("Google"), a:has-text("Sign in with Google")');
            const googleCount = await googleLink.count().catch(() => 0);
            if (googleCount > 0) {
                pass('Google external login link is present');
            } else {
                console.log('  [Info] Google login link not found (may not be configured)');
            }

            // Check for Register link
            const registerLink = page.locator('a:has-text("register"), a[href*="Register"]');
            const regCount = await registerLink.count().catch(() => 0);
            if (regCount > 0) {
                pass('Register link is present on login page');
            } else {
                console.log('  [Info] Register link not found on login page');
            }
        } catch (error) {
            fail(`Login page verification failed: ${error.message}`);
        }

        // =========================================================
        // Test 4: Register page
        // =========================================================
        console.log('\n--- Test 4: Register Page ---');
        try {
            await page.goto(`${base}/Identity/Account/Register`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });

            const title = await page.title();
            console.log(`  Title: "${title}"`);

            if (title.toLowerCase().includes('register') || title.toLowerCase().includes('sign up')) {
                pass('Register page has correct title');
            } else {
                fail(`Register page title unexpected: "${title}"`);
            }

            // Check for registration form
            const hasForm = await page.locator('form').first().isVisible({timeout: 5000}).catch(() => false);
            if (hasForm) {
                pass('Registration form is visible');
            } else {
                fail('Registration form not found');
            }

            // Check for email input
            const hasEmail = await page.locator('input[type="email"]').first().isVisible({timeout: 3000}).catch(() => false);
            if (hasEmail) {
                pass('Email input field is present');
            } else {
                fail('Email input field not found');
            }

            // Check for password input
            const hasPassword = await page.locator('input[type="password"]').first().isVisible({timeout: 3000}).catch(() => false);
            if (hasPassword) {
                pass('Password input field is present');
            } else {
                fail('Password input field not found');
            }

            // Check for confirm password (should have 2 password fields)
            const passwordFields = await page.locator('input[type="password"]').count().catch(() => 0);
            if (passwordFields >= 2) {
                pass('Confirm password field is present');
            } else {
                fail('Confirm password field not found');
            }

            // Check for submit button
            const hasSubmit = await page.locator('button[type="submit"]').first().isVisible({timeout: 3000}).catch(() => false);
            if (hasSubmit) {
                pass('Register submit button is present');
            } else {
                fail('Register submit button not found');
            }
        } catch (error) {
            fail(`Register page verification failed: ${error.message}`);
        }

        // =========================================================
        // Test 5: Layout and common elements
        // =========================================================
        console.log('\n--- Test 5: Layout and Common Elements ---');
        try {
            // Navigate to login page to check layout
            await page.goto(`${base}/Identity/Account/Login`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });

            // Check for navigation bar / topbar
            const hasNav = await page.locator('header.topbar, #sidebar, .topbar, nav').first().isVisible({timeout: 5000}).catch(() => false);
            if (hasNav) {
                pass('Navigation bar / topbar is present');
            } else {
                fail('Navigation bar not found');
            }

            // Check for skip link (accessibility)
            const skipLink = page.locator('a.app-skip-link, a[href="#main-content"]');
            const skipCount = await skipLink.count().catch(() => 0);
            if (skipCount > 0) {
                pass('Skip to main content link is present (accessibility)');
            } else {
                console.log('  [Info] Skip link not found');
            }

            // Check for main content area
            const hasMain = await page.locator('main, [role="main"]').first().isVisible({timeout: 3000}).catch(() => false);
            if (hasMain) {
                pass('Main content area is present');
            } else {
                fail('Main content area not found');
            }

            // Check for footer
            const hasFooter = await page.locator('footer').first().isVisible({timeout: 3000}).catch(() => false);
            if (hasFooter) {
                pass('Footer is present');
            } else {
                fail('Footer not found');
            }

            // Check for version in footer
            const footerText = await page.locator('footer').first().textContent({timeout: 3000}).catch(() => '');
            if (footerText && footerText.includes('Version')) {
                pass('Version number displayed in footer');
            } else {
                console.log('  [Info] Version not found in footer');
            }

            // Check the favicon set: SVG for modern browsers, .ico and PNG fallbacks, apple-touch-icon
            const iconHrefs = await page.locator('link[rel="icon"], link[rel="apple-touch-icon"]')
                .evaluateAll(links => links.map(link => link.getAttribute('href')))
                .catch(() => []);
            const expectedIcons = ['favicon.ico', 'favicon.svg', 'favicon-32x32.png', 'apple-touch-icon.png'];
            const missingIcons = expectedIcons.filter(name => !iconHrefs.some(href => href && href.includes(name)));
            if (missingIcons.length === 0) {
                pass('Favicon links are present (ico, svg, png, apple-touch-icon)');
            } else {
                fail(`Favicon links missing: ${missingIcons.join(', ')}`);
            }

            // Check viewport meta tag (responsive)
            const viewport = page.locator('meta[name="viewport"]');
            const vpCount = await viewport.count().catch(() => 0);
            if (vpCount > 0) {
                pass('Viewport meta tag is present (responsive design)');
            } else {
                fail('Viewport meta tag not found');
            }
        } catch (error) {
            fail(`Layout verification failed: ${error.message}`);
        }

        // =========================================================
        // Test 6: Health and alive endpoints
        // =========================================================
        console.log('\n--- Test 6: Health Endpoints ---');
        try {
            const healthResp = await page.goto(`${base}/health`, {
                waitUntil: 'networkidle',
                timeout: 10000
            });
            const healthStatus = healthResp?.status();
            console.log(`  /health status: ${healthStatus}`);
            if (healthStatus === 200) {
                pass('Health endpoint returns 200');
            } else {
                fail(`Health endpoint returned unexpected status: ${healthStatus}`);
            }
        } catch (error) {
            fail(`Health endpoint check failed: ${error.message}`);
        }

        try {
            const aliveResp = await page.goto(`${base}/alive`, {
                waitUntil: 'networkidle',
                timeout: 10000
            });
            const aliveStatus = aliveResp?.status();
            console.log(`  /alive status: ${aliveStatus}`);
            if (aliveStatus === 200) {
                pass('Alive endpoint returns 200');
            } else {
                fail(`Alive endpoint returned unexpected status: ${aliveStatus}`);
            }
        } catch (error) {
            fail(`Alive endpoint check failed: ${error.message}`);
        }

        // =========================================================
        // Test 7: Responsive viewport test
        // =========================================================
        console.log('\n--- Test 7: Responsive Viewport ---');
        try {
            const mobilePage = await context.newPage();
            await mobilePage.setViewportSize({width: 375, height: 667});

            await mobilePage.goto(`${base}/Identity/Account/Login`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });

            const mobileTitle = await mobilePage.title();
            console.log(`  Mobile viewport (375x667) title: "${mobileTitle}"`);

            const mobileForm = await mobilePage.locator('form').first().isVisible({timeout: 5000}).catch(() => false);
            if (mobileForm) {
                pass('Login form visible on mobile viewport');
            } else {
                fail('Login form not visible on mobile viewport');
            }

            // Check mobile bottom nav for mobile
            const bottomNav = mobilePage.locator('#mobileMenuToggle');
            const bottomNavCount = await bottomNav.count().catch(() => 0);
            if (bottomNavCount > 0) {
                pass('Mobile navbar toggler is present');
            } else {
                console.log('  [Info] Mobile toggler not found');
            }

            await mobilePage.close();
        } catch (error) {
            fail(`Responsive viewport test failed: ${error.message}`);
        }

        // =========================================================
        // Test 8: 404 handling for unknown routes
        // =========================================================
        console.log('\n--- Test 8: 404 Handling ---');
        try {
            const notFoundResp = await page.goto(`${base}/NonExistentPage`, {
                waitUntil: 'networkidle',
                timeout: 10000
            });
            const notFoundStatus = notFoundResp?.status();
            console.log(`  /NonExistentPage status: ${notFoundStatus}`);
            if (notFoundStatus === 404) {
                pass('Unknown route returns 404');
            } else if (notFoundStatus === 200) {
                // Some apps render a 404 view with status 200
                const content = await page.content();
                if (content.includes('404') || content.includes('not found') || content.toLowerCase().includes('page')) {
                    pass('Unknown route shows error page (status 200)');
                } else {
                    fail(`Unknown route returned 200 without error content`);
                }
            } else {
                fail(`Unknown route returned unexpected status: ${notFoundStatus}`);
            }
        } catch (error) {
            fail(`404 handling test failed: ${error.message}`);
        }

        // =========================================================
        // Test 8b: Cookie consent banner visibility
        // =========================================================
        console.log('\n--- Test 8b: Cookie Consent Banner ---');
        let bannerContext;
        try {
            bannerContext = await browser.newContext({
                ignoreHTTPSErrors: true,
                viewport: {width: 1280, height: 720}
            });
            const bannerPage = await bannerContext.newPage();
            const banner = bannerPage.locator('#cookieBanner');

            await bannerPage.goto(`${base}/`, {waitUntil: 'networkidle', timeout: 15000});
            if (await banner.isVisible({timeout: 3000}).catch(() => false)) {
                pass('Cookie banner is visible when no consent choice is stored');
            } else {
                fail('Cookie banner should be visible when no consent choice is stored');
            }

            await bannerPage.locator('#cookieAcceptAll').click();
            if (await banner.isHidden()) {
                pass('Cookie banner hides after "Accept All"');
            } else {
                fail('Cookie banner should hide after "Accept All"');
            }

            await bannerPage.reload({waitUntil: 'networkidle', timeout: 15000});
            if (await banner.isHidden()) {
                pass('Cookie banner stays hidden after reload once "Accept All" is stored');
            } else {
                fail('Cookie banner should stay hidden after reload once "Accept All" is stored');
            }

            await bannerContext.clearCookies();
            await bannerContext.addCookies([{
                name: 'pf_cookie_consent',
                value: 'essential',
                url: base
            }]);
            await bannerPage.reload({waitUntil: 'networkidle', timeout: 15000});
            if (await banner.isHidden()) {
                pass('Cookie banner stays hidden when "Essential Only" is stored');
            } else {
                fail('Cookie banner should stay hidden when "Essential Only" is stored');
            }
        } catch (error) {
            fail(`Cookie banner test failed: ${error.message}`);
        } finally {
            if (bannerContext) {
                await bannerContext.close();
            }
        }

        // =========================================================
        // Test 9: Register and login flow, then visit authenticated pages
        // =========================================================
        console.log('\n--- Test 9: Register + Login + Authenticated Pages ---');
        try {
            const testEmail = `e2etest_${Date.now()}@example.com`;
            const testPassword = 'TestPass1!';

            // Step 1: Register a new user
            console.log('  Registering test user...');
            await page.goto(`${base}/Identity/Account/Register`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });

            // Fill in the registration form
            await page.fill('input[type="email"]', testEmail);
            const passwordInputs = await page.locator('input[type="password"]').all();
            if (passwordInputs.length >= 2) {
                await passwordInputs[0].fill(testPassword);
                await passwordInputs[1].fill(testPassword);
            }
            await page.check('input[type="checkbox"][name="Input.AcceptTerms"]');

            // Submit the registration form using the specific button ID to avoid
            // matching other submit buttons on the page (e.g., the "Sign out" button)
            await page.click('#registerSubmit');
            await page.waitForLoadState('networkidle', {timeout: 15000});

            const afterRegisterUrl = page.url();
            console.log(`  After register: ${afterRegisterUrl}`);

            // If we're on a success page or already logged in, proceed to login
            // Some configs redirect to login after register
            if (!afterRegisterUrl.includes('/Networth') && !afterRegisterUrl.includes('/Dashboard')) {
                // Step 2: Login with the new credentials
                console.log('  Logging in with test user...');
                await page.goto(`${base}/Identity/Account/Login`, {
                    waitUntil: 'networkidle',
                    timeout: 15000
                });

                await page.fill('input[type="email"]', testEmail);
                await page.fill('input[type="password"]', testPassword);
                // Use the specific login submit button ID to avoid matching other
                // submit buttons on the page (e.g., the "Sign out" button)
                await page.click('#login-submit');
                await page.waitForLoadState('networkidle', {timeout: 15000});
            }

            const afterLoginUrl = page.url();
            console.log(`  After login: ${afterLoginUrl}`);

            // Check if we're logged in
            const isLoggedIn = afterLoginUrl.includes('/Networth') ||
                               afterLoginUrl.includes('/Dashboard') ||
                               !(afterLoginUrl.includes('/Identity/Account/Login'));

            if (isLoggedIn) {
                pass('Successfully registered and logged in');

                // Check for sidebar navigation on authenticated pages
                const hasSidebar = await page.locator('#sidebar').first().isVisible({timeout: 5000}).catch(() => false);
                if (hasSidebar) {
                    pass('Sidebar navigation is present on authenticated pages');
                } else {
                    fail('Sidebar navigation not found after login');
                }

                // Step 3: Visit authenticated feature pages
                const featurePages = [
                    {path: '/Dashboard', name: 'Dashboard'},
                    {path: '/Networth', name: 'Net Worth'},
                    {path: '/Budget', name: 'Budget'},
                    {path: '/SavingsGoals', name: 'Savings Goals'},
                    {path: '/MoneyPriorities', name: 'Money Priorities'},
                    {path: '/InvestmentCalculator', name: 'Investment Calculator'},
                    {path: '/News', name: 'Finance News'},
                    {path: '/Community', name: 'Community'},
                ];

                for (const fp of featurePages) {
                    try {
                        await page.goto(`${base}${fp.path}`, {
                            waitUntil: 'networkidle',
                            timeout: 15000
                        });
                        const pageUrl = page.url();
                        if (pageUrl.includes(fp.path) || pageUrl.includes(fp.path.replace('/', ''))) {
                            pass(`${fp.name} page loaded after login`);
                        } else {
                            fail(`${fp.name} page did not load: ${pageUrl}`);
                        }
                    } catch (error) {
                        fail(`${fp.name} page navigation failed: ${error.message}`);
                    }
                }

                // Step 4: Dashboard layout at desktop and mobile widths (same signed-in context)
                console.log('\n--- Test 9b: Dashboard at Desktop and Mobile Widths ---');
                const dashboardViewports = [
                    {name: 'desktop', width: 1280, height: 800, mobile: false},
                    {name: 'mobile', width: 390, height: 844, mobile: true},
                ];
                for (const vp of dashboardViewports) {
                    const dashPage = await context.newPage();
                    try {
                        await dashPage.setViewportSize({width: vp.width, height: vp.height});
                        if (vp.mobile) {
                            // Exercise the reduced-motion path on mobile; desktop keeps the default motion.
                            await dashPage.emulateMedia({reducedMotion: 'reduce'});
                        }
                        await dashPage.goto(`${base}/Dashboard`, {
                            waitUntil: 'networkidle',
                            timeout: 15000
                        });
                        // Let the short entrance fade settle before measuring layout.
                        await dashPage.waitForTimeout(500);
                        await checkDashboard(dashPage, vp);
                    } catch (error) {
                        fail(`Dashboard (${vp.name}) check failed: ${error.message}`);
                    } finally {
                        await dashPage.close();
                    }
                }
            } else {
                fail('Failed to log in after registration');
            }
        } catch (error) {
            fail(`Register + Login flow failed: ${error.message}`);
        }

        // =========================================================
        // Test 9c: Research first-session walkthrough (spec #217): a fresh Account on seeded snapshots
        // =========================================================
        console.log('\n--- Test 9c: Research First-Session Walkthrough ---');
        try {
            await runResearchWalkthrough({browser, base, pass, fail, consoleErrors, pageErrors});
        } catch (error) {
            fail(`Research walkthrough failed: ${error.message}`);
        }

        // =========================================================
        // Test 9d: Money Priorities walkthrough (spec #315): three leaves, Earlier results, the narrow path view
        // =========================================================
        console.log('\n--- Test 9d: Money Priorities Walkthrough ---');
        try {
            await runMoneyPrioritiesWalkthrough({browser, base, pass, fail, consoleErrors, pageErrors});
        } catch (error) {
            fail(`Money Priorities walkthrough failed: ${error.message}`);
        }

        // =========================================================
        // Test 10: Console error summary
        // =========================================================
        console.log('\n--- Test 10: Console Error Summary ---');
        if (consoleErrors.length === 0) {
            pass('No console errors detected during testing');
        } else {
            // Deduplicate errors
            const uniqueErrors = [...new Set(consoleErrors)];
            console.log(`  Total console errors: ${consoleErrors.length} (${uniqueErrors.length} unique)`);
            uniqueErrors.forEach(err => {
                console.log(`    - ${err.substring(0, 120)}${err.length > 120 ? '...' : ''}`);
            });

            // Check for known non-blocking errors (Fluent UI setTheme and related 404s)
            const knownErrors = uniqueErrors.filter(e =>
                e.includes('setTheme') || e.includes('fluentui') ||
                e.includes('Failed to load resource')
            );
            const unknownErrors = uniqueErrors.filter(e =>
                !e.includes('setTheme') && !e.includes('fluentui') &&
                !e.includes('Failed to load resource')
            );

            if (unknownErrors.length === 0 && knownErrors.length > 0) {
                pass('Only known non-blocking console errors (Fluent UI setTheme / resource 404)');
            } else if (unknownErrors.length > 0) {
                fail(`Unknown console errors detected: ${unknownErrors.length}`);
            }
        }

        // =========================================================
        // Test 11: Page errors summary
        // =========================================================
        console.log('\n--- Test 11: Page JavaScript Errors ---');
        if (pageErrors.length === 0) {
            pass('No uncaught JavaScript errors during testing');
        } else {
            const uniquePageErrors = [...new Set(pageErrors)];
            console.log(`  Total page errors: ${pageErrors.length} (${uniquePageErrors.length} unique)`);
            uniquePageErrors.forEach(err => {
                console.log(`    - ${err.substring(0, 120)}${err.length > 120 ? '...' : ''}`);
            });

            // Check for known non-blocking errors
            const knownPageErrors = uniquePageErrors.filter(e =>
                e.includes('setTheme') || e.includes('fluentui')
            );
            const unknownPageErrors = uniquePageErrors.filter(e =>
                !e.includes('setTheme') && !e.includes('fluentui')
            );

            if (unknownPageErrors.length === 0 && knownPageErrors.length > 0) {
                pass('Only known non-blocking page errors (Fluent UI setTheme export)');
            } else if (unknownPageErrors.length > 0) {
                fail(`${unknownPageErrors.length} uncaught JavaScript error(s) detected`);
            }
        }

        await context.close();

    } catch (error) {
        results.errors.push(`Browser launch failed: ${error.message}`);
        console.log(`  ✗ Browser launch failed: ${error.message}`);
    } finally {
        if (browser) {
            await browser.close();
        }
    }

    // Print summary
    console.log('\n' + '='.repeat(60));
    console.log('  UI Test Results Summary — All Pages');
    console.log('='.repeat(60));
    console.log(`  Passed: ${results.passed}`);
    console.log(`  Failed: ${results.failed}`);
    if (results.errors.length > 0) {
        console.log('\nFailures:');
        results.errors.forEach((err, i) => {
            console.log(`  ${i + 1}. ${err}`);
        });
    }
    console.log('='.repeat(60));

    process.exit(results.failed > 0 ? 1 : 0);

})();
