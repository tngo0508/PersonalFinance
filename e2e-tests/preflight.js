/**
 * Fail-fast check before running the E2E suites locally.
 *
 * Verifies, in order, that the SQL Server port is open and that the Web and API apps answer.
 * Run it with `npm run e2e:preflight`. It is not wired into `test:e2e`, so CI is unaffected.
 *
 * Environment variables (defaults match docs/DEVELOPMENT.md):
 *   E2E_DB_HOST  SQL Server host (default localhost)
 *   E2E_DB_PORT  SQL Server port (default 1433)
 *   WEB_URL      Web app base URL (default http://localhost:5000)
 *   API_URL      ApiService base URL (default https://localhost:7100)
 */
const net = require('net');
const http = require('http');
const https = require('https');

const dbHost = process.env.E2E_DB_HOST || 'localhost';
const dbPort = Number(process.env.E2E_DB_PORT || 1433);
const webUrl = process.env.WEB_URL || 'http://localhost:5000';
const apiUrl = process.env.API_URL || 'https://localhost:7100';

/**
 * Check that a TCP port accepts connections.
 * @param {string} host - Host name.
 * @param {number} port - TCP port.
 * @returns {Promise<boolean>} True when the connection succeeds within 3 seconds.
 */
function checkPort(host, port) {
    return new Promise(resolve => {
        const socket = net.connect({host, port, timeout: 3000});
        socket.once('connect', () => {
            socket.destroy();
            resolve(true);
        });
        socket.once('timeout', () => {
            socket.destroy();
            resolve(false);
        });
        socket.once('error', () => resolve(false));
    });
}

/**
 * Check that an HTTP(S) endpoint answers with any status code.
 * @param {string} url - Base URL to request.
 * @returns {Promise<boolean>} True when the server responds within 5 seconds.
 */
function checkHttp(url) {
    const client = url.startsWith('https') ? https : http;
    return new Promise(resolve => {
        const request = client.get(`${url.replace(/\/$/, '')}/alive`, {timeout: 5000, rejectUnauthorized: false}, response => {
            response.resume();
            resolve(true);
        });
        request.once('timeout', () => {
            request.destroy();
            resolve(false);
        });
        request.once('error', () => resolve(false));
    });
}

(async () => {
    const checks = [
        {
            name: `SQL Server at ${dbHost}:${dbPort}`,
            run: () => checkPort(dbHost, dbPort),
            hint: 'Start Docker Desktop and the SQL container, or point E2E_DB_HOST/E2E_DB_PORT at your SQL Server.'
        },
        {
            name: `Web app at ${webUrl}`,
            run: () => checkHttp(webUrl),
            hint: 'Start PersonalFinance.Web (see docs/DEVELOPMENT.md, "Running the E2E tests locally").'
        },
        {
            name: `API at ${apiUrl}`,
            run: () => checkHttp(apiUrl),
            hint: 'Start PersonalFinance.ApiService; it needs the database to be reachable at startup.'
        }
    ];

    for (const check of checks) {
        if (await check.run()) {
            console.log(`  ✓ ${check.name}`);
        } else {
            console.error(`  ✗ ${check.name} is not reachable.\n    ${check.hint}`);
            process.exit(1);
        }
    }
    console.log('Preflight passed. Run `npm run test:e2e` and `npm run test:api`.');
})();
