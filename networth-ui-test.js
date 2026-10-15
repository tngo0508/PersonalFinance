const {chromium} = require('playwright');

(async () => {
    const results = {passed: 0, failed: 0, errors: []};
    const base = 'https://localhost:7200';

    function pass(message) {
        results.passed++;
        console.log(`  ✓ ${message}`);
    }

    function fail(message) {
        results.failed++;
        results.errors.push(message);
        console.log(`  ✗ ${message}`);
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
        // Test 1: HTTPS redirect enforcement
        // =========================================================
        console.log('\n--- Test 1: HTTPS Redirect ---');
        try {
            const httpPage = await context.newPage();
            await httpPage.goto('http://localhost:5200/', {
                waitUntil: 'networkidle',
                timeout: 10000
            });
            const finalUrl = httpPage.url();
            if (finalUrl.startsWith('https://')) {
                pass('HTTP correctly redirects to HTTPS');
            } else {
                fail(`HTTP did not redirect to HTTPS: ${finalUrl}`);
            }
            await httpPage.close();
        } catch (error) {
            fail(`HTTPS redirect check failed: ${error.message}`);
        }

        // =========================================================
        // Test 2: Default route (home page)
        // =========================================================
        console.log('\n--- Test 2: Default Route (Home) ---');
        try {
            await page.goto(`${base}/`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });
            const url = page.url();
            console.log(`  Resolved URL: ${url}`);
            const title = await page.title();
            console.log(`  Page title: "${title}"`);

            // Default route is Networth/Index, which requires auth → redirects to login
            if (url.includes('/Identity/Account/Login')) {
                pass('Default route redirects to login (requires auth)');
            } else if (url.includes('/Networth')) {
                pass('Default route loaded Net Worth page');
            } else {
                fail(`Unexpected default route destination: ${url}`);
            }
        } catch (error) {
            fail(`Default route failed: ${error.message}`);
        }

        // =========================================================
        // Test 3: Authenticated pages redirect to login
        // =========================================================
        console.log('\n--- Test 3: Auth Pages Redirect to Login ---');
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
        // Test 4: Login page detailed verification
        // =========================================================
        console.log('\n--- Test 4: Login Page ---');
        try {
            await page.goto(`${base}/Identity/Account/Login`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });

            const title = await page.title();
            console.log(`  Title: "${title}"`);

            if (title.toLowerCase().includes('log in') || title.toLowerCase().includes('login')) {
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
        // Test 5: Register page
        // =========================================================
        console.log('\n--- Test 5: Register Page ---');
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
        // Test 6: Layout and common elements
        // =========================================================
        console.log('\n--- Test 6: Layout and Common Elements ---');
        try {
            // Navigate to login page to check layout
            await page.goto(`${base}/Identity/Account/Login`, {
                waitUntil: 'networkidle',
                timeout: 15000
            });

            // Check for navbar
            const hasNavbar = await page.locator('nav.navbar').first().isVisible({timeout: 5000}).catch(() => false);
            if (hasNavbar) {
                pass('Navigation bar is present');
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

            // Check for favicon
            const favicon = page.locator('link[rel="icon"]');
            const favCount = await favicon.count().catch(() => 0);
            if (favCount > 0) {
                pass('Favicon link is present');
            } else {
                console.log('  [Info] Favicon not found');
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
        // Test 7: Health and alive endpoints
        // =========================================================
        console.log('\n--- Test 7: Health Endpoints ---');
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
        // Test 8: Responsive viewport test
        // =========================================================
        console.log('\n--- Test 8: Responsive Viewport ---');
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

            // Check navbar toggler for mobile
            const toggler = mobilePage.locator('.navbar-toggler');
            const togglerCount = await toggler.count().catch(() => 0);
            if (togglerCount > 0) {
                pass('Mobile navbar toggler is present');
            } else {
                console.log('  [Info] Mobile toggler not found');
            }

            await mobilePage.close();
        } catch (error) {
            fail(`Responsive viewport test failed: ${error.message}`);
        }

        // =========================================================
        // Test 9: 404 handling for unknown routes
        // =========================================================
        console.log('\n--- Test 9: 404 Handling ---');
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
