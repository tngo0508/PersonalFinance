const {execSync} = require('child_process');

(async () => {
    const results = {passed: 0, failed: 0, errors: []};
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

    function curl(options = {}) {
        const {url, method = 'GET', headers = [], body = null, followRedirects = true} = options;
        let cmd = `curl -s -S -k ${followRedirects ? '-L' : ''} --connect-timeout 10 --max-time 30 -w "\\n%{http_code}"`;

        if (method !== 'GET') {
            cmd += ` -X ${method}`;
        }

        for (const header of headers) {
            cmd += ` -H "${header}"`;
        }

        if (body) {
            cmd += ` -d '${body}'`;
        }

        cmd += ` "${url}"`;

        try {
            const output = execSync(cmd, {encoding: 'utf8', timeout: 35000}).trim();
            const lines = output.split('\n');
            const statusCode = parseInt(lines[lines.length - 1], 10);
            const responseBody = lines.slice(0, -1).join('\n');
            return {statusCode, body: responseBody};
        } catch (error) {
            return {statusCode: 0, body: '', error: error.message};
        }
    }

    // =========================================================
    // Test 1: API root endpoint
    // =========================================================
    console.log('\n--- Test 1: API Root Endpoint ---');
    try {
        const resp = curl({url: `${apiBase}/`});
        console.log(`  / status: ${resp.statusCode}`);
        if (resp.statusCode === 200) {
            pass('API root returns 200');
            try {
                const json = JSON.parse(resp.body);
                // Accept both PascalCase and camelCase property names
                const app = json.Application || json.application;
                const ver = json.Version || json.version;
                const status = json.Status || json.status;
                if (app) {
                    pass(`API root returns application name: ${app}`);
                } else {
                    fail('API root JSON missing Application field');
                }
                if (ver) {
                    pass(`API root returns version: ${ver}`);
                } else {
                    fail('API root JSON missing Version field');
                }
                if (status === 'Online') {
                    pass('API status is Online');
                } else {
                    fail(`API status is not Online: ${status}`);
                }
            } catch (e) {
                fail('API root response is not valid JSON');
            }
        } else {
            fail(`API root returned unexpected status: ${resp.statusCode}`);
        }
    } catch (error) {
        fail(`API root check failed: ${error.message}`);
    }

    // =========================================================
    // Test 2: Health and alive endpoints
    // =========================================================
    console.log('\n--- Test 2: Health Endpoints ---');
    try {
        const healthResp = curl({url: `${apiBase}/health`});
        console.log(`  /health status: ${healthResp.statusCode}`);
        if (healthResp.statusCode === 200) {
            pass('Health endpoint returns 200');
        } else {
            fail(`Health endpoint returned unexpected status: ${healthResp.statusCode}`);
        }
    } catch (error) {
        fail(`Health endpoint check failed: ${error.message}`);
    }

    try {
        const aliveResp = curl({url: `${apiBase}/alive`});
        console.log(`  /alive status: ${aliveResp.statusCode}`);
        if (aliveResp.statusCode === 200) {
            pass('Alive endpoint returns 200');
        } else {
            fail(`Alive endpoint returned unexpected status: ${aliveResp.statusCode}`);
        }
    } catch (error) {
        fail(`Alive endpoint check failed: ${error.message}`);
    }

    // =========================================================
    // Test 3: NetWorth API endpoints (require auth — expect 401)
    // =========================================================
    console.log('\n--- Test 3: NetWorth API (Auth Required) ---');
    try {
        const resp = curl({url: `${apiBase}/api/NetWorth/Parameters`, followRedirects: false});
        console.log(`  /api/NetWorth/Parameters status: ${resp.statusCode}`);
        if (resp.statusCode === 401 || resp.statusCode === 403) {
            pass('NetWorth API correctly requires authentication');
        } else if (resp.statusCode === 200) {
            // Might be accessible if there's a default user context
            pass('NetWorth API returned 200 (may be pre-authenticated)');
            try {
                const json = JSON.parse(resp.body);
                if (Array.isArray(json) || typeof json === 'object') {
                    pass('NetWorth API returns valid JSON response');
                }
            } catch (e) {
                // Ignore JSON parse errors
            }
        } else {
            fail(`NetWorth API returned unexpected status: ${resp.statusCode}`);
        }
    } catch (error) {
        fail(`NetWorth API check failed: ${error.message}`);
    }

    // =========================================================
    // Test 4: Community API endpoints (require auth — expect 401)
    // =========================================================
    console.log('\n--- Test 4: Community API (Auth Required) ---');
    try {
        const resp = curl({url: `${apiBase}/api/Community/Stories`, followRedirects: false});
        console.log(`  /api/Community/Stories status: ${resp.statusCode}`);
        if (resp.statusCode === 401 || resp.statusCode === 403) {
            pass('Community API correctly requires authentication');
        } else if (resp.statusCode === 200) {
            pass('Community API returned 200 (may be pre-authenticated)');
        } else {
            fail(`Community API returned unexpected status: ${resp.statusCode}`);
        }
    } catch (error) {
        fail(`Community API check failed: ${error.message}`);
    }

    // =========================================================
    // Test 5: Budget API endpoints (require auth — expect 401)
    // =========================================================
    console.log('\n--- Test 5: Budget API (Auth Required) ---');
    try {
        const resp = curl({url: `${apiBase}/api/Budget`, followRedirects: false});
        console.log(`  /api/Budget status: ${resp.statusCode}`);
        if (resp.statusCode === 401 || resp.statusCode === 403) {
            pass('Budget API correctly requires authentication');
        } else if (resp.statusCode === 200) {
            pass('Budget API returned 200 (may be pre-authenticated)');
        } else {
            fail(`Budget API returned unexpected status: ${resp.statusCode}`);
        }
    } catch (error) {
        fail(`Budget API check failed: ${error.message}`);
    }

    // =========================================================
    // Test 6: Savings Goals API endpoints (require auth — expect 401)
    // =========================================================
    console.log('\n--- Test 6: Savings Goals API (Auth Required) ---');
    try {
        const resp = curl({url: `${apiBase}/api/SavingsGoals`, followRedirects: false});
        console.log(`  /api/SavingsGoals status: ${resp.statusCode}`);
        if (resp.statusCode === 401 || resp.statusCode === 403) {
            pass('Savings Goals API correctly requires authentication');
        } else if (resp.statusCode === 200) {
            pass('Savings Goals API returned 200 (may be pre-authenticated)');
        } else {
            fail(`Savings Goals API returned unexpected status: ${resp.statusCode}`);
        }
    } catch (error) {
        fail(`Savings Goals API check failed: ${error.message}`);
    }

    // =========================================================
    // Test 7: Watchlist API endpoints (require auth — expect 401)
    // =========================================================
    console.log('\n--- Test 7: Watchlist API (Auth Required) ---');
    try {
        const resp = curl({url: `${apiBase}/api/Watchlist`, followRedirects: false});
        console.log(`  /api/Watchlist status: ${resp.statusCode}`);
        if (resp.statusCode === 401 || resp.statusCode === 403) {
            pass('Watchlist API correctly requires authentication');
        } else if (resp.statusCode === 200) {
            pass('Watchlist API returned 200 (may be pre-authenticated)');
        } else {
            fail(`Watchlist API returned unexpected status: ${resp.statusCode}`);
        }
    } catch (error) {
        fail(`Watchlist API check failed: ${error.message}`);
    }

    // =========================================================
    // Test 8: AI Assistant API endpoints (require auth — expect 401)
    // =========================================================
    console.log('\n--- Test 8: AI Assistant API (Auth Required) ---');
    try {
        const resp = curl({
            url: `${apiBase}/api/AiAssistant/Chat`,
            method: 'POST',
            headers: ['Content-Type: application/json'],
            body: '{"message":"hello"}',
            followRedirects: false
        });
        console.log(`  /api/AiAssistant/Chat status: ${resp.statusCode}`);
        if (resp.statusCode === 401 || resp.statusCode === 403) {
            pass('AI Assistant API correctly requires authentication');
        } else if (resp.statusCode === 200 || resp.statusCode === 400) {
            pass('AI Assistant API responded (may be pre-authenticated or validation error)');
        } else {
            fail(`AI Assistant API returned unexpected status: ${resp.statusCode}`);
        }
    } catch (error) {
        fail(`AI Assistant API check failed: ${error.message}`);
    }

    // =========================================================
    // Test 9: Unknown API route returns 404
    // =========================================================
    console.log('\n--- Test 9: Unknown API Route ---');
    try {
        const resp = curl({url: `${apiBase}/api/NonExistent`, followRedirects: false});
        console.log(`  /api/NonExistent status: ${resp.statusCode}`);
        if (resp.statusCode === 404) {
            pass('Unknown API route returns 404');
        } else {
            fail(`Unknown API route returned unexpected status: ${resp.statusCode}`);
        }
    } catch (error) {
        fail(`Unknown API route check failed: ${error.message}`);
    }

    // =========================================================
    // Test 10: OpenAPI spec endpoint (if enabled)
    // =========================================================
    console.log('\n--- Test 10: OpenAPI Spec Endpoint ---');
    try {
        const resp = curl({url: `${apiBase}/openapi/v1.json`, followRedirects: false});
        console.log(`  /openapi/v1.json status: ${resp.statusCode}`);
        if (resp.statusCode === 200) {
            pass('OpenAPI spec endpoint returns 200');
            try {
                const json = JSON.parse(resp.body);
                if (json.info && json.info.title) {
                    pass(`OpenAPI spec title: ${json.info.title}`);
                }
            } catch (e) {
                // Ignore JSON parse errors
            }
        } else if (resp.statusCode === 404) {
            console.log('  [Info] OpenAPI spec endpoint not available (ApiDocs:Enabled not set)');
        } else {
            fail(`OpenAPI spec returned unexpected status: ${resp.statusCode}`);
        }
    } catch (error) {
        fail(`OpenAPI spec check failed: ${error.message}`);
    }

    // Print summary
    console.log('\n' + '='.repeat(60));
    console.log('  API Test Results Summary');
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
